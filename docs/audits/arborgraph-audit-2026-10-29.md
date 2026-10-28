# ArborGraph application audit and competitor analysis

Audit date: 29 October 2026, Asia/Manila. Checkout: `codex/arborgraph-site-taste` at `9abf18f`. Released preview: [v1.3.1](https://github.com/12valor/ArborGraph/releases/tag/v1.3.1). This is an audit, not authorization to implement changes. Application source was not modified. The pre-existing untracked WPF temporary project was preserved.

## 1. Executive summary

The Electron preview is a real React/TypeScript client backed by the existing .NET 8/SQLite engine. It can scan, search, visualize, analyze and export an index. It is not feature-equivalent to WPF and should not be presented as a complete cleanup tool. Five isolated reproductions show correctness issues:

1. **Cancelled repeat scan loses the previous index:** a 20,000-file generated fixture went from 20,000 indexed files to zero after an early cancellation. Original files remained on disk. This is loss of indexed analysis, not observed deletion of files.
2. **Junction cycle:** with following enabled, one generated file was indexed 64 times and the scan reported `Completed`, with one skipped directory.
3. **Hard-link estimate:** two names for one 1 MiB file produced 1 MiB of potential duplicate redundancy, although removing one name while the other remains would not free that file's contents.
4. **Duplicate overview inconsistency:** two empty files and two four-byte files produced zero overview candidates, then two confirmed groups.
5. **Hidden directory:** with hidden files excluded, a normal file inside a hidden directory was indexed.

The existing automated suites passed but did not catch these scenarios. Code also shows a persisted worker-count option with no scanner consumer, silent database error paths, and a WPF verification-to-deletion race.

## 2. Architecture, test scope and evidence

Electron 45 + React 19 + TypeScript use a narrow preload bridge; Electron main validates IPC and launches a .NET 8 EngineHost over JSON lines. The engine reuses C# scanning, SQLite, reporting and settings. The renderer has context isolation, sandboxing, no Node integration, denied new windows/navigation and a restrictive CSP. See [Electron main](../../desktop/main/main.ts), [validation](../../desktop/main/validation.ts), [engine host](../../engine/Program.cs) and [scanner](../../Services/ScannerService.cs).

WPF remains in the repository with additional destructive controls; evaluate it separately from the read-only Electron preview. SQLite holds files, rollups, scan metadata and category snapshots. Indexed lengths are logical sizes, not physical allocation. History is not a complete file-level snapshot. The side-by-side v1.3.1 x64 preview installer is unsigned; clean-install, upgrade and uninstall were not tested here.

Current-run tests:

- Electron production build and six IPC-validation tests passed.
- Existing WPF harness: 53/53 passed with `--no-build`; its executable identifies itself as v1.3.0, so this is not a fresh proof of the packaged v1.3.1 binary. The dedicated safety suite passed 9/9. Prior [release CI](https://github.com/12valor/ArborGraph/actions/runs/40725412335) passed 53 and 9 against the release branch.
- The staged v1.3.1 EngineHost fixture passed validation, scan totals, indexed queries/rollups, insights, history comparison, event ordering, overlapping-scan rejection, cancellation and shutdown.
- In the actual headless Electron renderer with an isolated generated index, all 13 navigation targets showed their expected heading without visible alerts. File search returned two matches, a treemap tile navigated, category selection worked, one duplicate group appeared, and a saved hidden-file setting and consent persisted on restart. A 960×680 emulated Files viewport had no document-level horizontal overflow. This is smoke coverage, not complete visual or accessibility validation.
- Separate generated fixtures reproduced the five failures above. No actual user or system files were deleted. Audit fixtures/scripts are in ignored `dist/audit-*` paths. One fixture contains a self-referencing junction; do not recursively follow it during cleanup.

Not tested: native picker/save dialogs, live recycle-bin operation, WPF destructive dialogs, real ACL denial under multiple accounts, process crash during write, large-scale memory/throughput, screen readers, high DPI, updater download/install, or installer lifecycle. An automated test name alone does not prove genuine permission denial was exercised.

## 3. Feature inventory

**Working** is limited to the tested scenario; **Partially working** means a documented limit or incomplete coverage; **Broken** means a reproduced failure; **Missing** means no traced preview feature; **Unverified** means insufficient evidence.

| Feature / surface | Status | Evidence and limitation |
|---|---|---|
| EULA / startup | Partially working | Engine acceptance and isolated-app restart persisted consent. Native decline/exit interaction unverified. [LegalGate](../../desktop/renderer/src/LegalGate.tsx). |
| Overview and drive selection | Partially working | Screen and query opened; real drive selection/capacity accuracy not independently checked. [App](../../desktop/renderer/src/App.tsx:159). |
| Scanner / progress / cancellation | Broken in part | Engine scan works on fixtures; early repeat-scan cancellation loses prior root index; native picker and unavailable-drive interactions unverified. [scanner](../../Services/ScannerService.cs:132). |
| Scan logs / errors | Partially working | Page opens; engine emits bounded details. Some skipped entries and DB failures may not be surfaced accurately. [ScanLogPage](../../desktop/renderer/src/ScanLogPage.tsx). |
| Files, search, sorting, paging | Partially working | Sample name search returned 2 rows; other filters, sorting directions and large-result UI not individually exercised. [FilesPage](../../desktop/renderer/src/FilesPage.tsx). |
| Treemap / hierarchy | Partially working | Sample tile navigated and engine rollup fixture passed. Large fan-out, precise tile area and keyboard behavior remain unverified. [TreemapPage](../../desktop/renderer/src/TreemapPage.tsx). |
| Analytics / largest files and folders | Partially working | Page opened with sample data; engine fixture exercised rollups and insights. Lists are bounded and allocation/hard-link accounting absent. [AnalyticsPage](../../desktop/renderer/src/AnalyticsPage.tsx). |
| File types | Partially working | Actual category click displayed largest Photoshop files. Extension edge cases and deeper paging unverified. [InsightsPages](../../desktop/renderer/src/InsightsPages.tsx). |
| Old files | Partially working | Page opened; engine age-insight fixture passed. UI thresholds and timezone cases unverified. Age is not evidence a file is safe to delete. |
| Photoshop inspector | Partially working | Page opened; engine fixture exercises associated-file insights. The UI explicitly does not measure live scratch activity or Photoshop performance. |
| Duplicate discovery | Partially working | One real SHA-256-confirmed group rendered; small-file overview and hard-link errors reproduced. Only 250 candidate-size groups and 100 displayed paths per group; hash failures can be skipped. [analyzer](../../Services/DuplicateAnalyzer.cs:18), [host](../../engine/Program.cs:710). |
| Duplicate selection / deletion in Electron | Missing | No keeper selection or deletion IPC on the preview's duplicate screen. [DuplicatesPage](../../desktop/renderer/src/DuplicatesPage.tsx). |
| WPF duplicate cleanup | Partially working; safety risk | Keeper and content guard exists, but verification handles close before path-based delete; the file may change. Dialogs and recycle-bin operation unverified here. [guard](../../Services/DuplicateSelectionGuard.cs), [view model](../../ViewModels/DuplicateViewModel.cs:238). |
| General file operations / cleanup | Missing in Electron; unverified in WPF | WPF implementations exist; preview settings explicitly say cleanup is unavailable. Per-file errors and native shell behavior require further tests. [FileActionService](../../Services/FileActionService.cs:143). |
| Developer storage | Partially working | Page opened; engine and context-marker fixtures passed. Populated UI and classification edge cases remain unverified; identified logical bytes are not guaranteed reclaimable. |
| History / comparison | Partially working | Page opened; engine fixture validates comparison of matching completed scopes. Comparison reports totals/categories, not a complete file-level change list. Cancelled Electron rescan can erase the previous index. [HistoryPage](../../desktop/renderer/src/HistoryPage.tsx). |
| Settings / exclusions | Broken in part | Saved hidden-file preference survived isolated restart, yet hidden directory traversal ignored it. Worker count is persisted but not used by the scanner. Exclusion dialogs not tested. [SettingsPage](../../desktop/renderer/src/SettingsPage.tsx:47). |
| CSV / HTML / JSON exports | Partially working | Engine/WPF temporary-file fixtures passed; native save dialogs and very large exports not exercised through the preview. [ExportService](../../Services/ExportService.cs). |
| Theme / scaling / accessibility | Unverified; missing in part | No theme preference found; 960×680 minimum enforced. Keyboard-only workflow, screen reader, contrast and high-DPI displays not verified. |
| Installer / update / recovery | Unverified in part | Installer built and release exists; no clean install/upgrade/uninstall test. No in-app updater found in traced Electron IPC; WPF update code exists. [installer](../../installer/desktop-preview.iss). |

## 4. Ranked issue register

P0: data safety or severe incorrect/lost results. P1: core correctness and reliability. P2: important usability/coverage. P3: lower-priority polish. Efforts are estimates, not benchmarks.

| ID | Severity / priority | Finding and evidence | Impact | Effort / dependencies |
|---|---|---|---|---|
| A1 | Critical / P0 | A completed 20,000-file fixture became an empty index after an early cancelled repeat scan. Scanner clears indexed root records before traversal; Electron does not restore the earlier snapshot. | Lost analysis context, not observed source-file deletion. | Large; transactional scan generations and recovery. |
| A2 | Critical / P0 | Self-referencing junction: one file → 64 indexed files; `Completed` with one directory skipped. Visited set uses normalized path strings rather than target identity. | Grossly wrong totals and repeated traversal. | Medium–Large; volume/file identity, scope policy, diagnostics. |
| A3 | High / P1 | Two hard-link names of one 1 MiB file yielded a 1 MiB potential-redundancy group. | Reclaimable-space impression can be wrong. | Large; stable identity and explicit logical/physical accounting. |
| A4 | High / P1 | Two empty and two four-byte files: overview 0 groups, verification 2 groups. SQL still requires `size >= 1024`; analyzer minimum is zero. | Contradictory UI and missed discovery. | Small; shared definition and fixture tests. [database](../../Services/DatabaseService.cs:2130). |
| A5 | High / P1 | Hidden directory's normal child was indexed with `includeHiddenFiles=false`; traversal filters file attributes, not directory attributes. | Unexpected scan coverage and inaccurate user expectations. | Small–Medium; define hidden/system directory rules. |
| A6 | High / P1 | WPF rehashes the target and keeper, closes verification handles, then deletes by path. | Potential deletion of a changed/replaced target or loss of intended keeper. Code-based race; not reproduced destructively. | Large; identity-aware operation boundary and safe confirmation. |
| A7 | High / P1 | Database clear, rollup and query methods catch/log exceptions; scan postprocessing catches failures without necessarily making the scan fail. | Errors may look like empty or successful results; precise cases need fault injection. | Medium–Large; explicit errors and transactional recovery. [database](../../Services/DatabaseService.cs:282), [scanner](../../Services/ScannerService.cs:506). |
| A8 | Medium / P1 | Worker count can be saved; ScannerService never reads `WorkerCount`. | Misleading setting and unsupported performance expectations. | Small to disable/clarify; Large to implement safe parallel traversal. |
| A9 | Medium / P1 | Duplicate analysis considers at most 250 size groups and returns at most 100 paths per group; failed hash/open attempts are skipped. | Incomplete results can be mistaken for comprehensive analysis. | Medium–Large; bounded pagination and explicit coverage/failure reporting. |
| A10 | Medium / P1 | Cloud placeholders and some reparse/attribute skips do not consistently increment skipped counters; a root can disappear after validation. | `Completed` and skip counts may understate missing data. Code finding; exact cases unverified. | Medium; unified diagnostics and fault injection. |
| A11 | High / P1 | WPF deletion recursively clears read-only attributes; batch actions discard individual errors. Protection covers some roots but not all system-managed descendants or redirected paths. | Unexpected attribute changes and weak safety/error reporting. No destructive UI test performed. | Large; reviewed identities, per-item reporting, validated recycle policy. |
| A12 | Medium / P2 | Electron has no reveal/context menu, direct cleanup or duplicate selection/deletion. | Slower review workflow. | Medium–Large; narrow OS bridge; A3/A6/A11 before destructive actions. |
| A13 | Medium / P2 | Preview minimum window is 960×680; many labels are 11–12px; active navigation lacks explicit programmatic current-page state; no theme switch found. | Scaling, low-vision and keyboard usability risk. Minimum-size smoke passed; assistive-tech validation remains open. | Medium; semantics, scaling and input testing. |
| A14 | Medium / P2 | Electron release is unsigned, installer lifecycle and updater integration unverified; legacy WPF release path is separate. | Release/upgrade uncertainty. | Medium–Large; isolated VM matrix, signing and release provenance. |
| A15 | Medium / P2 | 50,000-record channel is bounded, but directory queue and per-size candidate/result collections can grow; SQLite work is locked. | Large-dataset memory/latency risk, not a measured performance regression. | Medium–Large; profile first, then bound/paginate where evidence supports it. |

## 5. Performance, accuracy and safety

The scanner traverses the filesystem rather than reading the MFT directly. It uses a bounded ingestion channel and a database worker; the user-facing worker count has no effect in the traced scanner. No defensible throughput, peak-memory, cold/warm-cache, network-volume, or competitor benchmark was collected. The 20,000-file experiment establishes cancellation behavior only.

Logical `FileInfo.Length` is not allocated size. Hard links, sparse/compressed/deduplicated files and alternate streams require additional accounting. Report logical and allocated bytes separately, and label unsupported volumes and incomplete scans. A verified duplicate is not automatically safe to remove.

Default junction policy skips reparse directories, but optional follow mode lacks stable target identity and can leave the selected scope. Windows reparse behavior warrants explicit handling; see [Microsoft's file-operation guidance](https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points-and-file-operations). Stable volume/file IDs and a tested fallback for network filesystems are appropriate investigation directions, not an already implemented fix.

Electron sandboxing, sender checks, trusted picker roots, CSP and bounds are useful defenses. This was not a penetration test: adversarial IPC, process replacement, symlink swaps, export destinations, dependency supply chain and sandbox escapes remain unverified.

## 6. UI/UX assessment

The preview has consistent light content surfaces, a dark navigation rail, bounded tables and meaningful empty/error messages. It distinguishes indexed logical bytes from volume capacity and states that Photoshop analysis is not live scratch measurement. The 13-item rail is long, secondary text is often small, and active navigation is mainly visual. The duplicate-overview mismatch and ineffective worker setting are higher priority than cosmetic redesign.

Recommendations: retain the last complete result while clearly labeling current/partial scans; expose intentional exclusions, inaccessible paths, failed hashes and candidate coverage; add keyboard landmarks, visible focus and programmatic current navigation; test minimum window and 125–200% DPI with screen readers; keep any destructive operation in a separate review with named targets and per-item outcomes. These are design recommendations and hypotheses, not claims that every accessibility criterion failed.

## 7. Competitor feature comparison

For competitors, **Available and verified** means verified in vendor documentation, not independently tested. **Partially available** qualifies an edition or method. **Unknown** means this audit did not verify the feature, not that it is absent. ArborGraph refers to Electron unless stated otherwise. Vendor speed claims are not independent benchmarks.

Sources: [WizTree overview](https://diskanalyzer.com/), [guide](https://diskanalyzer.com/guide) and [release notes](https://diskanalyzer.com/whats-new); [TreeSize edition matrix](https://www.jam-software.com/treesize/editions.shtml) and [duplicate-search manual](https://manuals.jam-software.com/treesize/EN/duplicate_file_search.html); [WinDirStat official features](https://windirstat.net/); [SpaceSniffer features](https://www.uderzo.it/main_products/space_sniffer/features.html) and [filter guide](https://www.uderzo.it/main_products/space_sniffer/).

| Capability | ArborGraph preview | WizTree | TreeSize | WinDirStat | SpaceSniffer |
|---|---|---|---|---|---|
| Drive / folder scan | Partially available; fixtures pass, cancellation/junction defects | Available and verified in vendor docs; NTFS MFT or other traversal | Available and verified; edition/network restrictions | Available and verified; folders, drives, shares/MTP | Available and verified |
| MFT acceleration | Missing; full traversal | Available and verified for NTFS | Unknown | Partially available; vendor describes accelerated elevated NTFS scans | Unknown |
| Treemap / drilldown | Partially available; sample click tested | Available and verified | Available and verified, including Free | Available and verified | Available and verified |
| Largest files / search | Partially available; sample search, bounded paging | Available and verified; advanced filters | Partially available by edition | Available and verified | Available and verified; size/age/type filters |
| Content-based duplicates | Partially available; SHA-256, capped candidate groups | Not available by documented duplicate method: name/size/date only, no content comparison | Partially available: Personal/Professional, not Free | Available and verified in vendor docs; hash/sparse options | Unknown |
| Allocation / hard links | Missing per-file allocation; hard-link estimate defect reproduced | Available and verified as vendor-described hard-link-aware accounting | Partially available by edition; Free omits some NTFS features | Available and verified for logical/physical comparison; link details unknown | Partially available: ADS documented; allocation/link handling unknown |
| Export | Partially available: CSV files, HTML/JSON reports; dialog unverified | Available and verified: CSV and MFT data | Partially available by edition: PDF in Free, additional formats paid | Available and verified: CSV/JSON | Available and verified: customizable text |
| Saved scan comparison | Partially available: total/category snapshots, engine fixture | Unknown | Partially available: paid editions; scheduling Professional | Available and verified: save/reopen snapshots; comparison details unknown | Unknown |
| Review / cleanup | Missing in Electron; WPF exists with safety risks | Available and verified; duplicate candidates still need user verification | Partially available: advanced dedup in paid editions | Available and verified in vendor docs | Available and verified: Windows context menu; other safeguards unknown |
| Portable operation | Unverified; staged directory exists, supported portable lifecycle not established | Unknown | Partially available: Professional | Available and verified in vendor docs | Available and verified in vendor docs |
| Live updates / themes / shortcuts | Partially available or unknown; no theme control found | Unknown | Partially available by edition | Available and verified in vendor docs | Partially available: filesystem-event updates, tagging and configuration documented |

## 8. Gaps and opportunities

**Essential:** atomic recoverable scan results (A1/A2/A5/A7/A10), truthful duplicate/space accounting (A3/A4/A9), and safe file review before cleanup (A6/A11/A12). These directly support the core purpose; storage-generation and file-identity work are substantial but prevent wrong conclusions or unsafe actions.

**Competitive:** better indexed search (WizTree documents wildcards, size and date), keyboard-accessible treemap/drilldown (SpaceSniffer documents zoom/filtering), durable scan comparisons (paid TreeSize editions and WinDirStat document snapshots), and physical allocation/hard-link accounting (WizTree and paid TreeSize). Search/UI work is medium complexity; snapshots and allocation are large, platform-sensitive changes with storage, maintenance and performance costs.

**Differentiating:** coverage-aware explanations and an evidence-backed local review workflow; transparent Photoshop-associated file context without claiming a static scan measures live scratch activity; growth insights that show source scope, skipped data and confidence. These are medium–large or unknown effort and depend on trustworthy snapshots and identity; do not infer that old or duplicate files are safe to remove.

## 9. Phased roadmap and acceptance criteria

| Phase / work | Rationale / benefit | Effort / dependencies | Measurable acceptance |
|---|---|---|---|
| 1. Atomic scan generations | Preserve the last complete index | Large; compatible schema/engine migration | Baseline survives cancellation at every phase, process kill and DB failure; partial results explicitly labeled. |
| 1. Identity-aware traversal | Stop cycles and scope escapes | Medium–Large; file identity and cross-volume policy | Self/mutual junctions, duplicate links and inaccessible targets terminate without repeated physical traversal; report coverage. |
| 1. Safe cleanup boundary | Prevent deletion race and misleading outcomes | Large; A3/A6/A11 | Replacement, missing keeper, lock, symlink swap, ACL denial and partial batch never delete an unverified target; per-file results; isolated tests only. |
| 1. Duplicate summary | Remove reproduced mismatch | Small; empty-file policy | Empty, small, same-size/different-content and hash-failure fixtures show consistent counts and coverage. |
| 2. Settings and diagnostics | Controls and coverage match behavior | Medium; error model | Hidden/system file and directory fixtures follow documented policy; worker setting has measured effect or is disabled; skipped reasons/counts reconcile. |
| 2. Navigation / search / review | Investigate without deletion | Medium; IPC boundaries | Keyboard-only search/sort/paging/reveal at minimum size and 125–200% DPI; reference-dataset results exact; no arbitrary paths cross IPC. |
| 2. DB error propagation | Avoid false empty/success states | Medium–Large; transactions | Inject clear/write/rollup/query failures; report error, preserve last good state and never claim completion. |
| 3. Allocation and hard-link model | Support truthful physical comparison | Large; Windows identity and fallback | Hard links count once for physical/reclaimable totals; sparse/compressed/ADS behavior verified; unsupported volumes labeled unknown. |
| 3. Search and snapshots | Improve large-index and change investigation | Medium–Large; generations, pagination | Measured large dataset remains responsive; differing scopes rejected; coverage differences shown; total deltas distinguished from file changes. |
| 3. Duplicate review | Safe selection when justified | Large; A3/A6/A9/A11 | Page all candidate groups, report errors, require independently verified keeper; deletion opt-in, confirmed, itemized. |
| 4. Accessibility / polish | Usable Windows desktop experience | Medium; UI testing | Keyboard/focus/contrast/screen-reader audit and 100–200% DPI matrix; no clipping at supported minimum. |
| 4. Performance / release quality | Confidence before distribution | Medium–Large; CI and VMs | Record repeatable scan time, peak memory, cancellation latency; clean install/upgrade/uninstall/rollback and signing state verified. |
| 5. Growth / app-specific insight | Differentiate without invented certainty | Unknown; reliable snapshots and classification | Prototype shows evidence, uncertainty and scope; user research establishes value. |

Quick wins: fix the overview threshold, accurately label or disable worker count, and expose incomplete coverage. Major architecture: staged scan generations, file identity/allocated-size model, race-resistant deletion and durable per-file snapshots.

## 10. Test strategy and uncertainties

Use generated folders and isolated data only; never test deletion on user/system files. Add deterministic fixtures for same-name and same-size collisions, empty files, hash/open failures, changes during hashing, hard links, sparse/compressed files, junction/symlink cycles, unavailable roots, cancellation at each phase, SQLite failures, malformed IPC and process termination. Validate complete-state transitions and per-item errors. Exercise actual Electron IPC, keyboard/focus, native dialogs, settings restart, empty/loading/error states and 125/150/200% DPI; test WPF recycle operations in an isolated Windows environment. Use clean VMs for installer lifecycle and standard/elevated users. Benchmark identical datasets on local NTFS and network storage, recording dataset shape, cache state, elapsed time, peak RAM and cancellation latency; compare repeatably before optimizing. Untested scenarios are not passed.

## 11. Recommended first five tasks

1. Implement staged/atomic scan generations and preserve the last completed index on cancellation or failure (A1).
2. Add identity-aware junction traversal, explicit scope boundaries and cycle tests (A2).
3. Correct hard-link duplicate estimates and separate logical, allocated and reclaimable bytes (A3).
4. Align duplicate overview with verification and expose candidate/hash-failure coverage (A4/A9).
5. Redesign the WPF destructive-operation boundary around stable identity and per-item outcomes; keep Electron cleanup unavailable until it meets the same safety standard (A6/A11).

The audit stops here. No redesign, implementation, commit, push or deployment is part of this report.
