# ArborGraph Automated QA Test Suite Documentation

**Document Identifier:** AG-AT-001  
**Target Product:** ArborGraph — Filesystem Analytics & Visualization  
**Target Release:** v1.0.0 (`net8.0-windows` x64)  
**Test Project Location:** `tests/DiskScope.Tests.csproj`  

---

## 1. Overview & Architecture

The ArborGraph automated test harness provides deterministic, non-UI execution for core filesystem traversal, SQLite persistence, cryptographic duplicate detection, recursive folder rollup, developer storage detection, query parsing, and file exports.

### Key Architectural Characteristics
- **Isolation:** Tests generate temporary isolated filesystems under `%TEMP%\ArborGraph_QA_Harness\` and automatically clean them up upon completion.
- **Safety:** Never touches real system folders (`C:\Windows`, `C:\Program Files`, `C:\Users`).
- **Zero Mocking on Core Algorithms:** Operates directly against live SQLite database instances (`Microsoft.Data.Sqlite`) in WAL mode and real disk files to catch actual Win32/file-system edge cases.
- **Defect Correlation:** Automatically correlates test failures with known risks identified during the initial code audit (e.g. Blocker 1 Multi-Drive ClearIndex bug).

---

## 2. How to Run the Automated Test Suite

### Environment Prerequisites
ArborGraph is built on .NET 8.0 SDK. When running in environments where the active SDK is installed in user profile:
```powershell
$env:DOTNET_ROOT = "C:\Users\evang\.dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
```

### Run All Automated Tests
Executes all registered automated test cases across all categories:
```powershell
dotnet run --project tests/DiskScope.Tests.csproj
```
*(Alternative via build script: `start.bat test`)*

### Run Critical Tests Only (P0 & P1 Blockers)
Filters execution strictly to release-blocking and critical quality gates:
```powershell
dotnet run --project tests/DiskScope.Tests.csproj -- --critical
```

### Run Specific Test Filter
Filter by Test ID (e.g. `TC-DRV-01`), Category (e.g. `Duplicate`), or Title:
```powershell
dotnet run --project tests/DiskScope.Tests.csproj -- --filter TC-DRV
dotnet run --project tests/DiskScope.Tests.csproj -- --filter Rollup
```

### Run Scale Benchmarks on Large Datasets
Generates a deterministic temporary dataset of the specified file count (e.g. 1,000, 10,000, or 50,000 files) and measures traversal throughput, peak RAM delta, and rollup duration:
```powershell
dotnet run --project tests/DiskScope.Tests.csproj -- --benchmark 10000
dotnet run --project tests/DiskScope.Tests.csproj -- --benchmark 50000
```

### Run Legacy 20-Stage Integration Suite
Runs the full legacy end-to-end integration pipeline:
```powershell
dotnet run --project tests/DiskScope.Tests.csproj -- --legacy
```

---

## 3. Automated Test Coverage Matrix

| Test ID | Category | Title | Priority | Automated Verification Scope |
| :--- | :--- | :--- | :--- | :--- |
| **`TC-DRV-01`** | Database / Safety | Multi-Drive Sequential Scan Data Isolation | **P0 / BLOCKER** | Verifies scanning/clearing drive `D:\` does not wipe `C:\` records from SQLite. |
| **`TC-DRV-03`** | Database / Safety | Custom Directory Target Scoped ClearIndex | **P1 / CRITICAL** | Verifies scoped deletion only purges target directory prefix. |
| **`TC-DB-01`** | Database Concurrency | Concurrent SQLite Reads During Active Insertion | **P1 / CRITICAL** | Verifies WAL mode allows concurrent UI queries without lock errors. |
| **`TC-DB-03`** | Database Integrity | Database Schema Creation, WAL & Integrity Check | **P1 / CRITICAL** | Verifies schema creation, WAL journal mode, and `PRAGMA integrity_check`. |
| **`TC-SCN-01`** | Scanner Engine | Traversal Cancellation Responsiveness (< 500ms) | **P1 / CRITICAL** | Verifies channel consumer halts within 500ms upon cancellation. |
| **`TC-SCN-02`** | Scanner Engine | Inaccessible System Folders Graceful Bypass | **P1 / CRITICAL** | Verifies `UnauthorizedAccessException` is logged and skipped without crash. |
| **`TC-SCN-03`** | Scanner Engine | Deep Path Traversal (> 260 Characters) | **P1 / CRITICAL** | Verifies paths exceeding Windows `MAX_PATH` are indexed cleanly. |
| **`TC-SCN-EDGE`**| Scanner Engine | Unicode, Special Characters & Empty Folders | **P2 / MAJOR** | Verifies Japanese, Arabic, Emoji, and quoted filenames in SQLite. |
| **`TC-SET-01`** | Settings & Scanner | Scanner Exclusion Rules Enforcement | **P1 / CRITICAL** | Verifies paths configured in `SettingsService` are skipped by scanner. |
| **`TC-ROL-01`** | Directory Rollup | Recursive Rollup Parent/Child Size Calculations | **P1 / CRITICAL** | Verifies root rolled-up size exactly matches sum of child files. |
| **`TC-ROL-02`** | Directory Rollup | Synthetic 1,000-Directory Rollup Scalability | **P1 / CRITICAL** | Verifies hierarchical bottom-up rollup finishes in < 3,000 ms. |
| **`TC-DUP-01`** | Duplicate Detection | 3-Stage Pipeline & Collision Rejection | **P1 / CRITICAL** | Verifies genuine duplicate detection and rejection of same-size collisions. |
| **`TC-DUP-02`** | Duplicate Detection | Zero-Byte Files & Empty Candidate Handling | **P2 / MAJOR** | Verifies 0-byte files or empty databases do not throw exceptions. |
| **`TC-DEV-01`** | Developer Storage | Contextual Project Marker Verification | **P1 / CRITICAL** | Verifies Node, .NET, Rust, Gradle detected; generic target/build rejected. |
| **`TC-QRY-01`** | Query Engine | Multi-Criteria Storage Search & Paging | **P1 / CRITICAL** | Verifies filtering by size, age, extension, location, and pagination. |
| **`TC-QRY-SQLI`**| Query Engine | SQL Injection Safety in Search Inputs | **P1 / CRITICAL** | Verifies quotes, semicolons, and SQL operators are safely parameterized. |
| **`TC-EXP-01`** | Export Subsystem | Audit Export Integrity (HTML, JSON, CSV) | **P1 / CRITICAL** | Verifies HTML5 validity, JSON structure, and RFC 4180 CSV escaping. |
| **`TC-TMP-01`** | Treemap Engine | Squarified Treemap Layout Edge Cases | **P1 / CRITICAL** | Verifies zero-size, 1-item, and extreme aspect ratios produce valid rects. |
| **`TC-LGL-01`** | Settings & Legal | First-Run EULA Consent Gate Persistence | **P0 / BLOCKER** | Verifies EULA consent boolean and timestamp persist across restarts. |

---

## 4. Tests Requiring Manual Windows / UI Execution

The following tests require manual human verification or dedicated physical environments and cannot be fully automated headlessly:

| Test ID | Title | Why Manual Testing Is Required |
| :--- | :--- | :--- |
| **`TC-DEL-01`** | Recycle Bin Deletion & Shell Recovery | Requires Windows Shell desktop interaction with `IFileOperation` and manual desktop Recycle Bin restoration. |
| **`TC-DEL-02`** | Protected System Path Deletion Guard | Requires interactive confirmation that WPF modal dialogs block deletion attempts on `C:\Windows`. |
| **`TC-UI-RESP-01`**| Synchronous Deletion UI Thread Freezing | Requires observing whether the actual Windows desktop window title bar displays "(Not Responding)" during large deletions. |
| **`TC-USN-01`** | USN Journal Native Pointer Boundary Safety | Requires elevated Administrator privileges and real physical NTFS volume changes (`DeviceIoControl`). |
| **`TC-INS-01` to `06`**| Inno Setup 6 Desktop Installer | Requires physical setup wizard execution, Start Menu shortcut clicking, and Control Panel "Installed Apps" inspection. |
| **`TC-CMP-01`** | High-DPI Display Scaling Matrix (100%–200%) | Requires changing physical monitor display DPI settings in Windows Settings and visually inspecting vector sharpness and typography clipping. |
| **`TC-USB-01` to `06`**| Human First-Time User Usability Testing | Requires real human think-aloud evaluation measuring task completion, hesitation, and System Usability Scale (SUS) scores. |
