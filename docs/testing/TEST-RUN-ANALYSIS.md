# ArborGraph v1.0.0 — Automated QA Test Run Analysis & Defect Assessment

**Document Identifier:** AG-TRA-001  
**Target Product:** ArborGraph — Filesystem Analytics & Visualization  
**Target Release:** v1.0.0 (`net8.0-windows` x64)  
**Execution Timestamp:** 2026-10-04 23:38:03  
**Execution Host:** Windows 11 x64 (.NET SDK 8.0.425)  
**Execution Engine:** `tests/DiskScope.Tests.csproj` (Direct SQLite WAL & Disk Traversal)  
**Status:** 18 PASSED, 1 FAILED, 0 BLOCKED, 0 SKIPPED (Total: 19 Tests, 6.91s)  

---

## 1. Executive Summary & Test Run Overview

The automated QA test harness was executed against the **ArborGraph v1.0.0** codebase to evaluate deterministic system behavior without manual UI intervention. The test suite operated directly against live temporary NTFS filesystems and SQLite database instances (`Microsoft.Data.Sqlite`) in WAL mode.

**No production application code was modified to force tests to pass.** The harness evaluated real production behavior and successfully reproduced and confirmed **Release Blocker 1 (`BUG-001`)** in `DatabaseService.cs`.

```text
==========================================================================
  AUTOMATED QA EXECUTION SUMMARY DASHBOARD
==========================================================================
  Tests Discovered:       19
  Tests Executed:         19
  Passed:                 18 (94.7%)
  Failed:                 1  (5.3%)
  Blocked:                0  (0.0%)
  Skipped:                0  (0.0%)
  Total Execution Time:   6.91 seconds
==========================================================================
```

---

## 2. Detailed Failure Investigation: `TC-DRV-01`

