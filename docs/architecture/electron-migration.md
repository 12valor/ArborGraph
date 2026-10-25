# Electron migration: engine-host foundation

The first migration stage adds `engine/ArborGraph.EngineHost.csproj`. It is a separate console process that calls the current `DatabaseService`, `ScannerService`, `SettingsService`, and `DiskService`. It does not create a WPF window or write protocol diagnostics to stdout. The existing WPF app remains the supported release and fallback.

The next stages add `desktop/`, an Electron main process, sandboxed React renderer, and narrow preload bridge. The preview currently offers real drive geometry, indexed category totals, folder or drive selection, scan progress, cancellation, paged indexed file search and CSV export, HTML and JSON audit reports, directory treemap drilldown, logical-size rankings, stored scan comparisons, scan diagnostics, file age analysis, Photoshop-associated file analysis, read-only duplicate verification, and indexed developer project findings. It has Overview, Scanner, Files, Treemap, Analytics, Scan History, Scan Logs, File Types, Old Files, Photoshop, Duplicates, scanner Settings, and Developer Storage screens, plus a first-run license gate; there is no installer or release artifact for it.

For UI testing, set `ARBORGRAPH_DEV_DATA_DIR` to a dedicated test folder before `npm start`. Development builds pass that folder only to the engine host. Packaged builds ignore this variable. This avoids changing the user's existing scan index while testing the preview.

This host currently references `DiskScope.csproj`, so it still requires the Windows Desktop runtime and is **not yet a WPF-independent core library**. Moving reusable services and models into a separate library is a later stage, after parity tests cover their behavior. The host does not provide cleanup, deletion, update, or general file browsing commands yet.

