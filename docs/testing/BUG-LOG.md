# ArborGraph Defect & Bug Tracking Log

**Document Identifier:** AG-BL-001  
**Target Release:** v1.0.0 (`net8.0-windows` x64)  
**Tracking Policy:** All reproducible failures, performance breaches, data-safety risks, and UI stalls must be logged here.  
**Release Blocking Rule:** Any defect classified as **P0 / BLOCKER** or **P1 / CRITICAL** halts the release until remediated and verified through regression testing.

---

## 1. Summary of Defects

| Bug ID | Severity | Feature / Test ID | Short Description | Status | Target Fix |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **BUG-001** | P0 / BLOCKER | `FEAT-08` / `TC-DRV-01` | Multi-drive `ClearIndex` root path logic (`isFullClear`) wipes previous drives | CONFIRMED DEFECT (`TC-DRV-01` FAILED) | v1.0.0 |
| **BUG-002** | P0 / BLOCKER | `FEAT-19` / `TC-UI-RESP-01` | Synchronous deletion executes on UI dispatcher thread, freezing window | OPEN (Audit Flag) | v1.0.0 |
| **BUG-003** | P0 / BLOCKER | `FEAT-43` / `TC-INS-04` | Inno Setup `installer.iss` hardcodes outdated repository URL | OPEN (Audit Flag) | v1.0.0 |
| **BUG-004** | P1 / CRITICAL | `FEAT-04` / `TC-USN-01` | USN Journal native pointer boundary arithmetic risks memory violation | OPEN (Audit Flag) | v1.0.0 |

---

## 2. Detailed Bug Reports

### Bug ID: `BUG-001`
- **Severity:** P0 / BLOCKER
- **Feature / Test ID:** `FEAT-08` / `TC-DRV-01`
- **Title:** Multi-Drive Root Scan Wipes Previously Indexed Drives in SQLite (`ClearIndex` Scope Bug)
- **Environment:** Windows 11 x64, Multi-drive system (`C:\` and `D:\`)
- **Status:** CONFIRMED DEFECT (Reproduced and Failed in Automated Test `TC-DRV-01`)
- **Fix / Version:** v1.0.0 (Release Blocker)
- **Regression Status:** PENDING REMEDIATION & RE-TESTING
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
- **Feature / Test ID:** `FEAT-19`, `FEAT-38` / `TC-UI-RESP-01`, `TC-UI-RESP-02`
- **Title:** Synchronous File and Folder Deletion Blocks WPF UI Thread (Window "Not Responding")
- **Environment:** Windows 10 / 11 x64
- **Status:** OPEN (Identified in Code Audit — Pending Test Execution Confirmation)
- **Fix / Version:** v1.0.0
- **Regression Status:** PENDING VERIFICATION
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
- **Status:** OPEN (Identified in Code Audit — Pending Test Execution Confirmation)
- **Fix / Version:** v1.0.0
- **Regression Status:** PENDING VERIFICATION
- **Preconditions:** Application installed via `installer/installer.iss`.
- **Steps to Reproduce:**
  1. Inspect `installer/installer.iss` line 9.
  2. Install application on Windows.
  3. Check Windows `Settings` -> `Apps` -> `Installed Apps` -> `ArborGraph` -> Support URL.
- **Expected Result:** URL points to active repository: `https://github.com/12valor/ArborGraph`.
- **Actual Result (Audit Finding):**
  - `installer/installer.iss` line 9 specifies:
    ```pascal
    #define MyAppURL "https://github.com/12valor/C-file-scanner"
    ```
  - Points to the legacy precursor project repository instead of `ArborGraph`.

---

### Bug ID: `BUG-004`
- **Severity:** P1 / CRITICAL
- **Feature / Test ID:** `FEAT-04` / `TC-USN-01`
- **Title:** USN Change Journal Unmanaged Memory Pointer Bounds Risk on Fragmented NTFS Volumes
- **Environment:** Windows 10 / 11 x64, Active NTFS Volume
- **Status:** OPEN (Identified in Code Audit — Pending Test Execution Confirmation)
- **Fix / Version:** v1.0.0
- **Regression Status:** PENDING VERIFICATION
- **Preconditions:** Large, heavily fragmented NTFS volume with active USN Change Journal.
- **Steps to Reproduce:**
  1. Trigger incremental USN Journal scan on active volume.
  2. Inspect buffer parsing in `UsnJournalService.cs`.
- **Expected Result:** Pointer arithmetic is strictly bounded by buffer byte counts with structured boundary checks.
- **Actual Result (Audit Finding):**
  - `src/Services/UsnJournalService.cs` (lines 230–245) performs manual pointer advance:
    ```csharp
    IntPtr recordPtr = new IntPtr(bufferPtr.ToInt64() + 8);
    // Uses Marshal.ReadInt32 and Marshal.PtrToStructure without rigorous boundary asserts
    ```
  - If a corrupted or truncated USN record is returned by `DeviceIoControl`, reading past allocated buffer could trigger an uncatchable `AccessViolationException`.

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
