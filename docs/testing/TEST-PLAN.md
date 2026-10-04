# ArborGraph Master QA Test Plan (v1.0.0)

**Document Identifier:** AG-TP-001  
**Target Product:** ArborGraph — Filesystem Analytics & Visualization  
**Target Release:** v1.0.0 (`net8.0-windows` x64)  
**Methodological Alignment:**  
- **Quality Characteristics:** ISO/IEC 25010:2023 product quality model  
- **Testing Process & Documentation:** ISO/IEC/IEEE 29119 standards  
*(Note: ArborGraph is an independent open-source project; these standards serve strictly as professional quality frameworks and do not claim formal accredited certification).*

---

## 1. Testing Objectives

1. **Zero Data Loss:** Absolute guarantee that safe deletion, Recycle Bin operations, contextual developer cleaning, and multi-drive scans never corrupt, misplace, or unintendedly destroy user data.
2. **Absolute Numerical Accuracy:** Full statistical accounting of discovered vs. indexed files, mathematically exact recursive folder rollup, and zero false positives in cryptographic deduplication.
3. **Execution Stability:** Clean degradation under constrained permissions (standard non-admin users), unhandled exception interception, robust locked-file bypass, and resilient USN Journal fallback.
4. **UI Fluidity:** Decoupled asynchronous architecture preventing UI thread starvation during heavy directory traversal or large-scale file purges.

---

## 2. Scope

### In-Scope
- **Desktop Application:** Complete functionality across all 14 WPF Views and ViewModels (Overview, Scanner, Largest Files, Largest Folders, File Categories, Old Files, Duplicate Files, Developer Storage, Junk Cleaner, Treemap, Query Explorer, Cleanup Center, Scan History, Settings).
- **Scan Engines:** Bounded-channel BFS traversal engine (`ScannerService.cs`) and NTFS USN Change Journal incremental traversal (`UsnJournalService.cs`).
- **Database Subsystem:** SQLite WAL mode persistence, batch inserts, multi-drive index scoping, and query filtering (`DatabaseService.cs`).
- **Installer & Deployment:** Inno Setup 6 packaging (`installer/installer.iss`), fresh install, upgrade, uninstall, and shortcut generation.
- **Web Portal:** Official site (`site/index.html`, `site/app.js`, `site/style.css`), live canvas simulations, and Web Crypto SHA-256 verification.
- **Traceability:** Complete coverage of all 44 features identified in the audit (`FEAT-01` through `FEAT-44`).

### Out-of-Scope
- macOS, Linux, or Wine/Proton compatibility (ArborGraph is strictly native Windows WPF).
- Network protocol scanning beyond standard mounted SMB/UNC paths.

---

## 3. Risk Priorities & Blocker Tiers

| Priority Tier | Definition | Release Action |
| :--- | :--- | :--- |
| **P0 / BLOCKER** | Critical defects causing data loss, incorrect multi-drive index wiping, unhandled application crashes, unmanaged memory violations, or installer deployment failures. | **Must be 0.** Halts release immediately. |
| **P1 / CRITICAL** | Major functional failures, severe memory scaling bottlenecks, UI thread starvation (>2s), or inaccurate deduplication/rollup math. | **Must be 0.** Must be resolved before v1.0.0 sign-off. |
| **P2 / MAJOR** | Non-crashing functional errors with existing workarounds, minor export formatting bugs, or non-critical permission handling flaws. | Max allowable: 3, with documented release notes. |
| **P3 / MINOR** | Cosmetic UI glitches, minor typography discrepancies, or minor documentation inaccuracies. | Permitted for post-release patches. |

### Top Audit Risk Areas to Verify (P0/P1 Candidates)
1. **Multi-drive ClearIndex behavior:** In `DatabaseService.cs`, root paths (`C:`) triggering full wipes and destroying previously indexed drives (`D:`).
2. **Permanent deletion:** Confirmation dialog safety, absolute path bounds, and read-only file handling.
3. **Recycle Bin deletion:** Recovery guarantees via `IFileOperation` / shell API.
4. **Synchronous deletion on UI thread:** Freezing windows during bulk deletions in `LargestFoldersViewModel` and `LargestFilesViewModel`.
5. **USN Journal native memory handling:** P/Invoke buffer bounds and pointer arithmetic in `UsnJournalService.cs`.
6. **Scanner crashes & freezes:** Locked files (`FileShare.None`), permissions, and circular reparse points.
7. **Database integrity & lock contention:** SQLite concurrency between writer batches and UI readers.
8. **Large-scale directory rollup:** Large Object Heap (LOH) allocations and GC pressure in `BuildDirectoryRollup`.
9. **Installer metadata & URL drift:** Outdated repository URLs in Inno Setup.
10. **Scanner accuracy:** Exact numerical equivalence between disk state and displayed metrics.

---

## 4. Test Environments

