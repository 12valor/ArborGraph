# ArborGraph Formal Incident & Bug Report Template

**Document Identifier:** AG-BR-TMPL  
**Standard Compliance:** Aligned with ISO/IEC/IEEE 29119-3 incident reporting specification  
**Usage Instructions:** Use this template to file any functional defect, performance regression, data safety hazard, or visual anomaly discovered during ArborGraph testing.  

---

## Incident Report Header

```
BUG ID:           AG-BUG-[YYYYMMDD]-[001]
TITLE:            [Concise, unambiguous summary of the defect]
DATE REPORTED:    YYYY-MM-DD
SEVERITY:         [ ] P0 - Blocker  [ ] P1 - Critical  [ ] P2 - Major  [ ] P3 - Minor
STATUS:           [ ] New  [ ] Triaged  [ ] In Progress  [ ] Resolved  [ ] Verified  [ ] Closed
ASSIGNED TO:      [Developer Name / Team]
```

---

## Environment Information

```
ARBORGRAPH VERSION:  1.0.0 (Commit: [Git Commit Hash])
EXECUTION MODE:      [ ] Standalone Release EXE  [ ] Installed App  [ ] Debug Console
OPERATING SYSTEM:    Windows 10 / Windows 11 (Version: [e.g. 23H2], Build: [e.g. 22631])
USER PRIVILEGES:     [ ] Standard User (Non-Admin)  [ ] Elevated Administrator
CPU & ARCHITECTURE:  [e.g. Intel Core i7-12700H, AMD Ryzen 7 5800X, 64-bit x64]
RAM CAPACITY:        [e.g. 8 GB, 16 GB, 32 GB]
DISPLAY SCALING:     [ ] 100%  [ ] 125%  [ ] 150%  [ ] 175%  [ ] 200%
TARGET STORAGE:      [ ] NVMe SSD  [ ] SATA SSD  [ ] Mechanical HDD  [ ] USB exFAT  [ ] Other:
FILESYSTEM FORMAT:   [ ] NTFS  [ ] exFAT  [ ] FAT32  [ ] ReFS
```

---

## Defect Classification

```
FEATURE COMPONENT:
[ ] 01. Drive Auto-Discovery        [ ] 07. Treemap Visualization
[ ] 02. Filesystem Scanner Engine   [ ] 08. Duplicate Detection
[ ] 03. NTFS USN Journal Scanner    [ ] 09. Developer Storage
[ ] 04. SQLite Database & Index     [ ] 10. Junk Cleaner
[ ] 05. Directory Rollup Engine     [ ] 11. Reporting & Exports
[ ] 06. Multi-Criteria Filtering    [ ] 12. Installer & Packaging

DEFECT TYPE:
[ ] Data Loss Hazard       [ ] UI Thread Freeze / Lag   [ ] Numerical / Math Error
[ ] Application Crash      [ ] Permission / Access Error [ ] Visual / Layout Glitch
[ ] Memory Leak / Bloat    [ ] Regression               [ ] Other
```

---

## Detailed Replication Steps

### 1. Preconditions
*Specify system state, test dataset, active settings, and open windows prior to reproducing.*
- Example: Dataset C (100K files) indexed; user is on `Largest Folders` tab.

### 2. Steps to Reproduce
*Step-by-step instructions. Be specific, numbered, and deterministic.*
1. Launch ArborGraph.
2. Select target folder `C:\TestTree`.
3. Click `Start Scan`.
4. Navigate to `...`.
5. Perform action `...`.

### 3. Expected Result
*What the application should have done according to specifications.*
- Example: Folder is deleted asynchronously; window remains responsive; progress bar shows deletion status.

### 4. Actual Result
*What the application actually did.*
- Example: Application window freezes for 8 seconds; titlebar displays "(Not Responding)"; cursor shows wait spinner.

---

## Technical Diagnostics & Evidence

### Reproducibility Rate
- `[ ] Always (100%)`
- `[ ] Intermittent (~50%)`
- `[ ] Rare (< 10%)`
- `[ ] Observed Once`

### Diagnostic Log Excerpt (`%LOCALAPPDATA%\ArborGraph\app.log`)
```
[YYYY-MM-DD HH:mm:ss.fff] [Log Level] [Message]
[Stack trace or exception dump if applicable]
```

### Crash Information / Windows Event Viewer
```
Faulting Application Name: ArborGraph.exe
Exception Code:            0xC0000005 (Access Violation) / System.Exception
Faulting Module Name:      kernel32.dll / SQLitePCLRaw
```

### Visual Evidence & Screenshots
- *Attach or link screenshots illustrating UI anomalies, error dialogs, or memory profiler graphs.*
- `[Screenshot 1 Path / Link]`
- `[Screenshot 2 Path / Link]`

---

## Remediation & Root Cause Analysis (Internal QA / Dev Use)

```
ROOT CAUSE:
[Identified source code file, function, and root cause explanation]

FIX PROPOSED:
[Description of recommended architectural or code change]

REGRESSION TEST:
[Test ID in TEST-CASES.md to be executed to verify remediation]
```
