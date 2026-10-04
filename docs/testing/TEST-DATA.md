# ArborGraph Controlled Test Datasets Specification

**Document Identifier:** AG-TD-001  
**Target Product:** ArborGraph — Filesystem Analytics & Visualization  
**Target Release:** v1.0.0  
**Purpose:** Provides mathematically defined, reproducible filesystem datasets with known expected reference metrics to validate numerical correctness, edge-case resilience, and performance boundaries.  

---

## Controlled Datasets Reference Matrix

| Dataset ID | Name | Approximate File Count | Directory Count | Total Logical Bytes | Purpose | Expected Reference Result |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **DATA-A** | Small Filesystem | 15 files | 4 directories | 228,891 bytes (~223.5 KB) | Baseline sanity & accounting check | Exactly 15 files, 4 dirs, 228,891 bytes indexed; 100% accounting |
| **DATA-B** | Medium Filesystem | 10,000 files | 500 directories | ~2.00 GB | General query & sorting validation | Exactly 10,000 files; paged results match sorting; rollup totals accurate |
| **DATA-C** | Large Filesystem | 100,000 files | 5,000 directories | ~45.0 GB | Scale, indexing & LOH rollup benchmark | Throughput >= 15,000 files/sec; rollup time < 5.0s; peak RAM < 350 MB |
| **DATA-D** | Many Small Files | 50,000 files | 500 directories | 50,000,000 bytes (50 MB) | High IOPS & batch insertion test | Channel writer does not stall; batches of 5,000 persist without timeouts |
| **DATA-E** | Large Files | 5 files | 2 directories | 75,000,000,000 bytes (~69.8 GB) | Multi-gigabyte single file handling | 64-bit byte counters do not overflow; largest file accurately displays |
| **DATA-F** | Deep Directory Trees | 100 files | 30 nested levels | 1,024,000 bytes (~1.0 MB) | Path length > 260 chars (MAX_PATH) | Paths > 260 chars indexed cleanly; no `PathTooLongException` |
| **DATA-G** | Duplicate Files | 12 files (4 groups + collisions) | 3 directories | 4,194,304 bytes (4.0 MB) | 3-stage cryptographic validation | Exactly 4 duplicate groups found; size collisions rejected; zero false positives |
| **DATA-H** | Unicode Filenames | 20 files | 5 directories | 204,800 bytes (200 KB) | International character set handling | Japanese, Arabic, Cyrillic, Emoji names render cleanly without mojibake |
| **DATA-I** | Special Characters | 25 files | 4 directories | 150,000 bytes (~146 KB) | SQL injection & path parser safety | Names with `#`, `%`, `&`, `[`, `]`, `'`, spaces, leading dots query cleanly |
| **DATA-J** | Empty Folders | 0 files | 50 nested directories | 0 bytes | Empty tree traversal robustness | Visited dirs = 50; files indexed = 0; logical bytes = 0; no null exceptions |
| **DATA-K** | Inaccessible Folders | 10 files (5 inaccessible) | 4 directories (2 locked) | Known accessible bytes | Permission denial graceful handling | Accessible indexed; locked recorded in SkippedDirectories; zero crashes |
| **DATA-L** | Locked Files | 6 files (2 locked with FileShare.None) | 2 directories | 120,000 bytes | In-use file deletion safety | Cleaning deletes 4 unlocked; safely skips 2 locked; reports `FilesSkipped = 2` |
| **DATA-M** | Volatile Filesystem | 500 files fluctuating | 10 directories | Fluctuating bytes | Files added/deleted during BFS scan | Scanner traps `FileNotFoundException` as skipped file; completes cleanly |
| **DATA-N** | Developer Projects | 1,500 files | 80 directories | 120,000,000 bytes (~114 MB) | Contextual artifact detection | Node.js, .NET, Rust detected; fake `target` and `build` folders rejected |
| **DATA-O** | Creative & Media Assets | 35 files | 6 directories | 1,500,000,000 bytes (~1.4 GB) | Photoshop & scratch intelligence | PSD/PSB counted separately; `Photoshop Temp*` flagged as reclaimable |
| **DATA-P** | Multi-Drive Targets | 2,000 files across C: and D: | 40 directories | 500,000,000 bytes (~476 MB) | Drive scoping & ClearIndex safety | Scanning D: preserves C: index records; both queryable in analytics |
| **DATA-Q** | NTFS Volume | Standard NTFS disk | Multiple dirs | Arbitrary | USN journal query and checkpointing | USN checkpoint persists; incremental changes reflect in sub-second time |
| **DATA-R** | Non-NTFS Volume (exFAT/FAT32) | USB drive or VHD | Multiple dirs | Arbitrary | Filesystem detection fallback | `IsNtfsVolume` returns false; fallback to full scan; zero P/Invoke errors |
| **DATA-S** | Cloud Placeholders | 50 placeholder files | 5 directories | Online-only attributes | Network isolation / hydration check | `RecallOnDataAccess` files skipped; zero network bytes consumed |