| Environment ID | Platform & Hardware Specifications | Role & Target Focus | Status |
| :--- | :--- | :--- | :--- |
| **ENV-A** | **Developer Reference PC:** Windows 11 (24H2), AMD Ryzen 7 / Intel Core i7, 32 GB RAM, NVMe PCIe 4.0 SSD | High-throughput baseline, unit & integration tests, rapid regression | Available |
| **ENV-B** | **Mid-Range Commercial PC:** Windows 10 (22H2), Intel Core i5 (8th–11th Gen), 16 GB RAM, SATA SSD | Standard enterprise/consumer target, Win10 theme rendering | Planned |
| **ENV-C** | **Resource-Constrained Laptop:** Windows 10/11, Intel Core i3, 8 GB RAM, 5400 RPM Mechanical HDD | High disk latency, LOH rollup memory scaling, low-spec responsiveness | Planned |
| **ENV-D** | **Clean Virtual Machine:** Windows 11 Home, fresh OS install, zero dev tools/.NET SDKs | Clean install, missing dependency validation, lowest-privilege test | Planned |
| **ENV-E** | **macOS / Linux (Wine/Proton):** Non-Windows systems | Out of scope (Strictly Windows native) | N/A |

---

## 5. Controlled Test Datasets

To ensure numerical correctness, ArborGraph must be validated against mathematically defined datasets with known reference values:

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

## 6. Performance & Scalability Strategy

1. **Benchmark Tiers:** Measure scanner performance on datasets of **10K, 50K, 100K, 250K, 500K, and 1M+** files.
2. **Benchmark Repetitions:** Run each tier a minimum of **3 times**; calculate **Minimum, Maximum, and Average** throughput.
3. **Recorded Metrics:** Total elapsed scan time, files/second throughput, peak RAM (MB), average CPU (%), and SQLite database size on disk.
4. **Target Ceilings:**
   - Scan Throughput: $\ge 15,000$ files/second on modern NVMe SSD.
   - Peak Working Set RAM: $\le 350\text{ MB}$ under 100K files; $\le 650\text{ MB}$ under 500K files.
   - Memory Leak Policy: Zero unmanaged handle growth or progressive memory climbing across back-to-back rescans.
5. **Rollup Scaling:** Test `BuildDirectoryRollup` specifically on directory counts of 10K, 50K, 100K, and 250K to benchmark GC Gen 2 and LOH allocation behavior.

---

## 7. Compatibility Strategy

1. **Display Scaling Matrix:** Validate all custom graphics (`RealtimeMetricGraph`, `TreemapView`, storage cards) at:
   - **100% (96 DPI):** Standard 1080p monitor.
   - **125% (120 DPI):** 14"–15" laptop screens.
   - **150% (144 DPI):** 13" laptops and 27" 4K displays.
   - **175% (168 DPI):** High-density laptops.
   - **200% (192 DPI):** Microsoft Surface and compact 4K screens.
2. **Storage Media Types:** Verify operations on NVMe SSD, SATA SSD, 5400 RPM mechanical HDD, USB flash drive (exFAT/FAT32), BitLocker-encrypted partitions, and mounted SMB network shares.
3. **Cloud Files-on-Demand:** Explicitly ensure OneDrive/iCloud reparse points (`FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS`) do not trigger background downloading of online files.

---

## 8. Automation Strategy

| Test Domain | Automation Feasibility | Recommended Tool / Framework | Rationale |
| :--- | :--- | :--- | :--- |
| **Functional Traversal & Database** | 100% Automatable | .NET Integration Harness (`tests/DiskScope.Tests.csproj`) | Fast, headless, deterministic assertions on SQLite state and file counters. |
| **Deduplication & Rollup Math** | 100% Automatable | .NET Unit Tests / xUnit | Pure algorithmic validation against synthetic collision datasets. |
| **Query Engine & Exporters** | 100% Automatable | .NET Integration Harness | Verifies SQL generation, pagination limits, and streaming CSV/JSON syntax. |
| **Web Portal & Verification Tool** | 90% Automatable | Playwright / Headless Chromium | Automated testing of canvas sparklines and Web Crypto SHA-256 hashing. |
| **Shell & Explorer Context Menus** | Manual Only | Manual User Testing | Depends on Windows Shell desktop COM interactions. |
| **High-DPI Visual Layouts** | Manual Only | Multi-Monitor Manual Walkthrough | Evaluates visual clipping and WPF vector rendering quality. |
| **Inno Setup Installer** | Hybrid | PowerShell Silent CLI (`/VERYSILENT`) + Manual Wizard | Verifies silent exit codes automatically, GUI EULA manually. |
| **Usability & UX Fluidity** | Manual Only | Think-Aloud User Observation | Measures human intuition and task completion friction. |

---

## 9. Testing Standards & Reference Approach

- **ISO/IEC 25010:2023 Quality Model:** Test execution is evaluated across the 8 standard product quality characteristics: Functional Suitability, Performance Efficiency, Compatibility, Usability, Reliability, Security, Maintainability, and Portability.
- **ISO/IEC/IEEE 29119 Alignment:** Testing processes follow standardized test case specification formats, test data decoupling, concrete verification steps, and formal incident logging.
