# ArborGraph Defect & Bug Tracking Log

**Document Identifier:** AG-BL-001  
**Target Release:** v1.0.0 (`net8.0-windows` x64)  
**Tracking Policy:** All reproducible failures, performance breaches, data-safety risks, and UI stalls must be logged here.  
**Release Blocking Rule:** Any defect classified as **P0 / BLOCKER** or **P1 / CRITICAL** halts the release until remediated and verified through regression testing.

---

## 1. Summary of Defects

| Bug ID | Severity | Feature / Test ID | Short Description | Status | Target Fix |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **BUG-001** | P0 / BLOCKER | `FEAT-08` / `TC-DRV-01`, `TC-DRV-02` | Multi-drive `ClearIndex` root path logic (`isFullClear`) wipes previous drives | CLOSED (Verified: TC-DRV-01, TC-DRV-02 PASS) | v1.0.0 |
| **BUG-002** | P0 / BLOCKER | `FEAT-19` / `TC-UI-RESP-01`, `TC-UI-RESP-02` | Synchronous deletion executes on UI dispatcher thread, freezing window | CLOSED (Verified: TC-UI-RESP-01, TC-UI-RESP-02 PASS, Dispatcher latency < 1ms) | v1.0.0 |
| **BUG-003** | P0 / BLOCKER | `FEAT-43` / `TC-INS-04` | Inno Setup `installer.iss` hardcodes outdated repository URL | CLOSED (Verified: Clean Inno Setup compile & TC-INS-04 PASS) | v1.0.0 |
| **BUG-004** | P1 / CRITICAL | `FEAT-04` / `TC-USN-01` | USN Journal native pointer boundary arithmetic risks memory violation | CLOSED (Verified: TC-USN-01 to TC-USN-04 PASS) | v1.0.0 |
| **BUG-005** | P3 / MINOR | `FEAT-30` / `SEC-09`, `TC-QRY-01` | Location filter query boundary binds `LIKE 'C:\Test%'` without trailing slash | CLOSED (Verified: SEC-09, TC-QRY-01 PASS) | v1.0.0 |

*Note: All confirmed audit defects (BUG-001 through BUG-005) remediated and verified passing with zero open regressions.*

---

## 2. Detailed Bug Reports