## Run locally

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build engine/ArborGraph.EngineHost.csproj -c Release
./scripts/test-engine-host.ps1
Set-Location desktop
npm ci
npm test
npm start
```

## Stage a portable preview

After `npm ci` in `desktop/`, run `./scripts/build-desktop-preview.ps1` from the repository root. It copies the pinned Electron runtime, built renderer/main files, the self-contained win-x64 engine host, icon, EULA, and third-party notices into ignored `dist/desktop-preview/`. Run the script with `-BuildInstaller` to compile `installer/desktop-preview.iss` when Inno Setup 6 is installed. This preview uses a distinct installer AppId and install folder so the supported WPF application remains installed. No release workflow publishes the preview.

The staged self-contained engine passed `./scripts/test-engine-host.ps1 -EngineExecutable ./dist/desktop-preview/resources/engine/ArborGraph.EngineHost.exe` against an isolated fixture. The Electron window and Inno installer have not yet passed a clean-install, upgrade, or uninstall test. The local machine does not currently have the Inno compiler, so no preview installer was built.

For an isolated test database, launch `engine/bin/Release/net8.0-windows/ArborGraph.EngineHost.exe --data-dir <directory>`, or set `ARBORGRAPH_DEV_DATA_DIR` before `npm start`. Without an override, the existing `%LOCALAPPDATA%\ArborGraph` database and settings are used. In that mode, the host acquires WPF's existing single-instance mutex and refuses to start if WPF owns it. Electron main owns the host process and enforces one active application instance.

## Protocol version 1

Requests and responses are one UTF-8 JSON object per line. Each request has a string `id` of 1–128 characters and an allowlisted `command`. Maximum request length is 65,536 characters. Responses include `type: "response"`, the matching `id`, `ok`, and either `data` or an `error` with a stable `code` and readable `message`. Scanner events include `type: "event"`, `eventName`, and an increasing `sequence`. Stdout carries only these messages; stderr carries diagnostics.

| Command | Input | Result |
| --- | --- | --- |
| `ping` | None | Engine name and protocol version |
| `getLegal` | None | Embedded EULA v1.0 text and current consent status |
| `acceptEula` | Explicit `accepted: true` | Persist consent in the existing settings format |
| `getSettings` / `setScanSettings` | Bounded scanner settings | Read shared settings and update next-scan options while preserving consent |
| `addExclusion` / `removeExclusion` | Absolute folder path | Manage scan exclusions; Electron's add flow uses a native folder picker |
| `listDrives` | None | Available drive geometry |
| `getOverview` | None | Current indexed file count, logical bytes, and categories |
| `getFiles` | Bounded page, search, category, extension, location, and sort | File count and up to 200 indexed file records |
| `exportFilesCsv` | Absolute CSV destination and bounded file filters | Streams all matching indexed files through the existing C# exporter |
| `exportAuditReport` | HTML or JSON format and matching absolute destination | Builds and saves a report through the existing C# exporter |
| `getLargestFolders` | Limit up to 100 | Largest rolled-up folder sizes and file counts |
| `getDirectoryContents` | Indexed absolute directory path or root, limit up to 150 | Immediate child files and folders, total size, and total item count |
| `getScanHistory` | Limit up to 100 | Stored scan metadata in newest-first order |
| `compareScans` | Two completed scan IDs, oldest first | Indexed byte, file, and category deltas for identical targets |
| `getFileAgeInsights` | Age threshold and item limit | File age buckets and old indexed files |
| `getPhotoshopInsights` | Item limit | Photoshop-associated indexed files and summary |
| `getDuplicateOverview` | None | Unverified size collision counts and possible redundant bytes |
| `getDuplicateStatus` | None | Current analysis state and result availability |
| `startDuplicateAnalysis` / `cancelDuplicateAnalysis` | None | Background three stage content verification and cancellation |
| `getDuplicateResults` | Offset up to 10,000; limit up to 50 | Paginated verified groups, capped at 100 listed files per group |
| `getIndexedDeveloperStorage` | None | Context checked developer folders from the current index, capped at 100 listed folders per ecosystem |
| `startScan` | `roots`: 1–16 existing, distinct directories | Acceptance response, then `scanProgress` and `scanFinished` or `scanFailed` events |
| `cancelScan` | None | Cancellation acknowledgment, or `NO_ACTIVE_SCAN` |
| `shutdown` | None | Acknowledgment; cancels an active scan and exits |

`startScan` uses a full scan. A second active scan returns `SCAN_IN_PROGRESS`. Invalid or oversized messages and unknown commands receive errors. The scan event reports logical indexed bytes, discovered/indexed counts, skipped counts, elapsed time, and state. It does not imply physical disk allocation or complete filesystem coverage.

The final scan event includes up to 500 skipped file paths and 500 skipped directory paths with the scanner's reasons, plus a truncation flag. Scan Logs keeps activity and those details in memory for the current desktop session; stored Scan History remains the durable summary.

Until the EULA is accepted, the engine permits only `ping`, `getLegal`, `acceptEula`, and `shutdown`. The EULA text is embedded from the repository's `LICENSE.md` at build time; acceptance is stored by the existing `SettingsService` with version and UTC timestamp. An isolated development data folder has its own consent state.

The Settings screen edits worker count, junction traversal, and hidden/system file inclusion. It shows existing cleanup and history values without changing them. Windows and Recycle Bin exclusions remain built-in. Scan setting changes wait until an active scan finishes and take effect on the next scan.

Directory children use the scanner's `directories` rollup table for folder sizes and direct `files` rows for file sizes. Each level reports the full logical byte total even when the item list is capped. The queries only read the current index; they do not enumerate arbitrary paths on disk.

The treemap shows immediate children only and groups unshown nonzero bytes into a remainder tile. The Analytics folder ranking can include overlapping parent and child totals; identical-size ancestors are hidden to reduce repetition. These totals must not be added together. Empty folders with no indexed files have no area in the treemap.

Scan comparisons require the exact same stored target string and two completed scans. They report differences in indexed totals, not verified file additions or removals. A coverage warning appears when either scan skipped files or directories, or skipped counts changed. Per-directory historical deltas are unavailable in the current schema.

Photoshop results are classified from indexed paths and extensions. The summary is occupied logical storage for identified files, not a measurement of Photoshop's live scratch activity or performance. Old-file results use modification time and do not imply that a file is safe to delete.

The duplicate overview counts equal-size candidates of at least 1 KB, before content verification. The background analyzer checks at most 250 size groups using the existing partial signature and full SHA-256 pipeline. Confirmed groups are kept only in engine memory for this session and cleared by a new scan. Redundant byte estimates assume retaining one copy per group; no deletion or space reclamation occurs in this preview.

Developer Storage applies the existing project context checks to indexed directory names and rollups. It excludes global package caches in this stage and reports indexed logical bytes, not reclaimable space. No cleanup commands are exposed.

The Files screen exports all rows matching its applied filters, independent of the current page. Electron main chooses the destination with a native Save dialog; the renderer cannot provide an arbitrary output path. Export is unavailable during an active scan. The result describes indexed files and is not a filesystem inventory.

The Overview report export uses current indexed totals and the most recent completed scan's target and duration. It does not run a cache scan. HTML marks cache findings as not analyzed and same-size duplicate bytes as possible until the session's duplicate analysis has completed; JSON carries `JunkTargetsAnalyzed` and `DuplicateBytesAreVerified`. Reports do not claim physical allocation or reclaimable bytes.

## Next boundaries

- Extract the reusable services and transport DTOs from the WPF project while keeping existing data files and tests compatible.
- Add renderer screens incrementally and compare each workflow with WPF on the same fixtures.
- Preserve the existing installer and update path until the new package passes clean-install, upgrade, and data-recovery tests.
