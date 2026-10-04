using System.IO;
using System.Text;
using DiskScope.Models;

namespace DiskScope.Tests;

public record GeneratedDatasetStats(
    string RootPath,
    int ExpectedFileCount,
    int ExpectedDirectoryCount,
    long ExpectedTotalBytes,
    IReadOnlyList<string> FilePaths,
    IReadOnlyList<string> DirectoryPaths
);

/// <summary>
/// Reusable controlled test-data generator for ArborGraph automated test suites.
/// Generates deterministic temporary filesystem trees and guarantees safe cleanup.
/// Never operates on real system locations (C:\Windows, Program Files, etc.).
/// </summary>
public static class TestDataGenerator
{
    private static readonly string TestBaseTempDir = Path.Combine(Path.GetTempPath(), "ArborGraph_QA_Harness");

    public static string CreateIsolatedDirectory(string prefix = "test_run")
    {
        string dir = Path.Combine(TestBaseTempDir, $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void SafeCleanup(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return;

        // Safety gate: never delete outside of temp path
        string fullPath = Path.GetFullPath(directoryPath);
        string tempPath = Path.GetFullPath(Path.GetTempPath());
        if (!fullPath.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SAFETY ABORT: Refusing to clean up path outside TEMP: {fullPath}");
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                // Strip read-only attributes on files if any were set during testing
                foreach (var file in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                Directory.Delete(fullPath, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
            catch
            {
                break;
            }
        }
    }

    /// <summary>
    /// Dataset A: 13 files across 5 directories with known exact counts and sizes (228,891 bytes).
    /// </summary>
    public static GeneratedDatasetStats GenerateDatasetA(string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        string subA = Path.Combine(targetDir, "FolderA");
        string subB = Path.Combine(targetDir, "FolderB", "NestedB");
        string subPs = Path.Combine(targetDir, "PhotoshopProjects");

        Directory.CreateDirectory(subA);
        Directory.CreateDirectory(subB);
        Directory.CreateDirectory(subPs);

        var filePaths = new List<string>();

        void WriteFile(string path, byte[] content)
        {
            File.WriteAllBytes(path, content);
            filePaths.Add(path);
        }

        // Regular files
        WriteFile(Path.Combine(subA, "doc1.txt"), Encoding.UTF8.GetBytes(new string('A', 1024)));
        WriteFile(Path.Combine(subA, "doc2.pdf"), new byte[2048]);
        WriteFile(Path.Combine(subA, "script.cs"), Encoding.UTF8.GetBytes("class Foo { }"));

        // Zero byte file
        WriteFile(Path.Combine(subA, "zero_byte.dat"), Array.Empty<byte>());

        // File without extension
        WriteFile(Path.Combine(subA, "NO_EXTENSION_FILE"), Encoding.UTF8.GetBytes("no ext content"));

        // Unicode filename
        WriteFile(Path.Combine(subA, "résumé_2026_test.docx"), new byte[4096]);

        // Photoshop files
        WriteFile(Path.Combine(subPs, "artwork.psd"), new byte[8192]);
        WriteFile(Path.Combine(subPs, "banner_huge.psb"), new byte[16384]);
        WriteFile(Path.Combine(subPs, "brushes.abr"), new byte[512]);

        // True duplicates (identical bytes)
        byte[] dupPayload = new byte[65536];
        new Random(42).NextBytes(dupPayload);
        WriteFile(Path.Combine(subB, "dup1.bin"), dupPayload);
        WriteFile(Path.Combine(subB, "dup2.bin"), dupPayload);

        // Size collision (same size, different bytes - must NOT be treated as duplicates)
        byte[] collisionA = new byte[32768];
        byte[] collisionB = new byte[32768];
        new Random(101).NextBytes(collisionA);
        new Random(202).NextBytes(collisionB);
        WriteFile(Path.Combine(subB, "collision_a.bin"), collisionA);
        WriteFile(Path.Combine(subB, "collision_b.bin"), collisionB);

        var allDirs = Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories).ToList();
        allDirs.Add(targetDir);

        long totalBytes = filePaths.Sum(f => new FileInfo(f).Length);
        return new GeneratedDatasetStats(targetDir, filePaths.Count, allDirs.Count, totalBytes, filePaths, allDirs);
    }

    /// <summary>
    /// Deterministic scalable dataset generator for 10, 1,000, 10,000, 50,000, or 100,000 files.
    /// Uses deterministic pseudo-random seeds so expected file count and byte totals are known.
    /// </summary>
    public static GeneratedDatasetStats GenerateScaledDataset(string targetDir, int fileCount, int subDirsCount = 0)
    {
        Directory.CreateDirectory(targetDir);
        if (subDirsCount <= 0)
        {
            // Auto calculate reasonable directory distribution (approx 50 files per directory)
            subDirsCount = Math.Max(1, fileCount / 50);
        }

        var dirPaths = new List<string>(subDirsCount);
        for (int d = 0; d < subDirsCount; d++)
        {
            string dir = Path.Combine(targetDir, $"Folder_{d:D4}");
            Directory.CreateDirectory(dir);
            dirPaths.Add(dir);
        }

        var filePaths = new List<string>(fileCount);
        long totalBytes = 0;
        var rng = new Random(1337); // Deterministic seed

        // Deterministic size formula: (i % 20 + 1) * 256 bytes
        // e.g. sizes range from 256 bytes to 5,120 bytes
        for (int i = 0; i < fileCount; i++)
        {
            string targetSubDir = dirPaths[i % subDirsCount];
            string filePath = Path.Combine(targetSubDir, $"data_{i:D6}.bin");

            int size = ((i % 20) + 1) * 256;
            byte[] content = new byte[size];
            content[0] = (byte)(i & 0xFF);
            content[^1] = (byte)((i >> 8) & 0xFF);

            File.WriteAllBytes(filePath, content);
            filePaths.Add(filePath);
            totalBytes += size;
        }

        var allDirs = Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories).ToList();
        allDirs.Add(targetDir);

        return new GeneratedDatasetStats(targetDir, fileCount, allDirs.Count, totalBytes, filePaths, allDirs);
    }

    /// <summary>
    /// Dataset G: Cryptographic duplicates and collision pairs for duplicate detection testing.
    /// </summary>
    public static (GeneratedDatasetStats Stats, IReadOnlyList<string> Group1, IReadOnlyList<string> Group2, IReadOnlyList<string> Collisions) GenerateDuplicatesDataset(string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        string sub = Path.Combine(targetDir, "DupTest");
        Directory.CreateDirectory(sub);

        var filePaths = new List<string>();
        var grp1 = new List<string>();
        var grp2 = new List<string>();
        var collisions = new List<string>();

        // Group 1: 64 KB duplicate (2 copies)
        byte[] p1 = new byte[65536];
        new Random(42).NextBytes(p1);
        string g1a = Path.Combine(sub, "g1_a.bin");
        string g1b = Path.Combine(sub, "g1_b.bin");
        File.WriteAllBytes(g1a, p1);
        File.WriteAllBytes(g1b, p1);
        grp1.Add(g1a);
        grp1.Add(g1b);
        filePaths.AddRange(grp1);

        // Group 2: 128 KB duplicate (3 copies)
        byte[] p2 = new byte[131072];
        new Random(99).NextBytes(p2);
        string g2a = Path.Combine(sub, "g2_a.bin");
        string g2b = Path.Combine(sub, "g2_b.bin");
        string g2c = Path.Combine(sub, "g2_c.bin");
        File.WriteAllBytes(g2a, p2);
        File.WriteAllBytes(g2b, p2);
        File.WriteAllBytes(g2c, p2);
        grp2.Add(g2a);
        grp2.Add(g2b);
        grp2.Add(g2c);
        filePaths.AddRange(grp2);

        // Collisions: same size (32,768 bytes), differing byte payloads
        byte[] cA = new byte[32768];
        byte[] cB = new byte[32768];
        new Random(101).NextBytes(cA);
        new Random(202).NextBytes(cB);
        string colA = Path.Combine(sub, "col_a.bin");
        string colB = Path.Combine(sub, "col_b.bin");
        File.WriteAllBytes(colA, cA);
        File.WriteAllBytes(colB, cB);
        collisions.Add(colA);
        collisions.Add(colB);
        filePaths.AddRange(collisions);

        // Zero-byte files
        string z1 = Path.Combine(sub, "zero1.txt");
        string z2 = Path.Combine(sub, "zero2.txt");
        File.WriteAllBytes(z1, Array.Empty<byte>());
        File.WriteAllBytes(z2, Array.Empty<byte>());
        filePaths.Add(z1);
        filePaths.Add(z2);

        long totalBytes = filePaths.Sum(f => new FileInfo(f).Length);
        var dirs = Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories).ToList();
        dirs.Add(targetDir);

        var stats = new GeneratedDatasetStats(targetDir, filePaths.Count, dirs.Count, totalBytes, filePaths, dirs);
        return (stats, grp1, grp2, collisions);
    }

    /// <summary>
    /// Dataset N: Developer ecosystem verification (Node, .NET, Rust, Gradle) + False-positive traps.
    /// </summary>
    public static (GeneratedDatasetStats Stats, string NodeDir, string DotNetDir, string RustDir, string GradleDir, string FakeRustTarget, string FakeGradleBuild) GenerateDeveloperDataset(string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        // 1. Node.js project
        string nodeRoot = Path.Combine(targetDir, "web-app");
        Directory.CreateDirectory(nodeRoot);
        File.WriteAllText(Path.Combine(nodeRoot, "package.json"), "{}");
        string nodeModules = Path.Combine(nodeRoot, "node_modules");
        Directory.CreateDirectory(nodeModules);
        File.WriteAllBytes(Path.Combine(nodeModules, "index.js"), new byte[45000]);

        // 2. .NET project
        string dotnetRoot = Path.Combine(targetDir, "dotnet-service");
        Directory.CreateDirectory(dotnetRoot);
        File.WriteAllText(Path.Combine(dotnetRoot, "Service.csproj"), "<Project />");
        string binDir = Path.Combine(dotnetRoot, "bin");
        Directory.CreateDirectory(binDir);
        File.WriteAllBytes(Path.Combine(binDir, "service.dll"), new byte[85000]);

        // 3. Rust project
        string rustRoot = Path.Combine(targetDir, "rust-cli");
        Directory.CreateDirectory(rustRoot);
        File.WriteAllText(Path.Combine(rustRoot, "Cargo.toml"), "[package]");
        string rustTarget = Path.Combine(rustRoot, "target");
        Directory.CreateDirectory(rustTarget);
        File.WriteAllBytes(Path.Combine(rustTarget, "cli.exe"), new byte[150000]);

        // 4. Gradle project
        string gradleRoot = Path.Combine(targetDir, "gradle-app");
        Directory.CreateDirectory(gradleRoot);
        File.WriteAllText(Path.Combine(gradleRoot, "build.gradle"), "// gradle");
        string gradleBuild = Path.Combine(gradleRoot, "build");
        Directory.CreateDirectory(gradleBuild);
        File.WriteAllBytes(Path.Combine(gradleBuild, "app.jar"), new byte[75000]);

        // 5. False Positive Trap: Non-Rust folder with a subfolder named "target"
        string fakeRustRoot = Path.Combine(targetDir, "customer-targets");
        Directory.CreateDirectory(fakeRustRoot);
        string fakeRustTarget = Path.Combine(fakeRustRoot, "target");
        Directory.CreateDirectory(fakeRustTarget);
        File.WriteAllBytes(Path.Combine(fakeRustTarget, "quarterly_target.pdf"), new byte[25000]);

        // 6. False Positive Trap: Non-Gradle folder with a subfolder named "build"
        string fakeBuildRoot = Path.Combine(targetDir, "architectural-blueprints");
        Directory.CreateDirectory(fakeBuildRoot);
        string fakeGradleBuild = Path.Combine(fakeBuildRoot, "build");
        Directory.CreateDirectory(fakeGradleBuild);
        File.WriteAllBytes(Path.Combine(fakeGradleBuild, "drawing.dwg"), new byte[55000]);

        var allFiles = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories).ToList();
        var allDirs = Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories).ToList();
        allDirs.Add(targetDir);

