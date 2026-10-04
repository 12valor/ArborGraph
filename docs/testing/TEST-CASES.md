# ArborGraph Comprehensive Test Cases Specification

**Document Identifier:** AG-TC-001  
**Target Release:** v1.0.0  
**Test Suite Status:** NOT TESTED (Ready for Execution)  
**Execution Rule:** Status for all cases is set to `NOT TESTED`. Where prior verification exists, details are documented under `Notes` as `Previously observed / audit evidence`.  

---

## 1. Multi-Drive & Data Safety Test Suite (P0 / BLOCKER)

### Test ID: `TC-DRV-01`
- **Feature ID:** `FEAT-08` / `FEAT-01`
- **Title:** Multi-Drive Sequential Scan Data Isolation (ClearIndex Scoping Test)
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — ClearIndex in `DatabaseService.cs` treats any root with `length <= 2` and ending in `:` as a full wipe (`DELETE FROM files; DELETE FROM directories;`), potentially destroying index data of previously scanned drives.
- **Preconditions:** Host system has at least two accessible logical drives (e.g., `C:\` and `D:\` or virtual mounted drives).
- **Test Data:** Dataset P (Multi-drive mock trees with distinct marker files on each volume).
- **Steps:**
  1. Launch ArborGraph.
  2. Select target drive `C:\` only.
  3. Click `Start Scan` and wait for completion (`ScanState.Completed`).
  4. Query SQLite database: verify `SELECT COUNT(*) FROM files WHERE path LIKE 'C:%'` > 0. Record count as $N_C$.
  5. Uncheck `C:\` and select target drive `D:\`.
  6. Click `Start Scan` and wait for completion.
  7. Query SQLite database: verify `SELECT COUNT(*) FROM files WHERE path LIKE 'D:%'` > 0. Record count as $N_D$.
  8. Re-query `C:\` files in SQLite: `SELECT COUNT(*) FROM files WHERE path LIKE 'C:%'`.
  9. Navigate to Analytics view and inspect multi-drive totals.
- **Expected Result:**
  - Files discovered on `D:\` are indexed into SQLite.
  - Scanning `D:\` must **NOT** delete `C:\` data. `SELECT COUNT(*)` for `C:\` must remain exactly $N_C$.
  - Combined storage metrics reflect both `C:\` and `D:\`.
  - *(If `C:\` data is deleted, classify as RELEASE BLOCKING defect).*
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Code inspection of `DatabaseService.cs` line 185 shows `isFullClear` triggers on drive letter roots. Audit flagged this as Blocker 1.

---

### Test ID: `TC-DRV-02`
- **Feature ID:** `FEAT-01` / `FEAT-08`
- **Title:** Simultaneous Multi-Drive Root Scan
- **Priority:** P0 / BLOCKER
- **Risk:** High — Potential transaction rollback or channel deadlocks when ingesting multiple root drives simultaneously.
- **Preconditions:** Multiple drives selected in top toolbar.
- **Test Data:** Drives `C:\` and `D:\`.
- **Steps:**
  1. Check both `C:` and `D:` checkboxes in the toolbar drive strip.
  2. Click `Start Scan`.
  3. Monitor Scanner view throughput and live feed.
  4. Wait for scan completion.
  5. Inspect database records across both drives.
- **Expected Result:** Traversal processes both roots cleanly; SQLite index contains records from both roots; total byte count matches the combined logical storage of both targets.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Integration test 2 ran on single test root. Multi-root drive execution requires dedicated physical/virtual drive harness.

---

## 2. Deletion Safety & Destructive Operations Test Suite (P0 / P1)

### Test ID: `TC-DEL-01`
- **Feature ID:** `FEAT-37`
- **Title:** Windows Recycle Bin Safe Deletion & Recovery
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Deleting files must not permanently erase them if the user chose Recycle Bin.
- **Preconditions:** A known disposable test file exists on disk (`%TEMP%\delete_test_recycle.dat`).
- **Test Data:** 10MB test file with known MD5 hash.
- **Steps:**
  1. Index the test directory containing `delete_test_recycle.dat`.
  2. Open Largest Files view and locate `delete_test_recycle.dat`.
  3. Right-click the file and select `Move to Recycle Bin`.
  4. Confirm deletion dialog if prompted.
  5. Verify file is removed from disk path (`File.Exists` returns false).
  6. Verify record is removed from SQLite index.
  7. Open Windows Recycle Bin desktop folder.
  8. Restore `delete_test_recycle.dat` from Recycle Bin.
  9. Recompute MD5 hash of restored file.
- **Expected Result:**
  - File is safely moved to the Windows Recycle Bin without permanent destruction.
  - Restored file is byte-for-byte identical to original test file.
  - UI file grid updates immediately to remove deleted item.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `FileActionService.MoveToRecycleBin` uses `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` with fallback to `SHFileOperation(FOF_ALLOWUNDO)`.

---

### Test ID: `TC-DEL-02`
- **Feature ID:** `FEAT-37` / `FEAT-38`
- **Title:** System Protected Path Deletion Refusal (`IsProtectedPath`)
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Application must never allow deletion of drive roots, Windows OS directory, Program Files, or User profile roots.
- **Preconditions:** Application is running with standard or administrative permissions.
- **Test Data:** Protected paths: `C:\`, `C:\Windows`, `C:\Program Files`, `C:\Users\<CurrentUsername>`.
- **Steps:**
  1. Programmatically or via UI invoke `FileActionService.MoveToRecycleBin` on `C:\Windows`.
  2. Invoke `FileActionService.DeletePermanently` on `C:\`.
  3. Invoke `DeletePermanently` on `C:\Program Files`.
  4. Invoke `DeletePermanently` on `C:\Users\<CurrentUsername>`.
- **Expected Result:**
  - In all cases, `IsProtectedPath` returns `true`.
  - Deletion operation is completely aborted before executing any shell or filesystem API.
  - Safety Warning dialog is displayed to the user.
  - Target system paths remain completely intact.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `IsProtectedPath` code logic verified in audit report lines 143-176 of `FileActionService.cs`.

---

### Test ID: `TC-DEL-03`
- **Feature ID:** `FEAT-38`
- **Title:** Permanent Deletion Confirmation Enforcement
- **Priority:** P1 / CRITICAL
- **Risk:** High — Accidental permanent deletion without confirmation causes unrecoverable data loss.
- **Preconditions:** Disposable file exists on disk (`%TEMP%\delete_test_perm.dat`).
- **Test Data:** Test file.
- **Steps:**
  1. Select `delete_test_perm.dat` in Largest Files view.
  2. Right-click and choose `Delete Permanently`.
  3. In the confirmation dialog, click `No` (or press Escape).
  4. Verify file still exists on disk.
  5. Right-click again, choose `Delete Permanently`, and click `Yes`.
  6. Verify file is deleted from disk and index.
- **Expected Result:**
  - Clicking `No` cancels the operation with zero disk modification.
  - Clicking `Yes` permanently purges the file.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Modal `MessageBox.Show` with `MessageBoxButton.YesNo` is implemented in `FileActionService.DeletePermanently`.

---

### Test ID: `TC-DEL-04`
- **Feature ID:** `FEAT-38`
- **Title:** Read-Only Flag Reset on Permanent Deletion
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Deletion fails silently or crashes when target has `FileAttributes.ReadOnly`.
- **Preconditions:** Disposable file created and flagged with `FileAttributes.ReadOnly`.
- **Test Data:** Read-only test file.
- **Steps:**
  1. Create `%TEMP%\readonly_test.txt` and set `File.SetAttributes(path, FileAttributes.ReadOnly)`.
  2. Scan parent folder into ArborGraph.
  3. Execute `DeletePermanently` on the read-only file.
  4. Confirm deletion.
- **Expected Result:** ArborGraph unsets `IsReadOnly = false` before deletion and successfully removes the file without `UnauthorizedAccessException`.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `FileInfo.IsReadOnly = false` handling is present in `FileActionService.cs` line 318.

---

## 3. UI Responsiveness During Deletions Test Suite (P0 / P1)

### Test ID: `TC-UI-RESP-01`
- **Feature ID:** `FEAT-19` / `FEAT-38`
- **Title:** Synchronous UI Thread Starvation on Large Directory Tree Deletion
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Executing `Directory.Delete(path, recursive: true)` synchronously on the WPF UI thread freezes the window and triggers Windows "Not Responding" titlebar status.
- **Preconditions:** A folder with 20,000 nested test files and 1,000 subfolders is created in `%TEMP%\mass_delete_test`.
- **Test Data:** Dataset D (50,000 files / 1,000 folders).
- **Steps:**
  1. Scan `%TEMP%\mass_delete_test` into ArborGraph.
  2. Navigate to Largest Folders view.
  3. Select `mass_delete_test` folder.
  4. Click `Delete Permanently` and click `Yes` in confirmation.
  5. While deletion is running:
     - Try moving the application window.
     - Try clicking another tab (e.g. Overview).
     - Monitor window message pump for responsiveness.
- **Expected Result:**
  - The UI must remain responsive (or show a background progress indicator).
  - The application window must **NOT** hang or enter Windows "Not Responding" state.
  - *(If the window hangs for > 2.0 seconds, classify as Blocker 2 defect).*
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Code audit identified that `LargestFoldersViewModel.DeleteSelectedPermanently` calls `_fileActionService.DeletePermanently` synchronously without `await Task.Run(...)`.

---

### Test ID: `TC-UI-RESP-02`
- **Feature ID:** `FEAT-18` / `FEAT-37`
- **Title:** UI Responsiveness on Multi-Gigabyte File Recycle Bin Move
- **Priority:** P1 / CRITICAL
- **Risk:** High — Moving multi-gigabyte files across volume boundaries blocks the UI thread during shell file copy/delete.
- **Preconditions:** A 15GB test file exists on disk.
- **Test Data:** 15GB sparse or populated binary file.
- **Steps:**
  1. Select the 15GB file in Largest Files view.
  2. Click `Move to Recycle Bin`.
  3. Measure time to UI return and window responsiveness during the operation.
- **Expected Result:** Shell operation completes without locking the UI message dispatcher.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `LargestFilesViewModel.cs` calls `MoveToRecycleBin` synchronously on the UI thread.

---

## 4. NTFS USN Change Journal Test Suite (P1 / CRITICAL)

### Test ID: `TC-USN-01`
- **Feature ID:** `FEAT-04`
- **Title:** Initial Baseline Checkpoint Creation on NTFS Volume
- **Priority:** P1 / CRITICAL
- **Risk:** High — USN checkpoint must be correctly recorded in SQLite upon first full scan.
- **Preconditions:** Administrator privileges enabled; target volume is NTFS.
- **Test Data:** Local NTFS Drive `C:\`.
- **Steps:**
  1. Delete any existing USN checkpoint: `dbService.DeleteUsnCheckpoint("C:")`.
  2. Execute full scan on `C:\`.
  3. Check database table `usn_checkpoints` for `drive_letter = 'C:'`.
- **Expected Result:** Checkpoint is created with valid `journal_id` (> 0) and `next_usn` (> 0).
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified in integration test 17 persistence check.

---

### Test ID: `TC-USN-02`
- **Feature ID:** `FEAT-04`
- **Title:** USN Change Journal Non-Elevated Permission Graceful Fallback
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Standard users without Administrator privileges cannot open `\\.\C:`. App must fall back seamlessly without crashing.
- **Preconditions:** Launch ArborGraph as a standard non-elevated user account.
- **Test Data:** Drive `C:\`.
- **Steps:**
  1. As standard user, click `Start Scan` on `C:\`.
  2. Observe scanner initialization and logs.
  3. Verify scan completes and files are indexed.
- **Expected Result:**
  - `UsnJournalService.QueryJournalState` reports `RequiresElevation = true`.
  - Scanner service logs informational warning: `"Querying NTFS USN Change Journal requires Administrator privileges. Running in Standard Scan Mode."`
  - Traversal falls back to full BFS filesystem scan.
  - Zero crashes; zero access violation dialogs.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Integration test 17 confirmed fallback trigger and message generation under standard credentials.

---

### Test ID: `TC-USN-03`
- **Feature ID:** `FEAT-04`
- **Title:** Non-NTFS Volume (FAT32 / exFAT) Detection and Fallback
- **Priority:** P1 / CRITICAL
- **Risk:** High — Non-NTFS drives lack USN journals. P/Invoke calls must not execute.
- **Preconditions:** USB drive or VHD formatted as exFAT or FAT32 mounted.
- **Test Data:** exFAT target drive (e.g. `E:\`).
- **Steps:**
  1. Select exFAT volume in ArborGraph.
  2. Click `Start Scan`.
  3. Inspect Scan Log view.
- **Expected Result:** `IsNtfsVolume` returns `false`; USN querying is bypassed completely; standard scan runs cleanly.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Code path verified in `ScannerService.cs` line 173.

---

### Test ID: `TC-USN-04`
- **Feature ID:** `FEAT-04`
- **Title:** Journal ID Mismatch / Truncation Fallback
- **Priority:** P1 / CRITICAL
- **Risk:** High — If volume was reformatted or USN journal recreated, previous NextUsn is invalid. Reading could result in corrupted index.
- **Preconditions:** Saved checkpoint has an artificial mismatched JournalId (`0x9999999999999999`).
- **Test Data:** Mock checkpoint in database.
- **Steps:**
  1. Inject invalid checkpoint into `usn_checkpoints`.
  2. Run scan on volume.
- **Expected Result:** `ReadChanges` detects mismatch, returns `Success = false` with reason `"Journal ID mismatch"`, and initiates full scan baseline.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Tested in integration test 17.

---

## 5. Filesystem Scanner Engine Test Suite (P1 / CRITICAL)

### Test ID: `TC-SCN-01`
- **Feature ID:** `FEAT-03`
- **Title:** Bounded Channel Full Traversal & Exact Accounting
- **Priority:** P1 / CRITICAL
- **Risk:** High — Race conditions in counter incrementing could produce statistical discrepancies.
- **Preconditions:** Controlled test directory tree with known file and folder count (Dataset B).
- **Test Data:** 1,000 files, 50 directories, 150MB total logical bytes.
- **Steps:**
  1. Execute scan on test tree.
  2. Await completion.
  3. Validate final `ScanStats` properties:
     - `DirectoriesVisited == DirectoriesProcessed + DirectoriesSkipped`
     - `FilesDiscovered == FilesIndexed + FilesSkipped`
     - `FilesIndexed == 1000`
     - `LogicalBytesIndexed == 157286400`
- **Expected Result:** Exact mathematical accounting matches reference values with zero deviation.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Integration test 2 verified accounting assertions on small mock tree.

---

### Test ID: `TC-SCN-02`
- **Feature ID:** `FEAT-03`
- **Title:** Scanner Safe Cancellation Lifecycle
- **Priority:** P1 / CRITICAL
- **Risk:** High — Canceling mid-scan could leave SQLite transactions uncommitted, corrupted, or cause background worker thread leaks.
- **Preconditions:** Large file tree (> 50,000 files).
- **Test Data:** Dataset C.
- **Steps:**
  1. Click `Start Scan`.
  2. Wait 2 seconds until scan is actively writing records.
  3. Click `Stop Scan`.
  4. Verify state transitions to `Stopping` and then `Cancelled`.
  5. Run `dbService.CheckIntegrity()`.
  6. Re-click `Start Scan` to ensure scanner restarts cleanly.
- **Expected Result:**
  - Bounded channel writer completes cleanly.
  - Background SQLite worker drains remaining items and commits transaction.
  - Database integrity check returns `ok`.
  - Next scan starts without object disposal or thread state errors.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Cancellation handling verified in integration test 6.

---

### Test ID: `TC-SCN-03`
- **Feature ID:** `FEAT-03` / `FEAT-40`
- **Title:** Symlink & Junction Loop Avoidance (`FollowJunctions = false`)
- **Priority:** P1 / CRITICAL
- **Risk:** High — Infinite recursive directory traversal caused by circular directory junctions.
- **Preconditions:** Create circular junction: `mklink /J C:\test\loop C:\test`.
- **Test Data:** Directory containing circular junction.
- **Steps:**
  1. Ensure `FollowJunctions` is `false` in Settings.
  2. Scan parent directory.
- **Expected Result:** Junction is identified via `FileAttributes.ReparsePoint` and skipped. Traversal terminates normally.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Handled in `ScannerService.cs` line 456.

---

### Test ID: `TC-SCN-04`
- **Feature ID:** `FEAT-03`
- **Title:** Cloud Placeholders Hydration Avoidance (OneDrive / iCloud)
- **Priority:** P1 / CRITICAL
- **Risk:** Critical — Touching cloud placeholder files triggers automatic internet downloading of gigabytes of cloud data.
- **Preconditions:** OneDrive folder with "Files On-Demand" enabled and online-only files present.
- **Test Data:** Dataset S (`RecallOnDataAccess` / `RecallOnOpen` attributes).
- **Steps:**
  1. Scan OneDrive directory containing cloud-only placeholders.
  2. Monitor network traffic during scan.
- **Expected Result:**
  - Scanner checks attributes `(fi.Attributes & (RecallOnDataAccess | RecallOnOpen)) != 0` and skips file length read.
  - Zero bytes downloaded over network.
  - Files not hydrated to local disk.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Check implemented in `ScannerService.cs` lines 357-362.

---

### Test ID: `TC-SCN-05`
- **Feature ID:** `FEAT-07`
- **Title:** Live Directory Rolling Feed Buffer Throttling
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Unthrottled UI dispatcher invocations freeze the WPF render loop.
- **Preconditions:** Rapid file tree (> 30,000 files/sec).
- **Test Data:** In-memory or RAM disk filesystem tree.
- **Steps:**
  1. Run scan.
  2. Observe live directory feed on Overview and Scanner tabs.
  3. Verify rolling buffer maintains <= 60 displayed items.
  4. Measure UI frame rate.
- **Expected Result:** Updates throttled to 40ms stopwatch intervals; UI maintains smooth 60 FPS without stutter.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified in benchmark test 13.

---

## 6. Directory Rollup & Scalability Test Suite (P1 / CRITICAL)

### Test ID: `TC-ROL-01`
- **Feature ID:** `FEAT-10`
- **Title:** Mathematically Exact Hierarchical Directory Rollup
- **Priority:** P1 / CRITICAL
- **Risk:** High — Incorrect ancestor aggregation produces wrong folder sizes.
- **Preconditions:** Nested tree: Root -> SubA (10MB) -> SubB (20MB) -> File (30MB).
- **Test Data:** Controlled tree with 3 nested levels and known sizes.
- **Steps:**
  1. Scan test tree.
  2. Inspect `directories` table in SQLite for each level.
- **Expected Result:**
  - `SubB` reflects 30MB total.
  - `SubA` reflects 50MB total (direct 20MB + nested 30MB).
  - `Root` reflects 60MB total (direct 10MB + nested 50MB).
  - All direct and total counts match reality.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified in integration test 2B.

---

### Test ID: `TC-ROL-02`
- **Feature ID:** `FEAT-10`
- **Title:** Large-Scale Directory Rollup Memory Footprint (100K Folders)
- **Priority:** P1 / CRITICAL
- **Risk:** High — LOH object allocation in `BuildDirectoryRollup` causing Out-Of-Memory on 32-bit or constrained 8GB systems.
- **Preconditions:** Synthetic filesystem tree with 100,000 distinct directory paths.
- **Test Data:** 100,000 directories populated into SQLite.
- **Steps:**
  1. Populate SQLite `files` table with 100,000 distinct `parent` values.
  2. Invoke `dbService.BuildDirectoryRollup()`.
  3. Measure peak memory allocation and elapsed execution time.
- **Expected Result:** Execution completes in < 5.0 seconds; peak memory delta < 250 MB; zero GC gen-2 freeze.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Audit report flagged rollup as High Risk 1 due to in-memory dictionary allocation.

---

## 7. Cryptographic Duplicate Detection Test Suite (P1 / CRITICAL)

### Test ID: `TC-DUP-01`
- **Feature ID:** `FEAT-22`
- **Title:** 3-Stage Duplicate Verification (Zero False Positives on Size Collisions)
- **Priority:** P1 / CRITICAL
- **Risk:** Critical — Treating different files of the same size as duplicates leads to catastrophic data destruction.
- **Preconditions:** Two distinct files of identical byte size (e.g., exactly 65,536 bytes) with randomized non-identical content.
- **Test Data:** `collision_a.bin` and `collision_b.bin` (random byte payloads).
- **Steps:**
  1. Scan directory containing collision pair.
  2. Run duplicate detection: `analyzer.FindDuplicatesAsync()`.
  3. Inspect returned `DuplicateGroup` collection.
- **Expected Result:**
  - Stage 1 identifies size collision.
  - Stage 2 (head/tail MD5) or Stage 3 (full SHA-256) rejects pair.
  - Neither file is included in any duplicate group.
  - False positive rate is strictly 0.0%.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified in integration test 5.

---

### Test ID: `TC-DUP-02`
- **Feature ID:** `FEAT-22`
- **Title:** Exact Duplicate Content Identification
- **Priority:** P1 / CRITICAL
- **Risk:** High — True duplicates missed due to hashing truncation.
- **Preconditions:** Three identical files at different paths.
- **Test Data:** 128KB payload cloned to 3 locations.
- **Steps:**
  1. Scan directory.
  2. Run duplicate analysis.
- **Expected Result:** Group found with `FileCount = 3`, `WastedBytes = 256KB`, matching SHA-256 hash.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified in integration test 5.

---

### Test ID: `TC-DUP-03`
- **Feature ID:** `FEAT-23`
- **Title:** Duplicate Elimination Selection Strategies
- **Priority:** P1 / CRITICAL
- **Risk:** High — Automated strategy selecting all copies, leaving zero survivors.
- **Preconditions:** Duplicate group with 3 copies with different creation dates and path lengths.
- **Test Data:** Duplicate group.
- **Steps:**
  1. Apply `KeepNewest`: verify newest modified file is kept, others selected.
  2. Apply `KeepOldest`: verify oldest file is kept, others selected.
  3. Apply `KeepShortestPath`: verify shallowest path is kept, others selected.
- **Expected Result:** In all cases, exactly 1 file is retained as keeper; remaining $N-1$ files are marked for removal. Never marks all files.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Selection strategy logic verified in `DuplicateViewModel.cs` lines 154-160.

---

## 8. Developer Storage & Junk Cleaner Test Suite (P1 / CRITICAL)

### Test ID: `TC-DEV-01`
- **Feature ID:** `FEAT-24`
- **Title:** Contextual Developer Artifact Detection (Preventing False Positives)
- **Priority:** P1 / CRITICAL
- **Risk:** High — Misidentifying non-developer folders named `target` or `build` as disposable build outputs.
- **Preconditions:** Workspace contains:
  - `my-rust-app/target` (with `Cargo.toml` in parent)
  - `customer-targets/target` (NO `Cargo.toml`, contains PDFs)
  - `my-gradle-app/build` (with `build.gradle` in parent)
  - `architectural-build/build` (NO `build.gradle`, contains DWG blueprints)
- **Test Data:** Dataset N.
- **Steps:**
  1. Run `DeveloperStorageService.ScanWorkspaceAsync`.
  2. Inspect detected items.
- **Expected Result:**
  - `my-rust-app/target` is detected as Rust artifact.
  - `customer-targets/target` is **REJECTED** and not marked as junk.
  - `my-gradle-app/build` is detected as Gradle artifact.
  - `architectural-build/build` is **REJECTED** and not marked as junk.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified in integration test 15.

---

### Test ID: `TC-SAF-01`
- **Feature ID:** `FEAT-27`
- **Title:** Locked-File Safe Deletion Fallback (Zero Crash on In-Use Files)
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Active background apps locking temp files (e.g. Chrome/Word) causing unhandled `IOException` during cleanup.
- **Preconditions:** File `%TEMP%\locked_file.tmp` opened with `FileShare.None`.
- **Test Data:** 4 unlocked files, 1 locked file.
- **Steps:**
  1. Hold exclusive file handle on locked file.
  2. Invoke `JunkCleanerService.CleanTargetsAsync()`.
- **Expected Result:**
  - 4 unlocked files are deleted.
  - 1 locked file is safely skipped.
  - `FilesSkipped = 1`, `FilesDeleted = 4`.
  - Zero crashes; locked file remains undamaged on disk.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified in integration test 8.

---

## 9. Interactive Treemap Visualization Test Suite (P1 / P2)

### Test ID: `TC-TMP-01`
- **Feature ID:** `FEAT-30`
- **Title:** Squarified Treemap Canvas Tiling & Non-Overlapping Layout
- **Priority:** P1 / CRITICAL
- **Risk:** High — Layout engine producing overlapping rectangles, negative coordinates, or extending beyond canvas.
- **Preconditions:** Canvas dimensions 1000x600.
- **Test Data:** 10 items of varying sizes from 10MB to 500GB.
- **Steps:**
  1. Invoke `TreemapLayoutEngine.ComputeLayout(items, 1000, 600)`.
  2. Verify:
     - For all rects: $X \ge 0$, $Y \ge 0$, $W > 0$, $H > 0$.
     - $X + W \le 1000.01$, $Y + H \le 600.01$.
     - Sum of all rectangle areas equals $1000 \times 600 \pm 1.0$.
- **Expected Result:** Perfect rectangular tiling without gaps or overlaps.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified in integration test 9.

---

### Test ID: `TC-TMP-02`
- **Feature ID:** `FEAT-30`
- **Title:** Zero-Size / Empty File Handling in Treemap
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Zero-size files causing division by zero ($0/0 \implies \text{NaN}$) and WPF rendering crash.
- **Preconditions:** Dataset containing files with 0 bytes.
- **Test Data:** 5 zero-byte files, 2 normal files.
- **Steps:**
  1. Compute treemap layout.
- **Expected Result:** Zero-byte files filtered out gracefully; layout computed without NaN or Infinity.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Guarded in `TreemapLayoutEngine.cs` line 70.

---

### Test ID: `TC-TMP-03`
- **Feature ID:** `FEAT-30`
- **Title:** Canvas Extreme Dimension Robustness (0x0, Minimized Window)
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Minimizing window sets ActualWidth/Height to 0, causing exceptions in geometry.
- **Preconditions:** Window minimized or resized to 0x0.
- **Test Data:** Valid treemap items.
- **Steps:**
  1. Call `ComputeLayout(items, 0, 0)`.
  2. Call `ComputeLayout(items, -10, 500)`.
- **Expected Result:** Returns empty list without throwing exceptions.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Guarded in `TreemapLayoutEngine.cs` line 67.

---

## 10. Database Concurrency & Query Stress Test Suite (P1 / CRITICAL)

### Test ID: `TC-DB-01`
- **Feature ID:** `FEAT-08` / `FEAT-16`
- **Title:** Simultaneous Database Read During Active Scanner Bulk Ingestion
- **Priority:** P1 / CRITICAL
- **Risk:** High — SQLite lock wait timeout (`database is locked`) when UI tabs query database while scanner worker is inserting.
- **Preconditions:** Scanner actively writing 20,000 records/sec.
- **Test Data:** 100K active scan.
- **Steps:**
  1. Click `Start Scan`.
  2. While scanning is in progress:
     - Switch to Largest Files tab.
     - Switch to File Types tab.
     - Switch to Analytics tab.
     - Execute paged SQL query `GetFilesPaged()`.
- **Expected Result:**
  - WAL mode allows concurrent readers and single writer.
  - Queries return data without throwing `SqliteException: SQLite Error 5: 'database is locked'`.
  - Scanner continues uninterrupted.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: WAL mode configured (`PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;`). Rapid tab switching during scan tested in integration test 12 (Test 2e).

---

### Test ID: `TC-DB-02`
- **Feature ID:** `FEAT-09`
- **Title:** SQLite Integrity Check Verification (`PRAGMA integrity_check`)
- **Priority:** P1 / CRITICAL
- **Risk:** High — Corrupted index tables reporting false positives.
- **Preconditions:** Database initialized.
- **Test Data:** Target SQLite database.
- **Steps:**
  1. Call `dbService.CheckIntegrity()`.
- **Expected Result:** Scalar query returns `"ok"`; method returns `true`.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified in integration tests 1, 6, and 10.

---

## 11. Large-Scale Benchmark Suite (P1 / CRITICAL)

### Test ID: `TC-PERF-01`
- **Feature ID:** `FEAT-03`
- **Title:** 100,000 Files Ingestion Scalability Benchmark
- **Priority:** P1 / CRITICAL
- **Risk:** High — Scanner throughput collapse under high row volume.
- **Preconditions:** 100,000 test files generated across 2,000 directories on NVMe SSD.
- **Test Data:** Dataset C.
- **Steps:**
  1. Execute scan 3 times consecutively with database reset.
  2. Measure elapsed time, files/second, average RAM, peak RAM.
- **Expected Result:**
  - Throughput >= 15,000 files/sec on SSD.
  - Peak RAM <= 256 MB.
  - Average execution time recorded across 3 runs.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: 500-file benchmark achieved > 33,000 files/sec in integration test 13.

---

## 12. First-Run EULA & Settings Test Suite (P1 / CRITICAL)

### Test ID: `TC-LGL-01`
- **Feature ID:** `FEAT-39`
- **Title:** First-Run EULA Mandatory Gate & Rejection Process Shutdown
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Application must not run if user declines EULA.
- **Preconditions:** Delete `%LOCALAPPDATA%\ArborGraph\settings.json`.
- **Test Data:** Fresh environment.
- **Steps:**
  1. Launch `ArborGraph.exe`.
  2. Verify EULA dialog appears.
  3. Verify `Accept` button is disabled.
  4. Close window or click `Decline`.
  5. Check process exit code.
- **Expected Result:** Accept button disabled until checkbox checked; declining terminates process cleanly with code 0 without showing main window.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Tested in integration test 12 (`eulaDlg.IsAcceptButtonEnabled`).

---

### Test ID: `TC-SET-01`
- **Feature ID:** `FEAT-40`
- **Title:** Path Exclusion Enforcement in Scanner Engine
- **Priority:** P1 / CRITICAL
- **Risk:** High — Scanner ingesting paths explicitly excluded by user.
- **Preconditions:** Exclusion added: `C:\TestRoot\ExcludedFolder`.
- **Test Data:** Folder tree with excluded subfolder.
- **Steps:**
  1. Add path to `ExcludedPaths` in Settings.
  2. Run scan.
  3. Check `SkippedDirectories` in Scan Log.
  4. Query SQLite for files matching excluded path.
- **Expected Result:**
  - Directory is recorded as skipped with reason `"Excluded by configuration"`.
  - Zero files inside excluded folder exist in SQLite.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Verified in integration test 20.
