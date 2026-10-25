# ArborGraph v1.0.0 — Master QA Test Plan, Test Cases & Release Quality Gates Package

**Document Identifier:** AG-MQA-001  
**Target Product:** ArborGraph (Filesystem Analytics & Visualization)  
**Target Release:** v1.0.0  
**Target Platform:** Windows 10 / Windows 11 (64-bit x64, `net8.0-windows`)  
**Methodological Alignment:**  
- Quality Characteristics: Informed by ISO/IEC 25010:2023 product quality model  
- Testing Process & Documentation: Informed by ISO/IEC/IEEE 29119 testing standards  
*(Note: ArborGraph is an independent open-source project; these standards serve as professional quality frameworks and do not claim formal accredited certification).*  
**Execution Status:** Ready for Execution (All tests initialized to `NOT TESTED`)

---

# TABLE OF CONTENTS
1. [Executive Summary & Quality Objectives](#1-executive-summary--quality-objectives)
2. [Scope of Testing](#2-scope-of-testing)
3. [Prioritized Risk Tiers & Audit Hazards](#3-prioritized-risk-tiers--audit-hazards)
4. [Test Environments & Hardware Profiles](#4-test-environments--hardware-profiles)
5. [Controlled Test Datasets (DATA-A through DATA-S)](#5-controlled-test-datasets-data-a-through-data-s)
6. [Performance, Scalability & Compatibility Strategy](#6-performance-scalability--compatibility-strategy)
7. [Test Automation Strategy](#7-test-automation-strategy)
8. [Feature Inventory & Traceability Matrix (44 Features)](#8-feature-inventory--traceability-matrix-44-features)
9. [Comprehensive Executable Test Cases](#9-comprehensive-executable-test-cases)
   - 9.1 Multi-Drive Data Safety & Isolation (P0 / BLOCKER)
   - 9.2 Deletion Safety & Destructive Operations (P0 / P1)
   - 9.3 UI Responsiveness During Heavy Deletions (P0 / P1)
   - 9.4 NTFS USN Change Journal & Native Memory Safety (P0 / P1)
   - 9.5 Core BFS Scanner & Traversal Edge Cases (P1)
   - 9.6 Large-Scale Directory Rollup & LOH Memory Scaling (P1)
   - 9.7 Cryptographic Duplicate Detection & Collisions (P1)
   - 9.8 Contextual Developer, Junk & Cache Cleaning (P1)
   - 9.9 Interactive Squarified Treemap (P1)
   - 9.10 Database Concurrency & Lock Contention (P1)
   - 9.11 Analytics, Query & Reporting Accuracy (P1)
   - 9.12 Settings, Legal Gate & Error Handling (P1)
   - 9.13 Inno Setup 6 Desktop Installer & Deployment (P0 / P1)
   - 9.14 Web Portal & Diagnostic Simulation (P2)
   - 9.15 Human First-Time User Usability Testing (P1 / P2)
   - 9.16 Platform, Display Scaling & Compatibility (P1 / P2)
   - 9.17 Multi-Tier Performance Benchmarking (P1)
10. [Test Execution Results Log](#10-test-execution-results-log)
11. [Defect & Bug Tracking Log](#11-defect--bug-tracking-log)
12. [Release Verification & Quality Gates Checklist](#12-release-verification--quality-gates-checklist)

---

# 1. Executive Summary & Quality Objectives

This Master QA Package consolidates the complete software quality engineering strategy for **ArborGraph v1.0.0**, a high-performance Windows desktop disk space analyzer and visualization tool built on .NET 8 WPF, SQLite WAL mode, and Win32 P/Invoke filesystem APIs.

### Core Quality Objectives:
1. **Zero Data Loss:** Absolute guarantee that safe deletion, Recycle Bin operations, contextual developer cache cleaning, and multi-drive scans never corrupt, misplace, or unintendedly destroy user data.
2. **Absolute Numerical Accuracy:** Full statistical accounting of discovered vs. indexed files, mathematically exact recursive folder rollup, and zero false positives in cryptographic deduplication.
3. **Execution Stability:** Clean degradation under constrained permissions (standard non-admin users), unhandled exception interception, robust locked-file bypass, and resilient USN Journal fallback.
4. **UI Fluidity:** Decoupled asynchronous architecture preventing UI thread starvation during heavy directory traversal or large-scale file purges.

---

# 2. Scope of Testing

### In-Scope:
- **Desktop Application:** Complete functionality across all 14 WPF Views and ViewModels (Overview, Scanner, Largest Files, Largest Folders, File Categories, Old Files, Duplicate Files, Developer Storage, Junk Cleaner, Treemap, Query Explorer, Cleanup Center, Scan History, Settings).
- **Scan Engines:** Bounded-channel BFS traversal engine (`ScannerService.cs`) and NTFS USN Change Journal incremental traversal (`UsnJournalService.cs`).
- **Database Subsystem:** SQLite WAL mode persistence, batch inserts, multi-drive index scoping, and query filtering (`DatabaseService.cs`).
- **Installer & Deployment:** Inno Setup 6 packaging (`installer/installer.iss`), fresh install, upgrade, uninstall, and shortcut generation.
- **Web Portal:** Official site (`site/index.html`, `site/app.js`, `site/style.css`), live canvas simulations, and Web Crypto SHA-256 verification.
- **Traceability:** 100% coverage of all 44 features identified in the audit (`FEAT-01` through `FEAT-44`).

### Out-of-Scope:
- macOS, Linux, or Wine/Proton compatibility (ArborGraph is strictly native Windows WPF).
- Network protocol scanning beyond standard mounted SMB/UNC paths.

---

# 3. Prioritized Risk Tiers & Audit Hazards

| Priority Tier | Definition | Release Action |
| :--- | :--- | :--- |
| **P0 / BLOCKER** | Critical defects causing data loss, incorrect multi-drive index wiping, unhandled application crashes, unmanaged memory violations, or installer deployment failures. | **Must be 0.** Halts release immediately. |
| **P1 / CRITICAL** | Major functional failures, severe memory scaling bottlenecks, UI thread starvation (>2s), or inaccurate deduplication/rollup math. | **Must be 0.** Must be resolved before v1.0.0 sign-off. |
| **P2 / MAJOR** | Non-crashing functional errors with existing workarounds, minor export formatting bugs, or non-critical permission handling flaws. | Max allowable: 3, with documented release notes. |
| **P3 / MINOR** | Cosmetic UI glitches, minor typography discrepancies, or minor documentation inaccuracies. | Permitted for post-release patches. |

### Top 4 Release-Blocking Audit Hazards (Pre-Populated for Testing):
1. **Multi-Drive `ClearIndex` Scope Bug (`BUG-001` / `TC-DRV-01`):** In `DatabaseService.cs` lines 185–198, root paths (`C:`) evaluate `isFullClear = true` (`trimmed.Length <= 2 && trimmed.EndsWith(":")`), issuing `DELETE FROM files; DELETE FROM directories;` and wiping previously indexed drives when scanning a second drive.
2. **Synchronous Deletion on UI Thread (`BUG-002` / `TC-UI-RESP-01`):** In `LargestFoldersViewModel.cs` line 89 and `LargestFilesViewModel.cs` line 272, file/folder deletions run synchronously on the UI dispatcher, causing the window to freeze ("Not Responding") during deep directory purges.
3. **Inno Setup Repository URL Drift (`BUG-003` / `TC-INS-04`):** `installer/installer.iss` line 9 hardcodes `https://github.com/12valor/C-file-scanner` instead of `https://github.com/12valor/ArborGraph`.
4. **USN Journal Native Pointer Bounds Risk (`BUG-004` / `TC-USN-01`):** `UsnJournalService.cs` lines 230–245 performs manual pointer advance (`Marshal.PtrToStructure`, `Marshal.ReadInt32`) requiring bounds checks to prevent access violations on fragmented disks.

---

# 4. Test Environments & Hardware Profiles

| Environment ID | Platform & Hardware Specifications | Role & Target Focus | Status |
| :--- | :--- | :--- | :--- |
| **ENV-A** | **Developer Reference PC:** Windows 11 (24H2), AMD Ryzen 7 / Intel Core i7, 32 GB RAM, NVMe PCIe 4.0 SSD | High-throughput baseline, unit & integration tests, rapid regression | Available |
| **ENV-B** | **Mid-Range Commercial PC:** Windows 10 (22H2), Intel Core i5 (8th–11th Gen), 16 GB RAM, SATA SSD | Standard enterprise/consumer target, Win10 theme rendering | Planned |
| **ENV-C** | **Resource-Constrained Laptop:** Windows 10/11, Intel Core i3, 8 GB RAM, 5400 RPM Mechanical HDD | High disk latency, LOH rollup memory scaling, low-spec responsiveness | Planned |
| **ENV-D** | **Clean Virtual Machine:** Windows 11 Home, fresh OS install, zero dev tools/.NET SDKs | Clean install, missing dependency validation, lowest-privilege test | Planned |
| **ENV-E** | **macOS / Linux (Wine/Proton):** Non-Windows systems | Out of scope (Strictly Windows native) | N/A |

---

# 5. Controlled Test Datasets (DATA-A through DATA-S)

| Dataset ID | Name | Approx Files | Dirs | Logical Size | Purpose & Reference Criteria |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **DATA-A** | Small Filesystem | 13 files | 5 dirs | 228,891 bytes | Exact accounting sanity check; 1 duplicate group (65 KB), PSD/PSB files. |
| **DATA-B** | Medium Filesystem | 10,000 files | 500 dirs | ~2.00 GB | Query sorting, pagination, and multi-criteria filter accuracy. |
| **DATA-C** | Large Filesystem | 100,000 files | 5,000 dirs | ~45.0 GB | Scale benchmark: Throughput >= 15k files/s; rollup time < 5s; RAM < 350MB. |
| **DATA-D** | Many Small Files | 50,000 files | 500 dirs | 50 MB | High IOPS; batch insertions of 5,000 persist without channel stalls. |
| **DATA-E** | Large Files | 5 files | 2 dirs | ~69.8 GB | 64-bit byte counters validation; largest files list sorting. |
| **DATA-F** | Deep Directory Trees | 100 files | 30 levels | ~1.0 MB | Paths exceeding 260 characters (`MAX_PATH`); no `PathTooLongException`. |
| **DATA-G** | Cryptographic Duplicates | 12 files | 3 dirs | 4.0 MB | 2 true duplicate groups (64KB, 1MB); 3 size-collision pairs rejected. |
| **DATA-H** | Unicode Filenames | 20 files | 5 dirs | 200 KB | International characters (Japanese, Arabic, Cyrillic, Emoji); zero mojibake. |
| **DATA-I** | Special Characters | 25 files | 4 dirs | ~146 KB | Paths with `#`, `%`, `&`, `[`, `]`, `'`, spaces; SQL safety validation. |
| **DATA-J** | Empty Folders | 0 files | 50 dirs | 0 bytes | Visited dirs = 50, indexed = 0, bytes = 0; no null ref exceptions. |
| **DATA-K** | Inaccessible Folders | 10 files | 4 dirs | Known bytes | Permission denial graceful bypass (`UnauthorizedAccessException`). |
| **DATA-L** | Locked Files | 6 files | 2 dirs | 120 KB | Locked with `FileShare.None`; cleaner skips locked and deletes unlocked. |
| **DATA-M** | Volatile Filesystem | 500 files | 10 dirs | Dynamic | Files modified/deleted during BFS scan; safe `FileNotFoundException` bypass. |
| **DATA-N** | Developer Projects | 1,500 files | 80 dirs | ~114 MB | Node.js, .NET, Rust detected; generic target/build folders rejected. |
| **DATA-O** | Creative Media Assets | 35 files | 6 dirs | ~1.4 GB | PSD, PSB, ABR assets; Photoshop Temp files flagged as reclaimable. |
| **DATA-P** | Multi-Drive Targets | 2,000 files | 40 dirs | ~476 MB | Dual-drive test: Scanning `D:\` preserves `C:\` data in SQLite. |
| **DATA-Q** | NTFS Volume | Real drive | Multi-dir | Arbitrary | USN journal query, checkpoint persistence, sub-second incremental scan. |
| **DATA-R** | Non-NTFS (exFAT/FAT32) | USB drive | Multi-dir | Arbitrary | `IsNtfsVolume` returns false; seamless fallback to full BFS scan. |
| **DATA-S** | Cloud Placeholders | 50 files | 5 dirs | 10.0 GB | `RecallOnDataAccess` attributes skipped; zero network bytes downloaded. |

---

# 6. Performance, Scalability & Compatibility Strategy

### Performance Benchmarks
- Benchmark across **10K, 50K, 100K, 250K, 500K, and 1M+** file tiers.
- Run each tier **3 times**; record Minimum, Maximum, and Average throughput.
- Target Throughput: $\ge 15,000$ files/sec on NVMe SSD.
- Working Set RAM Ceilings: $\le 350\text{ MB}$ under 100K files; $\le 650\text{ MB}$ under 500K files.
- `BuildDirectoryRollup`: $< 5.0$ seconds for 100,000 directories with minimal Gen 2 LOH pressure.

### Compatibility Strategy
- **Display DPI Scaling:** Test at 100% (96 DPI), 125% (120 DPI), 150% (144 DPI), 175% (168 DPI), and 200% (192 DPI).
- **Filesystems & Media:** Test NVMe, SATA SSD, mechanical HDD, USB flash drive (exFAT/FAT32), and BitLocker volumes.
- **Cloud Placeholders:** Ensure OneDrive `RecallOnDataAccess` reparse points prevent automated network downloads.

---

# 7. Test Automation Strategy

| Test Domain | Automation Feasibility | Recommended Framework | Focus |
| :--- | :--- | :--- | :--- |
| **Traversal & Database** | 100% Automatable | .NET Integration Harness (`tests/DiskScope.Tests.csproj`) | Deterministic SQLite checks, batch commits, path traversal. |
| **Deduplication & Rollup** | 100% Automatable | .NET Unit Tests / xUnit | 3-stage hashing, collision rejection, parent size rollup. |
| **Query Engine & Exports** | 100% Automatable | .NET Integration Harness | SQL generation, pagination offsets, CSV/JSON/HTML export. |
| **Web Portal & Verification** | 90% Automatable | Playwright / Headless Chromium | Canvas sparklines, responsive CSS, Web Crypto SHA-256. |
| **Shell Context Menus** | Manual Only | Manual User Testing | Windows Explorer Shell COM context menu actions. |
| **High-DPI Display Scaling** | Manual Only | Multi-Monitor Walkthrough | Visual inspection of text clipping, border rendering. |
| **Inno Setup Installer** | Hybrid | PowerShell Silent CLI + Manual Wizard | Headless exit codes + GUI EULA consent. |
| **Usability & UX Fluidity** | Manual Only | Think-Aloud User Observation | Human task completion, friction, and SUS scorecard. |

---

# 8. Feature Inventory & Traceability Matrix (44 Features)

| Feature ID | Feature Name | Core Test Cases | Priority | Risk | Test Method |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **FEAT-01** | System Drive Auto-Discovery | `TC-DRV-01`, `TC-DRV-02` | High | Low | Automated |
| **FEAT-02** | Custom Directory Target Selection | `TC-DRV-03` | Medium | Medium | Automated / Manual |
| **FEAT-03** | Bounded-Channel BFS Traversal | `TC-SCN-01`, `TC-SCN-02`, `TC-SCN-03` | Critical | Critical | Automated |
| **FEAT-04** | NTFS USN Change Journal Scanner | `TC-USN-01`, `TC-USN-02`, `TC-USN-03`, `TC-USN-04` | High | Critical | Automated / Manual |
| **FEAT-05** | Real-Time Resource Telemetry | `TC-MON-01` | Medium | Medium | Automated |
| **FEAT-06** | Realtime Metric Graph Rendering | `TC-MON-02` | Medium | Medium | Automated / Manual |
| **FEAT-07** | Live Directory Rolling Feed | `TC-SCN-04` | Medium | Low | Automated |
| **FEAT-08** | SQLite Database Storage & WAL Mode | `TC-DB-01`, `TC-DB-02` | Critical | Critical | Automated |
| **FEAT-09** | Database Integrity Verification | `TC-DB-03` | High | High | Automated |
| **FEAT-10** | Recursive Folder Rollup | `TC-ROL-01`, `TC-ROL-02` | Critical | Critical | Automated |
| **FEAT-11** | Storage Explanation Narrative | `TC-ANL-01` | Low | Low | Automated |
| **FEAT-12** | Hierarchical Folder Drilldown | `TC-ANL-02` | Medium | Medium | Automated / Manual |
| **FEAT-13** | Storage Growth Polyline Chart | `TC-ANL-03` | Medium | Medium | Automated / Manual |
| **FEAT-14** | Scan Session Tracking & Comparison | `TC-ANL-04` | High | High | Automated |
| **FEAT-15** | File Age Temporal Breakdown | `TC-ANL-05` | High | High | Automated |
| **FEAT-16** | Multi-Criteria Storage Query | `TC-QRY-01`, `TC-QRY-02` | Critical | Critical | Automated |
| **FEAT-17** | Streaming Multi-Criteria CSV Export | `TC-EXP-01` | High | High | Automated |
| **FEAT-18** | Largest Files Paged Explorer | `TC-FIL-01` | High | High | Automated / Manual |
| **FEAT-19** | Rolled-Up Largest Folders Explorer | `TC-FOL-01` | High | High | Automated / Manual |
| **FEAT-20** | File Category Breakdown & Viewer | `TC-CAT-01` | Medium | Medium | Automated |
| **FEAT-21** | Old & Dormant Files Explorer | `TC-OLD-01` | Medium | Medium | Automated |
| **FEAT-22** | Cryptographic Duplicate Detection | `TC-DUP-01`, `TC-DUP-02` | Critical | Critical | Automated |
| **FEAT-23** | Duplicate Management & Elimination | `TC-DUP-03` | High | High | Automated / Manual |
| **FEAT-24** | Contextual Developer Storage Discovery | `TC-DEV-01` | High | High | Automated |
| **FEAT-25** | Developer Workspace Scanner | `TC-DEV-02` | High | High | Automated / Manual |
| **FEAT-26** | Junk & Cache Target Scanner | `TC-JNK-01` | High | High | Automated |
| **FEAT-27** | Locked-File Safe Deletion Fallback | `TC-SAF-01` | Critical | Critical | Automated |
| **FEAT-28** | Photoshop & Media Asset Intelligence | `TC-PS-01` | Medium | Medium | Automated |
| **FEAT-29** | Adobe Scratch & Temp Cache Reclaimer | `TC-PS-02` | High | High | Automated / Manual |
| **FEAT-30** | Squarified Treemap Layout Engine | `TC-TMP-01`, `TC-TMP-02` | High | High | Automated |
| **FEAT-31** | Interactive Treemap Node Drilldown | `TC-TMP-03` | Medium | Medium | Manual |
| **FEAT-32** | Consolidated Storage Cleanup Center | `TC-CLN-01` | High | High | Automated / Manual |
| **FEAT-33** | Executive HTML Storage Audit Export | `TC-EXP-02` | High | High | Automated |
| **FEAT-34** | Machine-Readable JSON Audit Export | `TC-EXP-03` | Medium | Medium | Automated |
| **FEAT-35** | Tabular CSV Exporters | `TC-EXP-04` | Medium | Medium | Automated |
| **FEAT-36** | Shell Integration & Explorer Actions | `TC-SHL-01` | High | High | Manual |
| **FEAT-37** | Windows Recycle Bin Deletion | `TC-DEL-01`, `TC-DEL-02` | Critical | Critical | Manual / Automated |
| **FEAT-38** | Permanent Deletion with Confirmation | `TC-DEL-03`, `TC-DEL-04` | Critical | Critical | Manual / Automated |
| **FEAT-39** | First-Run EULA Consent Dialog Gate | `TC-LGL-01` | Critical | Critical | Automated / Manual |
| **FEAT-40** | Settings & Exclusion Rules Persistence | `TC-SET-01` | High | High | Automated |
| **FEAT-41** | Diagnostic Scan Log Tracking | `TC-LOG-01` | Low | Low | Automated |
| **FEAT-42** | Global Unhandled Exception Handlers | `TC-ERR-01` | Critical | Critical | Manual / Automated |
| **FEAT-43** | Inno Setup 6 Desktop Installer | `TC-INS-01` to `TC-INS-07` | Critical | Critical | Manual |
| **FEAT-44** | Web Portal & Diagnostic Simulation | `TC-WEB-01` to `TC-WEB-03` | Medium | Medium | Manual |

---

# 9. Comprehensive Executable Test Cases

### 9.1 Multi-Drive Data Safety & Isolation (P0 / BLOCKER)

#### Test ID: `TC-DRV-01`
- **Feature ID:** `FEAT-08`, `FEAT-01`
- **Title:** Multi-Drive Sequential Scan Data Isolation (ClearIndex Scoping Bug Verification)
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Code in `DatabaseService.cs` treats root paths (`trimmed.Length <= 2 && trimmed.EndsWith(":")`) as `isFullClear = true`, issuing `DELETE FROM files; DELETE FROM directories;` and wiping previously indexed drives when scanning a second drive root.
- **Preconditions:** Host system has at least two accessible logical drives (`C:\` and `D:\` or virtual mounted drives).
- **Test Data:** Dataset P (Multi-drive mock trees with distinct marker files on each volume).
- **Steps:**
  1. Launch ArborGraph. Select target drive `C:\` only.
  2. Click `Start Scan` and wait for completion.
  3. Query SQLite: `SELECT COUNT(*) FROM files WHERE path LIKE 'C:%'`. Record count as $N_C$.
  4. Uncheck `C:\` and select drive `D:\`.
  5. Click `Start Scan` and wait for completion.
  6. Query SQLite: verify `SELECT COUNT(*) FROM files WHERE path LIKE 'D:%'` > 0. Record count as $N_D$.
  7. Re-query `C:\` files in SQLite: `SELECT COUNT(*) FROM files WHERE path LIKE 'C:%'`.
  8. Open Analytics tab and inspect dual-drive metrics.
- **Expected Result:**
  - Files discovered on `D:\` are indexed into SQLite.
  - Scanning `D:\` must **NOT** delete `C:\` data. Count for `C:\` must remain exactly $N_C$.
  - Combined analytics reflect both drives.
  - *(If `C:\` data is deleted, classify as a RELEASE-BLOCKING defect).*
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Code inspection in `DatabaseService.cs` lines 185–198 confirms `isFullClear` triggers on drive letter roots. Audit flagged this as Blocker 1.

---

#### Test ID: `TC-DRV-02`
- **Feature ID:** `FEAT-01`, `FEAT-08`
- **Title:** Simultaneous Multi-Drive Root Scan
- **Priority:** P0 / BLOCKER
- **Risk:** High — Potential channel deadlock or SQLite write contention during simultaneous multi-root traversal.
- **Preconditions:** Multiple drives selected in top toolbar.
- **Test Data:** Drives `C:\` and `D:\`.
- **Steps:**
  1. Check both `C:` and `D:` checkboxes in toolbar.
  2. Click `Start Scan`.
  3. Monitor live throughput and log feed.
  4. Wait for completion and inspect database records.
- **Expected Result:** Traversal completes cleanly; SQLite contains records for both drives; combined storage matches both targets.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Requires multi-drive physical/virtual setup.

---

#### Test ID: `TC-DRV-03`
- **Feature ID:** `FEAT-02`
- **Title:** Custom Directory Target Scoped Scan & Clear
- **Priority:** P1 / CRITICAL
- **Risk:** High — Verify that custom directory scanning (`C:\TestFolder`) only clears records belonging to `C:\TestFolder%` and leaves surrounding drive data intact.
- **Preconditions:** Database populated with `C:\` files.
- **Test Data:** Dataset A located at `C:\TestFolderA`.
- **Steps:**
  1. Choose custom folder `C:\TestFolderA`.
  2. Click `Start Scan`.
  3. Verify `ClearIndex` issues `DELETE FROM files WHERE path LIKE 'C:\TestFolderA%'`.
- **Expected Result:** Only target folder records are replaced; other indexed records remain intact.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Scoped deletion implemented in `DatabaseService.cs`.

---

### 9.2 Deletion Safety & Destructive Operations (P0 / P1)

#### Test ID: `TC-DEL-01`
- **Feature ID:** `FEAT-37`
- **Title:** Recycle Bin Deletion & Shell Recovery
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Failure of `IFileOperation` / `SHFileOperation` to send files to the Windows Recycle Bin or silent file destruction.
- **Preconditions:** Test directory with generated non-critical files.
- **Test Data:** Dataset A generated files in `%TEMP%\ArborGraph_DelTest`.
- **Steps:**
  1. Open Largest Files tab. Select test file `dummy_target.dat`.
  2. Click `Recycle Bin` deletion action.
  3. Confirm the action when prompted.
  4. Verify the file is absent from disk.
  5. Open Windows desktop Recycle Bin.
  6. Locate `dummy_target.dat` and click `Restore`.
- **Expected Result:** File disappears from directory; appears in Windows Recycle Bin; restores to original path with identical SHA-256 hash.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Uses Shell32 COM API wrappers.

---

#### Test ID: `TC-DEL-02`
- **Feature ID:** `FEAT-37`, `FEAT-38`
- **Title:** Protected System Path Deletion Guard
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Attempting to delete critical OS paths (`C:\`, `C:\Windows`, `C:\Program Files`, `C:\Users\<Current>`) could damage the OS.
- **Preconditions:** Scanner has indexed system files.
- **Test Data:** System paths in database (`C:\Windows\explorer.exe`, `C:\Program Files`).
- **Steps:**
  1. Locate a Windows system file in Largest Files or Query Explorer.
  2. Attempt to trigger `Move to Recycle Bin` or `Delete Permanently`.
- **Expected Result:** Application blocks the operation; displays a critical warning dialog; no filesystem operation is issued.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `FileSecurityHelper.IsProtectedPath()` blocks root drives and system directories.

---

#### Test ID: `TC-DEL-03`
- **Feature ID:** `FEAT-38`
- **Title:** Permanent Deletion Explicit Confirmation Gate
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Permanent deletion occurring without mandatory confirmation, or proceeding when user clicks "Cancel".
- **Preconditions:** Test files generated in `%TEMP%\ArborGraph_PermTest`.
- **Test Data:** `perm_target.dat`.
- **Steps:**
  1. Select `perm_target.dat` in Largest Files.
  2. Click `Delete Permanently`.
  3. When confirmation dialog appears, click `Cancel`.
  4. Verify file still exists on disk.
  5. Click `Delete Permanently` again; click `Confirm / Delete`.
  6. Verify file is deleted from disk and NOT present in Recycle Bin.
- **Expected Result:** Clicking Cancel prevents deletion; confirming permanently removes file.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `LargestFilesViewModel.cs` calls `WpfMessageBox` before invoking delete.

---

#### Test ID: `TC-DEL-04`
- **Feature ID:** `FEAT-38`
- **Title:** Read-Only File Deletion Safety
- **Priority:** P1 / CRITICAL
- **Risk:** High — Read-only attribute causing `UnauthorizedAccessException` or crashing application during batch deletion.
- **Preconditions:** Test file marked with `FileAttributes.ReadOnly`.
- **Test Data:** `readonly_file.dat` (`attrib +r`).
- **Steps:**
  1. Target `readonly_file.dat` for deletion.
  2. Confirm deletion.
- **Expected Result:** Application either strips read-only attribute and deletes, or safely informs user of failure without crashing.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Needs verification under standard user permissions.

---

### 9.3 UI Responsiveness During Heavy Deletions (P0 / P1)

#### Test ID: `TC-UI-RESP-01`
- **Feature ID:** `FEAT-19`, `FEAT-38`
- **Title:** Largest Folders Deletion Synchronous UI Thread Freezing Check
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Code inspection of `LargestFoldersViewModel.cs` (line 89) shows `DeletePermanently` executes synchronously on the UI dispatcher thread, causing the window to freeze ("Not Responding") during deep folder purges.
- **Preconditions:** Test directory containing 10,000 files across 200 nested folders.
- **Test Data:** Dataset D in `%TEMP%\ArborGraph_DeepDel`.
- **Steps:**
  1. Open Largest Folders tab. Select the deep test folder.
  2. Click `Delete Permanently` and confirm.
  3. Immediately attempt to drag the window, switch tabs, or click `Cancel`.
  4. Measure window responsiveness during deletion.
- **Expected Result:**
  - UI remains responsive; displays a progress indicator.
  - Window does **NOT** display Windows "Not Responding" title bar freeze.
  - *(If window freezes completely until deletion finishes, classify as Blocker 2 defect).*
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Audit flagged synchronous deletion on UI thread as Blocker 2.

---

#### Test ID: `TC-UI-RESP-02`
- **Feature ID:** `FEAT-18`, `FEAT-38`
- **Title:** Largest Files Bulk Deletion UI Thread Freezing Check
- **Priority:** P1 / CRITICAL
- **Risk:** High — `LargestFilesViewModel.cs` (line 272) executing deletion loop synchronously on dispatcher thread.
- **Preconditions:** 500 test files selected for deletion.
- **Test Data:** 500 dummy files in `%TEMP%\ArborGraph_BatchDel`.
- **Steps:**
  1. Select all 500 files in Largest Files grid.
  2. Trigger deletion.
  3. Monitor UI thread responsiveness.
- **Expected Result:** Deletion executes asynchronously on background worker; UI remains fluid.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Requires `Task.Run` wrapping.

---

### 9.4 NTFS USN Change Journal & Native Memory Safety (P0 / P1)

#### Test ID: `TC-USN-01`
- **Feature ID:** `FEAT-04`
- **Title:** USN Journal Pointer Arithmetic & Native Memory Safety
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — `UsnJournalService.cs` (line 233) uses raw P/Invoke pointer manipulation (`Marshal.PtrToStructure`, `Marshal.ReadInt32`). Malformed journal records on fragmented volumes could cause `AccessViolationException` crashes.
- **Preconditions:** Administrator session on NTFS drive with active USN journal.
- **Test Data:** Dataset Q (Active NTFS volume with rapid concurrent file modifications).
- **Steps:**
  1. Run initial scan on NTFS drive to establish USN checkpoint.
  2. Perform heavy file operations (generate 1,000 files, rename 500, delete 200).
  3. Trigger incremental scan via USN Journal.
  4. Monitor process stability and native memory allocations.
- **Expected Result:** USN incremental scan completes in sub-second time; all changes reflected in SQLite index; zero memory access violations.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Audit flagged raw pointer arithmetic in USN service as Blocker 4.

---

#### Test ID: `TC-USN-02`
- **Feature ID:** `FEAT-04`
- **Title:** USN Journal Non-NTFS Graceful Fallback
- **Priority:** P1 / CRITICAL
- **Risk:** High — Attempting to open USN journal handle on FAT32 or exFAT drives must not throw unhandled Win32 exceptions.
- **Preconditions:** Connected USB flash drive formatted as exFAT (Dataset R).
- **Test Data:** Dataset R.
- **Steps:**
  1. Select USB exFAT drive letter in ArborGraph.
  2. Click `Start Scan`.
- **Expected Result:** `IsNtfsVolume` returns `false`; scanner automatically falls back to full BFS traversal; zero P/Invoke exceptions.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `UsnJournalService.cs` includes filesystem format check before querying journal.

---

#### Test ID: `TC-USN-03`
- **Feature ID:** `FEAT-04`
- **Title:** USN Journal ID Mismatch Fallback
- **Priority:** P1 / CRITICAL
- **Risk:** High — If the USN Journal is deleted, reset, or recreated between scans (`UsnJournalId` mismatch), incremental scanning must safely abort and trigger a full rebuild.
- **Preconditions:** Prior USN checkpoint stored in database.
- **Test Data:** Simulated journal reset via `fsutil usn deletejournal /d <drive>`.
- **Steps:**
  1. Record initial USN checkpoint.
  2. Reset journal via administrative CLI.
  3. Trigger rescan in ArborGraph.
- **Expected Result:** ArborGraph detects ID mismatch; logs warning; executes clean full scan without corrupted deltas.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verifies journal reset safety branch.

---

#### Test ID: `TC-USN-04`
- **Feature ID:** `FEAT-04`
- **Title:** Standard Non-Admin User Fallback
- **Priority:** P1 / CRITICAL
- **Risk:** High — Standard users lack `SE_MANAGE_VOLUME_NAME` privilege; opening volume root handle returns `ERROR_ACCESS_DENIED`.
- **Preconditions:** Application executed in standard non-elevated user session.
- **Test Data:** Local NTFS drive.
- **Steps:**
  1. Launch ArborGraph as standard user.
  2. Start scan on `C:\`.
- **Expected Result:** USN journal handle opening fails cleanly with access denied; scanner falls back to full BFS traversal without user disruption.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified fallback pattern in `ScannerService.cs`.

---

### 9.5 Core BFS Scanner & Traversal Edge Cases (P1)

#### Test ID: `TC-SCN-01`
- **Feature ID:** `FEAT-03`
- **Title:** Traversal Cancellation Responsiveness & Token Propagation
- **Priority:** P1 / CRITICAL
- **Risk:** High — Stalled background threads or uncancelled I/O blocking UI state when user clicks `Cancel`.
- **Preconditions:** Scan in progress on large dataset.
- **Test Data:** Dataset C (100,000 files).
- **Steps:**
  1. Click `Start Scan`.
  2. Once throughput reaches steady state, click `Cancel Scan`.
  3. Measure time to reach `ScanState.Cancelled`.
- **Expected Result:** Scanner halts within 500 ms; channel writer completes; status bar reports "Scan cancelled"; partial data is safely queryable.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `CancellationTokenSource` propagated across BFS enumerator.

---

#### Test ID: `TC-SCN-02`
- **Feature ID:** `FEAT-03`
- **Title:** Inaccessible System Folders Graceful Bypass
- **Priority:** P1 / CRITICAL
- **Risk:** High — Scanner terminating abruptly upon hitting `System Volume Information` or permission-denied directories.
- **Preconditions:** Drive root containing restricted system directories.
- **Test Data:** Dataset K (Inaccessible folders).
- **Steps:**
  1. Scan drive root `C:\`.
  2. Verify handling of `C:\System Volume Information`.
- **Expected Result:** Traversal catches `UnauthorizedAccessException`; logs directory to `SkippedDirectories`; continues scanning without interruption.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `ScannerService.cs` line 210 handles directory access exceptions.

---

#### Test ID: `TC-SCN-03`
- **Feature ID:** `FEAT-03`
- **Title:** Deep Path Traversal (> 260 Characters / MAX_PATH)
- **Priority:** P1 / CRITICAL
- **Risk:** High — `PathTooLongException` thrown on deeply nested directory hierarchies.
- **Preconditions:** Dataset F constructed with 30 nested subfolders exceeding 260 characters.
- **Test Data:** Dataset F.
- **Steps:**
  1. Run scan targeted at Dataset F.
  2. Verify all files down to level 30 are indexed.
- **Expected Result:** All 100 deeply nested files are successfully indexed; byte counts match reference value exactly; zero path length exceptions.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: .NET 8 handles long paths by default when OS long paths or `\\?\` prefixes are active.

---

#### Test ID: `TC-SCN-04`
- **Feature ID:** `FEAT-07`, `FEAT-05`
- **Title:** Live Rolling Feed & Telemetry UI Decoupling
- **Priority:** P2 / MAJOR
- **Risk:** Medium — High scanner throughput flooding WPF dispatcher with live directory string updates, causing stutter.
- **Preconditions:** Active scan at > 20,000 files/sec.
- **Test Data:** Dataset D (Many small files).
- **Steps:**
  1. Start scan on Dataset D.
  2. Observe live directory text feed and throughput sparkline in Scanner view.
- **Expected Result:** Feed updates smoothly via throttling/batching; CPU usage of UI thread remains under 20%.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Feed uses dispatched throttling.

---

#### Test ID: `TC-SCN-05`
- **Feature ID:** `FEAT-03`
- **Title:** Cloud Files-on-Demand (OneDrive) Zero-Hydration Verification
- **Priority:** P1 / CRITICAL
- **Risk:** Critical — Scanner reading cloud placeholder files (`RecallOnDataAccess`), causing Windows to download gigabytes of online files.
- **Preconditions:** OneDrive folder containing cloud-only files.
- **Test Data:** Dataset S (50 placeholder files, 10 GB apparent size).
- **Steps:**
  1. Scan OneDrive directory.
  2. Monitor network adapter utilization and OneDrive sync client status.
- **Expected Result:** Network utilization remains 0 KB/s; OneDrive does not trigger download notifications; files are indexed by metadata only.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Reparse point attribute checking prevents stream reads.

---

### 9.6 Large-Scale Directory Rollup & LOH Memory Scaling (P1)

#### Test ID: `TC-ROL-01`
- **Feature ID:** `FEAT-10`
- **Title:** Recursive Rollup Numerical Correctness
- **Priority:** P1 / CRITICAL
- **Risk:** High — Off-by-one errors or parent size miscalculations leading to incorrect folder sizes in Largest Folders and Treemap.
- **Preconditions:** Dataset A indexed in database.
- **Test Data:** Dataset A (known reference: 13 files, 228,891 logical bytes).
- **Steps:**
  1. Scan Dataset A.
  2. Execute `BuildDirectoryRollup`.
  3. Verify size of `FolderB` equals sum of its immediate files plus all files in `FolderB/NestedB`.
- **Expected Result:** Root rollup size equals exactly 228,891 bytes; subfolder sums match child file totals with 100% precision.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Validated in integration test milestone 16.

---

#### Test ID: `TC-ROL-02`
- **Feature ID:** `FEAT-10`
- **Title:** Rollup Scalability & LOH Memory Ceiling Benchmark
- **Priority:** P1 / CRITICAL
- **Risk:** High — `BuildDirectoryRollup` allocating massive dictionary and tree structures on the Large Object Heap, triggering excessive Gen 2 GC pauses or OutOfMemoryException on 100K+ directories.
- **Preconditions:** 100,000 directories indexed in SQLite database.
- **Test Data:** Dataset C (100,000 files across 5,000 directories) and scaled mock index of 100,000 directories.
- **Steps:**
  1. Ingest 100,000 directory records.
  2. Invoke `BuildDirectoryRollup` while monitoring memory via `GC.GetTotalMemory` and stopwatch.
  3. Measure elapsed time and memory delta.
- **Expected Result:** Rollup completes in < 5.0 seconds; memory allocation delta < 150 MB; zero out-of-memory crashes.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Audit flagged rollup memory scaling as a key scalability risk.

---

### 9.7 Cryptographic Duplicate Detection & Collisions (P1)

#### Test ID: `TC-DUP-01`
- **Feature ID:** `FEAT-22`
- **Title:** 3-Stage Duplicate Pipeline Accuracy (Size -> MD5 -> SHA-256)
- **Priority:** P1 / CRITICAL
- **Risk:** Critical — False positives marking non-identical files as duplicates based on size or fast partial hash alone, leading to potential data loss upon cleanup.
- **Preconditions:** Dataset G indexed in database.
- **Test Data:** Dataset G (Contains 2 genuine duplicate groups and 3 size-collision pairs with differing payloads).
- **Steps:**
  1. Run Duplicate Files scan on Dataset G.
  2. Inspect duplicate groups in UI and database.
- **Expected Result:**
  - Exactly 2 duplicate groups identified (Group 1: 64 KB, Group 2: 1 MB).
  - All 3 collision pairs (same size, differing content) are strictly rejected.
  - Reclaimable space calculation equals exactly 2,162,688 bytes.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `DuplicateDetectorService.cs` implements three-tier filtering (Size -> MD5 16KB prefix -> full SHA-256).

---

#### Test ID: `TC-DUP-02`
- **Feature ID:** `FEAT-22`
- **Title:** Zero-Byte & Locked File Duplicate Handling
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Crashing when encountering 0-byte files or files locked by other processes during hash generation.
- **Preconditions:** Dataset G containing empty files and locked files.
- **Test Data:** Dataset G (`empty1.txt`, `empty2.txt`, locked file).
- **Steps:**
  1. Execute duplicate scan.
  2. Verify log output and group list.
- **Expected Result:** 0-byte files are ignored or grouped cleanly; locked file reading failure is caught and logged without aborting entire duplicate run.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Zero-byte files skipped before hashing.

---

#### Test ID: `TC-DUP-03`
- **Feature ID:** `FEAT-23`, `FEAT-37`
- **Title:** Duplicate Elimination Safety (Preserve Master Copy)
- **Priority:** P1 / CRITICAL
- **Risk:** Critical — Accidentally selecting and deleting all copies in a duplicate group, causing total data loss.
- **Preconditions:** Duplicate group with 3 copies listed in UI.
- **Test Data:** Group 2 from Dataset G.
- **Steps:**
  1. Open Duplicate Files view.
  2. Inspect automated selection rules ("Keep Newest", "Keep Oldest").
  3. Verify master copy cannot be accidentally purged in batch mode without explicit override.
  4. Execute deletion of duplicate copies.
- **Expected Result:** Exactly 1 original master copy remains on disk; redundant copies moved to Recycle Bin; reclaimable space updates immediately.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Selection model enforces retaining at least one copy per group.

---

### 9.8 Contextual Developer, Junk & Cache Cleaning (P1)

#### Test ID: `TC-DEV-01`
- **Feature ID:** `FEAT-24`, `FEAT-25`
- **Title:** Contextual Marker Verification (Node, .NET, Rust, Gradle)
- **Priority:** P1 / CRITICAL
- **Risk:** High — False positives deleting user folders named `target` or `build` that are NOT developer build caches.
- **Preconditions:** Dataset N indexed in database.
- **Test Data:** Dataset N (Legitimate developer projects alongside non-developer folders named `target` and `build`).
- **Steps:**
  1. Open Developer Storage tab.
  2. Trigger Developer Artifacts scan.
  3. Inspect detected item list.
- **Expected Result:**
  - `proj_node/node_modules`, `proj_dotnet/bin`, `proj_rust/target`, and `proj_gradle/build` are detected.
  - `customer-targets/target` and `architectural-build/build` are **strictly excluded**.
  - False positive count: exactly 0.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `DeveloperStorageService.cs` verifies parent marker files (`Cargo.toml`, `package.json`, `.csproj`, `build.gradle`).

---

#### Test ID: `TC-SAF-01`
- **Feature ID:** `FEAT-27`, `FEAT-26`
- **Title:** Locked-File Safe Deletion Fallback in Junk Cleaner
- **Priority:** P1 / CRITICAL
- **Risk:** High — Application crashes or partial deletions aborting when cleaning active system/browser caches containing locked files.
- **Preconditions:** Dataset L (Files locked with exclusive sharing mode `FileShare.None`).
- **Test Data:** Dataset L.
- **Steps:**
  1. Lock 2 files using background process with `FileShare.None`.
  2. Trigger Junk Cleaner clean operation on target folder.
- **Expected Result:**
  - 4 unlocked files are deleted cleanly.
  - 2 locked files are safely skipped without throwing unhandled exceptions.
  - Summary dialog reports: "4 files deleted, 2 files skipped (in use)".
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `JunkCleanerService.cs` lines 145–160 implements per-file try/catch with skip accounting.

---

#### Test ID: `TC-PS-01`
- **Feature ID:** `FEAT-28`, `FEAT-29`
- **Title:** Photoshop Media Asset Cataloging & Scratch Cache Reclaimer
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Photoshop temp files (`Photoshop Temp*`) not identified or regular assets misclassified.
- **Preconditions:** Dataset O indexed in database.
- **Test Data:** Dataset O (PSD, PSB, ABR files, Photoshop Temp files).
- **Steps:**
  1. Open Photoshop Intelligence tab.
  2. Inspect catalog breakdown and reclaimable scratch storage.
- **Expected Result:** PSD, PSB, and ABR files categorized correctly; active Photoshop scratch temp files flagged for cleanup.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Integrated into media intelligence queries.

---

### 9.9 Interactive Squarified Treemap (P1)

#### Test ID: `TC-TMP-01`
- **Feature ID:** `FEAT-30`
- **Title:** Squarified Geometry & Proportional Correctness
- **Priority:** P1 / CRITICAL
- **Risk:** High — Zero-size files, single items, or extreme aspect ratios causing division by zero, `NaN`, or `Infinity` layout crashes.
- **Preconditions:** Datasets with edge-case directory structures (Dataset A, Dataset J).
- **Test Data:** Dataset A and Dataset J (empty folders).
- **Steps:**
  1. Open Treemap view on Dataset A.
  2. Verify all rendered bounding rectangles satisfy: `Width > 0`, `Height > 0`, `X >= 0`, `Y >= 0`.
  3. Test with Dataset J (empty directories, 0-byte files).
- **Expected Result:**
  - Zero `NaN` or `Infinity` coordinate values in WPF layout pass.
  - Bounding rectangles fill canvas proportionally without overlapping invalid geometry.
  - Zero-byte files or empty folders display empty state message without crashing.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `TreemapView.xaml.cs` implements squarified layout algorithm with aspect ratio guards.

---

#### Test ID: `TC-TMP-02`
- **Feature ID:** `FEAT-30`
- **Title:** Dynamic Canvas Resizing & Aspect Ratio Recalculation
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Rapid window resizing or minimization corrupting canvas geometry.
- **Preconditions:** Treemap rendered on medium dataset.
- **Test Data:** Dataset B.
- **Steps:**
  1. Open Treemap view.
  2. Resize window rapidly from 1920x1080 to 800x600 and back.
  3. Minimize and restore window.
- **Expected Result:** Canvas recomputes layout smoothly; no layout exceptions or visual artifacts.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: WPF SizeChanged event bound to layout recomputation.

---

#### Test ID: `TC-TMP-03`
- **Feature ID:** `FEAT-31`
- **Title:** Hierarchical Node Drill-Down & Breadcrumb Navigation
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Breadcrumb navigation desynchronizing from active folder view.
- **Preconditions:** Treemap rendered with nested directories.
- **Test Data:** Dataset A.
- **Steps:**
  1. Double-click `FolderB` node in treemap.
  2. Verify view drills down into `FolderB/NestedB`.
  3. Verify breadcrumb bar updates: `Root > FolderB`.
  4. Click `Root` in breadcrumb bar.
- **Expected Result:** View drills down and zooms out smoothly; breadcrumb path accurately reflects navigation state.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Breadcrumb bar bound to navigation stack.

---

### 9.10 Database Concurrency & Lock Contention (P1)

#### Test ID: `TC-DB-01`
- **Feature ID:** `FEAT-08`
- **Title:** Concurrent Read During High-Throughput Scan
- **Priority:** P1 / CRITICAL
- **Risk:** High — SQLite write batches locking the database file (`busy_timeout`), causing UI query threads to throw `SqliteException: database is locked`.
- **Preconditions:** Active scan running on large dataset at > 15,000 files/sec.
- **Test Data:** Dataset C (100,000 files).
- **Steps:**
  1. Start scan on Dataset C.
  2. While scanner writes batches of 5,000 records, rapidly navigate:
     - Overview -> Largest Files -> Largest Folders -> Treemap -> Query Explorer.
  3. Execute multi-criteria search queries in Query Explorer during active scanning.
- **Expected Result:**
  - SQLite WAL mode allows concurrent readers without lock contention.
  - Zero `database is locked` exceptions; UI displays data without hanging.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `DatabaseService.cs` enables WAL journal mode and busy timeout = 5000ms.

---

#### Test ID: `TC-DB-02`
- **Feature ID:** `FEAT-08`, `FEAT-42`
- **Title:** Abrupt Process Termination & SQLite WAL Recovery
- **Priority:** P1 / CRITICAL
- **Risk:** High — Database corruption after power loss or abrupt process kill.
- **Preconditions:** Scan actively writing records to SQLite.
- **Test Data:** Active scan on Dataset C.
- **Steps:**
  1. Start scan.
  2. While actively writing, terminate `ArborGraph.exe` abruptly via `taskkill /F /IM ArborGraph.exe`.
  3. Relaunch ArborGraph.
  4. Inspect database state via `PRAGMA integrity_check`.
- **Expected Result:** SQLite WAL file replays automatically on restart; `PRAGMA integrity_check` returns `ok`; app launches cleanly.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: SQLite WAL guarantees crash atomicity.

---

#### Test ID: `TC-DB-03`
- **Feature ID:** `FEAT-09`
- **Title:** Explicit Database Integrity Verification Routine
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Corrupted index causing silent query errors.
- **Preconditions:** Application launched.
- **Test Data:** Local SQLite database.
- **Steps:**
  1. Trigger database integrity check.
  2. Verify log records result.
- **Expected Result:** Returns `ok`; integrity validated.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `DatabaseService.cs` implements PRAGMA integrity check.

---

### 9.11 Analytics, Query & Reporting Accuracy (P1)

#### Test ID: `TC-ANL-01`
- **Feature ID:** `FEAT-11`, `FEAT-14`
- **Title:** Overview Storage Totals & Growth Delta Accuracy
- **Priority:** P1 / CRITICAL
- **Risk:** High — Discrepancies between physical disk metrics, indexed files, and category rollup totals.
- **Preconditions:** Dataset A indexed in database.
- **Test Data:** Dataset A (known reference: 13 files, 228,891 logical bytes).
- **Steps:**
  1. Inspect Overview tab after scanning Dataset A.
  2. Verify Total Files displayed = 13.
  3. Verify Total Size displayed = 223.5 KB (228,891 bytes).
  4. Verify sum of all file category totals equals exactly 228,891 bytes.
- **Expected Result:** Numerical metrics match reference values with 100% precision; no unaccounted bytes.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Strict accounting test.

---

#### Test ID: `TC-QRY-01`
- **Feature ID:** `FEAT-16`, `FEAT-17`
- **Title:** Multi-Criteria Query Filtering & Paging Accuracy
- **Priority:** P1 / CRITICAL
- **Risk:** High — SQL injection vulnerabilities, broken filters, or pagination offset errors.
- **Preconditions:** Dataset B indexed in database.
- **Test Data:** Dataset B and Dataset I (special characters in paths).
- **Steps:**
  1. In Query Explorer, filter by:
     - Extension: `.pdf`
     - Minimum Size: `1 MB`
     - Age: `Older than 30 Days`
  2. Verify results match criteria.
  3. Test with search string containing quotes, brackets, and SQL characters: `test' OR '1'='1`.
- **Expected Result:** Query executes cleanly using parameterized SQL; zero syntax errors; pagination controls navigate pages accurately.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Queries use parameterized SQLite commands.

---

#### Test ID: `TC-EXP-01`
- **Feature ID:** `FEAT-17`, `FEAT-33`, `FEAT-34`, `FEAT-35`
- **Title:** Audit Export Integrity (HTML, JSON, CSV)
- **Priority:** P1 / CRITICAL
- **Risk:** High — Truncated exports, invalid JSON syntax, or unescaped CSV delimiters.
- **Preconditions:** Dataset A indexed in database.
- **Test Data:** Dataset A.
- **Steps:**
  1. Export Executive HTML Audit Report.
  2. Export Machine-Readable JSON.
  3. Export Tabular CSV.
  4. Validate syntax of all 3 files.
- **Expected Result:**
  - HTML report opens cleanly in browser with embedded charts.
  - JSON validates with `jq` / JSON parser; structure matches schema.
  - CSV opens cleanly in Excel without column misalignments.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Implemented in `ExportService.cs`.

---

### 9.12 Settings, Legal Gate & Error Handling (P1)

#### Test ID: `TC-LGL-01`
- **Feature ID:** `FEAT-39`
- **Title:** First-Run EULA Consent Gate
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Application allowing scan execution before user accepts legal terms.
- **Preconditions:** Fresh application launch with no existing `settings.json`.
- **Test Data:** Default environment.
- **Steps:**
  1. Delete `%LOCALAPPDATA%\ArborGraph\settings.json`.
  2. Launch ArborGraph.
  3. Verify EULA modal dialog displays on startup.
  4. Click `Decline` -> Verify application shuts down cleanly.
  5. Relaunch; click `Accept` -> Verify application opens main window.
  6. Relaunch a third time -> Verify EULA dialog does NOT appear again.
- **Expected Result:** EULA blocks access until accepted; decline exits cleanly; acceptance persists.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `EulaDialog.xaml` displays on `EulaAccepted = false`.

---

#### Test ID: `TC-SET-01`
- **Feature ID:** `FEAT-40`
- **Title:** Settings Persistence & Exclusion Rule Enforcement
- **Priority:** P1 / CRITICAL
- **Risk:** High — Excluded directory patterns ignored by scanner, or settings corruption causing crash on startup.
- **Preconditions:** Application launched.
- **Test Data:** Custom exclusion path added to settings (`C:\SkipMe`).
- **Steps:**
  1. Open Settings tab. Add `SkipMe` to Exclusion List. Save settings.
  2. Restart application. Verify exclusion persists in UI.
  3. Scan folder containing `SkipMe` subfolder.
- **Expected Result:** Scanner completely ignores `SkipMe` subfolder; zero files from `SkipMe` indexed into database.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `SettingsService.cs` loads/saves JSON with exclusion matching in `ScannerService.cs`.

---

#### Test ID: `TC-ERR-01`
- **Feature ID:** `FEAT-42`
- **Title:** Global Unhandled Exception Interception
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Silent application termination without error logging.
- **Preconditions:** Application running.
- **Test Data:** Simulated dispatch exception.
- **Steps:**
  1. Trigger unhandled exception in background thread or dispatcher.
  2. Inspect `%LOCALAPPDATA%\ArborGraph\app.log`.
- **Expected Result:** Global handler catches exception; writes stack trace to log; displays friendly error dialog; prevents abrupt silent crash.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `App.xaml.cs` hooks `DispatcherUnhandledException` and `AppDomain.CurrentDomain.UnhandledException`.

---

### 9.13 Inno Setup 6 Desktop Installer & Deployment (P0 / P1)

#### Test ID: `TC-INS-01`
- **Feature ID:** `FEAT-43`
- **Title:** Clean Fresh Installation (Standard Lowest-Privilege User)
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Installer failing or demanding unnecessary Administrator elevation (`UAC` prompt), preventing standard non-admin users from installing.
- **Preconditions:** Clean test machine without prior installation. Standard user session (non-elevated).
- **Test Data:** `ArborGraph-Setup-1.0.0-x64.exe`.
- **Steps:**
  1. Launch installer executable.
  2. Verify no mandatory UAC shield prompt appears (`PrivilegesRequired=lowest`).
  3. Verify Inno Setup wizard displays title: `"Setup - ArborGraph"`.
  4. Inspect License Agreement screen: verify `installer/eula.txt` displays cleanly.
  5. Select `"I accept the agreement"` and click `Next`.
  6. Verify default installation directory resolves to `{autopf}\ArborGraph` (`%LOCALAPPDATA%\Programs\ArborGraph`).
  7. Check `"Create a desktop shortcut"`.
  8. Click `Install` and complete setup.
  9. Click `Finish` with `"Launch ArborGraph"` checked.
- **Expected Result:** Setup completes without errors or elevation; binaries deployed to `{app}`; app launches successfully.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `installer.iss` specifies `PrivilegesRequired=lowest` and `LicenseFile=eula.txt`.

---

#### Test ID: `TC-INS-02`
- **Feature ID:** `FEAT-43`
- **Title:** Mandatory EULA Rejection Aborts Installation
- **Priority:** P1 / CRITICAL
- **Risk:** High — Legal licensing compliance failure if user can proceed without consenting.
- **Preconditions:** Fresh installer launch.
- **Test Data:** `ArborGraph-Setup-1.0.0-x64.exe`.
- **Steps:**
  1. Launch installer.
  2. On License page, select `"I do not accept the agreement"`.
  3. Attempt to click `Next`. Click `Cancel`.
- **Expected Result:** `Next` button is disabled when `"I do not accept"` is selected; clicking Cancel exits cleanly with zero filesystem changes.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Built-in Inno Setup behavior enforced by `LicenseFile`.

---

#### Test ID: `TC-INS-03`
- **Feature ID:** `FEAT-43`
- **Title:** Desktop & Start Menu Shortcut Verification
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Broken shortcut targets or missing icon resources.
- **Preconditions:** Installation completed with shortcuts enabled.
- **Test Data:** Installed application.
- **Steps:**
  1. Inspect Start Menu: verify `"ArborGraph"` shortcut exists.
  2. Inspect Desktop: verify `"ArborGraph"` shortcut exists.
  3. Inspect Properties: verify Target is `"{app}\ArborGraph.exe"`, Icon is `ArborGraph.ico`.
  4. Double-click each shortcut to launch.
- **Expected Result:** Both shortcuts launch application cleanly with correct icon.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Configured in `installer.iss` lines 47–49.

---

#### Test ID: `TC-INS-04`
- **Feature ID:** `FEAT-43`
- **Title:** Metadata & Repository URL Verification (URL Drift Check)
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Outdated links in Windows Control Panel ("Installed Apps") pointing to dead or old repository paths.
- **Preconditions:** App installed on Windows 10 or 11.
- **Test Data:** Installed registry entry.
- **Steps:**
  1. Open Windows `Settings` -> `Apps` -> `Installed Apps` (or `appwiz.cpl`).
  2. Locate `ArborGraph`. Inspect Publisher, Version, and Support/Update URLs.
- **Expected Result:**
  - Support and Publisher URLs must point to `https://github.com/12valor/ArborGraph`.
  - *(Audit finding: `installer.iss` line 9 currently hardcodes `https://github.com/12valor/C-file-scanner`. Must be updated prior to release).*
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Audit flagged repository URL drift as Blocker 3.

---

#### Test ID: `TC-INS-05`
- **Feature ID:** `FEAT-43`
- **Title:** In-Place Upgrade from Previous Installation
- **Priority:** P1 / CRITICAL
- **Risk:** High — Overwriting previous installation corrupts user settings or wipes `%LOCALAPPDATA%\ArborGraph\scan_index.db`.
- **Preconditions:** Previous version installed with active scan index.
- **Test Data:** Existing database and new installer build.
- **Steps:**
  1. Launch new installer build.
  2. Run setup without uninstalling previous build.
  3. Verify installer detects existing `AppId={{D37F7E1A-85F4-4BC3-9C1D-72810C24A59E}}`.
  4. Complete installation; launch application.
- **Expected Result:** Binaries updated in-place; user SQLite index and settings retained completely intact.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `AppId` stability verified in `installer.iss`.

---

#### Test ID: `TC-INS-06`
- **Feature ID:** `FEAT-43`
- **Title:** Complete Clean Uninstallation
- **Priority:** P1 / CRITICAL
- **Risk:** High — Leftover orphaned registry keys, binaries, or broken shortcuts after uninstall.
- **Preconditions:** ArborGraph installed via setup.
- **Test Data:** Installed system.
- **Steps:**
  1. Open `Installed Apps` -> Select `ArborGraph` -> Click `Uninstall`.
  2. Complete uninstallation wizard.
  3. Inspect `{app}` directory: verify `ArborGraph.exe` and `eula.txt` are deleted.
  4. Verify Start Menu and Desktop shortcuts are removed.
  5. Check Windows Registry: verify `Uninstall\ArborGraph` key is purged.
- **Expected Result:** Application binaries and shortcuts are 100% removed. User database in `%LOCALAPPDATA%` is preserved according to Windows desktop standards.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Defined in `[UninstallDelete]` in `installer.iss`.

---

#### Test ID: `TC-INS-07`
- **Feature ID:** `FEAT-43`
- **Title:** Silent Headless Deployment (`/VERYSILENT`)
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Scripted deployments hanging on interactive prompts.
- **Preconditions:** Command prompt session.
- **Test Data:** Installer executable.
- **Steps:**
  1. Execute: `ArborGraph-Setup-1.0.0-x64.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.
  2. Monitor process termination and exit code.
  3. Check target directory for deployed binaries.
- **Expected Result:** Process executes completely headless without displaying windows; exits with code 0; app deployed properly.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Standard Inno Setup capability.

---

### 9.14 Web Portal & Diagnostic Simulation (P2)

#### Test ID: `TC-WEB-01`
- **Feature ID:** `FEAT-44`
- **Title:** Web Portal Navigation, Assets & Responsive Layout
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Broken CSS, missing assets, or broken links on marketing/portal site.
- **Preconditions:** Web server hosting `site/` directory.
- **Test Data:** `site/index.html`, `site/style.css`, `site/app.js`.
- **Steps:**
  1. Load `site/index.html` in Chrome, Edge, and Firefox.
  2. Inspect layout at 1920px (Desktop), 768px (Tablet), and 375px (Mobile).
  3. Verify all navigation anchors, demo links, and GitHub links resolve cleanly.
- **Expected Result:** Clean responsive rendering; zero layout breakage; zero 404 console errors.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Vanilla HTML5/CSS3/JS implementation.

---

#### Test ID: `TC-WEB-02`
- **Feature ID:** `FEAT-44`
- **Title:** Interactive Scanner & Sparkline Live Simulation
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Canvas rendering exceptions in browser JS.
- **Preconditions:** Website opened in browser.
- **Test Data:** `site/app.js`.
- **Steps:**
  1. Interact with Demo UI tabs (Demo 02, Demo 03, Demo 04).
  2. Observe live simulated throughput sparkline and progress indicators.
- **Expected Result:** HTML5 canvas renders animated sparkline; zero JavaScript errors in developer console.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Pure client-side Canvas 2D animation.

---

#### Test ID: `TC-WEB-03`
- **Feature ID:** `FEAT-44`
- **Title:** Web Crypto SHA-256 Release Binary Verification Tool
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Hash calculation discrepancies between client-side Web Crypto and desktop binary SHA-256.
- **Preconditions:** Built `ArborGraph.exe` binary available.
- **Test Data:** Release binary.
- **Steps:**
  1. Open SHA-256 Verifier on website.
  2. Drag and drop `ArborGraph.exe` into the browser drop zone.
  3. Compare computed SHA-256 hash against `Get-FileHash ArborGraph.exe -Algorithm SHA256`.
- **Expected Result:** Web Crypto computed hash matches PowerShell SHA-256 hash character-for-character.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Implements browser `crypto.subtle.digest("SHA-256", buffer)`.

---

### 9.15 Human First-Time User Usability Testing (P1 / P2)

#### Test ID: `TC-USB-01`
- **Feature ID:** `FEAT-01`, `FEAT-39`
- **Title:** Usability Task 1: Start a Drive Scan
- **Priority:** P1 / CRITICAL
- **Risk:** High — Confusing first-run flow or hidden scan button preventing user from beginning analysis.
- **Preconditions:** Clean machine, no prior instruction provided to participant.
- **Test Data:** Real user environment.
- **Steps:**
  1. Instruct user: *"Open the application and begin analyzing your main drive to see where your disk space is going."*
  2. Observe participant navigation, hesitation, and path chosen.
- **Expected Result:** User independently accepts EULA and clicks `Start Scan` in < 20 seconds without assistance.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Protocol defined in usability specification.

---

#### Test ID: `TC-USB-02`
- **Feature ID:** `FEAT-19`
- **Title:** Usability Task 2: Find the Largest Folder on the Drive
- **Priority:** P2 / MAJOR
- **Risk:** Medium — User unable to locate largest folder view or understand rolled-up sizes.
- **Preconditions:** Drive scan completed.
- **Test Data:** Indexed drive.
- **Steps:**
  1. Instruct user: *"Find out which single directory is taking up the most storage on your computer."*
  2. Record completion time and confusion points.
- **Expected Result:** User navigates to `Largest Folders` and correctly names top folder in < 30 seconds.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Primary core user workflow.

---

#### Test ID: `TC-USB-03`
- **Feature ID:** `FEAT-18`
- **Title:** Usability Task 3: Find the Largest Individual File
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Confusion between folder views and file views.
- **Preconditions:** Drive scan completed.
- **Test Data:** Indexed drive.
- **Steps:**
  1. Instruct user: *"Identify the single largest individual file stored on your disk."*
- **Expected Result:** User navigates to `Largest Files` and identifies top file in < 20 seconds.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Standard user workflow.

---

#### Test ID: `TC-USB-04`
- **Feature ID:** `FEAT-30`, `FEAT-31`
- **Title:** Usability Task 4: Explore and Understand the Interactive Treemap
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Visual confusion regarding treemap block sizes and drilldown navigation.
- **Preconditions:** Drive scan completed.
- **Test Data:** Indexed drive.
- **Steps:**
  1. Instruct user: *"Navigate to the Treemap visualization and explain what the largest colored rectangle represents. Then zoom into it."*
- **Expected Result:** User explains proportional representation, double-clicks node to zoom, and uses breadcrumbs to zoom out.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Visual feedback evaluation.

---

#### Test ID: `TC-USB-05`
- **Feature ID:** `FEAT-22`, `FEAT-23`
- **Title:** Usability Task 5: Locate and Review Duplicate Files
- **Priority:** P1 / CRITICAL
- **Risk:** High — Accidental deletion of original copies due to confusing UI grouping.
- **Preconditions:** Drive scan completed.
- **Test Data:** Duplicate files present.
- **Steps:**
  1. Instruct user: *"Find out if you have identical duplicate files consuming wasted space."*
- **Expected Result:** User locates Duplicate Files tab, understands duplicate groups, and recognizes which copy is retained.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Evaluates safety of duplicate presentation.

---

#### Test ID: `TC-USB-06`
- **Feature ID:** `FEAT-32`
- **Title:** Usability Task 6: Review Cleanup Center Safe Recommendations
- **Priority:** P1 / CRITICAL
- **Risk:** High — User uncertainty about whether recommended cleanup items are safe to remove.
- **Preconditions:** Drive scan completed.
- **Test Data:** Reclaimable cache data.
- **Steps:**
  1. Instruct user: *"Go to Cleanup Center and review the storage recovery recommendations. Explain what is safe to delete."*
- **Expected Result:** User clearly distinguishes safe cache/junk from developer artifacts and personal files.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Evaluates clarity of safety indicators.

---

### 9.16 Platform, Display Scaling & Compatibility (P1 / P2)

#### Test ID: `TC-CMP-01`
- **Feature ID:** `FEAT-06`, `FEAT-30`
- **Title:** High-DPI Display Scaling Matrix (100% to 200%)
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Blurry rendering, text clipping, or canvas coordinate misalignment at non-standard DPI scales.
- **Preconditions:** Windows display settings configured to target DPI scale.
- **Test Data:** Application running at 100%, 125%, 150%, 175%, and 200% scaling.
- **Steps:**
  1. Launch ArborGraph under each display scaling factor.
  2. Inspect navigation bar tabs, sparkline canvas, treemap blocks, and data grid typography.
- **Expected Result:** All vector elements render sharp; zero clipped text or overflowing buttons; dialogs fit screen cleanly.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: WPF per-monitor DPI awareness.

---

#### Test ID: `TC-CMP-02`
- **Feature ID:** `FEAT-01`, `FEAT-03`
- **Title:** Removable Media (USB Flash Drive / exFAT) Scanning
- **Priority:** P1 / CRITICAL
- **Risk:** High — Removable media disconnection or non-NTFS format handling.
- **Preconditions:** USB flash drive formatted as exFAT connected.
- **Test Data:** Dataset R.
- **Steps:**
  1. Select USB drive letter. Scan volume.
  2. Verify file counts and storage metrics.
- **Expected Result:** Traversal completes accurately; non-NTFS detected; fallback to full BFS succeeds.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Cross-filesystem compatibility.

---

### 9.17 Multi-Tier Performance Benchmarking (P1)

#### Test ID: `TC-PERF-01`
- **Feature ID:** `FEAT-03`, `FEAT-08`, `FEAT-10`
- **Title:** Multi-Tier Scalability Benchmark (10K to 1M+ Files)
- **Priority:** P1 / CRITICAL
- **Risk:** High — Progressive memory leaks or throughput collapse on massive filesystems.
- **Preconditions:** Reference hardware environment (`ENV-A`).
- **Test Data:** Synthetic test trees generated at 10K, 50K, 100K, 250K, 500K, and 1,000,000 files.
- **Steps:**
  1. Execute scan for each volume tier 3 separate times.
  2. Capture metrics: Total Time, Files/Sec, Peak RAM (MB), Average CPU (%), SQLite DB Size.
  3. Calculate Minimum, Maximum, and Average throughput.
- **Expected Result:**
  - Throughput $\ge 15,000$ files/sec on NVMe SSD.
  - Peak RAM $\le 350\text{ MB}$ under 100K files; $\le 650\text{ MB}$ under 500K files.
  - Linear scaling; zero unhandled out-of-memory crashes.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Benchmark plan formalized in Test Plan.

---

# 10. Test Execution Results Log

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

---

# 11. Defect & Bug Tracking Log

### Bug ID: `BUG-001`
- **Severity:** P0 / BLOCKER
- **Feature / Test ID:** `FEAT-08` / `TC-DRV-01`
- **Title:** Multi-Drive Root Scan Wipes Previously Indexed Drives in SQLite (`ClearIndex` Scope Bug)
- **Environment:** Windows 11 x64, Multi-drive system (`C:\` and `D:\`)
- **Status:** OPEN (Identified in Code Audit — Pending Test Execution Confirmation)
- **Fix / Version:** v1.0.0
- **Regression Status:** PENDING VERIFICATION
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

# 12. Release Verification & Quality Gates Checklist

- [ ] **Gate 1.1 — P0 / Blocker Defect Count:** Strictly **0** open P0 defects.
- [ ] **Gate 1.2 — P1 / Critical Defect Count:** Strictly **0** open P1 defects.
- [ ] **Gate 1.3 — P2 / Major Defect Count:** Maximum **$\le 3$**, all with documented release notes and workarounds.
- [ ] **Gate 1.4 — P3 / Minor Defect Count:** Documented for subsequent patch updates.
- [ ] **Gate 2.1 — Multi-Drive Index Isolation (`BUG-001` / `TC-DRV-01`):** Scanning `D:\` root does NOT issue full wipe (`DELETE FROM files`) and does NOT wipe `C:\` data from SQLite.
- [ ] **Gate 2.2 — Deletion UI Thread Decoupling (`BUG-002` / `TC-UI-RESP-01`):** Deleting large folders runs asynchronously on worker thread; window never displays "Not Responding".
- [ ] **Gate 2.3 — Installer Repository URL Accuracy (`BUG-003` / `TC-INS-04`):** Inno Setup script updated to `https://github.com/12valor/ArborGraph`.
- [ ] **Gate 2.4 — USN Change Journal Pointer Safety (`BUG-004` / `TC-USN-01`):** Native buffer bounds checked with structured asserts.
- [ ] **Gate 3.1 — Protected Path Interception:** Deletion of `C:\`, `C:\Windows`, `C:\Program Files` blocked with warning.
- [ ] **Gate 3.2 — Mandatory Confirmation Gate:** Permanent deletion strictly requires user confirmation; Cancel aborts without file changes.
- [ ] **Gate 3.3 — Windows Recycle Bin Restorability:** Recoverable from Recycle Bin with matching SHA-256 hashes.
- [ ] **Gate 3.4 — Safe Duplicate Cleanup:** Batch duplicate elimination retains at least one master copy.
- [ ] **Gate 3.5 — Contextual Developer Cache Safety:** Non-developer folders named `target` or `build` are never flagged.
- [ ] **Gate 4.1 — Scanner Throughput Benchmark:** Traverses $\ge 15,000$ files/second on NVMe PCIe SSD.
- [ ] **Gate 4.2 — Working Set Memory Ceiling:** RAM remains $\le 350\text{ MB}$ under 100K files; $\le 650\text{ MB}$ under 500K files.
- [ ] **Gate 4.3 — Directory Rollup Efficiency:** `BuildDirectoryRollup` completes in $< 5.0$ seconds for 100,000 directories.
- [ ] **Gate 4.4 — Zero Memory Leaking:** Consecutive rescans show zero progressive unmanaged handle growth.
- [ ] **Gate 4.5 — SQLite Lock Contention Under Load:** Rapid tab switching during scanning succeeds without `database is locked`.
- [ ] **Gate 5.1 — 100% File Accounting:** Discovered count equals indexed database count on Dataset A.
- [ ] **Gate 5.2 — Byte Precision:** Sum of file sizes equals total storage size displayed on Overview tab.
- [ ] **Gate 5.3 — Treemap Geometric Validity:** Zero `NaN`, `Infinity`, or negative dimensions during layout and drill-down.
- [ ] **Gate 5.4 — Cryptographic Hash Deduplication:** Same-size different-payload files rejected from duplicate groups.
- [ ] **Gate 6.1 — Windows 11 & Windows 10 Execution:** Verified on Windows 11 (24H2) and Windows 10 (22H2) x64.
- [ ] **Gate 6.2 — High-DPI Display Scaling:** Sharp rendering across 100%, 125%, 150%, 175%, and 200% scaling.
- [ ] **Gate 6.3 — Removable & Non-NTFS Media:** exFAT and FAT32 USB flash drives scan successfully with full BFS fallback.
- [ ] **Gate 6.4 — Cloud Files-on-Demand (OneDrive):** Reparse points skipped without triggering background file downloads.
- [ ] **Gate 7.1 — Standard Non-Admin User Installation:** Installs cleanly without UAC administrator elevation prompt.
- [ ] **Gate 7.2 — Mandatory EULA Acceptance:** Setup aborts cleanly if user rejects license.
- [ ] **Gate 7.3 — In-Place Upgrade Retention:** Upgrading retains user database (`scan_index.db`) and settings.
- [ ] **Gate 7.4 — Complete Clean Uninstall:** Uninstaller removes all binaries and shortcuts cleanly.
- [ ] **Gate 7.5 — Silent Deployment Mode:** `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` exits with code 0.
- [ ] **Gate 8.1 — Automated Integration Suite Pass:** 20/20 integration test milestones in `tests/DiskScope.Tests.csproj` pass cleanly:
  ```powershell
  $env:DOTNET_ROOT = "C:\Users\evang\.dotnet"
  & "$env:DOTNET_ROOT\dotnet.exe" run --project tests/DiskScope.Tests.csproj
  ```
- [ ] **Gate 8.2 — Zero Compiler Warnings:** Clean compilation in Release mode:
  ```powershell
  & "$env:DOTNET_ROOT\dotnet.exe" build -c Release -p:Platform="Any CPU"
  ```
- [ ] **Gate 9.1 — Executable Binary Attributes:** Product Name is `ArborGraph`, Version is `1.0.0.0`, Copyright is `Copyright © 2026 AG DIAZ EVANGELISTA`.
- [ ] **Gate 9.2 — SHA-256 Hash Verification:** SHA-256 checksums documented in `SHA256SUMS.txt`.
- [ ] **Gate 9.3 — Web Verification Tool Check:** Uploading release binary to `site/index.html` computes matching SHA-256 hash.
- [ ] **Gate 10.1 — Git Tagging:** Tagged on `main` as `v1.0.0`.
- [ ] **Gate 10.2 — GitHub Release Artifacts Uploaded:** Setup installer, portable zip, and checksums uploaded.
- [ ] **Gate 10.3 — Release Notes Published:** Changelog and known minor limitations documented.
- [ ] **Gate 10.4 — Live Website Links Verified:** Download links resolve to live assets.

### Final Release Approval Sign-Off Table

| Milestone | Target Date | Sign-Off Status | Responsible Engineer | Notes / Approval Signature |
| :--- | :--- | :--- | :--- | :--- |
| **P0 / P1 Remediation** | | [ ] APPROVED | | Zero blocker/critical defects confirmed |
| **Automated Test Suite** | | [ ] APPROVED | | 20/20 Integration test milestones passing |
| **Installer & Packaging** | | [ ] APPROVED | | Clean install/uninstall verified on VM |
| **Final Release Candidate** | | [ ] APPROVED | | Full release authorization granted |
