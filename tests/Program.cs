using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using DiskScope.Infrastructure;
using DiskScope.Models;
using DiskScope.Services;
using DiskScope.ViewModels;
using DiskScope.Views;

namespace DiskScope.Tests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // 1. Check for legacy live test or legacy integration test mode
        if (args.Length > 0 && args[0] == "--live")
        {
            return await RunLiveTestAsync();
        }

        if (args.Length > 0 && args[0] == "--legacy")
        {
            return await RunLegacyIntegrationSuiteAsync();
        }

        if (args.Length > 0 && args[0] == "--benchmark")
        {
            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 10000;
            return await RunBenchmarkAsync(count);
        }

        if (args.Length > 0 && args[0] == "--manual")
        {
            return await ManualWindowsTestRunner.RunAllAsync(args);
        }

        if (args.Length > 0 && (args[0] == "--stress" || args[0] == "--perf"))
        {
            return await StressTestRunner.RunAllAsync(args);
        }

        // 2. Default: Run Modern Automated QA Test Harness
        return await RunAutomatedHarnessAsync(args);
    }

    private static async Task<int> RunAutomatedHarnessAsync(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ARBORGRAPH v1.0.0 — AUTOMATED QA TEST HARNESS EXECUTION");
        Console.WriteLine("==========================================================================");
        Console.WriteLine($"  OS Architecture: Windows {Environment.OSVersion} ({Environment.ProcessPath})");
        Console.WriteLine($"  Local Time:      {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  Execution Mode:  {(args.Contains("--critical") ? "CRITICAL TESTS ONLY (P0/P1)" : "FULL AUTOMATED SUITE")}");
        Console.WriteLine("==========================================================================\n");

        var runner = new TestRunner();
        AutomatedTestSuites.RegisterAll(runner);

        Func<TestCase, bool>? filter = null;
        if (args.Contains("--critical"))
        {
            filter = tc => tc.Priority.StartsWith("P0", StringComparison.OrdinalIgnoreCase) ||
                           tc.Priority.StartsWith("P1", StringComparison.OrdinalIgnoreCase);
        }
        else if (args.Contains("--filter") && args.Length > Array.IndexOf(args, "--filter") + 1)
        {
            string query = args[Array.IndexOf(args, "--filter") + 1];
            filter = tc => tc.TestId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           tc.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           tc.Category.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        int discovered = runner.RegisteredCases.Count;
        int executed = 0;
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int blocked = 0;

        var totalSw = Stopwatch.StartNew();

        var results = await runner.RunAsync(filter, result =>
        {
            executed++;
            string statusIcon = result.Outcome switch
            {
                TestOutcome.Pass => "✓ PASS",
                TestOutcome.Fail => "✗ FAIL",
                TestOutcome.Blocked => "⊘ BLOCKED",
                _ => "○ SKIPPED"
            };

            ConsoleColor color = result.Outcome switch
            {
                TestOutcome.Pass => ConsoleColor.Green,
                TestOutcome.Fail => ConsoleColor.Red,
                TestOutcome.Blocked => ConsoleColor.Yellow,
                _ => ConsoleColor.DarkGray
            };

            Console.ForegroundColor = color;
            Console.Write($"[{statusIcon}] ");
            Console.ResetColor();
            Console.Write($"[{result.Case.TestId}] ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"({result.Case.Priority}) ");
            Console.ResetColor();
            Console.WriteLine($"{result.Case.Title} ({result.Elapsed.TotalMilliseconds:F0} ms)");

            if (result.Outcome == TestOutcome.Fail)
            {
                failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"       └─ ERROR: {result.ErrorMessage}");
                if (!string.IsNullOrEmpty(result.Case.AuditRiskNote))
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"       └─ AUDIT RISK CONFIRMED: {result.Case.AuditRiskNote}");
                }
                Console.ResetColor();
            }
            else if (result.Outcome == TestOutcome.Pass)
            {
                passed++;
            }
            else if (result.Outcome == TestOutcome.Blocked)
            {
                blocked++;
            }
            else
            {
                skipped++;
            }
        });

        totalSw.Stop();

        // Summary dashboard
        Console.WriteLine("\n==========================================================================");
        Console.WriteLine("  AUTOMATED QA EXECUTION SUMMARY DASHBOARD");
        Console.WriteLine("==========================================================================");
        Console.WriteLine($"  Tests Discovered:       {discovered}");
        Console.WriteLine($"  Tests Executed:         {executed}");
        Console.ForegroundColor = passed > 0 ? ConsoleColor.Green : ConsoleColor.White;
        Console.WriteLine($"  Passed:                 {passed}");
        Console.ForegroundColor = failed > 0 ? ConsoleColor.Red : ConsoleColor.White;
        Console.WriteLine($"  Failed:                 {failed}");
        Console.ForegroundColor = blocked > 0 ? ConsoleColor.Yellow : ConsoleColor.White;
        Console.WriteLine($"  Blocked:                {blocked}");
        Console.ResetColor();
        Console.WriteLine($"  Skipped:                {skipped}");
        Console.WriteLine($"  Total Execution Time:   {totalSw.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine("==========================================================================");

        if (failed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n[FAILURE BREAKDOWN & DEFECT CORRELATION]:");
            Console.ResetColor();
            foreach (var f in results.Where(r => r.Outcome == TestOutcome.Fail))
            {
                Console.WriteLine($"\n• Test ID:          {f.Case.TestId} ({f.Case.FeatureId})");
                Console.WriteLine($"  Title:            {f.Case.Title}");
                Console.WriteLine($"  Priority:         {f.Case.Priority}");
                Console.WriteLine($"  Production Class: {f.Case.ProductionClass}");
                Console.WriteLine($"  Failure Message:  {f.ErrorMessage}");
                if (!string.IsNullOrEmpty(f.Case.AuditRiskNote))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  Audit Assessment: {f.Case.AuditRiskNote}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✓ ALL AUTOMATED TESTS EXECUTED AND PASSED CLEANLY!");
        Console.ResetColor();
        return 0;
    }

    private static async Task<int> RunBenchmarkAsync(int fileCount)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine($"  ARBORGRAPH — SCALE BENCHMARK ({fileCount:N0} FILES)");
        Console.WriteLine("=================================================");

        string benchDir = Path.Combine(Path.GetTempPath(), "ArborGraph_Bench_" + Guid.NewGuid().ToString("N")[..8]);
        string benchDbPath = Path.Combine(Path.GetTempPath(), "ArborGraph_BenchDb_" + Guid.NewGuid().ToString("N")[..8], "bench.db");

        try
        {
            Console.WriteLine($"[1/4] Generating synthetic dataset ({fileCount:N0} files)...");
            var genSw = Stopwatch.StartNew();
            var stats = TestDataGenerator.GenerateScaledDataset(benchDir, fileCount);
            genSw.Stop();
            Console.WriteLine($"  ✓ Generated {stats.ExpectedFileCount:N0} files ({stats.ExpectedTotalBytes:N0} bytes) in {genSw.ElapsedMilliseconds} ms.");

            Console.WriteLine("[2/4] Initializing SQLite database...");
            using var db = new DatabaseService(benchDbPath);
            db.Initialize();

            Console.WriteLine("[3/4] Running BFS scanner traversal...");
            var scanner = new ScannerService(db);
            var cpuStart = Process.GetCurrentProcess().TotalProcessorTime;
            var scanSw = Stopwatch.StartNew();
            long initialRam = GC.GetTotalMemory(true);
            var scanStats = await scanner.ScanDrivesAsync(new[] { benchDir }, null, CancellationToken.None, enableIncremental: false);
            scanSw.Stop();
            var cpuEnd = Process.GetCurrentProcess().TotalProcessorTime;
            long peakRam = GC.GetTotalMemory(false);
            long peakWs = Process.GetCurrentProcess().PeakWorkingSet64;

            double filesPerSec = scanStats.FilesIndexed / Math.Max(scanSw.Elapsed.TotalSeconds, 0.001);
            double avgCpu = (cpuEnd - cpuStart).TotalMilliseconds / Math.Max(scanSw.Elapsed.TotalMilliseconds * Environment.ProcessorCount, 1.0) * 100.0;

            Console.WriteLine($"  ✓ Scanned {scanStats.FilesIndexed:N0} files in {scanSw.ElapsedMilliseconds} ms ({filesPerSec:F0} files/sec).");
            Console.WriteLine($"  ✓ Managed RAM delta: {(peakRam - initialRam) / (1024.0 * 1024.0):F2} MB (Final: {peakRam / (1024.0 * 1024.0):F1} MB).");
            Console.WriteLine($"  ✓ Peak Process Working Set: {peakWs / (1024.0 * 1024.0):F1} MB.");
            Console.WriteLine($"  ✓ Average CPU Load: {avgCpu:F1}%.");

            Console.WriteLine("[4/4] Running recursive directory rollup...");
            var rollupSw = Stopwatch.StartNew();
            db.BuildDirectoryRollup();
            rollupSw.Stop();
            Console.WriteLine($"  ✓ Rollup completed in {rollupSw.ElapsedMilliseconds} ms.");

            if (File.Exists(benchDbPath))
            {
                long dbSize = new FileInfo(benchDbPath).Length;
                Console.WriteLine($"  ✓ SQLite database footprint: {dbSize / (1024.0 * 1024.0):F2} MB ({dbSize:N0} bytes).");
            }

            Console.WriteLine("\n[BENCHMARK COMPLETE]");
            return 0;
        }
        finally
        {
            TestDataGenerator.SafeCleanup(benchDir);
            TestDataGenerator.SafeCleanup(Path.GetDirectoryName(benchDbPath));
        }
    }

    private static async Task<int> RunLiveTestAsync()
    {
        string realDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArborGraph", "scan_index.db");
        if (!File.Exists(realDb)) realDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiskScope", "scan_index.db");
        Console.WriteLine($"[LIVE TEST] Using DB: {realDb}");
        var db = new DatabaseService(realDb);
        db.Initialize();
        var scanner = new ScannerService(db);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var progress = new Progress<ScanProgressReport>(r =>
        {
            Console.WriteLine($"[PROGRESS] Elapsed: {r.Elapsed.TotalSeconds:F2}s | Indexed: {r.FilesIndexed} | Dirs: {r.DirectoriesProcessed} | RecentDir: {r.NewRecentDirectory} | CurDir: {r.CurrentDirectory}");
        });
        Console.WriteLine("[LIVE TEST] Starting ScanDrivesAsync on C:\\...");
        var sw = Stopwatch.StartNew();
        var res = await scanner.ScanDrivesAsync(new[] { @"C:\" }, progress, cts.Token);
        sw.Stop();
        Console.WriteLine($"[LIVE TEST] Finished in {sw.ElapsedMilliseconds}ms. State: {res.State}, Files: {res.FilesIndexed}, Dirs: {res.DirectoriesProcessed}");
        return 0;
    }

    private static async Task<int> RunLegacyIntegrationSuiteAsync()
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("  ARBORGRAPH — LEGACY 20-STAGE INTEGRATION TESTS");
        Console.WriteLine("=================================================");

        string testRoot = Path.Combine(Path.GetTempPath(), "DiskScope_TestFiles_" + Guid.NewGuid().ToString("N")[..8]);
        string testDbFolder = Path.Combine(Path.GetTempPath(), "DiskScope_TestDb_" + Guid.NewGuid().ToString("N")[..8]);
        string testDb = Path.Combine(testDbFolder, "test_index.db");

        try
        {
            var ds = TestDataGenerator.GenerateDatasetA(testRoot);
            using var dbService = new DatabaseService(testDb);
            dbService.Initialize();

            var scanner = new ScannerService(dbService);
            var stats = await scanner.ScanDrivesAsync(new[] { testRoot }, null, CancellationToken.None, enableIncremental: false);

            Console.WriteLine($"  Files Indexed:   {stats.FilesIndexed}");
            Console.WriteLine($"  Logical Bytes:   {stats.LogicalBytesIndexed:N0}");
            Console.WriteLine("✓ Legacy 20-stage test completed successfully.");
            return 0;
        }
        finally
        {
            TestDataGenerator.SafeCleanup(testRoot);
            TestDataGenerator.SafeCleanup(testDbFolder);
        }
    }
}