| Evaluation Field | Findings & Source Audit Correlation |
| :--- | :--- |
| **1. Test ID** | **`TC-DRV-01`** |
| **2. Feature ID** | **`FEAT-08`** (SQLite Database Storage & WAL Mode) / **`FEAT-01`** (System Drive Auto-Discovery) |
| **3. Result** | **FAILED (Confirmed Authentic Production Code Defect)** |
| **4. Exact Error** | `CRITICAL DATA SAFETY FAILURE: Scanning/clearing drive D: wiped drive C: records! Expected 2 C: files, but found 0.` |
| **5. Production Location** | [`Services/DatabaseService.cs`](file:///c:/Users/evang/Downloads/diskscope/Services/DatabaseService.cs#L181-L198), method [`ClearIndex(IReadOnlyList<string>? roots = null)`](file:///c:/Users/evang/Downloads/diskscope/Services/DatabaseService.cs#L171) |
| **6. Root Cause** | In `DatabaseService.cs` lines 185–198:<br>```csharp<br>bool isFullClear = roots == null || roots.Count == 0;<br>if (!isFullClear && roots != null)<br>{<br>    // Check if the target root is a drive root (e.g. C:\ or C:), meaning full drive clear<br>    isFullClear = roots.Any(r =><br>    {<br>        string trimmed = r.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);<br>        return trimmed.Length <= 2 && trimmed.EndsWith(":");<br>    });<br>}<br>if (isFullClear)<br>{<br>    using var cmd = _connection.CreateCommand();<br>    cmd.Transaction = tx;<br>    cmd.CommandText = "DELETE FROM files; DELETE FROM directories;";<br>    cmd.ExecuteNonQuery();<br>}<br>```<br>When `ScannerService.ScanDrivesAsync(new[] { @"D:\" }, ...)` initiates a scan of drive `D:\`, it invokes `_dbService.ClearIndex(roots)`. Because `trimmed` (`"D:"`) has `Length == 2` and ends with `":"`, `isFullClear` evaluates to **`true`**. The method then executes an unqualified table purge (`DELETE FROM files; DELETE FROM directories;`), destroying all previously indexed data from drive `C:\` and any other drive. |
| **7. Severity** | **P0 / BLOCKER** (Release-Blocking Data Safety Hazard) |
| **8. Audit Risk Confirmed?** | **YES — Formally confirms Release Blocker 1 (`BUG-001`)** identified during the static code audit. |
| **9. Code Change Needed?** | **YES.** Production code in [`DatabaseService.cs`](file:///c:/Users/evang/Downloads/diskscope/Services/DatabaseService.cs#L185) must be corrected before release. |
| **10. Recommended Fix** | In `DatabaseService.ClearIndex`, remove the drive-root override from `isFullClear`. `isFullClear` should strictly be `true` only when `roots == null`, `roots.Count == 0`, or `roots` contains `"ALL"`. When any specific path or drive root (e.g. `D:\`) is passed, it must route to the scoped range deletion (`DELETE FROM files WHERE path = $root OR (path >= $prefix AND path < $prefixUpper)`), which safely isolates the deletion to that specific volume. |

---

## 3. Test Harness & Dataset Validity Verification

- **Deterministic Test Datasets:**  
  `TestDataGenerator` uses seeded pseudo-randomness (`new Random(42)`, `new Random(101)`, `new Random(1337)`) and explicit byte arrays. File counts, directory depths, and byte totals are mathematically fixed and verifiable against reference constants.
- **Filesystem Isolation:**  
  All tests execute strictly under `%TEMP%\ArborGraph_QA_Harness\<guid>\`. Database files are created in isolated temporary subdirectories.
- **Safety Guards Against System Data Modification:**  
  `TestDataGenerator.SafeCleanup()` enforces an active guard that validates `Path.GetFullPath(dir).StartsWith(Path.GetTempPath())`. It will throw an `InvalidOperationException` if any attempt is made to delete outside the temporary folder. Real user directories (`C:\Windows`, `C:\Program Files`, `C:\Users`) are never touched.
- **Appropriate Assertions:**  
  The assertion in `TC-DRV-01` is not overly strict; it tests the core functional requirement that scanning drive `D:\` does not delete existing `C:\` records.
- **No Fictitious API Calls:**  
  Every test calls real production classes and methods: `DatabaseService`, `ScannerService`, `DuplicateAnalyzer`, `DeveloperStorageService`, `TreemapLayoutEngine`, `ExportService`, and `SettingsService`.

---

## 4. Passing Tests Breakdown (18 Tests)

| Test ID | Priority | Category | Duration | Verification Scope & Observations |
| :--- | :--- | :--- | :--- | :--- |
| **`TC-DRV-03`** | P1 / CRITICAL | Database | 16 ms | Scoped `ClearIndex` for custom directory (`C:\TestFolderA%`) clears only matching records while leaving `C:\TestFolderB` intact. Proves range deletion syntax works for subdirectories. |
| **`TC-DB-01`** | P1 / CRITICAL | Database Concurrency | 569 ms | SQLite WAL mode allows concurrent UI readers to query paged results while background worker inserts bulk batches without lock timeouts or `database is locked` exceptions. |
| **`TC-DB-03`** | P1 / CRITICAL | Database Integrity | 14 ms | Database initializes in WAL journal mode, creates all required tables and indices, and passes SQLite low-level page check (`PRAGMA integrity_check`). |
| **`TC-SCN-01`** | P1 / CRITICAL | Scanner Engine | 343 ms | Bounded-channel BFS scanner halts within 343 ms when `CancellationToken` is cancelled, maintaining database integrity. |
| **`TC-SCN-02`** | P1 / CRITICAL | Scanner Engine | 29 ms | Scanner safely traps `UnauthorizedAccessException`, logs the folder to `SkippedDirectories`, and completes without crashing. |
| **`TC-SCN-03`** | P1 / CRITICAL | Scanner Engine | 44 ms | Deeply nested directory hierarchies (> 260 characters / Windows `MAX_PATH`) are traversed, indexed, and retrieved from SQLite. |
| **`TC-SCN-EDGE`**| P2 / MAJOR | Scanner Engine | 45 ms | Traversal accurately indexes Unicode paths (Japanese, Arabic, Cyrillic, Emoji), special characters (`#`, `%`, `&`, `'`), and empty folders with 100% numerical accounting. |
| **`TC-SET-01`** | P1 / CRITICAL | Settings & Scanner | 70 ms | Directories configured in `SettingsService` exclusion list are skipped by the scanner and recorded in `SkippedDirectories`. |
| **`TC-ROL-01`** | P1 / CRITICAL | Directory Rollup | 35 ms | `BuildDirectoryRollup` calculates exact recursive parent/child sums; root folder size matches sum of children (228,891 bytes). |
| **`TC-ROL-02`** | P1 / CRITICAL | Directory Rollup | 35 ms | Hierarchical bottom-up rollup on 1,000 synthetic directories across 4 levels completes in 35 ms. |
| **`TC-DUP-01`** | P1 / CRITICAL | Duplicate Detection | 52 ms | 3-stage cryptographic pipeline (Size -> MD5 16KB prefix -> full SHA-256) detects genuine duplicates and rejects same-size collision pairs. |
| **`TC-DUP-02`** | P2 / MAJOR | Duplicate Detection | 13 ms | 0-byte files and empty candidate sets are handled safely without division-by-zero or unhandled exceptions. |
| **`TC-DEV-01`** | P1 / CRITICAL | Developer Storage | 35 ms | Contextual parent markers correctly identify Node.js, .NET, Rust, and Gradle caches, while false-positive traps (`customer-targets/target`, `architectural-blueprints/build`) are rejected. |
| **`TC-QRY-01`** | P1 / CRITICAL | Query Engine | 18 ms | Multi-criteria SQL search filters by size, extension, age, and location prefix with working offset pagination. |
| **`TC-QRY-SQLI`**| P1 / CRITICAL | Query Engine | 13 ms | Injection-like input (`' OR '1'='1` and `'; DROP TABLE files; --`) is handled as literal parameters without SQL syntax errors or table corruption. |
| **`TC-EXP-01`** | P1 / CRITICAL | Export Subsystem | 53 ms | Standalone HTML5 report, JSON structure, and CSV files export cleanly with RFC 4180 quote escaping. |
| **`TC-TMP-01`** | P1 / CRITICAL | Treemap Engine | 64 ms | Squarified layout engine handles 0-size files, single items, and extreme aspect ratios (5000 x 50) without producing `NaN` or `Infinity`. |
| **`TC-LGL-01`** | P0 / BLOCKER | Settings & Legal | 9 ms | EULA acceptance defaults to `false` on clean install; accepted state, version, and timestamp persist across reloads. |

---

## 5. Failing Tests (1 Test)

- **`TC-DRV-01`**: Multi-Drive Sequential Scan Data Isolation (ClearIndex Root Scoping Bug)
  - **Status:** FAILED
  - **Severity:** P0 / BLOCKER
  - **Reason:** Code in `DatabaseService.cs` treats drive root targets like `"D:"` as a full database purge, clearing `C:\` data when `D:\` is scanned.

---

## 6. Blocked Tests (0 Tests)

- **None.** All 19 registered automated tests executed to completion without unhandled aborts.

---

## 7. Test Infrastructure Problems (0 Detected)

- **None.** The test harness compiled with 0 warnings, generated datasets deterministically, cleaned up all temporary resources, and produced reliable execution results.

---

## 8. Confirmed Release-Blocking Defects

1. **`BUG-001` (Confirmed by `TC-DRV-01`):**  
   **Multi-Drive Index Wiping:** `DatabaseService.ClearIndex` wipes previously indexed drives when scanning another drive root (`C:\` data deleted when `D:\` is scanned).

---

## 9. Recommended Fix Order & Release Roadmap

### 1. Fix First (Release Blocker — P0)
- **`BUG-001` (`TC-DRV-01`):** Correct drive-root scoping logic in [`DatabaseService.cs`](file:///c:/Users/evang/Downloads/diskscope/Services/DatabaseService.cs#L185-L198) so that drive roots execute scoped range deletions rather than full table purges.

### 2. Fix Second (Release Blocker — P0)
- **`BUG-002` (`TC-UI-RESP-01`):** Offload file and directory deletions in `LargestFoldersViewModel.cs` (line 89) and `LargestFilesViewModel.cs` (line 272) from the UI dispatcher thread to background tasks (`Task.Run`) with cancellation and progress indicators to prevent the window from freezing ("Not Responding").

### 3. Fix Third (Release Blocker — P0)
- **`BUG-003` (`TC-INS-04`):** Update `installer/installer.iss` line 9 to replace the legacy URL `https://github.com/12valor/C-file-scanner` with `https://github.com/12valor/ArborGraph`.

### 4. Fix Later (Critical Reliability Hardening — P1)
- **`BUG-004` (`TC-USN-01`):** Add explicit pointer boundary and buffer size guards to `UsnJournalService.cs` (lines 230–245) when reading variable-length USN records via raw pointer arithmetic.

### 5. Manual Testing Still Required
- **Shell Recycle Bin Recovery (`TC-DEL-01`):** Verify `IFileOperation` integration and file recovery from desktop Recycle Bin.
- **Protected System Paths (`TC-DEL-02`):** Verify interactive confirmation dialogs block attempts to delete `C:\Windows`.
- **UI Fluidity During Deletion (`TC-UI-RESP-01`):** Verify window responsiveness while deleting 10,000+ files.
- **Inno Setup Installer (`TC-INS-01` to `TC-INS-07`):** Verify lowest-privilege installation, shortcuts, and silent mode on a clean Windows VM.
- **High-DPI Display Scaling (`TC-CMP-01`):** Visually verify vector rendering across 100%, 125%, 150%, 175%, and 200% scaling.
- **Human First-Time User Usability (`TC-USB-01` to `TC-USB-06`):** Conduct think-aloud evaluation with representative users.
