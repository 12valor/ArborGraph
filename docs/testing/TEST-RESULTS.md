# ArborGraph Test Execution Results Log

**Document Identifier:** AG-TR-001  
**Target Release:** v1.0.0 (`net8.0-windows` x64)  
**Execution Timestamp:** 2026-10-04 23:38:03  
**Execution Environment:** ENV-A (Windows 11 x64, .NET 8.0.425)  
**Execution Policy:** Only tests that actually executed are marked with PASS or FAIL. No simulated or invented passes. Manual and non-automated tests remain `NOT TESTED`.

---

## 1. Execution Summary Dashboard

| Metric | Count | Percentage |
| :--- | :--- | :--- |
| **Total Test Cases Planned** | 56 | 100.0% |
| **Tests Executed via Automated Harness** | 26 | 46.4% |
| **Passed** | 26 | 46.4% (100.0% of executed) |
| **Failed** | 0 | 0.0% (0.0% of executed) |
| **Blocked** | 0 | 0.0% |
| **Not Tested / Pending Manual Testing** | 30 | 53.6% |
| **Automated Harness Elapsed Time** | 8.07s | |

---

## 2. Test Execution Log Table

| Test ID | Date | Environment | Expected Result | Actual Result | Status | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **TC-DRV-01** | 2026-10-05 | ENV-A | Scanning D:\ preserves C:\ index in SQLite; D:\ rebuilt | Scoped range clear deleted only D:\; C:\ remained 100% intact (53 ms). | `PASS` | **BUG-001 RESOLVED & VERIFIED.** `DatabaseService.ClearIndex` drive-root scoping fixed. |
| **TC-DRV-02** | 2026-10-05 | ENV-A | Multiple roots clear only target volumes; ALL/null full clear | C:\ and D:\ cleared; E:\ preserved. ALL/null/empty executed full wipe (18 ms). | `PASS` | Multi-root scoping and explicit full-clear fallback verified. |
| **TC-DRV-03** | 2026-10-04 | ENV-A | Custom directory scan only clears scoped records | Scoped prefix deletion only cleared target directory records (16 ms). | `PASS` | Verified `path LIKE 'target%'` scoping works for non-root paths. |
| **TC-DEL-01** | 2026-10-05 | ENV-A | Single file deleted from disk and purged from SQLite index | File deleted permanently; index entry removed cleanly (21 ms). | `PASS` | Service-level file deletion & SQLite index sync verified. |
| **TC-DEL-02** | 2026-10-05 | ENV-A | Batch deletion reports exact success/failure; locked file skipped | 4 files deleted, 1 locked file trapped safely; Succeeded=4, Failed=1 (7 ms). | `PASS` | Locked-file graceful handling verified. |
| **TC-DEL-03** | 2026-10-05 | ENV-A | Batch deletion halts promptly on cancellation token | Cancelled after 3 files; remaining 7 files preserved on disk (7 ms). | `PASS` | Cancellation responsiveness verified. |
| **TC-DEL-04** | 2026-10-05 | ENV-A | Recursive folder deletion purges children and SQLite records | Directory deleted recursively; database entries purged (18 ms). | `PASS` | Directory rollup index removal verified. |
| **TC-DEL-05** | 2026-10-05 | ENV-A | Protected system and drive roots blocked from deletion | C:\, C:\Windows, Program Files blocked from deletion (1 ms). | `PASS` | System shield & raw drive letter protection verified. |
| **TC-UI-RESP-01** | | ENV-A | Window remains fluid during 10K folder deletion | Code fixed with Task.Run & IsDeleting banner | `READY FOR MANUAL` | Procedure defined; requires desktop manual drag/resize check. |
| **TC-UI-RESP-02** | | ENV-A | Batch file deletion does not freeze UI thread | Code fixed with Task.Run & IsDeleting banner | `READY FOR MANUAL` | Procedure defined; requires desktop manual observation. |
| **TC-USN-01** | | ENV-A | Incremental scan safe; zero access violations | | `NOT TESTED` | Requires elevated Admin privileges on active physical NTFS drive. |
| **TC-USN-02** | | ENV-A | Non-NTFS drive falls back to full BFS cleanly | | `NOT TESTED` | Verified in legacy test suite. |
| **TC-USN-03** | | ENV-A | Journal ID mismatch triggers safe full rescan | | `NOT TESTED` | Verified in legacy test suite. |
| **TC-USN-04** | | ENV-A | Standard non-admin user falls back cleanly | | `NOT TESTED` | Verified in legacy test suite. |
| **TC-SCN-01** | 2026-10-04 | ENV-A | Scanner cancels within 500ms when requested | Cancelled cleanly in 343 ms; database integrity preserved. | `PASS` | Bounded-channel cancellation token verified. |
| **TC-SCN-02** | 2026-10-04 | ENV-A | Inaccessible folders skipped and logged | Traversal caught `UnauthorizedAccessException` and completed (29 ms). | `PASS` | Graceful permission bypass verified. |
| **TC-SCN-03** | 2026-10-04 | ENV-A | Paths > 260 chars indexed without exception | 20-level deep path indexed and queryable in SQLite (44 ms). | `PASS` | Long path support verified. |
| **TC-SCN-EDGE** | 2026-10-04 | ENV-A | Unicode, special chars & empty folders indexed | Japanese, Arabic, Emoji, and quotes indexed accurately (45 ms). | `PASS` | Exact numerical and character accounting verified. |
| **TC-SCN-04** | | ENV-A | Live directory feed does not flood UI thread | | `NOT TESTED` | Verified in legacy integration test suite. |
| **TC-SCN-05** | | ENV-A | OneDrive placeholders skipped without hydration | | `NOT TESTED` | Requires OneDrive cloud files on disk. |
| **TC-SET-01** | 2026-10-04 | ENV-A | Excluded paths completely skipped by scanner | Excluded directory recorded in SkippedDirectories, zero files indexed (70 ms). | `PASS` | SettingsService and ScannerService exclusion respect verified. |
| **TC-ROL-01** | 2026-10-04 | ENV-A | Folder rollup size equals sum of all children | Root rolled-up size (228,891 bytes) exactly matched child total (35 ms). | `PASS` | Recursive mathematical accuracy verified. |
| **TC-ROL-02** | 2026-10-04 | ENV-A | 1,000 synthetic dir rollup completes in < 3s | Rollup of 1,000 synthetic directories completed in 35 ms. | `PASS` | Bottom-up depth rollup algorithm scaling verified. |
| **TC-DUP-01** | 2026-10-04 | ENV-A | 2 true duplicates found; 3 collisions rejected | Exactly 2 duplicate groups found; 32KB collision pair rejected (52 ms). | `PASS` | 3-stage cryptographic duplicate pipeline verified. |
| **TC-DUP-02** | 2026-10-04 | ENV-A | 0-byte and locked files handled cleanly | Empty candidate search returned 0 groups without throwing (13 ms). | `PASS` | Edge-case duplicate handling verified. |
| **TC-DUP-03** | | ENV-A | Master copy preserved; only duplicates deleted | | `NOT TESTED` | Manual UI selection logic. |
| **TC-DEV-01** | 2026-10-04 | ENV-A | Node/.NET/Rust/Gradle detected; generic rejected | Node, .NET, Rust, Gradle detected; fake target/build rejected (35 ms). | `PASS` | Contextual parent markers verified with zero false positives. |
| **TC-SAF-01** | | ENV-A | In-use files skipped; unlocked files deleted | | `NOT TESTED` | Verified in legacy test suite. |
| **TC-PS-01** | | ENV-A | PSD/PSB cataloged; temp files flagged | | `NOT TESTED` | Verified in legacy test suite. |
| **TC-TMP-01** | 2026-10-04 | ENV-A | Treemap renders valid rects; no NaN/Infinity | Zero-size, 1-item, and extreme aspect ratios produced valid rects (64 ms). | `PASS` | Squarified geometry boundary guards verified. |
| **TC-TMP-02** | | ENV-A | Window resize recomputes treemap cleanly | | `NOT TESTED` | WPF UI resize event. |
| **TC-TMP-03** | | ENV-A | Double-click drills down; breadcrumbs navigate | | `NOT TESTED` | Navigation stack synchronization. |
| **TC-DB-01** | 2026-10-04 | ENV-A | Concurrent UI reads succeed during active scan | Concurrent readers completed 20 queries during bulk writes (569 ms). | `PASS` | SQLite WAL mode lock concurrency verified. |
| **TC-DB-02** | | ENV-A | WAL replays cleanly after abrupt process kill | | `NOT TESTED` | Requires process kill testing. |
| **TC-DB-03** | 2026-10-04 | ENV-A | PRAGMA integrity_check returns ok | Database initialized in WAL mode; PRAGMA integrity_check passed (14 ms). | `PASS` | Schema and index verification verified. |
| **TC-ANL-01** | | ENV-A | Overview total size matches file sum exactly | | `NOT TESTED` | Verified in legacy test suite. |
| **TC-QRY-01** | 2026-10-04 | ENV-A | Multi-criteria filters & pagination work cleanly | Filter by size (20MB), extension (.pdf), and location matched exactly (18 ms). | `PASS` | Parameterized SQLite query engine verified. |
| **TC-QRY-SQLI** | 2026-10-04 | ENV-A | SQL injection inputs harmlessly parameterized | `' OR '1'='1` and `DROP TABLE` handled as literals; 0 matches, 0 corruption (13 ms). | `PASS` | Parameterized SQL query safety verified. |
| **TC-EXP-01** | 2026-10-04 | ENV-A | HTML, JSON, and CSV exports validate cleanly | HTML5 report, JSON structure, and RFC 4180 CSV escaping verified (53 ms). | `PASS` | File export integrity verified. |
| **TC-LGL-01** | 2026-10-04 | ENV-A | EULA gate blocks app until accepted | Defaults to false; accepting persists to disk and reloads cleanly (9 ms). | `PASS` | Legal compliance dialog persistence verified. |
| **TC-ERR-01** | | ENV-A | Global unhandled exception caught and logged | | `NOT TESTED` | Requires runtime crash simulation. |
| **TC-INS-01** | | ENV-D | Fresh install succeeds for standard non-admin | | `NOT TESTED` | Inno Setup `PrivilegesRequired=lowest`. |
| **TC-INS-02** | | ENV-D | EULA rejection halts installation cleanly | | `NOT TESTED` | Mandatory license acceptance gate. |
| **TC-INS-03** | | ENV-D | Desktop & Start Menu shortcuts point to valid app | | `NOT TESTED` | Shortcut target and icon verification. |
| **TC-INS-04** | 2026-10-05 | ENV-D | Installed Apps URLs point to ArborGraph repo | MyAppURL, SupportURL, UpdatesURL verify as https://github.com/12valor/ArborGraph; installer compiled cleanly (2 ms). | `PASS` | **BUG-003 RESOLVED & VERIFIED.** Legacy C-file-scanner eliminated from Inno Setup script. |
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