---

## Detailed Dataset Specifications & Construction Guidelines

### DATA-A: Small Filesystem (Baseline Reference)
- **Path:** `%TEMP%\ArborGraph_DataA_<guid>`
- **Directory Structure:**
  - `FolderA/doc1.txt` (1,024 bytes)
  - `FolderA/doc2.pdf` (2,048 bytes)
  - `FolderA/script.cs` (13 bytes)
  - `FolderA/zero_byte.dat` (0 bytes)
  - `FolderA/NO_EXTENSION_FILE` (14 bytes)
  - `FolderA/résumé_2026_test.docx` (4,096 bytes)
  - `PhotoshopProjects/artwork.psd` (8,192 bytes)
  - `PhotoshopProjects/banner_huge.psb` (16,384 bytes)
  - `PhotoshopProjects/brushes.abr` (512 bytes)
  - `FolderB/NestedB/dup1.bin` (65,536 bytes)
  - `FolderB/NestedB/dup2.bin` (65,536 bytes, identical payload to dup1)
  - `FolderB/NestedB/collision_a.bin` (32,768 bytes, random seed 101)
  - `FolderB/NestedB/collision_b.bin` (32,768 bytes, random seed 202)
- **Reference Totals:**
  - Total Files: 13 regular files
  - Total Directories: 4 subdirectories + 1 root = 5 directories
  - Total Logical Bytes: 228,891 bytes
  - Photoshop Files: 1 PSD (8 KB), 1 PSB (16 KB), 1 Other (512 B)
  - Duplicate Groups: Exactly 1 group (65,536 bytes, 2 copies, 65,536 wasted bytes)
  - Age Distribution: 100% in `< 7 Days` bucket

---

### DATA-G: Cryptographic Duplicates & Size Collisions
- **Path:** `%TEMP%\ArborGraph_DataG_<guid>`
- **Structure:**
  - Group 1 (Exact Match, 64 KB): `group1_copy1.dat` and `group1_copy2.dat` (SHA-256: `A1B2...`)
  - Group 2 (Exact Match, 1 MB): `group2_copy1.bin`, `group2_copy2.bin`, `group2_copy3.bin` (3 copies)
  - Group 3 (Zero Byte Match): `empty1.txt`, `empty2.txt` (Must be ignored or handled cleanly)
  - Collision Pair 1 (32 KB): `col1_a.dat` and `col1_b.dat` (Identical size, differing byte 0)
  - Collision Pair 2 (128 KB): `col2_a.dat` and `col2_b.dat` (Identical first 4KB, differing middle)
  - Collision Pair 3 (256 KB): `col3_a.dat` and `col3_b.dat` (Identical first 4KB and last 4KB, differing middle)
- **Reference Totals:**
  - Confirmed Duplicate Groups: Exactly 2 groups (Group 1 and Group 2)
  - Wasted Space: $65,536 + (2 \times 1,048,576) = 2,162,688$ bytes
  - Collision Pairs Rejected: 3 pairs rejected by MD5 or SHA-256

---

### DATA-N: Developer Projects Context Verification
- **Path:** `%TEMP%\ArborGraph_DataN_<guid>`
- **Structure:**
  - `proj_node/package.json` + `proj_node/node_modules/dep.js` (40 KB) -> **MUST BE DETECTED** (Node.js)
  - `proj_dotnet/app.csproj` + `proj_dotnet/bin/app.dll` (100 KB) -> **MUST BE DETECTED** (.NET)
  - `proj_rust/Cargo.toml` + `proj_rust/target/app.exe` (200 KB) -> **MUST BE DETECTED** (Rust)
  - `proj_gradle/build.gradle` + `proj_gradle/build/app.jar` (80 KB) -> **MUST BE DETECTED** (Gradle)
  - `customer-targets/target/report.pdf` (30 KB, NO Cargo.toml) -> **MUST BE REJECTED** (Generic folder)
  - `architectural-build/build/blueprint.dwg` (60 KB, NO build.gradle) -> **MUST BE REJECTED** (Generic folder)
- **Reference Totals:**
  - Detected Ecosystems: Node.js (1 item), .NET (1 item), Rust (1 item), Gradle (1 item)
  - False Positive Count: Strictly 0

---

### DATA-S: Cloud File Placeholders (OneDrive Emulation)
- **Path:** `%USERPROFILE%\OneDrive - Test\Placeholders`
- **File Attributes Applied via Win32 `SetFileAttributes`:**
  - `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` (`0x00400000`)
  - `FILE_ATTRIBUTE_RECALL_ON_OPEN` (`0x00040000`)
- **Structure:** 50 files with total apparent logical size of 10.0 GB.
- **Reference Totals:**
  - Network Traffic During Scan: Exactly 0 bytes
  - Local Disk Free Space Delta: 0 bytes (no hydration)
  - Scanner Behavior: Bypassed without blocking or network timeout
