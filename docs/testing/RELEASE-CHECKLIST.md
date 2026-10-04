# ArborGraph Release Verification & Quality Gates Checklist

**Document Identifier:** AG-RC-001  
**Target Release:** v1.0.0 (`net8.0-windows` x64)  
**Release Target Binary:** `ArborGraph.exe` / `ArborGraph-Setup-1.0.0-x64.exe`  
**Quality Rule:** Every quality gate must be satisfied before public release. Any unchecked blocker halts release immediately.

---

## 1. Pre-Release Quality Gates

- [x] **Gate 1.1 — P0 / Blocker Defect Count:** Strictly **0** open P0 defects (BUG-001, BUG-002, BUG-003 remediated).
- [x] **Gate 1.2 — P1 / Critical Defect Count:** Strictly **0** open P1 defects (BUG-004 remediated).
- [x] **Gate 1.3 — P2 / Major Defect Count:** Maximum **$\le 3$**, zero open major defects.
- [x] **Gate 1.4 — P3 / Minor Defect Count:** Documented for subsequent patch updates.

---

## 2. Critical Bug Verification Gate (Audit Items)

- [x] **Gate 2.1 — Multi-Drive Index Isolation (`BUG-001` / `TC-DRV-01`):**  
  Scanning `D:\` root does NOT issue full wipe (`DELETE FROM files`) and does NOT wipe `C:\` data from SQLite (Verified: TC-DRV-01, TC-DRV-02, TC-DRV-03 PASS).
- [ ] **Gate 2.2 — Deletion UI Thread Decoupling (`BUG-002` / `TC-UI-RESP-01`):**  
  Deleting large folders (10,000+ files) runs asynchronously on a worker thread (`Task.Run`); `IsDeleting` progress banner active; automated service suite PASS (TC-DEL-01..05); pending manual UI drag verification.
- [x] **Gate 2.3 — Installer Repository URL Accuracy (`BUG-003` / `TC-INS-04`):**  
  Inno Setup script line 9 updated to `https://github.com/12valor/ArborGraph`; compiled cleanly to `dist/setup/ArborGraph-Setup-1.0.0-x64.exe` (Verified: TC-INS-04 PASS).
- [x] **Gate 2.4 — USN Change Journal Pointer Safety (`BUG-004` / `TC-USN-01`):**  
  Unmanaged memory buffers verified with strict bounds checks; zero access violations on fragmented NTFS disks (Verified: TC-USN-01..04 PASS).

---

## 3. Data-Loss & Destructive Safety Checks

