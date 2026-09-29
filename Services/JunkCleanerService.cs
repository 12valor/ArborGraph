using System.Diagnostics;
using System.IO;
using DiskScope.Models;

namespace DiskScope.Services;

public class JunkCleanerService
{
    public List<JunkTarget> GetDefaultTargets()
    {
        var targets = new List<JunkTarget>();

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // ----------------- SYSTEM JUNK -----------------
        targets.Add(new JunkTarget
        {
            Id = "sys_user_temp",
            Name = "User Temporary Files",
            Category = JunkCategory.System,
            Description = "Temporary files created by running user applications (%TEMP%).",
            TargetDirectories = new List<string>
            {
                Path.GetTempPath(),
                Path.Combine(localAppData, "Temp")
            }
        });

        targets.Add(new JunkTarget
        {
            Id = "sys_win_temp",
            Name = "Windows Temporary Files",
            Category = JunkCategory.System,
            Description = "Temporary system cache and installation leftovers (C:\\Windows\\Temp).",
            TargetDirectories = new List<string>
            {
                Path.Combine(winDir, "Temp")
            }
        });

        targets.Add(new JunkTarget
        {
            Id = "sys_update_download",
            Name = "Windows Update Download Cache",
            Category = JunkCategory.System,
            Description = "Completed and pending Windows Update installation files.",
            TargetDirectories = new List<string>
            {
                Path.Combine(winDir, "SoftwareDistribution", "Download")
            }
        });

        targets.Add(new JunkTarget
        {
            Id = "sys_crash_dumps",
            Name = "Crash Dumps & Error Reports",
            Category = JunkCategory.System,
            Description = "Application crash dumps (.dmp) and Windows Error Reporting logs.",
            TargetDirectories = new List<string>
            {
                Path.Combine(localAppData, "CrashDumps"),
                Path.Combine(localAppData, "Microsoft", "Windows", "WER")
            }
        });

        targets.Add(new JunkTarget
        {
            Id = "sys_thumbnails",
            Name = "Windows Explorer Thumbnail Cache",
            Category = JunkCategory.System,
            Description = "Cached image and video thumbnails created by Windows Explorer.",
            TargetDirectories = new List<string>
            {
                Path.Combine(localAppData, "Microsoft", "Windows", "Explorer")
            },
            SearchPattern = "thumbcache_*.db",
            Recursive = false
        });

        // ----------------- BROWSER CACHES -----------------
        targets.Add(new JunkTarget
        {
            Id = "browser_chrome",
            Name = "Google Chrome Cache",
            Category = JunkCategory.Browser,
            Description = "Web assets, media cache, and script bytecode for Google Chrome.",
            TargetDirectories = new List<string>
            {
                Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache"),
                Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Code Cache")
            }
        });

        targets.Add(new JunkTarget
        {
            Id = "browser_edge",
            Name = "Microsoft Edge Cache",
            Category = JunkCategory.Browser,
            Description = "Web cache, images, and script bytecode for Microsoft Edge.",
            TargetDirectories = new List<string>
            {
                Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache"),
                Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Code Cache")
            }
        });

        // Firefox profile cache dynamic discovery
        string firefoxProfiles = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        var firefoxDirs = new List<string>();
        if (Directory.Exists(firefoxProfiles))
        {
            try
            {
                foreach (var profile in Directory.EnumerateDirectories(firefoxProfiles))
                {
                    firefoxDirs.Add(Path.Combine(profile, "cache2"));
                }
            }
            catch { }
        }
        targets.Add(new JunkTarget
        {
            Id = "browser_firefox",
            Name = "Mozilla Firefox Cache",
            Category = JunkCategory.Browser,
            Description = "Web and script cache for Mozilla Firefox profiles.",
            TargetDirectories = firefoxDirs
        });

        targets.Add(new JunkTarget
        {
            Id = "browser_brave",
            Name = "Brave Browser Cache",
            Category = JunkCategory.Browser,
            Description = "Web assets and cached scripts for Brave Browser.",
            TargetDirectories = new List<string>
            {
                Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache"),
                Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Code Cache")
            }
        });

        targets.Add(new JunkTarget
        {
            Id = "browser_discord",
            Name = "Discord & Electron Cache",
            Category = JunkCategory.Browser,
            Description = "Cached images, media, and scripts from Discord desktop.",
            TargetDirectories = new List<string>
            {
                Path.Combine(appData, "discord", "Cache"),
                Path.Combine(appData, "discord", "Code Cache")
            }
        });

        // ----------------- DEVELOPER CACHES -----------------
        targets.Add(new JunkTarget
        {
            Id = "dev_nuget",
            Name = "NuGet Package Cache",
            Category = JunkCategory.Developer,
            Description = "Local global-packages folder for .NET and Visual Studio (%USERPROFILE%\\.nuget\\packages).",
            TargetDirectories = new List<string>
            {
                Path.Combine(userProfile, ".nuget", "packages")
            }
        });

        targets.Add(new JunkTarget
        {
            Id = "dev_npm",
            Name = "npm Cache",
            Category = JunkCategory.Developer,
            Description = "Locally cached Node.js packages and tarballs (%APPDATA%\\npm-cache).",
            TargetDirectories = new List<string>
            {
                Path.Combine(appData, "npm-cache")
            }
        });

        targets.Add(new JunkTarget
        {
            Id = "dev_pip",
            Name = "pip Cache",
            Category = JunkCategory.Developer,
            Description = "Python pip downloaded wheels and packages cache.",
            TargetDirectories = new List<string>
            {
                Path.Combine(localAppData, "pip", "cache")
            }
        });

        targets.Add(new JunkTarget
        {
            Id = "dev_cargo",
            Name = "Rust Cargo Registry Cache",
            Category = JunkCategory.Developer,
            Description = "Downloaded Rust crates and index cache (%USERPROFILE%\\.cargo\\registry\\cache).",
            TargetDirectories = new List<string>
            {
                Path.Combine(userProfile, ".cargo", "registry", "cache")
            }
        });

        targets.Add(new JunkTarget
        {
            Id = "dev_gradle",
            Name = "Gradle Cache",
            Category = JunkCategory.Developer,
            Description = "Downloaded JARs and build artifacts (%USERPROFILE%\\.gradle\\caches).",
            TargetDirectories = new List<string>
            {
                Path.Combine(userProfile, ".gradle", "caches")
            }
        });

        return targets;
    }

