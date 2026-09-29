using System.IO;
using System.Text;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.Tests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("  DISKSCOPE PRO — AUTOMATED INTEGRATION TESTS");
        Console.WriteLine("=================================================");

        string testRoot = Path.Combine(Path.GetTempPath(), "DiskScope_TestFiles_" + Guid.NewGuid().ToString("N")[..8]);
        string testDbFolder = Path.Combine(Path.GetTempPath(), "DiskScope_TestDb_" + Guid.NewGuid().ToString("N")[..8]);
        string testDb = Path.Combine(testDbFolder, "test_index.db");

        try
        {
            Directory.CreateDirectory(testRoot);

            // 1. Construct Mock Test Tree
            Console.WriteLine("[SETUP] Creating test filesystem tree...");
            string subA = Path.Combine(testRoot, "FolderA");
            string subB = Path.Combine(testRoot, "FolderB", "NestedB");
            string subPs = Path.Combine(testRoot, "PhotoshopProjects");
            Directory.CreateDirectory(subA);
            Directory.CreateDirectory(subB);
            Directory.CreateDirectory(subPs);

            // Regular files
            File.WriteAllBytes(Path.Combine(subA, "doc1.txt"), Encoding.UTF8.GetBytes(new string('A', 1024)));
            File.WriteAllBytes(Path.Combine(subA, "doc2.pdf"), new byte[2048]);
            File.WriteAllBytes(Path.Combine(subA, "script.cs"), Encoding.UTF8.GetBytes("class Foo { }"));

            // Zero byte file
            File.WriteAllBytes(Path.Combine(subA, "zero_byte.dat"), Array.Empty<byte>());

            // File without extension
            File.WriteAllBytes(Path.Combine(subA, "NO_EXTENSION_FILE"), Encoding.UTF8.GetBytes("no ext content"));

            // Unicode filename
            File.WriteAllBytes(Path.Combine(subA, "résumé_2026_test.docx"), new byte[4096]);

            // Photoshop files
            File.WriteAllBytes(Path.Combine(subPs, "artwork.psd"), new byte[8192]);
            File.WriteAllBytes(Path.Combine(subPs, "banner_huge.psb"), new byte[16384]);
            File.WriteAllBytes(Path.Combine(subPs, "brushes.abr"), new byte[512]);

            // True duplicates (identical bytes)
            byte[] dupPayload = new byte[65536];
            new Random(42).NextBytes(dupPayload);
            File.WriteAllBytes(Path.Combine(subB, "dup1.bin"), dupPayload);
            File.WriteAllBytes(Path.Combine(subB, "dup2.bin"), dupPayload);

            // Size collision (same size, different bytes - must NOT be treated as duplicates)
            byte[] collisionA = new byte[32768];
            byte[] collisionB = new byte[32768];
            new Random(101).NextBytes(collisionA);
            new Random(202).NextBytes(collisionB);
            File.WriteAllBytes(Path.Combine(subB, "collision_a.bin"), collisionA);
            File.WriteAllBytes(Path.Combine(subB, "collision_b.bin"), collisionB);

            // Calculate expected numbers
            var allTestFiles = Directory.GetFiles(testRoot, "*", SearchOption.AllDirectories);
            long expectedTotalBytes = allTestFiles.Sum(f => new FileInfo(f).Length);
            int expectedFileCount = allTestFiles.Length;
            var allTestDirs = Directory.GetDirectories(testRoot, "*", SearchOption.AllDirectories);
            int expectedDirCount = allTestDirs.Length + 1; // including testRoot

            Console.WriteLine($"[SETUP] Generated {expectedFileCount} files across {expectedDirCount} directories. Total bytes: {expectedTotalBytes:N0}.");

            // 2. Initialize DatabaseService
            Console.WriteLine("\n[TEST 1] Initializing SQLite database in WAL mode...");
            using var dbService = new DatabaseService(testDb);
            dbService.Initialize();
            Assert(dbService.CheckIntegrity(), "SQLite PRAGMA integrity_check failed!");
            Console.WriteLine("  ✓ SQLite index created and integrity verified.");

            // 3. Run ScannerService
            Console.WriteLine("\n[TEST 2] Running Scanner Engine...");
            var scanner = new ScannerService(dbService);
            var stats = await scanner.ScanDrivesAsync(new[] { testRoot }, null, CancellationToken.None);

            Console.WriteLine($"  Visited Dirs:    {stats.DirectoriesVisited}");
            Console.WriteLine($"  Processed Dirs:  {stats.DirectoriesProcessed}");
            Console.WriteLine($"  Skipped Dirs:    {stats.DirectoriesSkipped}");
            Console.WriteLine($"  Discovered Files:{stats.FilesDiscovered}");
            Console.WriteLine($"  Indexed Files:   {stats.FilesIndexed}");
            Console.WriteLine($"  Skipped Files:   {stats.FilesSkipped}");
            Console.WriteLine($"  Logical Bytes:   {stats.LogicalBytesIndexed:N0}");

            // Verify Section 9 Accounting Rules
            Assert(stats.DirectoriesVisited == stats.DirectoriesProcessed + stats.DirectoriesSkipped,
                "DirectoriesVisited != Processed + Skipped");
            Assert(stats.FilesDiscovered == stats.FilesIndexed + stats.FilesSkipped,
                "FilesDiscovered != Indexed + Skipped");
            Assert(stats.FilesIndexed == expectedFileCount,
                $"Indexed files ({stats.FilesIndexed}) != expected ({expectedFileCount})");
            Assert(stats.LogicalBytesIndexed == expectedTotalBytes,
                $"Logical bytes ({stats.LogicalBytesIndexed}) != expected ({expectedTotalBytes})");
            Assert(stats.State == ScanState.Completed, "ScanState should be Completed");
            Console.WriteLine("  ✓ Exact accounting verified. All files and directories perfectly accounted for.");

            // 4. Test Queries and Filtering
            Console.WriteLine("\n[TEST 3] Testing SQLite Query & Filtering Layer...");
            var pagedAll = dbService.GetFilesPaged(0, 100);
            Assert(pagedAll.Count == expectedFileCount, $"Paged files count {pagedAll.Count} != expected {expectedFileCount}");
            Assert(pagedAll[0].Size >= pagedAll[^1].Size, "Files not sorted by size descending");

            // Filter minSize >= 10000 bytes
            var filtered = dbService.GetFilesPaged(0, 100, minSize: 10000);
            Assert(filtered.All(f => f.Size >= 10000), "MinSize filter failed");
            Console.WriteLine($"  ✓ Paged and sorted queries verified ({filtered.Count} files >= 10 KB).");

            // 5. Test Photoshop Intelligence
            Console.WriteLine("\n[TEST 4] Testing Photoshop Intelligence...");
            var psStats = dbService.GetPhotoshopStats();
            Console.WriteLine($"  PSD Count: {psStats.PsdCount}, PSB Count: {psStats.PsbCount}, Other: {psStats.OtherCount}");
            Assert(psStats.PsdCount == 1, $"Expected 1 PSD, got {psStats.PsdCount}");
            Assert(psStats.PsbCount == 1, $"Expected 1 PSB, got {psStats.PsbCount}");
            Assert(psStats.OtherCount >= 1, $"Expected at least 1 other (.abr), got {psStats.OtherCount}");
            var psFiles = dbService.GetPhotoshopFiles();
            Assert(psFiles.Any(f => f.Extension.Equals(".psd", StringComparison.OrdinalIgnoreCase)), "Missing PSD in results");
            Assert(psFiles.Any(f => f.Extension.Equals(".psb", StringComparison.OrdinalIgnoreCase)), "Missing PSB in results");
            Console.WriteLine("  ✓ Photoshop asset intelligence verified.");

            // 6. Test Cryptographic Duplicate Detection
            Console.WriteLine("\n[TEST 5] Testing Cryptographic Duplicate Detection (3-Stage)...");
            var dupAnalyzer = new DuplicateAnalyzer(dbService);
            var dupGroups = await dupAnalyzer.FindDuplicatesAsync(minSize: 1024);

            Console.WriteLine($"  Duplicate Groups Found: {dupGroups.Count}");
            foreach (var g in dupGroups)
            {
                Console.WriteLine($"  - Size: {g.ExactSize:N0} bytes | Copies: {g.FileCount} | Wasted: {g.WastedBytes:N0} bytes | SHA: {g.ShortHash}");
            }

            Assert(dupGroups.Count == 1, $"Expected exactly 1 duplicate group, found {dupGroups.Count}");
            var group = dupGroups[0];
            Assert(group.ExactSize == 65536, $"Duplicate size should be 65536, got {group.ExactSize}");
            Assert(group.FileCount == 2, $"Duplicate group file count should be 2, got {group.FileCount}");
            Assert(group.WastedBytes == 65536, $"Wasted bytes should be 65536, got {group.WastedBytes}");
            // Size collision pair of 32768 bytes MUST NOT be in duplicate groups
            Assert(!dupGroups.Any(g => g.ExactSize == 32768), "Size collision with different content was falsely marked as duplicate!");
            Console.WriteLine("  ✓ Cryptographic duplicate detection verified: zero false positives on size collisions.");

            // 7. Test Safe Cancellation
            Console.WriteLine("\n[TEST 6] Testing Safe Cancellation...");
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately
            var cancelStats = await scanner.ScanDrivesAsync(new[] { testRoot }, null, cts.Token);
            Assert(cancelStats.State == ScanState.Cancelled, $"Expected State = Cancelled, got {cancelStats.State}");
            Assert(dbService.CheckIntegrity(), "Database corrupted after cancellation!");
            Console.WriteLine("  ✓ Safe cancellation verified: clean termination without database corruption.");

            // 8. Test Analytics Engine & Metrics
            Console.WriteLine("\n[TEST 7] Testing Analytics Engine & Metrics...");
            // Re-run scan to populate data and metadata for analytics testing
            await scanner.ScanDrivesAsync(new[] { testRoot }, null, CancellationToken.None);

            var history = dbService.GetScanHistory();
            Assert(history.Count >= 2, $"Expected at least 2 scan history records, got {history.Count}");
            Console.WriteLine($"  Scan History Count: {history.Count} (Latest indexed: {history[^1].FormattedBytes})");

            var ageBuckets = dbService.GetFileAgeBreakdown();
            Assert(ageBuckets.Count == 5, $"Expected 5 age buckets, got {ageBuckets.Count}");
            long totalAgeCount = ageBuckets.Sum(b => b.Count);
            Assert(totalAgeCount == expectedFileCount, $"Total files in age buckets ({totalAgeCount}) != expected ({expectedFileCount})");
            Console.WriteLine($"  File Age Buckets: {ageBuckets.Count} tiers verified ({ageBuckets[0].Name}: {ageBuckets[0].Count} files).");

            var dupOverview = dbService.GetDuplicateOverview();
            Assert(dupOverview.CandidateGroups >= 1, "Expected at least 1 duplicate candidate group");
            Console.WriteLine($"  Duplicate Overview: {dupOverview.CandidateGroups} candidate groups, {dupOverview.CandidateFiles} files, {dupOverview.PotentialWastedBytes:N0} bytes.");

            var (reclaimItems, totalReclaim) = dbService.GetReclaimableStorageBreakdown();
            Assert(reclaimItems.Count > 0, "Expected reclaimable storage categories");
            Console.WriteLine($"  Reclaimable Storage: {reclaimItems.Count} opportunity categories detected ({totalReclaim:N0} total bytes).");

            var largestPs = dbService.GetLargestPhotoshopFiles(5);
            Assert(largestPs.Count >= 2, $"Expected at least 2 PSD/PSB files, got {largestPs.Count}");
            Console.WriteLine($"  Largest Photoshop Files: {largestPs.Count} documents identified (Top: {largestPs[0].Name} - {largestPs[0].FormattedSize}).");

            var (totalFiles, totalBytes) = dbService.GetTotalIndexedStorage();
            Assert(totalFiles == expectedFileCount, $"Total indexed storage files ({totalFiles}) != expected ({expectedFileCount})");
            Console.WriteLine($"  Total Indexed Storage: {totalFiles} files, {totalBytes:N0} bytes.");
            Console.WriteLine("  ✓ Analytics engine and database queries fully verified.");

            // -----------------------------------------------------------------------------------------
            // TEST 8: Junk Cleaner Detection & Locked-File Safe Deletion
            // -----------------------------------------------------------------------------------------
            Console.WriteLine("\n[TEST 8] Testing Junk Cleaner Detection & Safe Deletion...");
            string testJunkRoot = Path.Combine(Path.GetTempPath(), "DiskScope_Junk_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(testJunkRoot);
            string junkTemp = Path.Combine(testJunkRoot, "Temp");
            string junkNuget = Path.Combine(testJunkRoot, "Nuget");
            Directory.CreateDirectory(junkTemp);
            Directory.CreateDirectory(junkNuget);

            File.WriteAllBytes(Path.Combine(junkTemp, "temp1.tmp"), new byte[2048]);
            File.WriteAllBytes(Path.Combine(junkTemp, "temp2.tmp"), new byte[4096]);
            string lockedFile = Path.Combine(junkTemp, "in_use.tmp");
            File.WriteAllBytes(lockedFile, new byte[8192]);

            File.WriteAllBytes(Path.Combine(junkNuget, "pkg1.nupkg"), new byte[16384]);
            File.WriteAllBytes(Path.Combine(junkNuget, "pkg2.nupkg"), new byte[32768]);

            var junkService = new JunkCleanerService();
            var mockTargets = new List<JunkTarget>
            {
                new()
                {
                    Id = "mock_temp",
                    Name = "Mock Temp",
                    Category = JunkCategory.System,
                    TargetDirectories = [junkTemp]
                },
                new()
                {
                    Id = "mock_nuget",
                    Name = "Mock NuGet",
                    Category = JunkCategory.Developer,
                    TargetDirectories = [junkNuget]
                }
            };

            await junkService.ScanAllAsync(mockTargets);
            Assert(mockTargets[0].FileCount == 3, $"Expected 3 files in mock_temp, got {mockTargets[0].FileCount}");
            Assert(mockTargets[1].FileCount == 2, $"Expected 2 files in mock_nuget, got {mockTargets[1].FileCount}");
            long expectedJunkBytes = 2048 + 4096 + 8192 + 16384 + 32768;
            long foundJunkBytes = mockTargets.Sum(t => t.SizeInBytes);
            Assert(foundJunkBytes == expectedJunkBytes, $"Expected {expectedJunkBytes} junk bytes, found {foundJunkBytes}");
            Console.WriteLine($"  ✓ Junk discovery verified: {mockTargets.Sum(t => t.FileCount)} files, {foundJunkBytes:N0} bytes.");

            // Now lock the in_use.tmp file to simulate an active running program
            FileStream? lockStream = null;
            try
            {
                lockStream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

                var cleanResult = await junkService.CleanTargetsAsync(mockTargets);
                Assert(cleanResult.FilesDeleted == 4, $"Expected 4 files deleted, got {cleanResult.FilesDeleted}");
                Assert(cleanResult.FilesSkipped == 1, $"Expected 1 locked file skipped, got {cleanResult.FilesSkipped}");
                Assert(cleanResult.BytesFreed == expectedJunkBytes - 8192, $"Expected {expectedJunkBytes - 8192} bytes freed, got {cleanResult.BytesFreed}");
                Assert(File.Exists(lockedFile), "Locked file should still exist and remain safe on disk");
                Console.WriteLine($"  ✓ Safe deletion verified: {cleanResult.FilesDeleted} deleted, {cleanResult.FilesSkipped} in-use file safely skipped.");
            }
            finally
            {
                lockStream?.Dispose();
                try { if (Directory.Exists(testJunkRoot)) Directory.Delete(testJunkRoot, true); } catch { }
            }

            Console.WriteLine("\n=================================================");
            Console.WriteLine("  ALL INTEGRATION TESTS PASSED SUCCESSFULLY! ✓");
            Console.WriteLine("=================================================");
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FAIL] Test threw exception: {ex.Message}\n{ex.StackTrace}");
            Console.ResetColor();
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
                if (Directory.Exists(testDbFolder)) Directory.Delete(testDbFolder, recursive: true);
            }
            catch { }
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception("Assertion Failed: " + message);
        }
    }
}