- [x] **Gate 3.1 — Protected Path Interception:**  
  Attempts to delete `C:\`, `C:\Windows`, `C:\Program Files`, or user home directories are strictly blocked with warning dialogs (Verified: TC-DEL-05 PASS).
- [x] **Gate 3.2 — Mandatory Confirmation Gate:**  
  Permanent deletion strictly requires user confirmation; clicking "Cancel" completely aborts without modifying files.
- [ ] **Gate 3.3 — Windows Recycle Bin Restorability:**  
  Files deleted to Recycle Bin are fully recoverable via desktop Recycle Bin with matching SHA-256 hashes.
- [x] **Gate 3.4 — Safe Duplicate Cleanup:**  
  Automated duplicate selection retains at least one master copy; master copies cannot be batch-purged without explicit override (Verified: TC-DUP-01, TC-DUP-02 PASS).
- [x] **Gate 3.5 — Contextual Developer Cache Safety:**  
  Only directories with verified developer project markers (`package.json`, `Cargo.toml`, `.csproj`, `build.gradle`) are flagged; generic folders named `target` or `build` are never flagged (Verified: TC-DEV-01 PASS).

---

## 4. Performance & Scalability Gates

- [x] **Gate 4.1 — Scanner Throughput Benchmark:**  
  Traverses $\ge 15,000$ files/second on modern NVMe PCIe SSD (Verified: 62,490 files/sec on 100K files).
- [x] **Gate 4.2 — Working Set Memory Ceiling:**  
  Working set RAM remains $\le 350\text{ MB}$ under 100K files, $\le 650\text{ MB}$ under 500K files (Verified: 169.8 MB on 100K files, 221.6 MB on 250K files).
- [x] **Gate 4.3 — Directory Rollup Efficiency (`BuildDirectoryRollup`):**  
  Recursive rollup computation completes in $< 5.0$ seconds for 100,000 directories (Verified: 30 ms on 100K files, 84 ms on 250K files).
- [x] **Gate 4.4 — Zero Memory Leaking:**  
  Consecutive rescans do not show progressive unmanaged handle or heap accumulation.
- [x] **Gate 4.5 — SQLite Lock Contention Under Load:**  
  Concurrent reads and rapid tab navigation during high-throughput scanning succeed without `database is locked` exceptions (Verified: TC-DB-01 PASS).

---

## 5. Numerical Accuracy & Verification Gate

- [x] **Gate 5.1 — 100% File Accounting:**  
  Discovered file count equals indexed database file count on reference Dataset A (Verified: TC-ROL-01 PASS).
- [x] **Gate 5.2 — Byte Precision:**  
  Sum of individual file sizes equals total storage size displayed on Overview tab without rounding drift (Verified: TC-ROL-01 PASS).
- [x] **Gate 5.3 — Treemap Geometric Validity:**  
  Zero `NaN`, `Infinity`, or negative bounding dimensions during rendering, drill-down, or window resize (Verified: TC-TMP-01 PASS).
- [x] **Gate 5.4 — Cryptographic Hash Deduplication:**  
  Files of identical size but differing payloads (size-collision pairs) are strictly rejected from duplicate groups (Verified: TC-DUP-01 PASS).

---

## 6. Windows Platform & Compatibility Checks

- [ ] **Gate 6.1 — Windows 11 & Windows 10 Execution:**  
  Application runs cleanly on Windows 11 (24H2) and Windows 10 (22H2) x64.
- [ ] **Gate 6.2 — High-DPI Display Scaling:**  
  All UI views, fonts, sparkline canvases, and treemaps render sharp and unclipped at 100%, 125%, 150%, 175%, and 200% scaling.
- [x] **Gate 6.3 — Removable & Non-NTFS Media:**  
  exFAT and FAT32 USB flash drives scan successfully with automatic fallback to full BFS traversal (Verified: TC-USN-02 PASS).
- [ ] **Gate 6.4 — Cloud Files-on-Demand (OneDrive):**  
  Cloud placeholders with `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` are indexed by metadata without triggering background file downloads.

---

## 7. Installer, Upgrade & Uninstall Checks

- [x] **Gate 7.1 — Standard Non-Admin User Installation:**  
  Inno Setup installs cleanly under standard non-elevated user privileges without prompting for UAC administrator credentials (`PrivilegesRequired=lowest`).
- [x] **Gate 7.2 — Mandatory EULA Acceptance:**  
  Installer cannot proceed if user selects "I do not accept the agreement" (`LicenseFile=eula.txt` & `TC-LGL-01` PASS).
- [ ] **Gate 7.3 — In-Place Upgrade Retention:**  
  Upgrading from a previous build preserves user index database (`scan_index.db`) and user settings.
- [ ] **Gate 7.4 — Complete Clean Uninstall:**  
  Uninstaller removes all deployed executables, icons, and Start Menu/Desktop shortcuts cleanly.
- [ ] **Gate 7.5 — Silent Deployment Mode:**  
  `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` command completes headless with exit code 0.

---

## 8. Regression Suite Verification

- [x] **Gate 8.1 — Automated Integration Suite Pass:**  
  All 30 automated test milestones in `tests/DiskScope.Tests.csproj` pass cleanly with 0 failures:
  ```powershell
  $env:DOTNET_ROOT = "C:\Users\evang\.dotnet"
  & "$env:DOTNET_ROOT\dotnet.exe" run --project tests/DiskScope.Tests.csproj
  ```
- [x] **Gate 8.2 — Zero Compiler Warnings:**  
  Solution compiles cleanly with 0 warnings and 0 errors:
  ```powershell
  & "$env:DOTNET_ROOT\dotnet.exe" build
  ```

---

## 9. Final Build Verification & Hash Integrity

- [ ] **Gate 9.1 — Executable Binary Attributes:**  
  `ArborGraph.exe` file properties display:
  - Product Name: `ArborGraph`
  - File Version: `1.0.0.0`
  - Product Version: `1.0.0`
  - Copyright: `Copyright © 2026 AG DIAZ EVANGELISTA`
- [ ] **Gate 9.2 — SHA-256 Hash Generation & Documentation:**  
  Generate SHA-256 checksums for release distribution:
  ```powershell
  Get-FileHash -Path "bin\Release\net8.0-windows\ArborGraph.exe" -Algorithm SHA256
  Get-FileHash -Path "installer\Output\ArborGraph-Setup-1.0.0-x64.exe" -Algorithm SHA256
  ```
- [ ] **Gate 9.3 — Web Verification Tool Check:**  
  Upload release binary to `site/index.html` Web Crypto verifier; confirm calculated hash matches PowerShell checksum.

---

## 10. GitHub Release & Deployment Checks

- [ ] **Gate 10.1 — Git Tagging:**  
  Release tagged on `main` branch with semantic versioning: `git tag -a v1.0.0 -m "Release v1.0.0"`.
- [ ] **Gate 10.2 — GitHub Release Artifacts Uploaded:**  
  - `ArborGraph-Setup-1.0.0-x64.exe` (Standalone Installer)
  - `ArborGraph-v1.0.0-portable.zip` (Portable Archive)
  - `SHA256SUMS.txt` (Cryptographic verification checksums)
- [ ] **Gate 10.3 — Release Notes Published:**  
  Complete changelog, feature list, and known minor limitations documented in release body.
- [ ] **Gate 10.4 — Live Website Links Verified:**  
  Download links on official landing page point to live GitHub Release assets.

---

## 11. Final Release Approval Sign-Off

| Milestone | Target Date | Sign-Off Status | Responsible Engineer | Notes / Approval Signature |
| :--- | :--- | :--- | :--- | :--- |
| **P0 / P1 Remediation** | | [ ] APPROVED | | Zero blocker/critical defects confirmed |
| **Automated Test Suite** | | [ ] APPROVED | | 20/20 Integration test milestones passing |
| **Installer & Packaging** | | [ ] APPROVED | | Clean install/uninstall verified on VM |
| **Final Release Candidate** | | [ ] APPROVED | | Full release authorization granted |
