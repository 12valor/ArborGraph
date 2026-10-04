# ArborGraph Master QA Test Plan (v1.0.0)

**Document Identifier:** AG-TP-001  
**Target Product:** ArborGraph — Filesystem Analytics & Visualization  
**Target Release:** v1.0.0  
**Target Platform:** Windows 10 / Windows 11 (64-bit x64)  
**Methodological Alignment:**  
- **Quality Characteristics:** Structured according to ISO/IEC 25010:2023 product quality model  
- **Testing Process & Structure:** Structured according to ISO/IEC/IEEE 29119 test documentation standards  
*(Note: ArborGraph is an independent open-source project; this document adheres to these engineering standards as best-practice frameworks and does not claim formal accredited certification).*  

---

## 1. Introduction & Executive Context

### 1.1 Purpose
This Master Test Plan establishes the formal testing strategy, scope, quality gates, and evaluation criteria for validating ArborGraph prior to general release. It provides complete verification criteria for all 44 functional features, 4 release-blocking hazards, and performance boundaries identified during the codebase audit.

### 1.2 Core Quality Objectives
1. **Zero Data Loss:** Absolute guarantee that safe deletion, Recycle Bin operations, contextual developer cleaning, and multi-drive scans never corrupt, misplace, or unintendedly destroy user data.
2. **Absolute Numerical Accuracy:** Full statistical accounting of discovered vs. indexed files, mathematically exact recursive folder rollup, and zero false positives in cryptographic deduplication.
3. **Execution Stability:** Clean degradation under constrained permissions (standard non-admin users), unhandled exception interception, and robust locked-file bypass.
4. **UI Fluidity:** Asynchronous decoupled architecture preventing UI thread starvation during heavy traversal or large-scale file purges.

---

## 2. Test Areas & Quality Model (ISO/IEC 25010:2023)

### 2.1 Functional Suitability
- **Objective:** Verify that every user action, scan mode, query, visualization, and export behaves exactly as specified.
- **Scope:** All 44 features in the ArborGraph feature inventory.
- **What will be tested:**
  - Drive auto-discovery, custom directory targeting, and folder picker.
  - Bounded-channel BFS scanner and NTFS USN Change Journal incremental traversal.
  - SQLite data persistence, multi-criteria filtering, and paged results.
  - 3-stage cryptographic duplicate detection (Size -> MD5 -> SHA-256).
  - Contextual developer storage discovery (Node.js, .NET, Rust, Gradle, Python).
  - System/browser/application junk discovery and Adobe Photoshop asset cataloging.
  - Squarified treemap calculation and interactive drill-down.
  - Executive HTML, machine-readable JSON, and streamed CSV exports.
- **Test Method:** Automated integration tests (`tests/DiskScope.Tests.csproj`) supplemented by manual UI workflow validation.
- **Expected Evidence:** Test runner execution logs, SQLite table row asserts, and export file validation.
- **Pass/Fail Criteria:** 100% pass on all functional assertions; zero schema or query errors.

### 2.2 Performance Efficiency
- **Objective:** Evaluate scanner throughput, SQLite transaction latency, and memory consumption under increasing filesystem volume.
- **Scope:** Scan engine, database ingestion, `BuildDirectoryRollup`, and `RealtimeMetricGraph` rendering.
- **What will be tested:**
  - Files/second throughput across 10K, 50K, 100K, 250K, 500K, and 1M+ file trees.
  - SQLite WAL batch ingestion performance (5,000 records/batch).
  - `BuildDirectoryRollup` execution time and Large Object Heap (LOH) allocations across 10K, 50K, 100K, and 250K directories.
  - Memory consumption stability across extended (1+ hour) scan operations.
- **Test Method:** Automated benchmarking harness executing timed test runs with system metric capture.
- **Expected Evidence:** Benchmark summary tables recording min/max/average scan times, memory ceiling graphs, and GC collection statistics.
- **Pass/Fail Criteria:** Scan throughput >= 15,000 files/sec on SSD; `BuildDirectoryRollup` completes in < 5.0 seconds for 100K directories; peak RAM < 350 MB.

