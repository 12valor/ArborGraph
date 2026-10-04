# ArborGraph Test Execution Results Log

**Document Identifier:** AG-TR-001  
**Target Release:** v1.0.0 (`net8.0-windows` x64)  
**Execution Status:** Ready for Execution (All tests initialized as `NOT TESTED`)  
**Execution Policy:** Results are left blank/empty until formal test execution is conducted. Pre-existing observations from code audits are documented in the Notes column as `Previously observed / audit evidence`.

---

## 1. Execution Summary Dashboard

| Metric | Count |
| :--- | :--- |
| **Total Test Cases Planned** | 56 |
| **Passed** | 0 |
| **Failed** | 0 |
| **Blocked** | 0 |
| **Not Tested / Pending Execution** | 56 |
| **Execution Progress** | 0.0% |

---

## 2. Test Execution Log Table

| Test ID | Date | Environment | Expected Result | Actual Result | Status | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **TC-DRV-01** | | ENV-A | Scanning D:\ preserves C:\ index in SQLite | | `NOT TESTED` | Previously observed: `DatabaseService.cs` line 185 has full wipe risk. |
| **TC-DRV-02** | | ENV-A | Multi-root scan traverses both roots cleanly | | `NOT TESTED` | Requires multi-drive configuration test. |
| **TC-DRV-03** | | ENV-A | Custom directory scan only clears scoped records | | `NOT TESTED` | Verifies `path LIKE 'target%'` scoping. |
| **TC-DEL-01** | | ENV-A | Deleted file recoverable from Recycle Bin | | `NOT TESTED` | Shell32 `IFileOperation` integration. |
| **TC-DEL-02** | | ENV-A | Protected paths (C:\Windows) blocked from deletion | | `NOT TESTED` | Handled by `FileSecurityHelper.cs`. |
| **TC-DEL-03** | | ENV-A | Cancel aborts; Confirm permanently deletes file | | `NOT TESTED` | Modal confirmation gate. |
| **TC-DEL-04** | | ENV-A | Read-only file deleted without unhandled exception | | `NOT TESTED` | Attribute stripping / error reporting. |
| **TC-UI-RESP-01** | | ENV-A | Window remains fluid during 10K folder deletion | | `NOT TESTED` | Previously observed: UI thread block in `LargestFoldersViewModel.cs`. |
| **TC-UI-RESP-02** | | ENV-A | Batch file deletion does not freeze UI thread | | `NOT TESTED` | Previously observed: Synchronous deletion in `LargestFilesViewModel.cs`. |
| **TC-USN-01** | | ENV-A | Incremental scan safe; zero access violations | | `NOT TESTED` | Previously observed: Raw pointer arithmetic in `UsnJournalService.cs`. |
| **TC-USN-02** | | ENV-A | Non-NTFS drive falls back to full BFS cleanly | | `NOT TESTED` | `IsNtfsVolume` check before query. |
| **TC-USN-03** | | ENV-A | Journal ID mismatch triggers safe full rescan | | `NOT TESTED` | Journal reset fallback branch. |
| **TC-USN-04** | | ENV-A | Standard non-admin user falls back cleanly | | `NOT TESTED` | Catches `ERROR_ACCESS_DENIED`. |
| **TC-SCN-01** | | ENV-A | Scanner cancels within 500ms when requested | | `NOT TESTED` | Bounded-channel cancellation token. |
| **TC-SCN-02** | | ENV-A | Inaccessible folders skipped and logged | | `NOT TESTED` | `UnauthorizedAccessException` handling. |
| **TC-SCN-03** | | ENV-A | Paths > 260 chars indexed without exception | | `NOT TESTED` | Long path support. |
| **TC-SCN-04** | | ENV-A | Live directory feed does not flood UI thread | | `NOT TESTED` | Dispatcher throttling verification. |
| **TC-SCN-05** | | ENV-A | OneDrive placeholders skipped without hydration | | `NOT TESTED` | Reparse point attribute handling. |
| **TC-ROL-01** | | ENV-A | Folder rollup size equals sum of all children | | `NOT TESTED` | Recursive mathematical accuracy. |
| **TC-ROL-02** | | ENV-A | 100K dir rollup completes < 5s; LOH RAM < 150MB | | `NOT TESTED` | GC Gen 2 and LOH allocation benchmark. |
| **TC-DUP-01** | | ENV-A | 2 true duplicates found; 3 collisions rejected | | `NOT TESTED` | 3-stage pipeline (Size -> MD5 -> SHA-256). |
| **TC-DUP-02** | | ENV-A | 0-byte and locked files handled cleanly | | `NOT TESTED` | Empty file exclusion before hashing. |
| **TC-DUP-03** | | ENV-A | Master copy preserved; only duplicates deleted | | `NOT TESTED` | Safe selection retention model. |
| **TC-DEV-01** | | ENV-A | Node/.NET/Rust/Gradle detected; generic rejected | | `NOT TESTED` | Contextual parent marker verification. |
| **TC-SAF-01** | | ENV-A | In-use files skipped; unlocked files deleted | | `NOT TESTED` | Per-file try/catch with skip accounting. |
| **TC-PS-01** | | ENV-A | PSD/PSB cataloged; temp files flagged | | `NOT TESTED` | Photoshop intelligence cataloging. |
| **TC-TMP-01** | | ENV-A | Treemap renders valid rects; no NaN/Infinity | | `NOT TESTED` | Squarified geometry boundary checks. |
| **TC-TMP-02** | | ENV-A | Window resize recomputes treemap cleanly | | `NOT TESTED` | WPF size changed event binding. |
| **TC-TMP-03** | | ENV-A | Double-click drills down; breadcrumbs navigate | | `NOT TESTED` | Navigation stack synchronization. |
| **TC-DB-01** | | ENV-A | Concurrent UI reads succeed during active scan | | `NOT TESTED` | SQLite WAL mode lock testing. |
| **TC-DB-02** | | ENV-A | WAL replays cleanly after abrupt process kill | | `NOT TESTED` | Database crash atomicity. |
| **TC-DB-03** | | ENV-A | PRAGMA integrity_check returns ok | | `NOT TESTED` | Schema and index verification. |
| **TC-ANL-01** | | ENV-A | Overview total size matches file sum exactly | | `NOT TESTED` | Exact numerical accounting. |
| **TC-QRY-01** | | ENV-A | Multi-criteria filters & pagination work cleanly | | `NOT TESTED` | Parameterized SQLite query engine. |
| **TC-EXP-01** | | ENV-A | HTML, JSON, and CSV exports validate cleanly | | `NOT TESTED` | File export integrity. |
| **TC-LGL-01** | | ENV-A | EULA gate blocks app until accepted | | `NOT TESTED` | Legal compliance dialog. |
| **TC-SET-01** | | ENV-A | Excluded paths completely skipped by scanner | | `NOT TESTED` | Settings persistence and traversal filter. |
| **TC-ERR-01** | | ENV-A | Global unhandled exception caught and logged | | `NOT TESTED` | AppDomain and Dispatcher exception hooks. |
| **TC-INS-01** | | ENV-D | Fresh install succeeds for standard non-admin | | `NOT TESTED` | Inno Setup `PrivilegesRequired=lowest`. |
| **TC-INS-02** | | ENV-D | EULA rejection halts installation cleanly | | `NOT TESTED` | Mandatory license acceptance gate. |
| **TC-INS-03** | | ENV-D | Desktop & Start Menu shortcuts point to valid app | | `NOT TESTED` | Shortcut target and icon verification. |
| **TC-INS-04** | | ENV-D | Installed Apps URLs point to ArborGraph repo | | `NOT TESTED` | Previously observed: Outdated URL in `installer.iss`. |
| **TC-INS-05** | | ENV-D | In-place upgrade retains database and settings | | `NOT TESTED` | AppId stability and user data retention. |
| **TC-INS-06** | | ENV-D | Uninstall removes all deployed binaries cleanly | | `NOT TESTED` | Registry and file purge validation. |
| **TC-INS-07** | | ENV-D | Silent install (/VERYSILENT) exits with code 0 | | `NOT TESTED` | Headless scripted deployment. |
| **TC-WEB-01** | | Web | Responsive site renders without console errors | | `NOT TESTED` | Cross-browser rendering and layout. |
| **TC-WEB-02** | | Web | Canvas sparkline animation runs smoothly | | `NOT TESTED` | Client-side simulation fidelity. |
| **TC-WEB-03** | | Web | Web Crypto SHA-256 matches binary hash | | `NOT TESTED` | Browser Web Crypto API verification. |
| **TC-USB-01** | | ENV-A | User initiates scan in < 20s without help | | `NOT TESTED` | Usability Task 1. |
| **TC-USB-02** | | ENV-A | User identifies largest folder in < 30s | | `NOT TESTED` | Usability Task 2. |
| **TC-USB-03** | | ENV-A | User identifies largest file in < 20s | | `NOT TESTED` | Usability Task 3. |
| **TC-USB-04** | | ENV-A | User understands treemap and drills down | | `NOT TESTED` | Usability Task 4. |
| **TC-USB-05** | | ENV-A | User understands duplicate copy retention | | `NOT TESTED` | Usability Task 5. |
| **TC-USB-06** | | ENV-A | User distinguishes safe cleanup from risky files | | `NOT TESTED` | Usability Task 6. |
| **TC-CMP-01** | | ENV-B | Clean vector rendering across 100%–200% DPI | | `NOT TESTED` | High-DPI display scaling matrix. |
| **TC-CMP-02** | | ENV-A | Removable exFAT USB scans without error | | `NOT TESTED` | Cross-filesystem compatibility. |
| **TC-PERF-01** | | ENV-A | Throughput >= 15k/s; RAM < 350MB on 100K files | | `NOT TESTED` | Multi-tier performance benchmark. |
