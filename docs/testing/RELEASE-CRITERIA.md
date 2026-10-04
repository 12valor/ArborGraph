# ArborGraph Release Quality Acceptance Gates & Sign-Off Criteria

**Document Identifier:** AG-RC-001  
**Target Release:** v1.0.0  
**Target Product:** ArborGraph — Filesystem Analytics & Visualization  
**Governance Policy:** Formal sign-off requires unanimous satisfaction of all mandatory gate conditions. No waivers permitted for P0 or P1 defects.  

---

## 1. Quality Gate Summary Checklist

| Gate ID | Quality Gate Description | Mandatory Requirement | Release Status |
| :--- | :--- | :--- | :--- |
| **GATE-01** | **Unresolved P0 (Blocker) Defects** | **Must be strictly 0** | `PENDING EXECUTION` |
| **GATE-02** | **Unresolved P1 (Critical) Defects** | **Must be strictly 0** | `PENDING EXECUTION` |
| **GATE-03** | **Critical Functional Tests** | **100% Pass** on all functional test cases | `PENDING EXECUTION` |
| **GATE-04** | **Numerical & Mathematical Accuracy** | **100% Exact Match** against reference datasets | `PENDING EXECUTION` |
| **GATE-05** | **Zero Data-Loss Verification** | Verified zero unintended deletions, safe Recycle Bin | `PENDING EXECUTION` |
| **GATE-06** | **Zero Normal-Workflow Crashes** | Zero unhandled exceptions or access violations | `PENDING EXECUTION` |
| **GATE-07** | **Memory & Resource Stability** | Peak RAM < 350 MB on 100K files; no unbounded leaks | `PENDING EXECUTION` |
| **GATE-08** | **UI Responsiveness (No Window Hangs)**| Zero UI thread freezes > 1.0s during deletions/queries | `PENDING EXECUTION` |
| **GATE-09** | **Inno Setup Installer Deployment** | Clean install, upgrade, and uninstall verified | `PENDING EXECUTION` |
| **GATE-10** | **Release Binary Integrity** | Version `1.0.0`, icons, metadata, and SHA-256 verified | `PENDING EXECUTION` |
| **GATE-11** | **Distribution Links Verification** | Release download URLs point to `12valor/ArborGraph` | `PENDING EXECUTION` |
| **GATE-12** | **Documented P2 / P3 Known Issues** | Comprehensive release notes documenting minor issues | `PENDING EXECUTION` |

---

## 2. Detailed Release Gate Specifications

### Gate 1 & 2: Defect Tolerance Thresholds
- **P0 / BLOCKER: Max allowable = 0.** Any defect involving data loss, incorrect index wiping, unmanaged memory crashes, or failure to install blocks release packaging immediately.
- **P1 / CRITICAL: Max allowable = 0.** Any defect involving inaccurate file accounting, UI thread freezing > 2.0s, broken core navigation, or duplicate false positives must be remediated.
- **P2 / MAJOR: Max allowable = 3.** Non-crashing edge cases with existing workarounds (e.g. specialized UNC path quirks, minor export formatting) are permitted only if documented in release notes.
- **P3 / MINOR: Max allowable = 10.** Minor cosmetic or typographical defects.

### Gate 4: Numerical & Mathematical Accuracy Standards
ArborGraph cannot be approved merely because "the scan completed." The following assertions must hold:
1. `FilesDiscovered == FilesIndexed + FilesSkipped`
2. `DirectoriesVisited == DirectoriesProcessed + DirectoriesSkipped`
3. Sum of category bytes equals total logical indexed bytes ($\pm 0$ bytes).
4. Sum of age bucket files equals total indexed files ($\pm 0$ files).
5. Rolled-up directory size matches the sum of descendant file sizes.
6. Treemap canvas area equals the sum of component item rectangles ($\pm 1.0$ pixel).

### Gate 5: Data Safety & Destructive Operations
1. `FileActionService.IsProtectedPath` must prevent deletion of all system roots.
2. `MoveToRecycleBin` must place items in the Windows Recycle Bin with verified ability to restore.
3. `DeletePermanently` must prompt with explicit warning modal dialog before unlinking.
4. Scanning secondary drives must never clear or alter SQLite records belonging to other volumes.
5. In-use locked files must be safely skipped during cleaning operations without raising unhandled I/O exceptions.

### Gate 8: UI Fluidity & Responsiveness
1. Deleting a directory containing up to 25,000 files must not freeze the application window or trigger Windows "Not Responding" state.
2. Background scanner throughput must not starve the UI thread dispatcher below 30 FPS.
3. Switching tabs during an active scan must resolve within 500ms without SQLite lock timeouts.

### Gate 10: Binary Packaging & Cryptographic Integrity
1. Assembly product title: `ArborGraph Filesystem Analytics & Visualization`.
2. Assembly version: `1.0.0.0`.
3. Application icons: Valid 256x256 embedded icons with green circular tree emblem.
4. Standalone binary `ArborGraph.exe` signed or checksummed with SHA-256 hash published in release notes.
5. Inno Setup installer `ArborGraph-Setup-1.0.0-x64.exe` compiles cleanly and executes with lowest privileges.

---

## 3. Formal Sign-Off Authorization Template

| Quality Role | Evaluator Name | Signature / Sign-Off Date | Decision |
| :--- | :--- | :--- | :--- |
| **Lead QA Engineer** | `_______________________` | `_______________________` | `[ ] ACCEPT  [ ] REJECT` |
| **Core Systems Developer** | `_______________________` | `_______________________` | `[ ] ACCEPT  [ ] REJECT` |
| **Release Manager** | `_______________________` | `_______________________` | `[ ] ACCEPT  [ ] REJECT` |