### 2.3 Reliability & Fault Tolerance
- **Objective:** Ensure the application recovers gracefully from unexpected operating system conditions and I/O interruptions.
- **Scope:** Filesystem I/O exceptions, safe cancellation, locked files, and database corruption.
- **What will be tested:**
  - Traversal cancellation at arbitrary points during scan execution.
  - Files locked by running processes (`FileShare.None`) during Junk Cleaner operations.
  - Inaccessible directories (`UnauthorizedAccessException`) such as `System Volume Information`.
  - SQLite database integrity check (`PRAGMA integrity_check`) after abrupt process kill.
  - Corrupted settings file (`settings.json`) auto-recovery to hardened defaults.
- **Test Method:** Fault-injection harnesses, simulated process locks, and explicit token cancellation.
- **Expected Evidence:** Verification that SQLite database passes `PRAGMA integrity_check = ok`; skipped items logged in `ScanLogVM`.
- **Pass/Fail Criteria:** Zero application crashes; zero database corruptions; locked files are safely bypassed.

### 2.4 Usability
- **Objective:** Validate that the user interface is intuitive, accessible, and provides clear operational feedback.
- **Scope:** Main navigation, tab switching, responsive resizing, and explanatory guidance.
- **What will be tested:**
  - Tab loading overlay and 10-second timeout guard behavior during heavy database queries.
  - Storage explanation narrative clarity on the Overview tab.
  - Treemap visual hierarchy, color categorization, and breadcrumb readability.
  - High-DPI display scaling (100%, 125%, 150%, 175%, 200%).
  - First-time user task completion for primary workflows.
- **Test Method:** Structured human usability testing protocols (see `docs/testing/USABILITY.md`) and UI STA thread layout tests.
- **Expected Evidence:** Usability scorecards, completion time logs, and visual inspection screenshots.
- **Pass/Fail Criteria:** First-time users complete core workflows without external guidance; zero UI clipping or rendering anomalies across DPI scales.

