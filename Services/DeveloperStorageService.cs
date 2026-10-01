using System.Collections.ObjectModel;
using System.IO;
using DiskScope.Infrastructure;
using DiskScope.Models;

namespace DiskScope.Services;

public class DeveloperStorageService
{
    private readonly DatabaseService _dbService;

    public DeveloperStorageService(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    public static readonly string[] CandidateDirectoryNames =
    [
        "node_modules",
        "bin",
        "obj",
        "__pycache__",
        "target",
        ".gradle",
        "build"
    ];

    public async Task<List<DeveloperEcosystemSummary>> ScanDeveloperStorageAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var detectedItems = new List<DeveloperJunkItem>();

            // 1. Scan SQLite indexed directories
            var candidates = _dbService.GetDirectoriesByNames(CandidateDirectoryNames);

            foreach (var cand in candidates)
            {
                if (ct.IsCancellationRequested) break;
                var item = EvaluateContextualJunk(cand.Path, cand.Name, cand.Parent, cand.Size, (int)cand.FileCount);
                if (item != null)
                {
                    detectedItems.Add(item);
                }
            }

            // 2. Discover global developer caches
            var globalCaches = DiscoverGlobalCaches(ct);
            detectedItems.AddRange(globalCaches);

            // Group into ecosystem summaries
            return GroupByEcosystem(detectedItems);
        }, ct);
    }

    public async Task<List<DeveloperEcosystemSummary>> ScanWorkspaceAsync(string workspacePath, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var detectedItems = new List<DeveloperJunkItem>();
            if (!Directory.Exists(workspacePath)) return GroupByEcosystem(detectedItems);

            var dirQueue = new Queue<string>();
            dirQueue.Enqueue(workspacePath);

            while (dirQueue.Count > 0)
            {
                if (ct.IsCancellationRequested) break;
                string current = dirQueue.Dequeue();
                progress?.Report($"Scanning: {Path.GetFileName(current)}");

                try
                {
                    foreach (var subDir in Directory.EnumerateDirectories(current))
                    {
                        if (ct.IsCancellationRequested) break;
                        string dirName = Path.GetFileName(subDir);

                        if (CandidateDirectoryNames.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                        {
                            var evaluated = EvaluateContextualJunk(subDir, dirName, current, -1, -1);
                            if (evaluated != null)
                            {
                                // Measure on disk if not already known
                                if (evaluated.SizeBytes <= 0)
                                {
                                    var (bytes, count) = MeasureDirectory(subDir, ct);
                                    evaluated.SizeBytes = bytes;
                                    evaluated.FileCount = count;
                                }
                                detectedItems.Add(evaluated);
                                // Skip recursing into detected junk folders like node_modules
                                continue;
                            }
                        }

                        // Otherwise queue subdirectory for scanning unless hidden / system
                        var di = new DirectoryInfo(subDir);
                        if (!di.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            dirQueue.Enqueue(subDir);
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception) { }
            }

            return GroupByEcosystem(detectedItems);
        }, ct);
    }

    private static DeveloperJunkItem? EvaluateContextualJunk(string dirPath, string name, string parent, long totalSize, int fileCount)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(parent))
            {
                parent = Path.GetDirectoryName(dirPath) ?? string.Empty;
            }

            string projectName = string.IsNullOrWhiteSpace(parent) ? name : Path.GetFileName(parent);
            DateTime lastModified = DateTime.MinValue;

            if (Directory.Exists(dirPath))
            {
                try { lastModified = Directory.GetLastWriteTime(dirPath); } catch { }
            }

            // 1. Node.js: node_modules
            if (string.Equals(name, "node_modules", StringComparison.OrdinalIgnoreCase))
            {
                // Check if nested inside another node_modules
                string norm = dirPath.Replace('/', '\\');
                int firstIdx = norm.IndexOf(@"\node_modules\", StringComparison.OrdinalIgnoreCase);
                int lastIdx = norm.LastIndexOf(@"\node_modules", StringComparison.OrdinalIgnoreCase);
                if (firstIdx >= 0 && firstIdx < lastIdx)
                {
                    // Nested dependency of a dependency, skip to avoid double-counting
                    return null;
                }

                bool hasPackageJson = File.Exists(Path.Combine(parent, "package.json"))
                    || File.Exists(Path.Combine(parent, "package-lock.json"))
                    || File.Exists(Path.Combine(parent, "pnpm-lock.yaml"))
                    || File.Exists(Path.Combine(parent, "yarn.lock"));

                if (hasPackageJson || Directory.Exists(dirPath))
                {
                    return new DeveloperJunkItem
                    {
                        Ecosystem = DeveloperEcosystem.NodeJs,
                        JunkType = DeveloperJunkType.ProjectDependency,
                        Name = name,
                        Path = dirPath,
                        ProjectName = projectName,
                        ProjectPath = parent,
                        SizeBytes = Math.Max(0, totalSize),
                        FileCount = Math.Max(0, fileCount),
                        LastModified = lastModified,
                        ContextReason = hasPackageJson ? "Node.js project (package.json present)" : "Node.js dependencies folder"
                    };
                }
            }

            // 2. .NET: bin & obj
            if (string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase))
            {
                bool isDotNetProject = Directory.Exists(parent) &&
                    (Directory.EnumerateFiles(parent, "*.*proj").Any()
                     || File.Exists(Path.Combine(parent, "Directory.Build.props"))
                     || File.Exists(Path.Combine(parent, "Directory.Build.targets")));

                if (!isDotNetProject && !string.IsNullOrWhiteSpace(parent))
                {
                    // Check grandparent (e.g. for bin/Debug)
                    string grandParent = Path.GetDirectoryName(parent) ?? string.Empty;
                    if (Directory.Exists(grandParent) && Directory.EnumerateFiles(grandParent, "*.*proj").Any())
                    {
                        isDotNetProject = true;
                        projectName = Path.GetFileName(grandParent);
                    }
                }

                if (isDotNetProject)
                {
                    return new DeveloperJunkItem
                    {
                        Ecosystem = DeveloperEcosystem.DotNet,
                        JunkType = DeveloperJunkType.BuildArtifact,
                        Name = name,
                        Path = dirPath,
                        ProjectName = projectName,
                        ProjectPath = parent,
                        SizeBytes = Math.Max(0, totalSize),
                        FileCount = Math.Max(0, fileCount),
                        LastModified = lastModified,
                        ContextReason = $".NET build output (*.proj in {projectName})"
                    };
                }
            }

            // 3. Python: __pycache__
            if (string.Equals(name, "__pycache__", StringComparison.OrdinalIgnoreCase))
            {
                return new DeveloperJunkItem
                {
                    Ecosystem = DeveloperEcosystem.Python,
                    JunkType = DeveloperJunkType.CompilerCache,
                    Name = name,
                    Path = dirPath,
                    ProjectName = projectName,
                    ProjectPath = parent,
                    SizeBytes = Math.Max(0, totalSize),
                    FileCount = Math.Max(0, fileCount),
                    LastModified = lastModified,
                    ContextReason = "Python bytecode compilation cache"
                };
            }

            // 4. Rust: target
            if (string.Equals(name, "target", StringComparison.OrdinalIgnoreCase))
            {
                // CRUCIAL: Must have Cargo.toml or Cargo.lock in parent!
                bool isRustProject = File.Exists(Path.Combine(parent, "Cargo.toml")) || File.Exists(Path.Combine(parent, "Cargo.lock"));
                if (isRustProject)
                {
                    return new DeveloperJunkItem
                    {
                        Ecosystem = DeveloperEcosystem.Rust,
                        JunkType = DeveloperJunkType.BuildArtifact,
                        Name = name,
                        Path = dirPath,
                        ProjectName = projectName,
                        ProjectPath = parent,
                        SizeBytes = Math.Max(0, totalSize),
                        FileCount = Math.Max(0, fileCount),
                        LastModified = lastModified,
                        ContextReason = "Rust Cargo build output (Cargo.toml present)"
                    };
                }
            }

            // 5. Gradle / Java: .gradle
            if (string.Equals(name, ".gradle", StringComparison.OrdinalIgnoreCase))
            {
                bool isGradle = File.Exists(Path.Combine(parent, "build.gradle"))
                    || File.Exists(Path.Combine(parent, "build.gradle.kts"))
                    || File.Exists(Path.Combine(parent, "settings.gradle"))
                    || File.Exists(Path.Combine(parent, "gradlew"));

                if (isGradle || parent.Contains(".gradle", StringComparison.OrdinalIgnoreCase))
                {
                    return new DeveloperJunkItem
                    {
                        Ecosystem = DeveloperEcosystem.GradleJava,
                        JunkType = DeveloperJunkType.ProjectDependency,
                        Name = name,
                        Path = dirPath,
                        ProjectName = projectName,
                        ProjectPath = parent,
                        SizeBytes = Math.Max(0, totalSize),
                        FileCount = Math.Max(0, fileCount),
                        LastModified = lastModified,
                        ContextReason = "Gradle project cache (build.gradle/gradlew present)"
                    };
                }
            }

            // 6. Java / Gradle: build (MUST be verified!)
            if (string.Equals(name, "build", StringComparison.OrdinalIgnoreCase))
            {
                bool isGradleOrMaven = File.Exists(Path.Combine(parent, "build.gradle"))
                    || File.Exists(Path.Combine(parent, "build.gradle.kts"))
                    || File.Exists(Path.Combine(parent, "pom.xml"))
                    || File.Exists(Path.Combine(parent, "gradlew"));

                if (isGradleOrMaven)
                {
                    return new DeveloperJunkItem
                    {
                        Ecosystem = DeveloperEcosystem.GradleJava,
                        JunkType = DeveloperJunkType.BuildArtifact,
                        Name = name,
                        Path = dirPath,
                        ProjectName = projectName,
                        ProjectPath = parent,
                        SizeBytes = Math.Max(0, totalSize),
                        FileCount = Math.Max(0, fileCount),
                        LastModified = lastModified,
                        ContextReason = "Gradle/Maven build directory (build.gradle/pom.xml present)"
                    };
                }
            }
        }
        catch { }

        return null;
    }

    private static List<DeveloperJunkItem> DiscoverGlobalCaches(CancellationToken ct)
    {
        var list = new List<DeveloperJunkItem>();

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var cacheDefinitions = new (DeveloperEcosystem eco, string name, string path, string reason)[]
        {
            (DeveloperEcosystem.NodeJs, "npm Cache", Path.Combine(appData, "npm-cache"), "Global npm downloaded packages cache"),
            (DeveloperEcosystem.DotNet, "NuGet Packages Cache", Path.Combine(userProfile, ".nuget", "packages"), "Global NuGet package restore cache"),
            (DeveloperEcosystem.Python, "pip Cache", Path.Combine(localAppData, "pip", "cache"), "Global pip wheels and downloaded packages"),
            (DeveloperEcosystem.Rust, "Cargo Registry Cache", Path.Combine(userProfile, ".cargo", "registry", "cache"), "Cargo downloaded crate packages (.crate)"),
            (DeveloperEcosystem.GradleJava, "Gradle Caches", Path.Combine(userProfile, ".gradle", "caches"), "Global Gradle dependency and build caches")
        };

        foreach (var (eco, name, path, reason) in cacheDefinitions)
        {
            if (ct.IsCancellationRequested) break;
            if (Directory.Exists(path))
            {
                var (bytes, count) = MeasureDirectory(path, ct);
                if (bytes > 0)
                {
                    list.Add(new DeveloperJunkItem
                    {
                        Ecosystem = eco,
                        JunkType = DeveloperJunkType.PackageCache,
                        Name = name,
                        Path = path,
                        ProjectName = "Global Package Cache",
                        ProjectPath = path,
                        SizeBytes = bytes,
                        FileCount = count,
                        LastModified = Directory.GetLastWriteTime(path),
                        ContextReason = reason,
                        IsGlobalCache = true
                    });
                }
            }
        }

        return list;
    }

    private static (long Bytes, int Files) MeasureDirectory(string dirPath, CancellationToken ct)
    {
        long bytes = 0;
        int count = 0;

        try
        {
            var di = new DirectoryInfo(dirPath);
            foreach (var fi in di.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    bytes += fi.Length;
                    count++;
                }
                catch { }
            }
        }
        catch { }

        return (bytes, count);
    }

    private static List<DeveloperEcosystemSummary> GroupByEcosystem(List<DeveloperJunkItem> items)
    {
        var summaries = new List<DeveloperEcosystemSummary>();

        var ecosystems = new[]
        {
            (DeveloperEcosystem.NodeJs, "Node.js (node_modules & npm)"),
            (DeveloperEcosystem.DotNet, ".NET (bin, obj & NuGet)"),
            (DeveloperEcosystem.Python, "Python (__pycache__ & pip)"),
            (DeveloperEcosystem.Rust, "Rust (target & Cargo)"),
            (DeveloperEcosystem.GradleJava, "Gradle / Java (.gradle & build)")
        };

        foreach (var (eco, title) in ecosystems)
        {
            var ecoItems = items.Where(i => i.Ecosystem == eco).OrderByDescending(i => i.SizeBytes).ToList();
            var summary = new DeveloperEcosystemSummary
            {
                Ecosystem = eco,
                Name = title,
                TotalBytes = ecoItems.Sum(i => i.SizeBytes),
                ItemCount = ecoItems.Count,
                Items = new ObservableCollection<DeveloperJunkItem>(ecoItems)
            };
            summary.RefreshSubcategories();
            summaries.Add(summary);
        }

        return summaries;
    }
}