### Bug ID: `BUG-001`
- **Severity:** P0 / BLOCKER
- **Feature / Test ID:** `FEAT-08` / `TC-DRV-01`, `TC-DRV-02`
- **Title:** Multi-Drive Root Scan Wipes Previously Indexed Drives in SQLite (`ClearIndex` Scope Bug)
- **Environment:** Windows 11 x64, Multi-drive system (`C:\` and `D:\`)
- **Status:** CLOSED (FIXED in `Services/DatabaseService.cs`; Verified by automated tests `TC-DRV-01` and `TC-DRV-02`)
- **Fix / Version:** v1.0.0
- **Regression Status:** VERIFIED PASS (0 Failures, 100% Pass Rate across 20 automated tests)
- **Preconditions:** Host system has at least two drives (`C:\` and `D:\`).
- **Steps to Reproduce:**
  1. Launch ArborGraph. Select drive `C:\` and click `Start Scan`.
  2. Wait for scan completion. Verify `SELECT COUNT(*) FROM files WHERE path LIKE 'C:%'` > 0.
  3. Uncheck `C:\` and select drive `D:\`.
  4. Click `Start Scan`.
  5. Inspect SQLite database during or after scan of `D:\`.
- **Expected Result:**
  - Scanning `D:\` only deletes and updates records for `D:\`.
  - Records for `C:\` remain in the database so the user can view multi-drive analytics.
- **Actual Result (Audit Finding):**
  - In `src/Services/DatabaseService.cs` (lines 185–198):
    ```csharp
    bool isFullClear = string.IsNullOrEmpty(rootPath) || 
                       trimmed.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                       (trimmed.Length <= 2 && trimmed.EndsWith(":", StringComparison.OrdinalIgnoreCase));
    if (isFullClear)
    {
        // Executes full wipe of all drives!
        using var cmd = new SqliteCommand("DELETE FROM files; DELETE FROM directories;", conn);
        cmd.ExecuteNonQuery();
    }
    ```
  - Scanning `D:` triggers `isFullClear = true` because `D:` has length 2 and ends in `:`, deleting all `C:` records.

---

### Bug ID: `BUG-002`
- **Severity:** P0 / BLOCKER
- **Feature / Test ID:** `FEAT-19`, `FEAT-38` / `TC-UI-RESP-01`, `TC-UI-RESP-02`, `TC-DEL-01`..`05`
- **Title:** Synchronous File and Folder Deletion Blocks WPF UI Thread (Window "Not Responding")
- **Environment:** Windows 10 / 11 x64
- **Status:** CLOSED (FIXED in `ViewModels/LargestFoldersViewModel.cs` and `ViewModels/LargestFilesViewModel.cs`; Verified by live Dispatcher latency benchmark `TC-UI-RESP-01` and `TC-UI-RESP-02`)
- **Fix / Version:** v1.0.0
- **Regression Status:** VERIFIED PASS (TC-UI-RESP-01: deleted 10,000 files in 1,330ms, avg latency 0.22ms, max 0.82ms, 0 frames > 50ms; TC-UI-RESP-02: deleted 500 files in 97ms).
- **Preconditions:** Folder containing 10,000+ files or large directory tree.
- **Steps to Reproduce:**
  1. Navigate to `Largest Folders` view.
  2. Select large test folder.
  3. Click `Delete Permanently` and confirm modal dialog.
  4. Attempt to move window or interact with tabs while deletion is underway.
- **Expected Result:** Deletion runs asynchronously on background thread (`Task.Run`); UI remains responsive; displays indeterminate progress bar.
- **Actual Result (Audit Finding):**
  - In `src/ViewModels/LargestFoldersViewModel.cs` (line 89):
    ```csharp
    FileSecurityHelper.DeletePermanently(folder.Path); // Executed synchronously on UI Dispatcher!
    ```
  - In `src/ViewModels/LargestFilesViewModel.cs` (line 272):
    ```csharp
    FileSecurityHelper.DeletePermanently(file.Path); // Synchronous loop on UI Dispatcher!
    ```
  - Synchronous blocking causes the Windows OS to flag the window as "Not Responding" during long I/O operations.

---

### Bug ID: `BUG-003`
- **Severity:** P0 / BLOCKER
- **Feature / Test ID:** `FEAT-43` / `TC-INS-04`
- **Title:** Inno Setup Installer Hardcodes Outdated Repository and Publisher URL
- **Environment:** Windows Inno Setup 6 / Windows Installed Apps Settings
- **Status:** CLOSED (FIXED in `installer/installer.iss`; Verified by Inno Setup 6.7.3 compiler & automated test `TC-INS-04`)
- **Fix / Version:** v1.0.0
- **Regression Status:** VERIFIED PASS (Inno Setup 6 compiler succeeded with 0 errors; TC-INS-04 passed in 2ms; full 26-test suite passed)
- **Preconditions:** Application installer built via `installer/installer.iss`.
- **Steps to Reproduce:**
  1. Inspect `installer/installer.iss` line 9.
  2. Notice legacy repository URL `https://github.com/12valor/C-file-scanner`.
  3. When compiled, Inno Setup injects `AppPublisherURL`, `AppSupportURL`, and `AppUpdatesURL` into the Windows uninstall registry.
- **Expected Result:** URL points to active repository: `https://github.com/12valor/ArborGraph`.
- **Actual Result Prior to Fix:**
  - `installer/installer.iss` line 9 specified:
    ```pascal
    #define MyAppURL "https://github.com/12valor/C-file-scanner"
    ```
  - Pointed to the legacy precursor project repository instead of `ArborGraph`.
- **Remediation Details:**
  - Updated `#define MyAppURL "https://github.com/12valor/ArborGraph"` in `installer/installer.iss`.
  - Compiled clean binary `dist/setup/ArborGraph-Setup-1.0.0-x64.exe` (68,240,131 bytes) using Inno Setup 6.7.3 (`iscc.exe`).
  - Added and executed automated test `TC-INS-04` confirming `MyAppURL`, `AppPublisherURL`, `AppSupportURL`, and `AppUpdatesURL` are set to `https://github.com/12valor/ArborGraph` with zero references to `C-file-scanner`.

---

### Bug ID: `BUG-004`
- **Severity:** P1 / CRITICAL
- **Feature / Test ID:** `FEAT-04` / `TC-USN-01`
- **Title:** USN Change Journal Unmanaged Memory Pointer Bounds Risk on Fragmented NTFS Volumes
- **Environment:** Windows 10 / 11 x64, Active NTFS Volume
- **Status:** CLOSED (FIXED in `Services/UsnJournalService.cs`; Verified by automated tests `TC-USN-01` through `TC-USN-04`)
- **Fix / Version:** v1.0.0
- **Regression Status:** VERIFIED PASS (Automated suite 30/30 passed; zero memory corruption or access violation exceptions)
- **Preconditions:** Large, heavily fragmented NTFS volume with active USN Change Journal, or truncated driver buffer.
- **Steps to Reproduce:**
  1. Trigger incremental USN Journal scan on active volume.
  2. Inspect buffer parsing in `UsnJournalService.cs`.
- **Expected Result:** Pointer arithmetic is strictly bounded by buffer byte counts with structured boundary checks.
- **Actual Result Prior to Fix:**
  - In `Services/UsnJournalService.cs`:
    - Inner while-loop did not check if remaining bytes were sufficient to read `recordLength` (4 bytes).
    - Did not check if `recordLength` was valid (at least 8 bytes and `<= bytesReturned - offset`).
    - Did not check if `recordLength` for V2 record was at least 60 bytes before reading header fields.
    - Did not check if `fileNameOffset` and `fileNameLength` resided within `[60, recordLength]`, allowing `Marshal.PtrToStringUni` to read out-of-bounds process memory.
- **Remediation Details:**
  - Introduced `UsnRecordValidationStatus` and `UsnRecordValidator` to enforce bounds checks on `recordLength`, minimum header size (8 bytes), V2 header size (60 bytes), and filename bounds.
  - Implemented `TryReadNextRecord` which validates offsets and byte counts before any native reads or string marshaling.
  - Capped effective bytes in `ReadChanges` to `Math.Min(bytesReturned, bufferSize)`.
  - Added diagnostic logging to `%LOCALAPPDATA%\ArborGraph\app.log`.
  - Added automated tests `TC-USN-01` through `TC-USN-04` validating valid records, zero-length EOF, small record lengths, overflowing record lengths, buffer truncations, corrupt filename offsets, multi-record sequences, and fallback branches.

---

### Bug ID: `BUG-005`
- **Severity:** P3 / MINOR
- **Feature / Test ID:** `FEAT-30` / `SEC-09`, `TC-QRY-01`
- **Title:** Location Prefix Filter In SQLite Query Binds Sibling Folders Without Trailing Slash (`LIKE 'C:\Test%'`)
- **Environment:** Windows 10 / 11 x64, SQLite 3
- **Status:** CLOSED (FIXED in `Services/DatabaseService.cs`; Verified by automated tests `SEC-09` and `TC-QRY-01`)
- **Fix / Version:** v1.0.0
- **Regression Status:** VERIFIED PASS (SEC-09 PASS, TC-QRY-01 PASS, 30/30 suite PASS)
- **Preconditions:** Files indexed across folders sharing a common prefix (e.g. `C:\Test` and `C:\Test2`).
- **Steps to Reproduce:**
  1. Index files in both `C:\Test` (e.g. `C:\Test\in_target.txt`) and `C:\Test2` (e.g. `C:\Test2\sibling.txt`).
  2. Call `DatabaseService.GetFilesPaged(..., locationPrefix: @"C:\Test")`.
  3. Inspect returned file results.
- **Expected Result:**
  - Query returns only files inside `C:\Test\` and its subdirectories.
  - Files in sibling directory `C:\Test2` are excluded.
- **Actual Result Prior to Fix:**
  - In `Services/DatabaseService.cs` lines 561-562, 657, and 1123:
    ```csharp
    whereClause += " AND path LIKE $loc";
    command.Parameters.AddWithValue("$loc", $"{locationPrefix.TrimEnd('\\', '/')}%");
    ```
  - `TrimEnd('\\', '/')` stripped the trailing slash, causing `$loc` to bind to `C:\Test%`.
  - In SQLite, `path LIKE 'C:\Test%'` matched both `C:\Test\in_target.txt` AND `C:\Test2\sibling.txt`.
- **Remediation Details:**
  - In `Services/DatabaseService.cs` (`GetFilesPaged`, `GetFilteredFileCount`, and `StreamFilteredFiles`):
    Normalized `locationPrefix` by trimming trailing slashes, ensuring drive letter casing, and appending a trailing path separator before the wildcard (`$"{cleanLoc}\\%"` and `$"{cleanLoc.Replace('\\', '/')}/%"`):
    ```csharp
    string cleanLoc = locationPrefix.Trim().TrimEnd('\\', '/');
    if (cleanLoc.Length >= 2 && cleanLoc[1] == ':')
    {
        cleanLoc = char.ToUpperInvariant(cleanLoc[0]) + cleanLoc.Substring(1);
    }
    if (!string.IsNullOrEmpty(cleanLoc))
    {
        whereClause += " AND (path LIKE $loc OR path LIKE $locFwd)";
        cmd.Parameters.AddWithValue("$loc", $"{cleanLoc}\\%");
        cmd.Parameters.AddWithValue("$locFwd", $"{cleanLoc.Replace('\\', '/')}/%");
    }
    ```
  - Strengthened `TC-QRY-01` in `tests/AutomatedTestSuites.cs` to insert sibling folder fixtures (`C:\Media2`) and assert strict boundary isolation across `GetFilesPaged`, `GetFilteredFileCount`, and `StreamFilteredFiles`.
  - Verified via `tests/SecurityAuditRunner.cs` test `SEC-09` that sibling folder leaks are completely eliminated (`SEC-09` PASS).

---

## 3. Defect Report Template (For New Defect Logging)

```markdown
### Bug ID: `BUG-XXX`
- **Severity:** [P0 / BLOCKER | P1 / CRITICAL | P2 / MAJOR | P3 / MINOR]
- **Feature / Test ID:** [e.g. FEAT-22 / TC-DUP-01]
- **Title:** [Concise description of the failure]
- **Environment:** [OS, Architecture, Hardware, RAM, Disk Type]
- **Status:** [NEW | IN PROGRESS | RESOLVED | RE-TESTING | CLOSED]
- **Fix / Version:** [Target version or commit hash]
- **Regression Status:** [PASSED | FAILED | NOT RUN]
- **Preconditions:** [State of the system/data before reproducing]
- **Steps to Reproduce:**
  1. ...
  2. ...
- **Expected Result:** [What the system should have done]
- **Actual Result:** [What the system actually did]
- **Stack Trace / Log Snippet:**
  ```text
  [Insert logs from %LOCALAPPDATA%\ArborGraph\app.log]
  ```
```

---

## 4. Documentation & Release Claim Discrepancies Register (Prompt 15 Truth Check & Prompt 16 Remediation)

| Discrepancy ID | Severity | Source / Location | Description | Current Status | Remediation Required & Verification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **DISC-001** | P2 / MAJOR | `README.md` (line 204) | Clone instruction referenced legacy URL: `git clone https://github.com/12valor/DiskScope.git` | **CLOSED** | **VERIFIED.** Updated URL to `https://github.com/12valor/ArborGraph.git` and `cd ArborGraph` in `README.md`. |
| **DISC-002** | P2 / MAJOR | `docs/testing/RELEASE-CHECKLIST.md` (Gate 9.3), `TEST-CASES.md` (`TC-WEB-03`) | QA documents stated web tool verifies binary via file upload, but `site/index.html` implements text input paste comparator | **CLOSED** | **VERIFIED.** Updated `RELEASE-CHECKLIST.md` Gate 9.3 to reflect paste comparator interaction model. |
| **DISC-003** | P2 / MAJOR | `Views/AnalyticsView.xaml`, `Views/ScanLogView.xaml` | Views and ViewModels exist in code and `App.xaml` DataTemplates, but have no navigation buttons in `MainWindow.xaml` sidebar | **AS DESIGNED** | Kept dormant internal views for deep linking / tests; zero false advertising in public docs or website. |
| **DISC-004** | P3 / MINOR | `README.md` (line 67), `site/index.html` (line 740) | Cites "20-stage integration test suite", whereas suite now has 30 milestones + 9 security audits | **CLOSED** | **VERIFIED.** Updated `README.md` line 67 and `site/index.html` line 740 to 30-stage regression suite and 9 security audits. |

---

### Detailed Discrepancy Reports

#### Discrepancy ID: `DISC-001`
- **Severity:** P2 / MAJOR
- **Location:** `README.md` (line 204)
- **Title:** Outdated Repository Clone URL in Developer Setup Instructions
- **Status:** **CLOSED (VERIFIED PASS)**
- **Evidence:**
  ```bash
  git clone https://github.com/12valor/DiskScope.git
  cd DiskScope
  ```
- **Remediation Details:**
  Updated `README.md` lines 204–205 to:
  ```bash
  git clone https://github.com/12valor/ArborGraph.git
  cd ArborGraph
  ```
  Verified via git diff and grep search.

---

#### Discrepancy ID: `DISC-002`
- **Severity:** P2 / MAJOR
- **Location:** `docs/testing/RELEASE-CHECKLIST.md` (Gate 9.3), `docs/testing/TEST-CASES.md` (`TC-WEB-03`)
- **Title:** Checksum Verification Tool Described as File Upload Hasher Rather Than Paste Comparator
- **Status:** **CLOSED (VERIFIED PASS)**
- **Evidence:**
  - `RELEASE-CHECKLIST.md` line 136 originally stated: "Upload release binary to site/index.html Web Crypto verifier; confirm calculated hash matches PowerShell checksum."
  - `site/index.html` lines 426–441 implements `<input type="text" id="verifyInput">` comparator.
- **Remediation Details:**
  Updated `RELEASE-CHECKLIST.md` Gate 9.3 to:
  "Paste calculated SHA-256 release hash into `site/index.html` verification input; confirm match against official checksum (`SHA256SUMS.txt`)."

---

#### Discrepancy ID: `DISC-003`
- **Severity:** P2 / MAJOR
- **Location:** `Views/AnalyticsView.xaml`, `Views/ScanLogView.xaml`, `ViewModels/AnalyticsViewModel.cs`, `ViewModels/ScanLogViewModel.cs`
- **Title:** Analytics & Scan Log Views Implemented But Not User-Accessible From MainWindow Sidebar
- **Status:** **AS DESIGNED / RETAINED INTERNAL**
- **Evidence:**
  - `MainWindow.xaml` sidebar exposes 9 primary user-facing workspaces: `Overview`, `Scanner`, `Files`, `Treemap`, `Duplicates`, `Cleanup`, `Developer`, `Photoshop`, `Settings`.
  - `AnalyticsView` and `ScanLogView` exist in codebase and `App.xaml` DataTemplates, but have no sidebar navigation entry.
- **Remediation Details:**
  Confirmed that neither view is advertised in `README.md` or `site/index.html`. Retained as internal views for v1.0.0 testing and deep linking.

---

#### Discrepancy ID: `DISC-004`
- **Severity:** P3 / MINOR
- **Location:** `README.md` (line 67), `site/index.html` (line 740)
- **Title:** Outdated Integration Test Suite Count in README and Website
- **Status:** **CLOSED (VERIFIED PASS)**
- **Evidence:**
  - `README.md` line 67 stated: "20-stage integration test suite".
  - `site/index.html` line 740 stated: "Run 20-Stage Integration Suite:".
- **Remediation Details:**
  Updated `README.md` line 67 to "Run the automated 30-stage regression suite and 9 security audits".
  Updated `site/index.html` line 740 to "Run 30-Stage Regression Suite:".
  Verified against automated test suite execution (30/30 tests pass + 9/9 security tests pass).

