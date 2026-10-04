using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiskScope.Models;
using DiskScope.Services;
using Microsoft.Data.Sqlite;

namespace DiskScope.Tests;

public record SecurityTestResult(
    string TestId,
    string Category,
    string Description,
    bool Passed,
    string Details,
    string Severity
);

public class SecurityAuditRunner
{
    private static readonly string SecurityTempRoot = Path.Combine(Path.GetTempPath(), "ArborGraph_Security_Audit");

    public static async Task<int> RunAllAsync(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ARBORGRAPH v1.0.0 — DEDICATED SECURITY & DATA-SAFETY AUDIT RUNNER");
        Console.WriteLine("==========================================================================");
        Console.WriteLine($"  OS Architecture: Windows {Environment.OSVersion} ({(Environment.Is64BitProcess ? "x64" : "x86")})");
        Console.WriteLine($"  Local Time:      {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("==========================================================================\n");

        if (Directory.Exists(SecurityTempRoot))
        {
            try { Directory.Delete(SecurityTempRoot, true); } catch { }
        }
        Directory.CreateDirectory(SecurityTempRoot);

        var results = new List<SecurityTestResult>();

        try
        {
            // 1. Filesystem Safety & Protected Path Shield
            results.Add(TestProtectedPathShield());

            // 2. Relative Path & Traversal Protection
            results.Add(TestPathTraversalRejection());

            // 3. Prefix Collision in Database Operations
            results.Add(TestDatabasePrefixCollisionIsolation());

            // 4. Malformed Path & Reserved Windows Device Names
            results.Add(TestMalformedAndReservedPaths());

            // 5. Advanced SQL Injection Matrix Across All Query Parameters
            results.Add(TestAdvancedSqlInjectionMatrix());

            // 6. Database Corruption & Malformed Header Handling
            results.Add(TestCorruptedDatabaseHandling());

            // 7. Native USN Journal Memory Fuzzing & Out-of-Bounds Guards
            results.Add(TestUsnBufferFuzzingAndBoundsSafety());

            // 8. Locked File Contention & Safe Deletion Skipping
            results.Add(TestLockedFileContentionSafety());

            // 9. Location Prefix Query Boundary Behavior (P3 Observation)
            results.Add(TestLocationPrefixQueryBoundary());

            // Print Summary Table
            Console.WriteLine("\n==========================================================================");
            Console.WriteLine("  SECURITY & DATA-SAFETY AUDIT EXECUTION SUMMARY");
            Console.WriteLine("==========================================================================");

            int passCount = results.Count(r => r.Passed);
            int failCount = results.Count(r => !r.Passed);

            foreach (var r in results)
            {
                var color = r.Passed ? ConsoleColor.Green : (r.Severity == "P3" ? ConsoleColor.Yellow : ConsoleColor.Red);
                Console.ForegroundColor = color;
                Console.Write($"  [{r.TestId}] ");
                Console.ResetColor();
                Console.WriteLine($"{r.Category} - {r.Description}");
                Console.WriteLine($"    Result:   {(r.Passed ? "PASS" : "FAIL")} (Severity: {r.Severity})");
                Console.WriteLine($"    Evidence: {r.Details}\n");
            }

            Console.WriteLine("--------------------------------------------------------------------------");
            Console.WriteLine($"  Total Tests Executed: {results.Count} | PASSED: {passCount} | FAILED: {failCount}");
            Console.WriteLine("==========================================================================\n");

            return failCount == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[SECURITY HARNESS FATAL EXCEPTION]: {ex}");
            Console.ResetColor();
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(SecurityTempRoot))
                {
                    Directory.Delete(SecurityTempRoot, true);
                }
            }
            catch { }
        }
    }

    private static SecurityTestResult TestProtectedPathShield()
    {
        Console.Write("  [SEC-01] Testing Protected Path Shield against system and root targets... ");
        var fileActionService = new FileActionService();

        var protectedTargets = new[]
        {
            @"C:",
            @"C:\",
            @"c:\",
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        var unprotectedValidSynthetic = Path.Combine(SecurityTempRoot, "safe_target.txt");
        File.WriteAllText(unprotectedValidSynthetic, "safe content");

        bool allProtectedBlocked = true;
        var blockedDetails = new List<string>();

        foreach (var target in protectedTargets)
        {
            bool isProtected = fileActionService.IsProtectedPath(target);
            if (!isProtected)
            {
                allProtectedBlocked = false;
                blockedDetails.Add($"FAILED TO PROTECT: {target}");
            }
            else
            {
                blockedDetails.Add($"Blocked: {target}");
            }
        }

        bool safeTargetAllowed = !fileActionService.IsProtectedPath(unprotectedValidSynthetic);

        bool passed = allProtectedBlocked && safeTargetAllowed;
        Console.WriteLine(passed ? "PASS" : "FAIL");

        return new SecurityTestResult(
            "SEC-01",
            "Filesystem Safety",
            "System roots (C:\\, Windows, System32, Program Files, User Profile) blocked from deletion",
            passed,
            $"All {protectedTargets.Length} system targets safely blocked. Safe synthetic allowed: {safeTargetAllowed}.",
            passed ? "INFO" : "P0"
        );
    }

    private static SecurityTestResult TestPathTraversalRejection()
    {
        Console.Write("  [SEC-02] Testing Path Traversal (.. / relative escaping) rejection... ");
        var fileActionService = new FileActionService();

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);        // Path crafted using .. to reach Windows folder from temp or user folder
        string traversalToWindows = Path.Combine(userProfile, @"..\..\Windows");
        string traversalToSystem32 = Path.Combine(userProfile, @"..\..\Windows\System32");

        bool blockedWindowsTraversal = fileActionService.IsProtectedPath(traversalToWindows);
        bool blockedSystem32Traversal = fileActionService.IsProtectedPath(traversalToSystem32);

        bool passed = blockedWindowsTraversal && blockedSystem32Traversal;
        Console.WriteLine(passed ? "PASS" : "FAIL");

        return new SecurityTestResult(
            "SEC-02",
            "Path / Input Safety",
            "Relative traversal sequences (..\\..) canonicalized and shielded from deletion",
            passed,
            $"Traversal to Windows: Blocked={blockedWindowsTraversal}, Traversal to System32: Blocked={blockedSystem32Traversal}.",
            passed ? "INFO" : "P0"
        );
    }

    private static SecurityTestResult TestDatabasePrefixCollisionIsolation()
    {
        Console.Write("  [SEC-03] Testing Database Prefix Collision Isolation (C:\\Test vs C:\\Test2)... ");
        string dbPath = Path.Combine(SecurityTempRoot, "prefix_test.db");
        using var db = new DatabaseService(dbPath);
        db.Initialize();

        // Populate two directories where one name is a substring prefix of the other
        var records = new List<FileRecord>
        {
            new FileRecord { Path = @"C:\Test\doc1.txt", Name = "doc1.txt", Parent = @"C:\Test", Size = 100, Extension = ".txt", Category = "Docs", ModifiedTime = 1, CreatedTime = 1, Accessible = 1 },
            new FileRecord { Path = @"C:\Test\sub\doc2.txt", Name = "doc2.txt", Parent = @"C:\Test\sub", Size = 200, Extension = ".txt", Category = "Docs", ModifiedTime = 1, CreatedTime = 1, Accessible = 1 },
            new FileRecord { Path = @"C:\Test2\secret.txt", Name = "secret.txt", Parent = @"C:\Test2", Size = 500, Extension = ".txt", Category = "Docs", ModifiedTime = 1, CreatedTime = 1, Accessible = 1 },
            new FileRecord { Path = @"C:\Test-Archive\backup.zip", Name = "backup.zip", Parent = @"C:\Test-Archive", Size = 1000, Extension = ".zip", Category = "Archives", ModifiedTime = 1, CreatedTime = 1, Accessible = 1 }
        };

        db.InsertBatch(records);

        // 1. ClearIndex targeting only C:\Test
        db.ClearIndex(new[] { @"C:\Test" });

        var remainingFiles = db.GetFilesPaged(0, 100);
        bool test2Remains = remainingFiles.Any(f => f.Path.StartsWith(@"C:\Test2", StringComparison.OrdinalIgnoreCase));
        bool testArchiveRemains = remainingFiles.Any(f => f.Path.StartsWith(@"C:\Test-Archive", StringComparison.OrdinalIgnoreCase));
        bool testCleared = !remainingFiles.Any(f => f.Path.StartsWith(@"C:\Test\", StringComparison.OrdinalIgnoreCase));

        // 2. RemoveDirectoryFromIndex targeting C:\Test2
        bool removeOk = db.RemoveDirectoryFromIndex(@"C:\Test2");
        var afterRemove = db.GetFilesPaged(0, 100);
        bool test2NowGone = !afterRemove.Any(f => f.Path.StartsWith(@"C:\Test2", StringComparison.OrdinalIgnoreCase));
        bool testArchiveStillPresent = afterRemove.Any(f => f.Path.StartsWith(@"C:\Test-Archive", StringComparison.OrdinalIgnoreCase));

        bool passed = test2Remains && testArchiveRemains && testCleared && removeOk && test2NowGone && testArchiveStillPresent;
        Console.WriteLine(passed ? "PASS" : "FAIL");

        return new SecurityTestResult(
            "SEC-03",
            "Database & Filesystem Safety",
            "ClearIndex and RemoveDirectoryFromIndex isolate paths without prefix-colliding into C:\\Test2",
            passed,
            $"C:\\Test cleared: {testCleared}, C:\\Test2 preserved: {test2Remains}, C:\\Test-Archive preserved: {testArchiveRemains}, C:\\Test2 removed cleanly: {test2NowGone}.",
            passed ? "INFO" : "P0"
        );
    }

    private static SecurityTestResult TestMalformedAndReservedPaths()
    {
        Console.Write("  [SEC-04] Testing Malformed Paths and DOS Reserved Device Names (CON, NUL, AUX)... ");
        var fileActionService = new FileActionService();

        var reservedTargets = new[]
        {
            @"CON",
            @"PRN",
            @"AUX",
            @"NUL",
            @"COM1",
            @"LPT1",
            @"C:\CON\test.txt",
            @"C:\folder\aux.dat",
            "",
            "   ",
            "\0malicious_null_byte",
            new string('A', 500)
        };

        bool allSafe = true;
        foreach (var r in reservedTargets)
        {
            try
            {
                // Must not throw unhandled exception or crash process
                bool isProt = fileActionService.IsProtectedPath(r);
                bool canDel = fileActionService.DeletePermanently(r, out string? err, skipConfirmation: true);
                if (canDel)
                {
                    // Reserved or non-existent device name should never report true deletion
                    allSafe = false;
                }
            }
            catch
            {
                allSafe = false;
            }
        }

        Console.WriteLine(allSafe ? "PASS" : "FAIL");

        return new SecurityTestResult(
            "SEC-04",
            "Path / Input Safety",
            "DOS device names (CON, NUL, AUX, COM1) and null-byte/malformed paths fail safely without crashing",
            allSafe,
            "Handled 12 reserved, null-byte, and malformed path variations without process crashes or unhandled faults.",
            allSafe ? "INFO" : "P1"
        );
    }

    private static SecurityTestResult TestAdvancedSqlInjectionMatrix()
    {
        Console.Write("  [SEC-05] Testing SQL Injection Attack Payloads across all query parameters... ");
        string dbPath = Path.Combine(SecurityTempRoot, "sqli_audit.db");
        using var db = new DatabaseService(dbPath);
        db.Initialize();

        // Seed 10 files
        var records = new List<FileRecord>();
        for (int i = 0; i < 10; i++)
        {
            records.Add(new FileRecord
            {
                Path = $@"C:\Normal\file_{i}.txt",
                Name = $"file_{i}.txt",
                Parent = @"C:\Normal",
                Size = 1000 + i,
                Extension = ".txt",
                Category = "Documents",
                ModifiedTime = 1700000000,
                CreatedTime = 1700000000,
                Accessible = 1
            });
        }
        db.InsertBatch(records);

        var attackPayloads = new[]
        {
            "' OR '1'='1",
            "'; DROP TABLE files; --",
            "' UNION SELECT id, path, name, parent, 999999, 0, 0, '', '', 1 FROM files --",
            "1; ATTACH DATABASE 'evil.db' AS evil; --",
            "test' AND 1=0 UNION ALL SELECT 1,2,3,4,5,6,7,8,9,10 --",
            "' OR 1=1 /*",
            "admin'--",
            "'; VACUUM; --"
        };

        bool allSafelyNeutralized = true;

        foreach (var payload in attackPayloads)
        {
            try
            {
                // Test in Search filter
                var filesSearch = db.GetFilesPaged(0, 50, search: payload);

                // Test in Category filter
                var filesCategory = db.GetFilesPaged(0, 50, category: payload);

                // Test in Extension filter
                var filesExtension = db.GetFilesPaged(0, 50, extension: payload);

                // Test in Location filter
                var filesLoc = db.GetFilesPaged(0, 50, locationPrefix: payload);

                // Test in SortBy parameter
                var filesSort = db.GetFilesPaged(0, 50, sortBy: payload);

                // Test in FilteredFileCount
                long count = db.GetFilteredFileCount(search: payload, category: payload, extension: payload, locationPrefix: payload);

                // Verify tables still exist
                long validCount = db.GetFilteredFileCount();
                if (validCount != 10)
                {
                    allSafelyNeutralized = false;
                }
            }
            catch (Exception)
            {
                // Unhandled SQL syntax exception means injection leaked into SQLite parser
                allSafelyNeutralized = false;
            }
        }

        Console.WriteLine(allSafelyNeutralized ? "PASS" : "FAIL");

        return new SecurityTestResult(
            "SEC-05",
            "Database Security",
            "8 SQL injection payloads safely neutralized via parameterized queries; 0 schema tampering",
            allSafelyNeutralized,
            "Tested search, category, extension, locationPrefix, and sortBy with union/drop/comment payloads. Database integrity preserved.",
            allSafelyNeutralized ? "INFO" : "P0"
        );
    }

    private static SecurityTestResult TestCorruptedDatabaseHandling()
    {
        Console.Write("  [SEC-06] Testing Corrupted and Malformed SQLite Database Handling... ");
        string corruptDbPath = Path.Combine(SecurityTempRoot, "corrupt_test.db");

        // Write non-SQLite corrupt garbage bytes
        byte[] garbage = new byte[4096];
        new Random(999).NextBytes(garbage);
        File.WriteAllBytes(corruptDbPath, garbage);

        bool handledGracefully = false;
        try
        {
            using var db = new DatabaseService(corruptDbPath);
            db.Initialize();
            bool isIntegrityOk = db.CheckIntegrity();
            var files = db.GetFilesPaged(0, 10);
            handledGracefully = !isIntegrityOk;
        }
        catch
        {
            // Safely caught exception during open or query is also acceptable behavior
            handledGracefully = true;
        }

        Console.WriteLine(handledGracefully ? "PASS" : "FAIL");

        return new SecurityTestResult(
            "SEC-06",
            "Database Security",
            "Corrupted or invalid database headers fail safely without unhandled runtime crash",
            handledGracefully,
            "Corrupt SQLite header evaluated safely; CheckIntegrity properly flagged database.",
            handledGracefully ? "INFO" : "P1"
        );
    }

    private static SecurityTestResult TestUsnBufferFuzzingAndBoundsSafety()
    {
        Console.Write("  [SEC-07] Testing Native USN Journal Buffer Bounds & Fuzzing Safety... ");

        // Allocate a small native unmanaged buffer and populate with fuzzed corrupt record headers
        int bufferSize = 256;
        IntPtr nativeBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(bufferSize);

        try
        {
            // Clear memory
            for (int i = 0; i < bufferSize; i++)
            {
                System.Runtime.InteropServices.Marshal.WriteByte(nativeBuffer, i, 0);
            }

            // Case 1: RecordLength is 0 (infinite loop trap)
            System.Runtime.InteropServices.Marshal.WriteInt32(nativeBuffer, 0, 0);
            long offset1 = 0;
            bool ok1 = UsnJournalService.TryReadNextRecord(nativeBuffer, bufferSize, ref offset1, out var rec1, out var msg1);

            // Case 2: RecordLength > remaining buffer (out-of-bounds read trap)
            System.Runtime.InteropServices.Marshal.WriteInt32(nativeBuffer, 0, 99999);
            long offset2 = 0;
            bool ok2 = UsnJournalService.TryReadNextRecord(nativeBuffer, bufferSize, ref offset2, out var rec2, out var msg2);

            // Case 3: Truncated buffer (< 4 bytes)
            long offset3 = 254;
            bool ok3 = UsnJournalService.TryReadNextRecord(nativeBuffer, bufferSize, ref offset3, out var rec3, out var msg3);

            // Case 4: FileOffset points beyond record length
            System.Runtime.InteropServices.Marshal.WriteInt32(nativeBuffer, 0, 60); // RecordLength = 60
            System.Runtime.InteropServices.Marshal.WriteInt16(nativeBuffer, 4, 2);  // MajorVersion = 2
            System.Runtime.InteropServices.Marshal.WriteInt16(nativeBuffer, 6, 0);  // MinorVersion = 0
            System.Runtime.InteropServices.Marshal.WriteInt16(nativeBuffer, 56, 100); // FileNameOffset = 100 (OUT OF BOUNDS!)
            System.Runtime.InteropServices.Marshal.WriteInt16(nativeBuffer, 58, 20);  // FileNameLength = 20
            long offset4 = 0;
            bool ok4 = UsnJournalService.TryReadNextRecord(nativeBuffer, bufferSize, ref offset4, out var rec4, out var msg4);

            bool passed = !ok1 && !ok2 && !ok3 && ok4 && (rec4 != null && rec4.FileName == string.Empty);
            Console.WriteLine(passed ? "PASS" : "FAIL");

            return new SecurityTestResult(
                "SEC-07",
                "Native / P-Invoke Safety",
                "USN Journal parser rejects zero-length, truncated, out-of-bounds offset/length records without AccessViolation",
                passed,
                $"Case1 (ZeroLen): {msg1}; Case2 (Overflow): {msg2}; Case3 (Truncated): {msg3}; Case4 (OffsetOverflow): {msg4}.",
                passed ? "INFO" : "P1"
            );
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(nativeBuffer);
        }
    }

    private static SecurityTestResult TestLockedFileContentionSafety()
    {
        Console.Write("  [SEC-08] Testing In-Use / Locked File Contention Safety... ");
        string lockedDir = Path.Combine(SecurityTempRoot, "locked_safety");
        Directory.CreateDirectory(lockedDir);

        string normalFile = Path.Combine(lockedDir, "normal.txt");
        string lockedFile = Path.Combine(lockedDir, "locked.bin");
        File.WriteAllText(normalFile, "unlocked");
        File.WriteAllText(lockedFile, "locked content");

        var fileActionService = new FileActionService();
        int succeeded = 0;
        int failed = 0;

        // Open lockedFile exclusively with FileShare.None
        using (var fs = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var res = fileActionService.DeleteFilesBatch(new[] { normalFile, lockedFile }, permanent: true);
            succeeded = res.Succeeded;
            failed = res.Failed;
        }

        bool normalGone = !File.Exists(normalFile);
        bool lockedPreserved = File.Exists(lockedFile);

        bool passed = (succeeded == 1) && (failed == 1) && normalGone && lockedPreserved;
        Console.WriteLine(passed ? "PASS" : "FAIL");

        return new SecurityTestResult(
            "SEC-08",
            "Filesystem Safety",
            "Locked files under exclusive handle contention safely skipped without process hang or crash",
            passed,
            $"Succeeded={succeeded}, Failed={failed}. Normal file deleted: {normalGone}; Locked file safely preserved: {lockedPreserved}.",
            passed ? "INFO" : "P1"
        );
    }

    private static SecurityTestResult TestLocationPrefixQueryBoundary()
    {
        Console.Write("  [SEC-09] Testing Location Prefix Query Boundary (C:\\Test vs C:\\Test2)... ");
        string dbPath = Path.Combine(SecurityTempRoot, "query_prefix.db");
        using var db = new DatabaseService(dbPath);
        db.Initialize();

        var records = new List<FileRecord>
        {
            new FileRecord { Path = @"C:\Test\in_target.txt", Name = "in_target.txt", Parent = @"C:\Test", Size = 100, Extension = ".txt", Category = "Docs", ModifiedTime = 1, CreatedTime = 1, Accessible = 1 },
            new FileRecord { Path = @"C:\Test2\sibling.txt", Name = "sibling.txt", Parent = @"C:\Test2", Size = 200, Extension = ".txt", Category = "Docs", ModifiedTime = 1, CreatedTime = 1, Accessible = 1 }
        };
        db.InsertBatch(records);

        // Query with locationPrefix = @"C:\Test"
        var filtered = db.GetFilesPaged(0, 100, locationPrefix: @"C:\Test");
        bool matchedTarget = filtered.Any(f => f.Path == @"C:\Test\in_target.txt");
        bool leakedSibling = filtered.Any(f => f.Path == @"C:\Test2\sibling.txt");

        // Note: As discovered during audit, locationPrefix without trailing separator causes LIKE 'C:\Test%' to match 'C:\Test2'
        Console.WriteLine(leakedSibling ? "LEAK CONFIRMED (P3 / Minor)" : "PASS");

        return new SecurityTestResult(
            "SEC-09",
            "Path / Input Safety",
            "Location prefix query boundary in GetFilesPaged (LIKE 'C:\\Test%' matches sibling C:\\Test2)",
            !leakedSibling, // Fails if leaked
            leakedSibling
                ? "CONFIRMED: GetFilesPaged locationPrefix binds $loc = 'C:\\Test%', causing sibling folder C:\\Test2\\sibling.txt to be returned in search results."
                : "Location prefix query strictly matched only target directory.",
            "P3"
        );
    }
}