        long totalBytes = allFiles.Sum(f => new FileInfo(f).Length);
        var stats = new GeneratedDatasetStats(targetDir, allFiles.Count, allDirs.Count, totalBytes, allFiles, allDirs);

        return (stats, nodeModules, binDir, rustTarget, gradleBuild, fakeRustTarget, fakeGradleBuild);
    }

    /// <summary>
    /// Edge cases dataset: Unicode, deep nesting (> 260 chars), special characters, empty folders.
    /// </summary>
    public static GeneratedDatasetStats GenerateEdgeCasesDataset(string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        var filePaths = new List<string>();

        // 1. Unicode filenames (Japanese, Arabic, Cyrillic, Emoji)
        string unicodeDir = Path.Combine(targetDir, "Unicode_Test");
        Directory.CreateDirectory(unicodeDir);
        string u1 = Path.Combine(unicodeDir, "ドキュメント_日本語.txt");
        string u2 = Path.Combine(unicodeDir, "ملف_عربي.pdf");
        string u3 = Path.Combine(unicodeDir, "документ_русский.dat");
        string u4 = Path.Combine(unicodeDir, "tree_🌲_rocket_🚀.log");
        File.WriteAllText(u1, "Japanese content");
        File.WriteAllBytes(u2, new byte[1024]);
        File.WriteAllText(u3, "Cyrillic content");
        File.WriteAllText(u4, "Emoji content");
        filePaths.AddRange(new[] { u1, u2, u3, u4 });

        // 2. Special characters in file and directory names
        string specialDir = Path.Combine(targetDir, "SpecialChars [Test] & More #1");
        Directory.CreateDirectory(specialDir);
        string s1 = Path.Combine(specialDir, "file with spaces & symbols %20.txt");
        string s2 = Path.Combine(specialDir, "quote'test'and#hash.bin");
        File.WriteAllText(s1, "spaces and symbols");
        File.WriteAllBytes(s2, new byte[512]);
        filePaths.AddRange(new[] { s1, s2 });

        // 3. Deep directory tree (> 260 characters / MAX_PATH)
        string deepCurr = Path.Combine(targetDir, "DeepNesting");
        Directory.CreateDirectory(deepCurr);
        for (int i = 0; i < 20; i++)
        {
            deepCurr = Path.Combine(deepCurr, $"SubLevel_{i:D2}_NestedFolderLongName");
            Directory.CreateDirectory(deepCurr);
        }
        string deepFile = Path.Combine(deepCurr, "deeply_nested_leaf_file.dat");
        File.WriteAllBytes(deepFile, new byte[100]);
        filePaths.Add(deepFile);

        // 4. Empty folders
        for (int i = 0; i < 5; i++)
        {
            Directory.CreateDirectory(Path.Combine(targetDir, $"EmptyDir_{i}"));
        }

        var allDirs = Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories).ToList();
        allDirs.Add(targetDir);
        long totalBytes = filePaths.Sum(f => new FileInfo(f).Length);

        return new GeneratedDatasetStats(targetDir, filePaths.Count, allDirs.Count, totalBytes, filePaths, allDirs);
    }
}