### 2.5 Security & Safety
- **Objective:** Ensure user data cannot be unintentionally destroyed or compromised.
- **Scope:** Destructive operations (`DeletePermanently`, `MoveToRecycleBin`), path validation, and unmanaged P/Invoke boundaries.
- **What will be tested:**
  - System directory deletion protection (`IsProtectedPath` protecting `C:\`, `C:\Windows`, `C:\Program Files`, `C:\Users\<Current>`).
  - Read-only file deletion and explicit modal confirmation behavior.
  - USN Journal native pointer boundary checks preventing unmanaged memory access violations.
  - Multi-drive `ClearIndex` scoping to ensure scanning `D:\` does not wipe index data for `C:\`.
- **Test Method:** Boundary testing on dummy filesystem trees, mock drive root arguments, and safety guard unit tests.
- **Expected Evidence:** Refusal logs for protected paths, Recycle Bin verification, and database state audits.
- **Pass/Fail Criteria:** Absolute zero unintended file deletions; zero access violation exceptions.

### 2.6 Compatibility & Portability
- **Objective:** Guarantee flawless operation across target Windows versions and storage hardware configurations.
- **Scope:** Windows 10 / Windows 11 x64 architectures, diverse storage media, and filesystem types.
- **What will be tested:**
  - Operating systems: Windows 10 (22H2), Windows 11 (23H2 / 24H2).
  - Filesystems: NTFS, FAT32, exFAT, and ReFS.
  - Storage types: NVMe SSD, SATA SSD, mechanical HDD, USB flash drives, and BitLocker volumes.
  - Cloud file placeholders: OneDrive / iCloud files with `RecallOnDataAccess` flags (verify hydration is prevented).
- **Test Method:** Cross-machine hardware test matrix (see `docs/testing/COMPATIBILITY.md`).
- **Expected Evidence:** Environment compatibility matrices, scan logs from diverse volumes, and network isolation telemetry.
- **Pass/Fail Criteria:** ArborGraph executes cleanly on all supported platforms; zero network downloads triggered for cloud files.

### 2.7 Maintainability & Testability
- **Objective:** Maintain clean MVVM separation, comprehensive logging, and test harness repeatability.
- **Scope:** Solution architecture, logging subsystem, and unit test compilation.
- **What will be tested:**
  - Isolated instantiation of all 14 WPF view models and views without static resource dependencies.
  - Diagnostic logging output in `%LOCALAPPDATA%\ArborGraph\app.log`.
  - Automated integration test execution repeatability via `start.bat test`.
- **Test Method:** Solution build automation and CI execution checks.
- **Expected Evidence:** Build logs without warnings, automated test run reports.
- **Pass/Fail Criteria:** 100% clean build under Release configuration; automated test suite passes on demand.

### 2.8 Installer & Deployment
- **Objective:** Validate clean installation, upgrading, shortcut deployment, and uninstallation.
- **Scope:** Inno Setup 6 installer (`installer/installer.iss`) and packaged binary (`ArborGraph.exe`).
- **What will be tested:**
  - Lowest-privilege user installation without administrator prompt.
  - EULA display and mandatory acceptance gate.
  - Clean upgrade from previous installations preserving user database.
  - Complete uninstallation removing application binaries without leaving orphaned files.
  - Correct assembly metadata, icon resolution, and Start Menu/Desktop shortcuts.
- **Test Method:** Automated silent install/uninstall sequences and manual setup wizard walkthroughs (see `docs/testing/INSTALLER-TESTS.md`).
- **Expected Evidence:** Windows Installed Apps registry entries, uninstaller execution logs, and file system diffs.
- **Pass/Fail Criteria:** Installer builds and runs without errors; uninstaller removes all deployed files; legacy settings migrate cleanly.

### 2.9 Web Portal & Diagnostic Simulation
- **Objective:** Verify official website functionality, diagnostic simulations, and binary verification links.
- **Scope:** `site/index.html`, `site/app.js`, and `site/style.css`.
- **What will be tested:**
  - Client-side scanner and sparkline canvas simulations matching desktop behavior.
  - Web Crypto SHA-256 hash verifier against release binaries.
  - Responsive layout across desktop, tablet, and mobile viewports.
  - Download links and external GitHub repository links.
- **Test Method:** Browser cross-compatibility validation (Edge, Chrome, Firefox) and mobile emulation.
- **Expected Evidence:** Browser console logs (zero errors), layout verification screenshots.
- **Pass/Fail Criteria:** Zero JavaScript errors; responsive design renders cleanly down to 360px viewport width; download URLs point to valid releases.

### 2.10 Regression Testing
- **Objective:** Prevent re-introduction of defects during pre-release bug fixing.
- **Scope:** Entire application test suite.
- **What will be tested:** Full automated regression suite executed before and after every bug remediation.
- **Test Method:** Full execution of `tests/DiskScope.Tests.csproj` and verification of all 20 automated milestones.
- **Expected Evidence:** Test suite output logs with zero failed assertions.
- **Pass/Fail Criteria:** Zero regressions in previously passing test scenarios.

---

## 3. Complete Feature Traceability Matrix

The following matrix maps every feature identified in the ArborGraph Feature Inventory (FEAT-01 through FEAT-44) to concrete test cases, risk classifications, and priority tiers:

| Requirement / Feature ID | Feature Name | Test Case IDs | Risk Classification | Priority | Test Method |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **FEAT-01** | System Drive Auto-Discovery | `TC-DRV-01`, `TC-DRV-02` | Low | High | Automated |
| **FEAT-02** | Custom Directory Target Selection | `TC-DRV-03`, `TC-DRV-04` | Medium | Medium | Automated / Manual |
| **FEAT-03** | Bounded-Channel BFS Traversal | `TC-SCN-01`, `TC-SCN-02`, `TC-SCN-03` | Critical | Critical | Automated |
| **FEAT-04** | NTFS USN Change Journal Scanner | `TC-USN-01`, `TC-USN-02`, `TC-USN-03`, `TC-USN-04` | Critical | High | Automated / Manual |
| **FEAT-05** | Real-Time Resource Telemetry | `TC-MON-01`, `TC-MON-02` | Medium | Medium | Automated |
| **FEAT-06** | Realtime Metric Graph Rendering | `TC-MON-03`, `TC-MON-04` | Medium | Medium | Automated / Manual |
| **FEAT-07** | Live Directory Rolling Feed | `TC-SCN-04`, `TC-SCN-05` | Low | Medium | Automated |
| **FEAT-08** | SQLite Database Storage & WAL Mode | `TC-DB-01`, `TC-DB-02`, `TC-DB-03` | Critical | Critical | Automated |
| **FEAT-09** | Database Integrity Verification | `TC-DB-04` | High | High | Automated |
| **FEAT-10** | Recursive Folder Rollup | `TC-ROL-01`, `TC-ROL-02`, `TC-ROL-03` | Critical | Critical | Automated |
| **FEAT-11** | Storage Explanation Narrative | `TC-ANL-01` | Low | Low | Automated |
| **FEAT-12** | Hierarchical Folder Drilldown | `TC-UI-01`, `TC-UI-02` | Medium | Medium | Automated / Manual |
| **FEAT-13** | Storage Growth Polyline Chart | `TC-ANL-02` | Medium | Medium | Automated / Manual |
| **FEAT-14** | Scan Session Tracking & Comparison | `TC-ANL-03`, `TC-ANL-04` | High | High | Automated |
| **FEAT-15** | File Age Temporal Breakdown | `TC-ANL-05` | High | High | Automated |
| **FEAT-16** | Multi-Criteria Storage Query | `TC-QRY-01`, `TC-QRY-02`, `TC-QRY-03` | Critical | Critical | Automated |
| **FEAT-17** | Streaming Multi-Criteria CSV Export | `TC-EXP-01` | High | High | Automated |
| **FEAT-18** | Largest Files Paged Explorer | `TC-FIL-01`, `TC-FIL-02` | High | High | Automated / Manual |
| **FEAT-19** | Rolled-Up Largest Folders Explorer | `TC-FOL-01`, `TC-FOL-02` | High | High | Automated / Manual |
| **FEAT-20** | File Category Breakdown & Viewer | `TC-CAT-01`, `TC-CAT-02` | Medium | Medium | Automated |
| **FEAT-21** | Old & Dormant Files Explorer | `TC-OLD-01`, `TC-OLD-02` | Medium | Medium | Automated |
| **FEAT-22** | Cryptographic Duplicate Detection | `TC-DUP-01`, `TC-DUP-02`, `TC-DUP-03` | Critical | Critical | Automated |
| **FEAT-23** | Duplicate Management & Elimination | `TC-DUP-04`, `TC-DUP-05` | High | High | Automated / Manual |
| **FEAT-24** | Contextual Developer Storage Discovery | `TC-DEV-01`, `TC-DEV-02` | High | High | Automated |
| **FEAT-25** | Developer Workspace Scanner | `TC-DEV-03`, `TC-DEV-04` | High | High | Automated / Manual |
| **FEAT-26** | Junk & Cache Target Scanner | `TC-JNK-01`, `TC-JNK-02` | High | High | Automated |
| **FEAT-27** | Locked-File Safe Deletion Fallback | `TC-SAF-01`, `TC-SAF-02` | Critical | Critical | Automated |
| **FEAT-28** | Photoshop & Media Asset Intelligence | `TC-PS-01`, `TC-PS-02` | Medium | Medium | Automated |
| **FEAT-29** | Adobe Scratch & Temp Cache Reclaimer | `TC-PS-03` | High | High | Automated / Manual |
| **FEAT-30** | Squarified Treemap Layout Engine | `TC-TMP-01`, `TC-TMP-02`, `TC-TMP-03` | High | High | Automated |
| **FEAT-31** | Interactive Treemap Node Drilldown | `TC-TMP-04`, `TC-TMP-05` | Medium | Medium | Manual |
| **FEAT-32** | Consolidated Storage Cleanup Center | `TC-CLN-01`, `TC-CLN-02` | High | High | Automated / Manual |
| **FEAT-33** | Executive HTML Storage Audit Export | `TC-EXP-02` | High | High | Automated |
| **FEAT-34** | Machine-Readable JSON Audit Export | `TC-EXP-03` | Medium | Medium | Automated |
| **FEAT-35** | Tabular CSV Exporters | `TC-EXP-04` | Medium | Medium | Automated |
| **FEAT-36** | Shell Integration & Explorer Actions | `TC-SHL-01`, `TC-SHL-02`, `TC-SHL-03` | High | High | Manual |
| **FEAT-37** | Windows Recycle Bin Deletion | `TC-DEL-01`, `TC-DEL-02` | Critical | Critical | Manual / Automated |
| **FEAT-38** | Permanent Deletion with Confirmation | `TC-DEL-03`, `TC-DEL-04` | Critical | Critical | Manual / Automated |
| **FEAT-39** | First-Run EULA Consent Dialog Gate | `TC-LGL-01`, `TC-LGL-02`, `TC-LGL-03` | Critical | Critical | Automated / Manual |
| **FEAT-40** | Settings & Exclusion Rules Persistence| `TC-SET-01`, `TC-SET-02` | High | High | Automated |
| **FEAT-41** | Diagnostic Scan Log Tracking | `TC-LOG-01` | Low | Low | Automated |
| **FEAT-42** | Global Unhandled Exception Handlers | `TC-ERR-01`, `TC-ERR-02` | Critical | Critical | Manual / Automated |
| **FEAT-43** | Inno Setup 6 Desktop Installer | `TC-INS-01`, `TC-INS-02`, `TC-INS-03` | Critical | Critical | Manual |
| **FEAT-44** | Web Portal & Diagnostic Simulation | `TC-WEB-01`, `TC-WEB-02`, `TC-WEB-03` | Medium | Medium | Manual |

---

## 4. Priority Tiers & Release Quality Gates

| Priority Tier | Definition | Release Blocking Rule |
| :--- | :--- | :--- |
| **P0 / BLOCKER** | Critical defects causing data loss, incorrect multi-drive index wiping, unhandled application crashes, unmanaged memory violations, or installer deployment failures. | **Must be 0.** Any P0 defect halts release immediately. |
| **P1 / CRITICAL** | Major functional failures, severe memory scaling bottlenecks, UI thread starvation (>2s), or inaccurate deduplication/rollup math. | **Must be 0.** Must be resolved before v1.0.0 sign-off. |
| **P2 / MAJOR** | Non-crashing functional errors with existing workarounds, minor export formatting bugs, or non-critical permission handling flaws. | Max allowable: 3, with documented release notes. |
| **P3 / MINOR** | Cosmetic UI glitches, minor typography discrepancies, or minor documentation inaccuracies. | Permitted for post-release patches. |

---

## 5. Test Documentation Architecture

Testing artifacts for ArborGraph are partitioned into dedicated, specialized specification documents:

1. [TEST-PLAN.md](file:///c:/Users/evang/Downloads/diskscope/docs/testing/TEST-PLAN.md) — *This document:* Master test strategy, quality characteristics, and feature traceability.
2. [TEST-CASES.md](file:///c:/Users/evang/Downloads/diskscope/docs/testing/TEST-CASES.md) — Concrete, executable test case definitions across all features and risk areas.
3. [TEST-DATA.md](file:///c:/Users/evang/Downloads/diskscope/docs/testing/TEST-DATA.md) — Specifications for controlled datasets (A through S) with verified reference values.
4. [INSTALLER-TESTS.md](file:///c:/Users/evang/Downloads/diskscope/docs/testing/INSTALLER-TESTS.md) — Inno Setup installation, upgrade, shortcut, and uninstallation verification.
5. [COMPATIBILITY.md](file:///c:/Users/evang/Downloads/diskscope/docs/testing/COMPATIBILITY.md) — Multi-machine, operating system, filesystem, and DPI scaling test matrix.
6. [USABILITY.md](file:///c:/Users/evang/Downloads/diskscope/docs/testing/USABILITY.md) — Protocol for human first-time user interaction testing.
7. [AUTOMATION-MATRIX.md](file:///c:/Users/evang/Downloads/diskscope/docs/testing/AUTOMATION-MATRIX.md) — Automation feasibility and recommended tooling framework.
8. [RELEASE-CRITERIA.md](file:///c:/Users/evang/Downloads/diskscope/docs/testing/RELEASE-CRITERIA.md) — Formal quality gate sign-off criteria for general release.
9. [BUG-REPORT-TEMPLATE.md](file:///c:/Users/evang/Downloads/diskscope/docs/testing/BUG-REPORT-TEMPLATE.md) — Standardized incident reporting template.
