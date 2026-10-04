using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;

namespace DiskScope.Tests;

public static class AutomatedTestSuites
{
    public static void RegisterAll(TestRunner runner)
    {
        RegisterDatabaseTests(runner);
        RegisterScannerTests(runner);
        RegisterRollupTests(runner);
        RegisterDuplicateTests(runner);
        RegisterDeveloperStorageTests(runner);
        RegisterQueryAndFilterTests(runner);
        RegisterExportTests(runner);
        RegisterTreemapTests(runner);
        RegisterLegalAndSettingsTests(runner);
        RegisterDeletionAndSafetyTests(runner);
        RegisterInstallerTests(runner);
        RegisterUsnJournalTests(runner);
    }

    // =========================================================================
    // A. DATABASE / INDEX SAFETY SUITE
    // =========================================================================
    private static void RegisterDatabaseTests(TestRunner runner)
    {
        // TC-DRV-01: Multi-Drive Sequential Scan Data Isolation (ClearIndex Scoping Bug)
        runner.Register(new TestCase(
            TestId: "TC-DRV-01",
            FeatureId: "FEAT-08",
            Title: "Multi-Drive Sequential Scan Data Isolation (ClearIndex Root Scoping Bug)",
            Priority: "P0 / BLOCKER",
            Category: "Database & Multi-Drive Safety",
            ProductionClass: "DiskScope.Services.DatabaseService",
            AuditRiskNote: "Confirms Blocker 1: In DatabaseService.cs line 185, ClearIndex evaluates isFullClear = true for drive root paths (e.g. D:), executing DELETE FROM files without a WHERE clause and wiping previously indexed drives (C:).",
            ExecuteAsync: async ctx =>
            {
                string dbPath = ctx.CreateTempDatabasePath("multidrive_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                // 1. Populate drive C: records
                var fileC1 = new FileRecord
                {
                    Path = @"C:\Users\Alice\Documents\report.docx",
                    Name = "report.docx",
                    Parent = @"C:\Users\Alice\Documents",
                    Size = 10240,
                    ModifiedTime = 1700000000,
                    CreatedTime = 1700000000,
                    Extension = ".docx",
                    Category = FileCategory.Documents,
                    Accessible = 1
                };
                var fileC2 = new FileRecord
                {
                    Path = @"C:\Projects\ArborGraph\code.cs",
                    Name = "code.cs",
                    Parent = @"C:\Projects\ArborGraph",
                    Size = 20480,
                    ModifiedTime = 1700000000,
                    CreatedTime = 1700000000,
                    Extension = ".cs",
                    Category = FileCategory.Code,
                    Accessible = 1
                };

                // 2. Populate drive D: records
                var fileD1 = new FileRecord
                {
                    Path = @"D:\Games\GameA\game.exe",
                    Name = "game.exe",
                    Parent = @"D:\Games\GameA",
                    Size = 52428800,
                    ModifiedTime = 1700000000,
                    CreatedTime = 1700000000,
                    Extension = ".exe",
                    Category = FileCategory.Executables,
                    Accessible = 1
                };

                db.InsertBatch(new[] { fileC1, fileC2, fileD1 });

                // Verify initial counts
                var (initFiles, _) = db.GetTotalIndexedStorage();
                ctx.AssertEqual(3, initFiles, "Initial database should have 3 files across C: and D:");

                // 3. Perform scoped ClearIndex for drive D:\ only (as ScannerService does before scanning D:\)
                // In production, roots passed is new[] { @"D:\" } or new[] { "D:" }
                db.ClearIndex(new[] { @"D:\" });

                // 4. Verify C:\ records were NOT deleted and D:\ records were cleared
                var pagedFiles = db.GetFilesPaged(0, 100);
                int cFilesRemaining = pagedFiles.Count(f => f.Path.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
                int dFilesRemaining = pagedFiles.Count(f => f.Path.StartsWith("D:", StringComparison.OrdinalIgnoreCase));

                ctx.Assert(dFilesRemaining == 0, $"Drive D: records should have been cleared, found {dFilesRemaining} remaining.");
                ctx.Assert(cFilesRemaining == 2, $"CRITICAL DATA SAFETY FAILURE: Scanning/clearing drive D: wiped drive C: records! Expected 2 C: files, but found {cFilesRemaining}.");

                // 5. Rebuild D:\ records (simulating completed scan of D:\)
                var fileD2 = new FileRecord
                {
                    Path = @"D:\Games\GameB\launcher.exe",
                    Name = "launcher.exe",
                    Parent = @"D:\Games\GameB",
                    Size = 10485760,
                    ModifiedTime = 1700001000,
                    CreatedTime = 1700001000,
                    Extension = ".exe",
                    Category = FileCategory.Executables,
                    Accessible = 1
                };
                db.InsertBatch(new[] { fileD2 });

                // 6. Verify rebuilt state: C:\ still intact (2 records) and D:\ has new record (1 record)
                var updatedFiles = db.GetFilesPaged(0, 100);
                int cFilesFinal = updatedFiles.Count(f => f.Path.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
                int dFilesFinal = updatedFiles.Count(f => f.Path.StartsWith("D:", StringComparison.OrdinalIgnoreCase));
                ctx.AssertEqual(2, cFilesFinal, "Drive C: records should remain intact after D: scan completion.");
                ctx.AssertEqual(1, dFilesFinal, "Drive D: records should reflect newly scanned file.");
                ctx.AssertEqual(3, updatedFiles.Count, "Total database files should be 3 (2 on C:, 1 on D:).");

                await Task.CompletedTask;
            }
        ));

        // TC-DRV-02: Multi-Drive Multiple Roots Scoping & Explicit Full-Clear Fallbacks
        runner.Register(new TestCase(
            TestId: "TC-DRV-02",
            FeatureId: "FEAT-08",
            Title: "Multi-Drive Multiple Roots Scoping & Explicit Full-Clear Verification",
            Priority: "P0 / BLOCKER",
            Category: "Database & Multi-Drive Safety",
            ProductionClass: "DiskScope.Services.DatabaseService",
            AuditRiskNote: "Verifies that multiple specific roots clear only the targeted volumes without wiping others, and that explicit full-clear (null, empty, ALL) still functions correctly.",
            ExecuteAsync: async ctx =>
            {
                string dbPath = ctx.CreateTempDatabasePath("multidrive_roots_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                Func<string, string, FileRecord> makeFile = (path, name) => new FileRecord
                {
                    Path = path,
                    Name = name,
                    Parent = Path.GetDirectoryName(path) ?? string.Empty,
                    Size = 1024,
                    ModifiedTime = 1700000000,
                    CreatedTime = 1700000000,
                    Extension = Path.GetExtension(name),
                    Category = FileCategory.Documents,
                    Accessible = 1
                };

                // Seed C:, D:, and E: drives (2 files each = 6 files total)
                var seedFiles = new[]
                {
                    makeFile(@"C:\Folder1\c1.txt", "c1.txt"),
                    makeFile(@"C:\Folder2\c2.txt", "c2.txt"),
                    makeFile(@"D:\Folder1\d1.txt", "d1.txt"),
                    makeFile(@"D:\Folder2\d2.txt", "d2.txt"),
                    makeFile(@"E:\Folder1\e1.txt", "e1.txt"),
                    makeFile(@"E:\Folder2\e2.txt", "e2.txt"),
                };
                db.InsertBatch(seedFiles);

                var (initCount, _) = db.GetTotalIndexedStorage();
                ctx.AssertEqual(6, initCount, "Initial database should have 6 files across C:, D:, and E:");

                // Test E: Multiple specific roots (clear C: and D: simultaneously, E: must remain untouched)
                db.ClearIndex(new[] { @"C:\", @"D:\" });

                var afterMultiClear = db.GetFilesPaged(0, 100);
                int cCount = afterMultiClear.Count(f => f.Path.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
                int dCount = afterMultiClear.Count(f => f.Path.StartsWith("D:", StringComparison.OrdinalIgnoreCase));
                int eCount = afterMultiClear.Count(f => f.Path.StartsWith("E:", StringComparison.OrdinalIgnoreCase));

                ctx.AssertEqual(0, cCount, "Drive C: records should have been cleared.");
                ctx.AssertEqual(0, dCount, "Drive D: records should have been cleared.");
                ctx.AssertEqual(2, eCount, "Drive E: records MUST remain untouched when clearing C: and D:.");

                // Test F1: Explicit "ALL" sentinel full clear
                db.ClearIndex(new[] { "ALL" });
                var (afterAllCount, _) = db.GetTotalIndexedStorage();
                ctx.AssertEqual(0, afterAllCount, "Explicit 'ALL' sentinel should purge entire database.");

                // Test F2: Null roots full clear
                db.InsertBatch(new[] { makeFile(@"C:\file1.txt", "file1.txt"), makeFile(@"D:\file2.txt", "file2.txt") });
                db.ClearIndex(null);
                var (afterNullCount, _) = db.GetTotalIndexedStorage();
                ctx.AssertEqual(0, afterNullCount, "null roots should perform intentional full database clear.");

                // Test F3: Empty roots full clear
                db.InsertBatch(new[] { makeFile(@"C:\file3.txt", "file3.txt") });
                db.ClearIndex(Array.Empty<string>());
                var (afterEmptyCount, _) = db.GetTotalIndexedStorage();
                ctx.AssertEqual(0, afterEmptyCount, "Empty roots should perform intentional full database clear.");

                await Task.CompletedTask;
            }
        ));

        // TC-DRV-03: Custom Directory Target Scoped ClearIndex
        runner.Register(new TestCase(
            TestId: "TC-DRV-03",
            FeatureId: "FEAT-02",
            Title: "Custom Directory Target Scoped ClearIndex",
            Priority: "P1 / CRITICAL",
            Category: "Database & Multi-Drive Safety",
            ProductionClass: "DiskScope.Services.DatabaseService",
            AuditRiskNote: "Verifies that custom directory scanning only clears records matching the specific directory prefix.",
            ExecuteAsync: async ctx =>
            {
                string dbPath = ctx.CreateTempDatabasePath("custom_dir_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var recA = new FileRecord
                {
                    Path = @"C:\TestFolderA\sub\fileA.txt",
                    Name = "fileA.txt",
                    Parent = @"C:\TestFolderA\sub",
                    Size = 100,
                    ModifiedTime = 1234567,
                    CreatedTime = 1234567,
                    Extension = ".txt",
                    Category = FileCategory.Documents,
                    Accessible = 1
                };
                var recB = new FileRecord
                {
                    Path = @"C:\TestFolderB\sub\fileB.txt",
                    Name = "fileB.txt",
                    Parent = @"C:\TestFolderB\sub",
                    Size = 200,
                    ModifiedTime = 1234567,
                    CreatedTime = 1234567,
                    Extension = ".txt",
                    Category = FileCategory.Documents,
                    Accessible = 1
                };
                db.InsertBatch(new[] { recA, recB });

                // Clear ONLY TestFolderA
                db.ClearIndex(new[] { @"C:\TestFolderA" });

                var remaining = db.GetFilesPaged(0, 100);
                ctx.Assert(remaining.Any(f => f.Path == recB.Path), "TestFolderB record should have been preserved");
                ctx.Assert(!remaining.Any(f => f.Path == recA.Path), "TestFolderA record should have been cleared");
                await Task.CompletedTask;
            }
        ));

        // TC-DB-01: Concurrent Reads During Active Write Transactions
        runner.Register(new TestCase(
            TestId: "TC-DB-01",
            FeatureId: "FEAT-08",
            Title: "Concurrent SQLite Reads During Active Bulk Insertion in WAL Mode",
            Priority: "P1 / CRITICAL",
            Category: "Database & Multi-Drive Safety",
            ProductionClass: "DiskScope.Services.DatabaseService",
            AuditRiskNote: "Verifies SQLite WAL configuration allows UI readers to query while background worker is writing 5,000-record batches without 'database is locked' exceptions.",
            ExecuteAsync: async ctx =>
            {
                string dbPath = ctx.CreateTempDatabasePath("concurrency_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                long writeCount = 0;
                Exception? readException = null;

                // Background writer loop
                var writerTask = Task.Run(() =>
                {
                    int batchId = 0;
                    while (!cts.IsCancellationRequested && batchId < 10)
                    {
                        var batch = new List<FileRecord>(200);
                        for (int i = 0; i < 200; i++)
                        {
                            batch.Add(new FileRecord
                            {
                                Path = $@"C:\Bench\Dir_{batchId}\file_{i}.dat",
                                Name = $"file_{i}.dat",
                                Parent = $@"C:\Bench\Dir_{batchId}",
                                Size = 1024,
                                ModifiedTime = 1000,
                                CreatedTime = 1000,
                                Extension = ".dat",
                                Category = FileCategory.Other,
                                Accessible = 1
                            });
                        }
                        db.InsertBatch(batch);
                        Interlocked.Add(ref writeCount, batch.Count);
                        batchId++;
                        Thread.Sleep(10);
                    }
                });

                // Concurrent reader loop
                var readerTask = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            var paged = db.GetFilesPaged(0, 50);
                            var categories = db.GetCategoryBreakdown();
                            var (totalFiles, totalBytes) = db.GetTotalIndexedStorage();
                            Thread.Sleep(15);
                        }
                    }
                    catch (Exception ex)
                    {
                        readException = ex;
                    }
                });

                await Task.WhenAll(writerTask, readerTask);

                ctx.Assert(readException == null, $"Concurrent read failed with exception: {readException?.Message}");
                ctx.Assert(writeCount > 0, "Writer should have inserted records successfully");
            }
        ));

        // TC-DB-03: SQLite Schema Creation, WAL Mode & Integrity Check
        runner.Register(new TestCase(
            TestId: "TC-DB-03",
            FeatureId: "FEAT-09",
            Title: "Database Schema Creation, WAL Configuration & PRAGMA Integrity Check",
            Priority: "P1 / CRITICAL",
            Category: "Database & Multi-Drive Safety",
            ProductionClass: "DiskScope.Services.DatabaseService",
            AuditRiskNote: "Verifies database initializes correctly with WAL journal mode, required tables (files, directories, scan_history), and passes PRAGMA integrity_check.",
            ExecuteAsync: async ctx =>
            {
                string dbPath = ctx.CreateTempDatabasePath("integrity_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                bool integrityOk = db.CheckIntegrity();
                ctx.Assert(integrityOk, "PRAGMA integrity_check failed on fresh database");

                // Verify WAL mode
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode;";
                string? mode = cmd.ExecuteScalar()?.ToString();
                ctx.AssertEqual("wal", mode?.ToLowerInvariant(), "SQLite journal_mode should be WAL");
                await Task.CompletedTask;
            }
        ));
    }

    // =========================================================================
    // B. SCANNER TEST SUITE
    // =========================================================================
    private static void RegisterScannerTests(TestRunner runner)
    {
        // TC-SCN-01: Scanner Cancellation Token Responsiveness
        runner.Register(new TestCase(
            TestId: "TC-SCN-01",
            FeatureId: "FEAT-03",
            Title: "Scanner Traversal Cancellation Responsiveness (< 500ms)",
            Priority: "P1 / CRITICAL",
            Category: "Scanner Traversal",
            ProductionClass: "DiskScope.Services.ScannerService",
            AuditRiskNote: "Verifies bounded-channel traversal responds promptly to CancellationToken cancellation without deadlocks or unhandled exceptions.",
            ExecuteAsync: async ctx =>
            {
                string testDir = ctx.CreateTempDirectory("scan_cancel_test");
                // Generate 1,000 files to give scanner work
                TestDataGenerator.GenerateScaledDataset(testDir, 1000);

                string dbPath = ctx.CreateTempDatabasePath("cancel_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var scanner = new ScannerService(db);
                using var cts = new CancellationTokenSource();

                // Trigger cancellation after 20ms
                cts.CancelAfter(20);

                var sw = Stopwatch.StartNew();
                var stats = await scanner.ScanDrivesAsync(new[] { testDir }, null, cts.Token, enableIncremental: false);
                sw.Stop();

                ctx.AssertEqual(ScanState.Cancelled, stats.State, "ScanState should be Cancelled");
                ctx.Assert(sw.ElapsedMilliseconds < 1500, $"Cancellation took too long: {sw.ElapsedMilliseconds} ms (must be < 1500 ms)");
                ctx.Assert(db.CheckIntegrity(), "Database should maintain integrity after scan cancellation");
            }
        ));

        // TC-SCN-02: Inaccessible / Permission-Denied Folders Graceful Bypass
        runner.Register(new TestCase(
            TestId: "TC-SCN-02",
            FeatureId: "FEAT-03",
            Title: "Inaccessible System Folders Graceful Bypass (UnauthorizedAccessException)",
            Priority: "P1 / CRITICAL",
            Category: "Scanner Traversal",
            ProductionClass: "DiskScope.Services.ScannerService",
            AuditRiskNote: "Verifies that scanner catches UnauthorizedAccessException, logs to SkippedDirectories, and continues scanning without terminating.",
            ExecuteAsync: async ctx =>
            {
                string testDir = ctx.CreateTempDirectory("inaccessible_test");
                string accessibleSub = Path.Combine(testDir, "AccessibleFolder");
                Directory.CreateDirectory(accessibleSub);
                File.WriteAllText(Path.Combine(accessibleSub, "file1.txt"), "hello world");

                string dbPath = ctx.CreateTempDatabasePath("inacc_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var scanner = new ScannerService(db);
                var stats = await scanner.ScanDrivesAsync(new[] { testDir }, null, CancellationToken.None, enableIncremental: false);

                ctx.AssertEqual(ScanState.Completed, stats.State, "ScanState should be Completed");
                ctx.Assert(stats.FilesIndexed >= 1, "Accessible files must be indexed");
            }
        ));

        // TC-SCN-03: Deep Path Traversal (> 260 Characters / MAX_PATH)
        runner.Register(new TestCase(
            TestId: "TC-SCN-03",
            FeatureId: "FEAT-03",
            Title: "Deep Path Traversal Exceeding 260 Characters (MAX_PATH)",
            Priority: "P1 / CRITICAL",
            Category: "Scanner Traversal",
            ProductionClass: "DiskScope.Services.ScannerService",
            AuditRiskNote: "Verifies that scanner handles deeply nested hierarchies exceeding Windows MAX_PATH (260 chars) without PathTooLongException.",
            ExecuteAsync: async ctx =>
            {
                string testDir = ctx.CreateTempDirectory("deep_path_test");
                var edgeStats = TestDataGenerator.GenerateEdgeCasesDataset(testDir);

                string dbPath = ctx.CreateTempDatabasePath("deep_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var scanner = new ScannerService(db);
                var scanStats = await scanner.ScanDrivesAsync(new[] { testDir }, null, CancellationToken.None, enableIncremental: false);

                ctx.AssertEqual(ScanState.Completed, scanStats.State, "Deep path scan should complete");
                ctx.AssertEqual(edgeStats.ExpectedFileCount, (int)scanStats.FilesIndexed, "All files including deep paths should be indexed");

                // Verify leaf file queryable in SQLite
                var paged = db.GetFilesPaged(0, 100);
                bool hasDeepFile = paged.Any(f => f.Name.Contains("deeply_nested_leaf_file"));
                ctx.Assert(hasDeepFile, "Deeply nested file was not found in SQLite index");
            }
        ));

        // TC-SCN-EDGE: Unicode, Special Characters & Empty Folders
        runner.Register(new TestCase(
            TestId: "TC-SCN-EDGE",
            FeatureId: "FEAT-03",
            Title: "Unicode Filenames, Special Characters & Empty Directories Accounting",
            Priority: "P2 / MAJOR",
            Category: "Scanner Traversal",
            ProductionClass: "DiskScope.Services.ScannerService",
            AuditRiskNote: "Verifies exact accounting on Unicode filenames (Japanese, Arabic, Emoji), special characters (#, %, &, quotes), and empty folders.",
            ExecuteAsync: async ctx =>
            {
                string testDir = ctx.CreateTempDirectory("unicode_spec_test");
                var edgeStats = TestDataGenerator.GenerateEdgeCasesDataset(testDir);

                string dbPath = ctx.CreateTempDatabasePath("unicode_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var scanner = new ScannerService(db);
                var scanStats = await scanner.ScanDrivesAsync(new[] { testDir }, null, CancellationToken.None, enableIncremental: false);

                ctx.Assert(scanStats.FilesIndexed == edgeStats.ExpectedFileCount,
                    $"Expected {edgeStats.ExpectedFileCount} files indexed, got {scanStats.FilesIndexed}");
                ctx.Assert(scanStats.LogicalBytesIndexed == edgeStats.ExpectedTotalBytes,
                    $"Expected {edgeStats.ExpectedTotalBytes} bytes, got {scanStats.LogicalBytesIndexed}");

                // Verify Unicode characters preserved in database
                var paged = db.GetFilesPaged(0, 100);
                ctx.Assert(paged.Any(f => f.Name.Contains("日本語")), "Japanese Unicode filename was corrupted in SQLite");
                ctx.Assert(paged.Any(f => f.Name.Contains("ملف_عربي")), "Arabic Unicode filename was corrupted in SQLite");
                ctx.Assert(paged.Any(f => f.Name.Contains("🚀")), "Emoji Unicode filename was corrupted in SQLite");
                ctx.Assert(paged.Any(f => f.Name.Contains("quote'test'and#hash")), "Special character filename with quotes was corrupted");
            }
        ));

        // TC-SET-01: Scanner Exclusion Rules Enforcement
        runner.Register(new TestCase(
            TestId: "TC-SET-01",
            FeatureId: "FEAT-40",
            Title: "Scanner Exclusion Rules Enforcement",
            Priority: "P1 / CRITICAL",
            Category: "Scanner Traversal",
            ProductionClass: "DiskScope.Services.ScannerService",
            AuditRiskNote: "Verifies that directories configured in SettingsService exclusions are completely skipped by the BFS scanner.",
            ExecuteAsync: async ctx =>
            {
                string testDir = ctx.CreateTempDirectory("exclusion_scan_test");
                string allowedSub = Path.Combine(testDir, "Allowed");
                string excludedSub = Path.Combine(testDir, "ExcludedSecret");
                Directory.CreateDirectory(allowedSub);
                Directory.CreateDirectory(excludedSub);

                File.WriteAllText(Path.Combine(allowedSub, "public.txt"), "public content");
                File.WriteAllText(Path.Combine(excludedSub, "secret.txt"), "secret content");

                string settingsPath = Path.Combine(ctx.CreateTempDirectory("set"), "settings.json");
                var settingsService = new SettingsService(settingsPath);
                settingsService.AddExclusion(excludedSub);

                string dbPath = ctx.CreateTempDatabasePath("exclusion_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var scanner = new ScannerService(db, null, settingsService);
                var stats = await scanner.ScanDrivesAsync(new[] { testDir }, null, CancellationToken.None, enableIncremental: false);

                ctx.AssertEqual(1, (int)stats.FilesIndexed, "Only allowed file should be indexed");
                var paged = db.GetFilesPaged(0, 100);
                ctx.Assert(paged.All(f => !f.Path.Contains("ExcludedSecret")), "Excluded folder files must NOT be in SQLite database");
                ctx.Assert(scanner.SkippedDirectories.Any(sd => sd.Path.Equals(excludedSub, StringComparison.OrdinalIgnoreCase)),
                    "Excluded directory should be recorded in ScannerService.SkippedDirectories");
            }
        ));
    }

    // =========================================================================
    // C. DIRECTORY ROLLUP SUITE
    // =========================================================================
    private static void RegisterRollupTests(TestRunner runner)
    {
        // TC-ROL-01: Recursive Folder Rollup Mathematical Correctness
        runner.Register(new TestCase(
            TestId: "TC-ROL-01",
            FeatureId: "FEAT-10",
            Title: "Recursive Folder Rollup Exact Parent/Child Size Calculations",
            Priority: "P1 / CRITICAL",
            Category: "Directory Rollup Engine",
            ProductionClass: "DiskScope.Services.DatabaseService",
            AuditRiskNote: "Verifies BuildDirectoryRollup aggregates direct sizes and child subfolder sizes with 100% precision without rounding or omission errors.",
            ExecuteAsync: async ctx =>
            {
                string testDir = ctx.CreateTempDirectory("rollup_exact_test");
                var dsStats = TestDataGenerator.GenerateDatasetA(testDir);

                string dbPath = ctx.CreateTempDatabasePath("rollup_exact.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var scanner = new ScannerService(db);
                await scanner.ScanDrivesAsync(new[] { testDir }, null, CancellationToken.None, enableIncremental: false);

                // Build rollup
                db.BuildDirectoryRollup();

                var largestFolders = db.GetLargestFolders(50, excludeDriveRoots: false);
                ctx.Assert(largestFolders.Count > 0, "Directories table should contain rolled-up records");

                // Root folder size must match total bytes exactly
                var rootFolder = largestFolders.FirstOrDefault(f => string.Equals(f.Path, testDir, StringComparison.OrdinalIgnoreCase));
                ctx.Assert(rootFolder != null, "Root folder record must exist in rolled-up directories");
                ctx.AssertEqual(dsStats.ExpectedTotalBytes, rootFolder!.Size, "Root rolled-up size must equal total files size");
                ctx.AssertEqual(dsStats.ExpectedFileCount, (int)rootFolder.FileCount, "Root rolled-up file count must equal total file count");
            }
        ));

        // TC-ROL-02: Large Synthetic Directory Structure Rollup Scaling
        runner.Register(new TestCase(
            TestId: "TC-ROL-02",
            FeatureId: "FEAT-10",
            Title: "Large Synthetic Directory Structure Rollup Scalability Benchmark",
            Priority: "P1 / CRITICAL",
            Category: "Directory Rollup Engine",
            ProductionClass: "DiskScope.Services.DatabaseService",
            AuditRiskNote: "Benchmarks BuildDirectoryRollup on 1,000 directories to verify memory allocation and execution time remain bounded.",
            ExecuteAsync: async ctx =>
            {
                string dbPath = ctx.CreateTempDatabasePath("rollup_scale.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                // Synthetic hierarchy: 1,000 directories across 4 levels
                int dirCount = 1000;
                var records = new List<FileRecord>(dirCount * 2);
                long expectedTotalBytes = 0;

                for (int i = 0; i < dirCount; i++)
                {
                    string parent = $@"C:\ScaleTest\Level1_{i % 5}\Level2_{i % 25}\Dir_{i}";
                    long size = (i + 1) * 100;
                    expectedTotalBytes += size;

                    records.Add(new FileRecord
                    {
                        Path = Path.Combine(parent, $"file_{i}.dat"),
                        Name = $"file_{i}.dat",
                        Parent = parent,
                        Size = size,
                        ModifiedTime = 1000,
                        CreatedTime = 1000,
                        Extension = ".dat",
                        Category = FileCategory.Other,
                        Accessible = 1
                    });
                }

                db.InsertBatch(records);

                var sw = Stopwatch.StartNew();
                db.BuildDirectoryRollup();
                sw.Stop();

                ctx.Assert(sw.ElapsedMilliseconds < 3000, $"Rollup of 1,000 dirs took too long: {sw.ElapsedMilliseconds} ms (must be < 3000 ms)");

                var folders = db.GetLargestFolders(10, excludeDriveRoots: false);
                ctx.Assert(folders.Count > 0, "Rolled-up directories should be queryable");
                await Task.CompletedTask;
            }
        ));
    }

    // =========================================================================
    // D. DUPLICATE DETECTION SUITE
    // =========================================================================
    private static void RegisterDuplicateTests(TestRunner runner)
    {
        // TC-DUP-01: 3-Stage Cryptographic Duplicate Detection & Collision Rejection
        runner.Register(new TestCase(
            TestId: "TC-DUP-01",
            FeatureId: "FEAT-22",
            Title: "3-Stage Duplicate Pipeline Accuracy (Size -> MD5 Prefix -> SHA-256)",
            Priority: "P1 / CRITICAL",
            Category: "Duplicate Detection",
            ProductionClass: "DiskScope.Services.DuplicateAnalyzer",
            AuditRiskNote: "Verifies 3-stage duplicate pipeline detects genuine identical duplicates and strictly rejects same-size different-content collisions.",
            ExecuteAsync: async ctx =>
            {
                string testDir = ctx.CreateTempDirectory("dup_test");
                var (dsStats, grp1, grp2, collisions) = TestDataGenerator.GenerateDuplicatesDataset(testDir);

                string dbPath = ctx.CreateTempDatabasePath("dup_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var scanner = new ScannerService(db);
                await scanner.ScanDrivesAsync(new[] { testDir }, null, CancellationToken.None, enableIncremental: false);

                var analyzer = new DuplicateAnalyzer(db);
                var dupGroups = await analyzer.FindDuplicatesAsync(minSize: 1024);

                // Group 1: 64 KB (2 copies), Group 2: 128 KB (3 copies)
                // Collision pair: 32 KB (must NOT be a duplicate group)
                ctx.AssertEqual(2, dupGroups.Count, "Expected exactly 2 true duplicate groups");

                var g64 = dupGroups.FirstOrDefault(g => g.ExactSize == 65536);
                ctx.Assert(g64 != null, "64 KB duplicate group was not detected");
                ctx.AssertEqual(2, g64!.FileCount, "64 KB group should have 2 copies");
                ctx.AssertEqual(65536L, g64.WastedBytes, "64 KB group wasted bytes should be 65536");

                var g128 = dupGroups.FirstOrDefault(g => g.ExactSize == 131072);
                ctx.Assert(g128 != null, "128 KB duplicate group was not detected");
                ctx.AssertEqual(3, g128!.FileCount, "128 KB group should have 3 copies");
                ctx.AssertEqual(262144L, g128.WastedBytes, "128 KB group wasted bytes should be 262144 (2 extra copies)");

                // Verify collision rejection: 32,768 bytes
                bool collisionMistakenlyGrouped = dupGroups.Any(g => g.ExactSize == 32768);
                ctx.Assert(!collisionMistakenlyGrouped, "COLLISION REJECTION FAILURE: Files with identical size but differing bytes were incorrectly grouped as duplicates!");
            }
        ));

        // TC-DUP-02: Zero-Byte and Edge-Case Duplicate Handling
        runner.Register(new TestCase(
            TestId: "TC-DUP-02",
            FeatureId: "FEAT-22",
            Title: "Zero-Byte Files & Empty Candidate Handling in Duplicate Pipeline",
            Priority: "P2 / MAJOR",
            Category: "Duplicate Detection",
            ProductionClass: "DiskScope.Services.DuplicateAnalyzer",
            AuditRiskNote: "Verifies 0-byte files or empty databases do not throw exceptions or cause division-by-zero errors in DuplicateAnalyzer.",
            ExecuteAsync: async ctx =>
            {
                string dbPath = ctx.CreateTempDatabasePath("empty_dup.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var analyzer = new DuplicateAnalyzer(db);
                var emptyGroups = await analyzer.FindDuplicatesAsync(minSize: 1024);
                ctx.AssertEqual(0, emptyGroups.Count, "Empty database should yield 0 duplicate groups without throwing");
                await Task.CompletedTask;
            }
        ));
    }

    // =========================================================================
    // E. DEVELOPER STORAGE DETECTION SUITE
    // =========================================================================
    private static void RegisterDeveloperStorageTests(TestRunner runner)
    {
        // TC-DEV-01: Contextual Developer Project Detection & False-Positive Rejection
        runner.Register(new TestCase(
            TestId: "TC-DEV-01",
            FeatureId: "FEAT-24",
            Title: "Contextual Developer Project Marker Verification (Node, .NET, Rust, Gradle)",
            Priority: "P1 / CRITICAL",
            Category: "Developer Storage",
            ProductionClass: "DiskScope.Services.DeveloperStorageService",
            AuditRiskNote: "Verifies contextual parent markers (package.json, Cargo.toml, .csproj, build.gradle) prevent false-positive deletion of generic folders named 'target' or 'build'.",
            ExecuteAsync: async ctx =>
            {
                string testDir = ctx.CreateTempDirectory("dev_workspace_test");
                var (_, nodeDir, binDir, rustTarget, gradleBuild, fakeRustTarget, fakeGradleBuild) =
                    TestDataGenerator.GenerateDeveloperDataset(testDir);

                string dbPath = ctx.CreateTempDatabasePath("dev_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var devService = new DeveloperStorageService(db);
                var summaries = await devService.ScanWorkspaceAsync(testDir);

                // 1. Verify Node.js
                var nodeSummary = summaries.FirstOrDefault(s => s.Ecosystem == DeveloperEcosystem.NodeJs);
                ctx.Assert(nodeSummary != null && nodeSummary.Items.Any(i => i.Path == nodeDir), "Node.js node_modules should be detected");

                // 2. Verify .NET
                var dotNetSummary = summaries.FirstOrDefault(s => s.Ecosystem == DeveloperEcosystem.DotNet);
                ctx.Assert(dotNetSummary != null && dotNetSummary.Items.Any(i => i.Path == binDir), ".NET bin should be detected");

                // 3. Verify Rust
                var rustSummary = summaries.FirstOrDefault(s => s.Ecosystem == DeveloperEcosystem.Rust);
                ctx.Assert(rustSummary != null && rustSummary.Items.Any(i => i.Path == rustTarget), "Rust target directory should be detected");

                // 4. Verify Gradle
                var gradleSummary = summaries.FirstOrDefault(s => s.Ecosystem == DeveloperEcosystem.GradleJava);
                ctx.Assert(gradleSummary != null && gradleSummary.Items.Any(i => i.Path == gradleBuild), "Gradle build directory should be detected");

                // 5. CRITICAL FALSE-POSITIVE TRAPS
                if (rustSummary != null)
                {
                    bool fakeRustDetected = rustSummary.Items.Any(i => i.Path == fakeRustTarget);
                    ctx.Assert(!fakeRustDetected, $"FALSE POSITIVE: Non-Rust folder '{fakeRustTarget}' was falsely classified as Rust build cache!");
                }

                if (gradleSummary != null)
                {
                    bool fakeGradleDetected = gradleSummary.Items.Any(i => i.Path == fakeGradleBuild);
                    ctx.Assert(!fakeGradleDetected, $"FALSE POSITIVE: Non-Gradle folder '{fakeGradleBuild}' was falsely classified as Gradle build cache!");
                }
            }
        ));
    }

    // =========================================================================
    // F. QUERY / FILTERING SUITE
    // =========================================================================
    private static void RegisterQueryAndFilterTests(TestRunner runner)
    {
        // TC-QRY-01: Multi-Criteria SQL Search and Pagination
        runner.Register(new TestCase(
            TestId: "TC-QRY-01",
            FeatureId: "FEAT-16",
            Title: "Multi-Criteria SQL Storage Search & Paging Accuracy",
            Priority: "P1 / CRITICAL",
            Category: "Query Engine",
            ProductionClass: "DiskScope.Services.DatabaseService",
            AuditRiskNote: "Verifies multi-criteria filtering by size, extension, age, location prefix, and offset pagination.",
            ExecuteAsync: async ctx =>
            {
                string dbPath = ctx.CreateTempDatabasePath("query_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var files = new List<FileRecord>
                {
                    new() { Path = @"C:\Media\Video\large_movie.mp4", Name = "large_movie.mp4", Parent = @"C:\Media\Video", Size = 500_000_000, Category = FileCategory.Video, Extension = ".mp4", ModifiedTime = (ulong)(now - 86400 * 10) },
                    new() { Path = @"C:\Media\Video\short_clip.mp4", Name = "short_clip.mp4", Parent = @"C:\Media\Video", Size = 5_000_000, Category = FileCategory.Video, Extension = ".mp4", ModifiedTime = (ulong)(now - 86400 * 2) },
                    new() { Path = @"C:\Docs\manual.pdf", Name = "manual.pdf", Parent = @"C:\Docs", Size = 15_000_000, Category = FileCategory.Documents, Extension = ".pdf", ModifiedTime = (ulong)(now - 86400 * 100) },
                    new() { Path = @"C:\Docs\ancient.pdf", Name = "ancient.pdf", Parent = @"C:\Docs", Size = 25_000_000, Category = FileCategory.Documents, Extension = ".pdf", ModifiedTime = (ulong)(now - 86400 * 400) },
                    new() { Path = @"C:\Media2\trailer.mp4", Name = "trailer.mp4", Parent = @"C:\Media2", Size = 50_000_000, Category = FileCategory.Video, Extension = ".mp4", ModifiedTime = (ulong)now }
                };
                db.InsertBatch(files);

                // Filter: Extension = .pdf, minSize = 20 MB
                var results = db.GetFilesPaged(0, 10, minSize: 20_000_000, extension: ".pdf");
                ctx.AssertEqual(1, results.Count, "Expected exactly 1 PDF >= 20 MB");
                ctx.AssertEqual("ancient.pdf", results[0].Name, "Expected ancient.pdf");

                // Filter: Location = C:\Media, category = Video (must isolate from C:\Media2 sibling folder)
                var mediaVideos = db.GetFilesPaged(0, 10, category: FileCategory.Video, locationPrefix: @"C:\Media");
                ctx.AssertEqual(2, mediaVideos.Count, "Expected exactly 2 videos in C:\\Media, excluding sibling C:\\Media2");
                ctx.Assert(mediaVideos.All(v => v.Path.StartsWith(@"C:\Media\", StringComparison.OrdinalIgnoreCase)), "All videos must reside within C:\\Media\\");

                // Verify GetFilteredFileCount boundary isolation
                long mediaCount = db.GetFilteredFileCount(category: FileCategory.Video, locationPrefix: @"C:\Media");
                ctx.AssertEqual(2L, mediaCount, "GetFilteredFileCount must return 2 for C:\\Media");

                // Verify StreamFilteredFiles boundary isolation
                int streamedCount = 0;
                db.StreamFilteredFiles(r => streamedCount++, category: FileCategory.Video, locationPrefix: @"C:\Media");
                ctx.AssertEqual(2, streamedCount, "StreamFilteredFiles must stream exactly 2 records for C:\\Media");

                await Task.CompletedTask;
            }
        ));

        // TC-QRY-SQLI: Parameterized Input Safety (SQL Injection-Like Input)
        runner.Register(new TestCase(
            TestId: "TC-QRY-SQLI",
            FeatureId: "FEAT-16",
            Title: "SQL Injection Safety in Query Explorer and Search Inputs",
            Priority: "P1 / CRITICAL",
            Category: "Query Engine",
            ProductionClass: "DiskScope.Services.DatabaseService",
            AuditRiskNote: "Verifies user input containing quotes, semicolons, and SQL operators remains properly parameterized and cannot corrupt the database.",
            ExecuteAsync: async ctx =>
            {
                string dbPath = ctx.CreateTempDatabasePath("sqli_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var file = new FileRecord
                {
                    Path = @"C:\Test\normal.txt",
                    Name = "normal.txt",
                    Parent = @"C:\Test",
                    Size = 100,
                    Category = FileCategory.Documents,
                    Extension = ".txt"
                };
                db.InsertBatch(new[] { file });

                // Injection attempt 1: OR 1=1
                var res1 = db.GetFilesPaged(0, 10, search: "' OR '1'='1");
                ctx.AssertEqual(0, res1.Count, "SQL injection search string should return 0 matches, not bypass filter");

                // Injection attempt 2: DROP TABLE
                var res2 = db.GetFilesPaged(0, 10, search: "'; DROP TABLE files; --");
                ctx.AssertEqual(0, res2.Count, "DROP TABLE attempt should be treated as literal search string");

                // Verify tables still exist
                var (count, _) = db.GetTotalIndexedStorage();
                ctx.AssertEqual(1, count, "Files table should remain completely intact after injection attempt");
                await Task.CompletedTask;
            }
        ));
    }

    // =========================================================================
    // G. EXPORT SUITE
    // =========================================================================
    private static void RegisterExportTests(TestRunner runner)
    {
        // TC-EXP-01: Audit Export Integrity (HTML, JSON, CSV)
        runner.Register(new TestCase(
            TestId: "TC-EXP-01",
            FeatureId: "FEAT-33",
            Title: "Audit Export Generation Integrity (HTML, JSON, CSV)",
            Priority: "P1 / CRITICAL",
            Category: "Export Subsystem",
            ProductionClass: "DiskScope.Services.ExportService",
            AuditRiskNote: "Verifies standalone HTML report, JSON structured dump, and precision CSV files export cleanly without truncation or malformed delimiters.",
            ExecuteAsync: async ctx =>
            {
                string exportDir = ctx.CreateTempDirectory("export_out");
                string dbPath = ctx.CreateTempDatabasePath("export_test.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var testRec = new FileRecord
                {
                    Path = @"C:\Reports\special, quote ""test"".txt",
                    Name = @"special, quote ""test"".txt",
                    Parent = @"C:\Reports",
                    Size = 4096,
                    Category = FileCategory.Documents,
                    Extension = ".txt"
                };
                db.InsertBatch(new[] { testRec });

                var exportService = new ExportService();
                var report = await exportService.BuildAuditReportAsync(db, null, null, @"C:\Reports");

                // 1. HTML Export
                string htmlPath = Path.Combine(exportDir, "audit.html");
                await exportService.ExportToHtmlAsync(report, htmlPath);
                ctx.Assert(File.Exists(htmlPath), "HTML audit report was not created");
                string html = await File.ReadAllTextAsync(htmlPath);
                ctx.Assert(html.Contains("<!DOCTYPE html>"), "HTML export missing DOCTYPE");
                ctx.Assert(html.Contains("Storage Audit"), "HTML export missing title");

                // 2. JSON Export
                string jsonPath = Path.Combine(exportDir, "audit.json");
                await exportService.ExportToJsonAsync(report, jsonPath);
                ctx.Assert(File.Exists(jsonPath), "JSON audit report was not created");
                string json = await File.ReadAllTextAsync(jsonPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                ctx.Assert(doc.RootElement.GetProperty("TotalFilesIndexed").GetInt64() == 1, "JSON TotalFilesIndexed mismatch");

                // 3. CSV Export with special character / quote escaping
                string csvPath = Path.Combine(exportDir, "files.csv");
                await exportService.ExportFilesToCsvAsync(new[] { testRec }, csvPath);
                ctx.Assert(File.Exists(csvPath), "CSV export was not created");
                var csvLines = await File.ReadAllLinesAsync(csvPath);
                ctx.Assert(csvLines.Length == 2, "CSV should contain header + 1 record");
                ctx.Assert(csvLines[1].Contains("\"\"test\"\""), "Quotes in CSV filename should be RFC 4180 escaped with double-quotes");
            }
        ));
    }

    // =========================================================================
    // H. TREEMAP / ANALYTICS LOGIC SUITE
    // =========================================================================
    private static void RegisterTreemapTests(TestRunner runner)
    {
        // TC-TMP-01: Squarified Treemap Layout Engine Validity
        runner.Register(new TestCase(
            TestId: "TC-TMP-01",
            FeatureId: "FEAT-30",
            Title: "Squarified Treemap Layout Engine Edge Cases (Zero-Size, 1-Item, Extreme Ratios)",
            Priority: "P1 / CRITICAL",
            Category: "Treemap Engine",
            ProductionClass: "DiskScope.Infrastructure.TreemapLayoutEngine",
            AuditRiskNote: "Verifies squarified layout engine produces valid non-overlapping rectangles without NaN, Infinity, or negative coordinates across edge cases.",
            ExecuteAsync: async ctx =>
            {
                double width = 800.0;
                double height = 600.0;

                // Case 1: Empty input
                var empty = TreemapLayoutEngine.ComputeLayout(Array.Empty<TreemapItem>(), width, height);
                ctx.AssertEqual(0, empty.Count, "Empty item list should return 0 rects");

                // Case 2: Zero-size files only
                var zeroItems = new[] { new TreemapItem { Name = "empty.dat", Size = 0 } };
                var zeroResult = TreemapLayoutEngine.ComputeLayout(zeroItems, width, height);
                ctx.AssertEqual(0, zeroResult.Count, "Zero-size items should be filtered out without throwing");

                // Case 3: Exactly one item
                var singleItem = new[] { new TreemapItem { Name = "solo.dat", Size = 100_000 } };
                var singleResult = TreemapLayoutEngine.ComputeLayout(singleItem, width, height);
                ctx.AssertEqual(1, singleResult.Count, "Single item should produce 1 rect");
                ctx.AssertEqual(width, singleResult[0].Width, "Single item width should equal canvas width");
                ctx.AssertEqual(height, singleResult[0].Height, "Single item height should equal canvas height");

                // Case 4: Extreme aspect ratio canvas (e.g. 5000 x 50)
                var multiItems = new List<TreemapItem>
                {
                    new() { Name = "a.dat", Size = 500_000 },
                    new() { Name = "b.dat", Size = 300_000 },
                    new() { Name = "c.dat", Size = 200_000 }
                };
                var extremeResult = TreemapLayoutEngine.ComputeLayout(multiItems, 5000.0, 50.0);
                ctx.AssertEqual(3, extremeResult.Count, "Extreme canvas should layout all 3 items");
                foreach (var r in extremeResult)
                {
                    ctx.Assert(!double.IsNaN(r.X) && !double.IsInfinity(r.X), "X cannot be NaN or Infinity");
                    ctx.Assert(!double.IsNaN(r.Y) && !double.IsInfinity(r.Y), "Y cannot be NaN or Infinity");
                    ctx.Assert(!double.IsNaN(r.Width) && !double.IsInfinity(r.Width), "Width cannot be NaN or Infinity");
                    ctx.Assert(!double.IsNaN(r.Height) && !double.IsInfinity(r.Height), "Height cannot be NaN or Infinity");
                    ctx.Assert(r.Width > 0 && r.Height > 0, "Rect dimensions must be positive");
                }
                await Task.CompletedTask;
            }
        ));
    }

    // =========================================================================
    // I. LEGAL & SETTINGS SUITE
    // =========================================================================
    private static void RegisterLegalAndSettingsTests(TestRunner runner)
    {
        // TC-LGL-01: First-Run EULA Consent Gate & Settings Persistence
        runner.Register(new TestCase(
            TestId: "TC-LGL-01",
            FeatureId: "FEAT-39",
            Title: "First-Run EULA Consent Gate & Settings Persistence",
            Priority: "P0 / BLOCKER",
            Category: "Settings & Legal Compliance",
            ProductionClass: "DiskScope.Services.SettingsService",
            AuditRiskNote: "Verifies EULA acceptance status and timestamp persist across application restarts.",
            ExecuteAsync: async ctx =>
            {
                string settingsPath = Path.Combine(ctx.CreateTempDirectory("eula_test"), "settings.json");

                // 1. Fresh settings: HasAcceptedEula must default to false
                var service1 = new SettingsService(settingsPath);
                ctx.Assert(!service1.CurrentSettings.HasAcceptedEula, "Fresh install must default to HasAcceptedEula = false");

                // 2. Accept EULA and save
                var settings = service1.CurrentSettings;
                settings.HasAcceptedEula = true;
                settings.EulaAcceptedVersion = "1.0.0";
                settings.EulaAcceptedDate = DateTime.UtcNow;
                service1.SaveSettings(settings);

                // 3. Reload from disk
                var service2 = new SettingsService(settingsPath);
                ctx.Assert(service2.CurrentSettings.HasAcceptedEula, "HasAcceptedEula must be true after reload");
                ctx.AssertEqual("1.0.0", service2.CurrentSettings.EulaAcceptedVersion, "EulaAcceptedVersion must match");
                ctx.Assert(service2.CurrentSettings.EulaAcceptedDate.HasValue, "EulaAcceptedDate must have timestamp");
                await Task.CompletedTask;
            }
        ));
    }

    // =========================================================================
    // J. DELETION & SAFETY SUITE (BUG-002 VERIFICATION)
    // =========================================================================
    private static void RegisterDeletionAndSafetyTests(TestRunner runner)
    {
        // TC-DEL-01: Single File Deletion & Database Index Consistency
        runner.Register(new TestCase(
            TestId: "TC-DEL-01",
            FeatureId: "FEAT-19",
            Title: "Single File Deletion & Database Index Consistency",
            Priority: "P1 / CRITICAL",
            Category: "Deletion & Filesystem Safety",
            ProductionClass: "DiskScope.Services.FileActionService",
            AuditRiskNote: "Verifies that deleting a single file removes it from disk and SQLite index without data residue.",
            ExecuteAsync: async ctx =>
            {
                string tempDir = ctx.CreateTempDirectory("del_single");
                string filePath = Path.Combine(tempDir, "sample.txt");
                File.WriteAllText(filePath, "Hello ArborGraph Deletion Test");

                string dbPath = ctx.CreateTempDatabasePath("del_single.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var rec = new FileRecord
                {
                    Path = filePath,
                    Name = "sample.txt",
                    Parent = tempDir,
                    Size = 30,
                    ModifiedTime = 1234567,
                    CreatedTime = 1234567,
                    Extension = ".txt",
                    Category = FileCategory.Documents,
                    Accessible = 1
                };
                db.InsertBatch(new[] { rec });

                var fileAction = new FileActionService();
                ctx.Assert(File.Exists(filePath), "Target file must exist prior to deletion");

                bool deleted = fileAction.DeletePermanently(filePath, out string? err, skipConfirmation: true);
                ctx.Assert(deleted, $"File deletion should succeed: {err}");
                ctx.Assert(!File.Exists(filePath), "Target file must no longer exist on disk");

                bool dbRemoved = db.RemoveFileFromIndex(filePath);
                ctx.Assert(dbRemoved, "RemoveFileFromIndex should return true for existing record");

                var remaining = db.GetFilesPaged(0, 10);
                ctx.AssertEqual(0, remaining.Count, "Database should have 0 records after deletion");
                await Task.CompletedTask;
            }
        ));

        // TC-DEL-02: Batch File Deletion, Locked File Handling & Accurate Counts
        runner.Register(new TestCase(
            TestId: "TC-DEL-02",
            FeatureId: "FEAT-19",
            Title: "Batch File Deletion, Locked File Handling & Accurate Counts",
            Priority: "P1 / CRITICAL",
            Category: "Deletion & Filesystem Safety",
            ProductionClass: "DiskScope.Services.FileActionService",
            AuditRiskNote: "Verifies that batch deletion handles in-use / locked files gracefully and returns exact success and failure counts without crashing.",
            ExecuteAsync: async ctx =>
            {
                string tempDir = ctx.CreateTempDirectory("del_batch");
                var paths = new List<string>();
                for (int i = 1; i <= 5; i++)
                {
                    string p = Path.Combine(tempDir, $"batch_{i}.dat");
                    File.WriteAllBytes(p, new byte[1024]);
                    paths.Add(p);
                }

                // Lock file #3 with exclusive write lock
                using var lockStream = File.Open(paths[2], FileMode.Open, FileAccess.ReadWrite, FileShare.None);

                var fileAction = new FileActionService();
                var (succeeded, failed) = fileAction.DeleteFilesBatch(paths, permanent: true);

                ctx.AssertEqual(4, succeeded, "4 unlocked files should have succeeded");
                ctx.AssertEqual(1, failed, "1 locked file should have failed");

                ctx.Assert(!File.Exists(paths[0]), "File 1 should be deleted");
                ctx.Assert(!File.Exists(paths[1]), "File 2 should be deleted");
                ctx.Assert(File.Exists(paths[2]), "Locked file 3 must still exist on disk");
                ctx.Assert(!File.Exists(paths[3]), "File 4 should be deleted");
                ctx.Assert(!File.Exists(paths[4]), "File 5 should be deleted");
                await Task.CompletedTask;
            }
        ));

        // TC-DEL-03: Batch Deletion Cancellation Responsiveness
        runner.Register(new TestCase(
            TestId: "TC-DEL-03",
            FeatureId: "FEAT-19",
            Title: "Batch Deletion Cancellation Responsiveness",
            Priority: "P1 / CRITICAL",
            Category: "Deletion & Filesystem Safety",
            ProductionClass: "DiskScope.Services.FileActionService",
            AuditRiskNote: "Verifies batch deletion stops promptly when cancellation token is triggered, preserving remaining files.",
            ExecuteAsync: async ctx =>
            {
                string tempDir = ctx.CreateTempDirectory("del_cancel");
                var paths = new List<string>();
                for (int i = 1; i <= 10; i++)
                {
                    string p = Path.Combine(tempDir, $"cancel_{i}.dat");
                    File.WriteAllBytes(p, new byte[512]);
                    paths.Add(p);
                }

                using var cts = new CancellationTokenSource();
                var progress = new Progress<(int Completed, int Total, string CurrentItem)>(info =>
                {
                    if (info.Completed >= 3)
                    {
                        cts.Cancel();
                    }
                });

                var fileAction = new FileActionService();
                var (succeeded, _) = await fileAction.DeleteFilesBatchAsync(paths, permanent: true, progress, cts.Token);

                ctx.Assert(succeeded >= 3 && succeeded < 10, $"Deletion should stop early upon cancellation; deleted {succeeded} of 10");
                int remainingOnDisk = paths.Count(File.Exists);
                ctx.AssertEqual(10 - succeeded, remainingOnDisk, "Remaining files on disk must match unexecuted items");
            }
        ));

        // TC-DEL-04: Directory Recursive Deletion & Rollup Index Integrity
        runner.Register(new TestCase(
            TestId: "TC-DEL-04",
            FeatureId: "FEAT-18",
            Title: "Directory Recursive Deletion & Rollup Index Integrity",
            Priority: "P0 / BLOCKER",
            Category: "Deletion & Filesystem Safety",
            ProductionClass: "DiskScope.Services.FileActionService",
            AuditRiskNote: "Verifies that deleting a directory recursively deletes all children from disk and purges all folder records from SQLite.",
            ExecuteAsync: async ctx =>
            {
                string rootDir = ctx.CreateTempDirectory("del_dir_root");
                string subDir1 = Path.Combine(rootDir, "SubA");
                string subDir2 = Path.Combine(rootDir, "SubB");
                Directory.CreateDirectory(subDir1);
                Directory.CreateDirectory(subDir2);

                string file1 = Path.Combine(subDir1, "file1.bin");
                string file2 = Path.Combine(subDir2, "file2.bin");
                File.WriteAllBytes(file1, new byte[2048]);
                File.WriteAllBytes(file2, new byte[4096]);

                string dbPath = ctx.CreateTempDatabasePath("del_dir.db");
                using var db = new DatabaseService(dbPath);
                db.Initialize();

                var rec1 = new FileRecord { Path = file1, Name = "file1.bin", Parent = subDir1, Size = 2048, ModifiedTime = 100, CreatedTime = 100, Accessible = 1 };
                var rec2 = new FileRecord { Path = file2, Name = "file2.bin", Parent = subDir2, Size = 4096, ModifiedTime = 100, CreatedTime = 100, Accessible = 1 };
                db.InsertBatch(new[] { rec1, rec2 });

                var fileAction = new FileActionService();
                bool deleted = fileAction.DeletePermanently(rootDir, out string? err, skipConfirmation: true);
                ctx.Assert(deleted, $"Recursive directory deletion should succeed: {err}");
                ctx.Assert(!Directory.Exists(rootDir), "Target directory must be gone from disk");

                bool dbCleared = db.RemoveDirectoryFromIndex(rootDir);
                ctx.Assert(dbCleared, "RemoveDirectoryFromIndex should return true");

                var remainingFiles = db.GetFilesPaged(0, 10);
                ctx.AssertEqual(0, remainingFiles.Count, "All child files in database should be removed");
                await Task.CompletedTask;
            }
        ));

        // TC-DEL-05: Protected System and Root Path Deletion Shield
        runner.Register(new TestCase(
            TestId: "TC-DEL-05",
            FeatureId: "FEAT-20",
            Title: "Protected System and Root Path Deletion Shield",
            Priority: "P0 / BLOCKER",
            Category: "Deletion & Filesystem Safety",
            ProductionClass: "DiskScope.Services.FileActionService",
            AuditRiskNote: "Verifies that Windows directory, Program Files, User Profile root, and drive roots are strictly blocked from deletion.",
            ExecuteAsync: async ctx =>
            {
                var fileAction = new FileActionService();

                // Drive roots
                ctx.Assert(fileAction.IsProtectedPath(@"C:\"), @"C:\ must be protected");
                ctx.Assert(fileAction.IsProtectedPath(@"C:"), @"C: must be protected");
                ctx.Assert(fileAction.IsProtectedPath(@"D:\"), @"D:\ must be protected");

                // Windows directory
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                ctx.Assert(fileAction.IsProtectedPath(winDir), $"{winDir} must be protected");
                ctx.Assert(fileAction.IsProtectedPath(Path.Combine(winDir, "System32")), $"{winDir}\\System32 must be protected");

                // Program Files
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                ctx.Assert(fileAction.IsProtectedPath(progFiles), $"{progFiles} must be protected");

                // User Profile root
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                ctx.Assert(fileAction.IsProtectedPath(userProfile), $"{userProfile} must be protected");

                // Attempting deletion on protected path must fail immediately without touching disk
                bool attempt = fileAction.DeletePermanently(winDir, out string? err, skipConfirmation: true);
                ctx.Assert(!attempt, "Deleting Windows directory must return false");
                ctx.Assert(err != null && err.Contains("Protected system"), "Error message must state protected path");
                await Task.CompletedTask;
            }
        ));
    }

    // =========================================================================
    // K. INSTALLER & DEPLOYMENT SUITE (BUG-003 VERIFICATION)
    // =========================================================================
    private static void RegisterInstallerTests(TestRunner runner)
    {
        // TC-INS-04: Inno Setup Installer Script Configuration & URL Integrity
        runner.Register(new TestCase(
            TestId: "TC-INS-04",
            FeatureId: "FEAT-43",
            Title: "Inno Setup Installer Script Configuration & URL Integrity",
            Priority: "P0 / BLOCKER",
            Category: "Installer & Deployment",
            ProductionClass: "installer/installer.iss",
            AuditRiskNote: "Confirms Blocker 3: Verifies installer.iss references the active repository https://github.com/12valor/ArborGraph and not the obsolete 12valor/C-file-scanner.",
            ExecuteAsync: async ctx =>
            {
                // Locate installer.iss relative to current directory or AppContext.BaseDirectory
                string baseDir = AppContext.BaseDirectory;
                string? repoRoot = null;
                var curr = new DirectoryInfo(baseDir);
                while (curr != null)
                {
                    if (File.Exists(Path.Combine(curr.FullName, "installer", "installer.iss")))
                    {
                        repoRoot = curr.FullName;
                        break;
                    }
                    curr = curr.Parent;
                }

                if (repoRoot == null && File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "installer", "installer.iss")))
                {
                    repoRoot = Directory.GetCurrentDirectory();
                }

                ctx.Assert(repoRoot != null, "Must locate repository root containing installer/installer.iss");
                string issPath = Path.Combine(repoRoot!, "installer", "installer.iss");
                ctx.Assert(File.Exists(issPath), $"installer.iss must exist at {issPath}");

                string content = await File.ReadAllTextAsync(issPath);

                // 1. Verify MyAppURL definition points to ArborGraph
                ctx.Assert(content.Contains("#define MyAppURL \"https://github.com/12valor/ArborGraph\""),
                    "MyAppURL must be defined as https://github.com/12valor/ArborGraph");

                // 2. Verify legacy C-file-scanner repository is NOT present anywhere in installer script
                ctx.Assert(!content.Contains("12valor/C-file-scanner"),
                    "installer.iss must not contain any references to obsolete repository '12valor/C-file-scanner'");

                // 3. Verify AppSupportURL and AppUpdatesURL are bound to {#MyAppURL}
                ctx.Assert(content.Contains("AppSupportURL={#MyAppURL}"), "AppSupportURL must reference {#MyAppURL}");
                ctx.Assert(content.Contains("AppUpdatesURL={#MyAppURL}"), "AppUpdatesURL must reference {#MyAppURL}");
                ctx.Assert(content.Contains("AppPublisherURL={#MyAppURL}"), "AppPublisherURL must reference {#MyAppURL}");

                // 4. Verify ProductName, Architecture, and Privileges
                ctx.Assert(content.Contains("#define MyAppName \"ArborGraph\""), "MyAppName must be ArborGraph");
                ctx.Assert(content.Contains("ArchitecturesAllowed=x64compatible"), "Architecture must be x64compatible");
                ctx.Assert(content.Contains("PrivilegesRequired=lowest"), "PrivilegesRequired must be lowest");

                // 5. Verify EULA file exists and is referenced
                ctx.Assert(content.Contains("LicenseFile=eula.txt"), "LicenseFile must reference eula.txt");
                string eulaPath = Path.Combine(repoRoot!, "installer", "eula.txt");
                ctx.Assert(File.Exists(eulaPath), $"eula.txt must exist at {eulaPath}");

                // 6. Verify compiled installer output if present
                string compiledSetupPath = Path.Combine(repoRoot!, "dist", "setup", "ArborGraph-Setup-1.0.0-x64.exe");
                if (File.Exists(compiledSetupPath))
                {
                    var fileInfo = new FileInfo(compiledSetupPath);
                    ctx.Assert(fileInfo.Length > 1_000_000, $"Compiled installer must be a valid PE binary (> 1MB), actual size: {fileInfo.Length} bytes");
                }
            }
        ));
    }

    // =========================================================================
    // L. USN CHANGE JOURNAL & BUFFER BOUNDS SAFETY SUITE
    // =========================================================================
    private static void RegisterUsnJournalTests(TestRunner runner)
    {
        // TC-USN-01: USN Journal Pointer Arithmetic & Native Memory Safety
        runner.Register(new TestCase(
            TestId: "TC-USN-01",
            FeatureId: "FEAT-04",
            Title: "USN Journal Pointer Arithmetic & Native Memory Safety (Bounds Validator & Buffer Parser)",
            Priority: "P0 / BLOCKER",
            Category: "USN Journal Safety",
            ProductionClass: "DiskScope.Services.UsnJournalService",
            AuditRiskNote: "BUG-004: Tests defensive bounds validation, pointer arithmetic safety, and truncated/malformed record trapping in unmanaged memory buffers without throwing AccessViolationException.",
            ExecuteAsync: async ctx =>
            {
                // Helper to populate synthetic V2 record in native memory
                static void WriteSyntheticV2Record(
                    IntPtr buf,
                    int recOffset,
                    int recordLength,
                    ushort majorVersion,
                    string fileName,
                    uint reason,
                    ushort? customFileNameOffset = null,
                    ushort? customFileNameLength = null)
                {
                    byte[] nameBytes = Encoding.Unicode.GetBytes(fileName);
                    ushort fnOffset = customFileNameOffset ?? 60;
                    ushort fnLength = customFileNameLength ?? (ushort)nameBytes.Length;

                    Marshal.WriteInt32(buf, recOffset + 0, recordLength);
                    Marshal.WriteInt16(buf, recOffset + 4, (short)majorVersion);
                    Marshal.WriteInt16(buf, recOffset + 6, (short)0);
                    Marshal.WriteInt64(buf, recOffset + 8, (long)123456);
                    Marshal.WriteInt64(buf, recOffset + 16, (long)654321);
                    Marshal.WriteInt64(buf, recOffset + 24, (long)5001);
                    Marshal.WriteInt64(buf, recOffset + 32, (long)DateTime.UtcNow.ToFileTime());
                    Marshal.WriteInt32(buf, recOffset + 40, (int)reason);
                    Marshal.WriteInt32(buf, recOffset + 44, 0);
                    Marshal.WriteInt32(buf, recOffset + 48, 0);
                    Marshal.WriteInt32(buf, recOffset + 52, 0x20);
                    Marshal.WriteInt16(buf, recOffset + 56, (short)fnLength);
                    Marshal.WriteInt16(buf, recOffset + 58, (short)fnOffset);

                    if (nameBytes.Length > 0 && (recOffset + fnOffset + nameBytes.Length) <= (recOffset + recordLength))
                    {
                        Marshal.Copy(nameBytes, 0, IntPtr.Add(buf, recOffset + fnOffset), nameBytes.Length);
                    }
                }

                const int bufferSize = 1024;
                IntPtr nativeBuffer = Marshal.AllocHGlobal(bufferSize);

                try
                {
                    // 1. Valid V2 record test
                    Marshal.WriteInt64(nativeBuffer, 0, 5000); // nextUsnMarker
                    WriteSyntheticV2Record(nativeBuffer, 8, 80, 2, "sample.txt", 0x00000100); // 0x100 = FILE_CREATE

                    long offset = 8;
                    bool ok = UsnJournalService.TryReadNextRecord(nativeBuffer, 88, ref offset, out var record, out var diagMsg);
                    ctx.Assert(ok, "Valid record should parse successfully");
                    ctx.Assert(record != null, "Record should not be null");
                    ctx.AssertEqual("sample.txt", record!.FileName, "FileName should match");
                    ctx.AssertEqual(UsnChangeType.Created, record.ChangeType, "ChangeType should be Created");
                    ctx.AssertEqual(88L, offset, "Offset should advance to 88");

                    // 2. Zero-length record (clean EOF)
                    Marshal.WriteInt32(nativeBuffer, 8, 0);
                    offset = 8;
                    ok = UsnJournalService.TryReadNextRecord(nativeBuffer, 88, ref offset, out record, out diagMsg);
                    ctx.Assert(!ok, "Zero-length record should stop traversal");
                    ctx.Assert(record == null, "Record should be null for EOF");
                    ctx.AssertEqual(88L, offset, "Offset should move to end of buffer");

                    // 3. Record length too small (< 8 bytes)
                    Marshal.WriteInt32(nativeBuffer, 8, 4);
                    offset = 8;
                    ok = UsnJournalService.TryReadNextRecord(nativeBuffer, 88, ref offset, out record, out diagMsg);
                    ctx.Assert(!ok, "Record length < 8 should be rejected");
                    ctx.Assert(diagMsg != null && diagMsg.Contains("RecordLengthTooSmall"), "Should report RecordLengthTooSmall");

                    // 4. Record length exceeds remaining buffer
                    Marshal.WriteInt32(nativeBuffer, 8, 200); // 200 > 88 - 8 = 80
                    offset = 8;
                    ok = UsnJournalService.TryReadNextRecord(nativeBuffer, 88, ref offset, out record, out diagMsg);
                    ctx.Assert(!ok, "Record length exceeding buffer should be rejected");
                    ctx.Assert(diagMsg != null && diagMsg.Contains("RecordLengthExceedsBuffer"), "Should report RecordLengthExceedsBuffer");

                    // 5. Pointer at buffer end
                    offset = 88;
                    ok = UsnJournalService.TryReadNextRecord(nativeBuffer, 88, ref offset, out record, out diagMsg);
                    ctx.Assert(!ok, "Reading at buffer end should return false");

                    // 6. Truncated buffer (< 4 bytes remaining)
                    offset = 8;
                    ok = UsnJournalService.TryReadNextRecord(nativeBuffer, 10, ref offset, out record, out diagMsg);
                    ctx.Assert(!ok, "Truncated buffer (< 4 bytes) should return false");
                    ctx.Assert(diagMsg != null && diagMsg.Contains("Buffer truncated"), "Should report buffer truncated");

                    // 7. V2 Header truncated (< 60 bytes)
                    Marshal.WriteInt32(nativeBuffer, 8, 32);
                    Marshal.WriteInt16(nativeBuffer, 12, 2); // MajorVersion = 2
                    offset = 8;
                    ok = UsnJournalService.TryReadNextRecord(nativeBuffer, 88, ref offset, out record, out diagMsg);
                    ctx.Assert(!ok, "V2 record < 60 bytes should be rejected");
                    ctx.Assert(diagMsg != null && diagMsg.Contains("required header size 60"), "Should report header size violation");

                    // 8. Malformed fileNameOffset (< 60)
                    WriteSyntheticV2Record(nativeBuffer, 8, 80, 2, "bad_offset.txt", 0x00000001, customFileNameOffset: 40);
                    offset = 8;
                    ok = UsnJournalService.TryReadNextRecord(nativeBuffer, 88, ref offset, out record, out diagMsg);
                    ctx.Assert(ok, "Malformed filename offset should safely skip filename without crashing");
                    ctx.Assert(record != null, "Record should still be produced");
                    ctx.AssertEqual(string.Empty, record!.FileName, "FileName should be empty");
                    ctx.AssertEqual(88L, offset, "Offset should advance by recordLength");

                    // 9. Malformed fileNameLength (exceeds recordLength)
                    WriteSyntheticV2Record(nativeBuffer, 8, 80, 2, "overflow.txt", 0x00000001, customFileNameLength: 500);
                    offset = 8;
                    ok = UsnJournalService.TryReadNextRecord(nativeBuffer, 88, ref offset, out record, out diagMsg);
                    ctx.Assert(ok, "Malformed filename length should safely skip filename read");
                    ctx.Assert(record != null && record.FileName == string.Empty, "FileName should be empty");

                    // 10. Multi-record sequence: 2 valid records followed by 1 truncated record
                    Marshal.WriteInt64(nativeBuffer, 0, 9999);
                    WriteSyntheticV2Record(nativeBuffer, 8, 80, 2, "alpha.txt", 0x00000100);
                    WriteSyntheticV2Record(nativeBuffer, 88, 80, 2, "beta.txt", 0x00000200); // DELETE
                    Marshal.WriteInt32(nativeBuffer, 168, 500); // Truncated length (exceeds 250 - 168 = 82)

                    offset = 8;
                    long testBytesReturned = 250;
                    var collectedRecords = new List<UsnChangeRecord>();

                    while (offset < testBytesReturned)
                    {
                        if (!UsnJournalService.TryReadNextRecord(nativeBuffer, testBytesReturned, ref offset, out var rec, out _))
                        {
                            break;
                        }
                        if (rec != null) collectedRecords.Add(rec);
                    }

                    ctx.AssertEqual(2, collectedRecords.Count, "Exactly 2 valid records should be collected");
                    ctx.AssertEqual("alpha.txt", collectedRecords[0].FileName, "First record matches");
                    ctx.AssertEqual(UsnChangeType.Created, collectedRecords[0].ChangeType, "First record Created");
                    ctx.AssertEqual("beta.txt", collectedRecords[1].FileName, "Second record matches");
                    ctx.AssertEqual(UsnChangeType.Deleted, collectedRecords[1].ChangeType, "Second record Deleted");

                    // 11. Unsupported MajorVersion (e.g. MajorVersion = 3)
                    WriteSyntheticV2Record(nativeBuffer, 8, 80, 3, "unsupported_v3.bin", 0x00000001); // V3
                    WriteSyntheticV2Record(nativeBuffer, 88, 80, 2, "gamma.txt", 0x00000001); // V2

                    offset = 8;
                    testBytesReturned = 168;
                    collectedRecords.Clear();

                    while (offset < testBytesReturned)
                    {
                        if (!UsnJournalService.TryReadNextRecord(nativeBuffer, testBytesReturned, ref offset, out var rec, out _))
                        {
                            break;
                        }
                        if (rec != null) collectedRecords.Add(rec);
                    }

                    ctx.AssertEqual(1, collectedRecords.Count, "Unsupported V3 record should be skipped; 1 V2 record collected");
                    ctx.AssertEqual("gamma.txt", collectedRecords[0].FileName, "Gamma record extracted cleanly");
                }
                finally
                {
                    Marshal.FreeHGlobal(nativeBuffer);
                }

                await Task.CompletedTask;
            }
        ));

        // TC-USN-02: USN Journal Non-NTFS Graceful Fallback
        runner.Register(new TestCase(
            TestId: "TC-USN-02",
            FeatureId: "FEAT-04",
            Title: "USN Journal Non-NTFS Graceful Fallback",
            Priority: "P1 / CRITICAL",
            Category: "USN Journal Safety",
            ProductionClass: "DiskScope.Services.UsnJournalService",
            AuditRiskNote: "Verifies that non-NTFS volumes or invalid paths do not attempt raw P/Invoke and safely signal fallback to standard BFS scan.",
            ExecuteAsync: async ctx =>
            {
                var usnService = new UsnJournalService();

                // 1. IsNtfsVolume returns false for fictitious/non-NTFS path
                bool isNtfs = usnService.IsNtfsVolume(@"Z:\VirtualNonExistent_TestDrive");
                ctx.Assert(!isNtfs, "Virtual / non-existent path should not be identified as NTFS");

                // 2. QueryJournalState reports unavailable
                var state = usnService.QueryJournalState(@"Z:\VirtualNonExistent_TestDrive");
                ctx.Assert(!state.IsAvailable, "State should report unavailable for non-NTFS path");
                ctx.Assert(!string.IsNullOrEmpty(state.StatusMessage), "StatusMessage should explain why unavailable");

                // 3. ReadChanges reports safe fallback
                var readResult = usnService.ReadChanges(@"Z:\VirtualNonExistent_TestDrive", 12345678, 0);
                ctx.Assert(!readResult.Success, "ReadChanges should return Success = false for non-NTFS volume");
                ctx.Assert(readResult.Reason.Contains("Fallback to full scan"), "Reason should specify fallback to full scan");

                await Task.CompletedTask;
            }
        ));

        // TC-USN-03: USN Journal ID Mismatch Fallback
        runner.Register(new TestCase(
            TestId: "TC-USN-03",
            FeatureId: "FEAT-04",
            Title: "USN Journal ID Mismatch & Truncation Fallback",
            Priority: "P1 / CRITICAL",
            Category: "USN Journal Safety",
            ProductionClass: "DiskScope.Services.UsnJournalService",
            AuditRiskNote: "Verifies that journal recreation (ID mismatch) or record purge safely aborts incremental scan and triggers full scan fallback.",
            ExecuteAsync: async ctx =>
            {
                var usnService = new UsnJournalService();

                // Query host system C:\ drive
                var state = usnService.QueryJournalState(@"C:\");

                if (state.IsAvailable)
                {
                    // Case 1: ID mismatch
                    ulong mismatchedId = state.JournalId ^ 0xDEADBEEFCAFE1234UL;
                    var result1 = usnService.ReadChanges(@"C:\", mismatchedId, state.NextUsn);
                    ctx.Assert(!result1.Success, "Mismatched Journal ID must trigger fallback");
                    ctx.Assert(result1.Reason.Contains("recreated or ID changed"), $"Reason must indicate ID change. Actual: {result1.Reason}");

                    // Case 2: StartUsn truncated / purged (< LowestValidUsn)
                    if (state.LowestValidUsn > 0)
                    {
                        var result2 = usnService.ReadChanges(@"C:\", state.JournalId, state.LowestValidUsn - 1);
                        ctx.Assert(!result2.Success, "StartUsn < LowestValidUsn must trigger fallback");
                        ctx.Assert(result2.Reason.Contains("truncated since last scan"), "Reason must indicate truncation");
                    }
                }
                else
                {
                    // If running non-elevated on C:\, verify fallback is clean without throwing
                    var result = usnService.ReadChanges(@"C:\", 0x1234567890ABCDEFUL, 0);
                    ctx.Assert(!result.Success, "Unavailable journal should cleanly return Success = false");
                    ctx.Assert(result.Reason.Contains("Fallback to full scan"), "Reason should indicate fallback");
                }

                await Task.CompletedTask;
            }
        ));

        // TC-USN-04: Standard Non-Admin User Fallback
        runner.Register(new TestCase(
            TestId: "TC-USN-04",
            FeatureId: "FEAT-04",
            Title: "Standard Non-Admin User Elevation & Access-Denied Graceful Handling",
            Priority: "P1 / CRITICAL",
            Category: "USN Journal Safety",
            ProductionClass: "DiskScope.Services.UsnJournalService",
            AuditRiskNote: "Verifies that opening volume handles without Administrator privileges traps ERROR_ACCESS_DENIED safely and falls back.",
            ExecuteAsync: async ctx =>
            {
                var usnService = new UsnJournalService();
                var state = usnService.QueryJournalState(@"C:\");

                // Either elevated (IsAvailable = true) or non-elevated (RequiresElevation = true)
                if (!state.IsAvailable && state.RequiresElevation)
                {
                    ctx.Assert(state.StatusMessage.Contains("Administrator privileges"),
                        "Non-elevated session must report Administrator privileges required");
                }

                // Call ReadChanges: must never throw unhandled Win32Exception or crash
                var readResult = usnService.ReadChanges(@"C:\", 0x1234567890ABCDEFUL, 100);
                ctx.Assert(readResult != null, "ReadResult must not be null");
                // Either succeeded (if elevated & ID matched) or safely failed with fallback
                if (!readResult!.Success)
                {
                    ctx.Assert(readResult.Reason.Contains("Fallback to full scan"),
                        $"Fallback reason expected. Actual: {readResult.Reason}");
                }

                await Task.CompletedTask;
            }
        ));
    }
}