    public async Task ScanTargetAsync(JunkTarget target, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            target.IsBusy = true;
            target.Status = "Scanning...";

            long totalBytes = 0;
            int fileCount = 0;
            var filesPreview = new List<JunkFileItem>();

            // Distinct directory paths that exist
            var validDirs = target.TargetDirectories
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(Directory.Exists)
                .ToList();

            foreach (var dir in validDirs)
            {
                if (ct.IsCancellationRequested) break;
                ScanDirectory(dir, target.SearchPattern, target.Recursive, ref totalBytes, ref fileCount, filesPreview, ct);
            }

            target.SizeInBytes = totalBytes;
            target.FileCount = fileCount;
            target.DiscoveredFiles = filesPreview;
            target.Status = fileCount > 0 ? "Ready to clean" : "Clean";
            target.IsBusy = false;
        }, ct);
    }

    public async Task ScanAllAsync(
        IEnumerable<JunkTarget> targets,
        IProgress<JunkScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var targetList = targets.ToList();
        int completed = 0;

        foreach (var target in targetList)
        {
            if (ct.IsCancellationRequested) break;

            progress?.Report(new JunkScanProgress
            {
                CurrentTargetName = target.Name,
                CompletedTargets = completed,
                TotalTargets = targetList.Count
            });

            await ScanTargetAsync(target, ct);
            completed++;
        }

        progress?.Report(new JunkScanProgress
        {
            CurrentTargetName = "Complete",
            CompletedTargets = completed,
            TotalTargets = targetList.Count
        });
    }

    public async Task<JunkCleanResult> CleanTargetsAsync(
        IEnumerable<JunkTarget> targets,
        IProgress<JunkCleanProgress>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            var result = new JunkCleanResult();

            var selectedTargets = targets.Where(t => t.IsSelected && t.SizeInBytes > 0).ToList();
            int totalFilesToClean = selectedTargets.Sum(t => t.FileCount);
            int filesProcessed = 0;

            foreach (var target in selectedTargets)
            {
                if (ct.IsCancellationRequested) break;

                target.IsBusy = true;
                target.Status = "Cleaning...";

                var validDirs = target.TargetDirectories
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(Directory.Exists)
                    .ToList();

                long targetFreed = 0;
                int targetDeleted = 0;
                int targetSkipped = 0;

                foreach (var dir in validDirs)
                {
                    if (ct.IsCancellationRequested) break;
                    CleanDirectory(dir, target.SearchPattern, target.Recursive, ref targetFreed, ref targetDeleted, ref targetSkipped,
                        ref filesProcessed, totalFilesToClean, result.SkippedReasons, progress, ct);
                }

                result.BytesFreed += targetFreed;
                result.FilesDeleted += targetDeleted;
                result.FilesSkipped += targetSkipped;

                // Re-evaluate target state
                target.SizeInBytes = Math.Max(0, target.SizeInBytes - targetFreed);
                target.FileCount = Math.Max(0, target.FileCount - targetDeleted);
                target.Status = target.FileCount == 0 ? "Cleaned" : $"{targetSkipped} skipped (in use)";
                target.IsBusy = false;
            }

            sw.Stop();
            result.Elapsed = sw.Elapsed;
            return result;
        }, ct);
    }

    private static void ScanDirectory(
        string dir,
        string pattern,
        bool recursive,
        ref long totalBytes,
        ref int fileCount,
        List<JunkFileItem> previewList,
        CancellationToken ct)
    {
        var dirsToVisit = new Stack<string>();
        dirsToVisit.Push(dir);

        while (dirsToVisit.Count > 0)
        {
            if (ct.IsCancellationRequested) return;
            string currentDir = dirsToVisit.Pop();

            try
            {
                var dirInfo = new DirectoryInfo(currentDir);
                if (!dirInfo.Exists) continue;

                // Enumerate files
                foreach (var fi in dirInfo.EnumerateFiles(pattern))
                {
                    if (ct.IsCancellationRequested) return;

                    totalBytes += fi.Length;
                    fileCount++;

                    if (previewList.Count < 500)
                    {
                        previewList.Add(new JunkFileItem
                        {
                            FullPath = fi.FullName,
                            FileName = fi.Name,
                            SizeInBytes = fi.Length,
                            LastModified = fi.LastWriteTime
                        });
                    }
                }

                // If recursive, push subdirectories
                if (recursive)
                {
                    foreach (var subDir in dirInfo.EnumerateDirectories())
                    {
                        // Skip reparse points / symbolic links to avoid loop traps
                        if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        dirsToVisit.Push(subDir.FullName);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
            catch (Exception) { }
        }
    }

    private static void CleanDirectory(
        string dir,
        string pattern,
        bool recursive,
        ref long targetFreed,
        ref int targetDeleted,
        ref int targetSkipped,
        ref int totalProcessed,
        int totalExpected,
        List<string> skippedReasons,
        IProgress<JunkCleanProgress>? progress,
        CancellationToken ct)
    {
        var subDirs = new List<string>();

        try
        {
            var dirInfo = new DirectoryInfo(dir);
            if (!dirInfo.Exists) return;

            foreach (var fi in dirInfo.EnumerateFiles(pattern))
            {
                if (ct.IsCancellationRequested) return;

                totalProcessed++;
                progress?.Report(new JunkCleanProgress
                {
                    CurrentFileName = fi.Name,
                    CompletedFiles = totalProcessed,
                    TotalFiles = totalExpected,
                    BytesFreedSoFar = targetFreed
                });

                try
                {
                    long length = fi.Length;
                    // Remove read-only attribute if present
                    if (fi.IsReadOnly)
                    {
                        fi.IsReadOnly = false;
                    }

                    fi.Delete();
                    targetFreed += length;
                    targetDeleted++;
                }
                catch (IOException ex)
                {
                    // File in use by another process
                    targetSkipped++;
                    if (skippedReasons.Count < 20)
                    {
                        skippedReasons.Add($"{fi.Name} (in use: {ex.Message})");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    targetSkipped++;
                    if (skippedReasons.Count < 20)
                    {
                        skippedReasons.Add($"{fi.Name} (access denied: {ex.Message})");
                    }
                }
                catch (Exception ex)
                {
                    targetSkipped++;
                    if (skippedReasons.Count < 20)
                    {
                        skippedReasons.Add($"{fi.Name} ({ex.Message})");
                    }
                }
            }

            if (recursive)
            {
                foreach (var subDir in dirInfo.EnumerateDirectories())
                {
                    if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    subDirs.Add(subDir.FullName);
                    CleanDirectory(subDir.FullName, pattern, true, ref targetFreed, ref targetDeleted, ref targetSkipped,
                        ref totalProcessed, totalExpected, skippedReasons, progress, ct);
                }

                // Clean empty directory after children are removed
                try
                {
                    if (dirInfo.Exists && !dirInfo.EnumerateFileSystemInfos().Any())
                    {
                        dirInfo.Delete();
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
