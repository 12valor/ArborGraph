# Changelog

All notable changes to **ArborGraph** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [1.3.2] - 2026-10-30

### Changed
- Separate shared storage services from WPF, reducing the desktop preview package size.

### Known limitations
- Clean install, upgrade and uninstall testing remain incomplete; this is a preview release.
- Existing scanner and duplicate-analysis issues are documented in the application audit.

---

## [1.3.1] - 2026-10-29

### Fixed
- Reject empty or unavailable scan targets before clearing the index and use full traversal for incremental scan requests.
- Include small and empty files in duplicate analysis and skip candidates with stale indexed sizes.
- In the WPF application, require a retained path and recheck duplicate content before cleanup.

### Known limitations
- Electron duplicate analysis remains limited to 250 size groups and does not provide duplicate deletion.
- WPF duplicate cleanup does not eliminate the interval between rechecking content and deleting a file.

---

## [1.3.0] - 2026-10-26

### Added
- **Modern Dark Blue Visual Theme:** Replaced the legacy green palette across the desktop preview UI with an elegant deep dark blue design system (`#0f1e36` sidebar, Fluent `#005fb8` primary accents, `#93c5fd` & `#bfdbfe` treemap tiles, and crisp slate neutral tones).
- **Refined Non-Bold Light Typography:** Prioritized `Segoe UI Semilight` and `Segoe UI Light` with lighter display weights (`font-weight: 300` for headings and stat figures) and standardized all UI elements to clean `font-weight: 400`, eliminating harsh bold styling.
- **Cross-Platform Electron Desktop Preview Package:** Packaged side-by-side Electron 45 desktop preview with independent executable (`ArborGraph Desktop Preview.exe`), side-by-side Inno Setup installer (`ArborGraph-Desktop-Preview-Setup-v1.3.0-x64.exe`), and Vite-based production bundling.
- **Out-of-Process .NET Engine Host:** Integrated high-performance `ArborGraph.EngineHost.exe` providing structured JSON IPC protocol over stdin/stdout with comprehensive automated protocol test coverage (`scripts/test-engine-host.ps1`).
- **Comprehensive Desktop Analytics & Insights:** Introduced interactive file age analysis, Photoshop temporary file inspector, read-only duplicate verification, scan history comparison, and developer project store detection.
- **Audit Report & Data Exports:** Added full multi-format export support for scan results in HTML, JSON, and CSV formats.

### Fixed
- **PowerShell 5.1 Host Test Compatibility:** Restored compatibility for `[Diagnostics.ProcessStartInfo]` in `test-engine-host.ps1` across Windows PowerShell 5.1 (.NET Framework) and PowerShell 7+.
- **Typography Inheritance & Baseline Resets:** Standardized font-family inheritance across legal dialogs and added defensive baseline resets to prevent browser heading/optgroup bold overrides.

---

## [1.2.1] - 2026-10-14

### Performance & Optimization
- **Application Launch Acceleration:** Optimized cold and warm startup sequencing, deferring secondary analytics breakdown computation and reducing main UI thread initialization latency by over 90%.
- **Lightweight Startup Schema Validation:** Replaced synchronous, blocking `PRAGMA integrity_check;` on large SQLite databases with instant schema format validation (`ValidateSchemaState()`), eliminating launch hangs on multi-gigabyte databases.

### Fixed
- **Database Integrity Status Indicator:** Corrected `OverviewViewModel.DatabaseIntegrityStatus` and XAML data binding so the database status badge accurately displays validation state ("Valid", "Checking...", "Corrupt", "Error") without false corruption states.
- **Startup Lock Contention:** Eliminated database contention during startup by moving detailed verification tasks to background execution and avoiding monitor lock starvation on `DatabaseService._lock`.

---

## [1.2.0] - 2026-10-13

### Added
- **Windows 11 Fluent 2 Design System Integration:** Integrated `WPF-UI` (v4.3.0) and `LiveChartsCore.SkiaSharpView.WPF` (v2.0.5) libraries.
- **Fluent Window Experience:** Converted main window shell to `ui:FluentWindow` featuring native Windows 11 `Mica` backdrop, integrated `<ui:TitleBar>`, and smooth maximize/restore behaviors.
- **Live Analytical Visualizations:** Integrated SkiaSharp-backed LiveCharts2 donut/pie distribution charts in the Analytics view for file type categories and size distribution tiers.
- **Modern Action Glyphs:** Added vector `ui:SymbolIcon` iconography across navigation bars, action toolbars, table headers, and metric cards.
- **Fluent 2 Cards & Corner Radii:** Standardized card layouts with 8px corner radii, subtle borders (`BrushBorderLight`), and typography hierarchy across all workspace views (Overview, Scanner, Analytics, Cleanup Center, Developer Storage, Duplicates, Largest Files/Folders, File Types, Old Files, Photoshop Inspector, Junk Cleaner, Treemap, and EULA Dialog).
- **GPU-Accelerated Realtime Graphing:** Optimized native WPF CompositionTarget area-gradient rendering in `RealtimeMetricGraph` for real-time CPU and RAM sparklines.
- **Open-Source Attribution:** Added full MIT license notices in `THIRD_PARTY_LICENSES.md` for WPF-UI and LiveCharts2.

### Changed
- Refactored `OverviewView` and `ScannerView` layouts into organized Fluent 2 card groupings with real-time traversal telemetry and drive selection.
- Enhanced status bar with distinct badge indicators for database engine (SQLite WAL), companion status, scan status, and offline privacy.

### Verified
- Zero regressions across the entire automated test suite: 40/40 tests passing in both Debug and Release configurations, including `TC-UI-01` XAML StaticResource Audit Integrity.

---

## [1.1.6] - 2026-10-12

### Added
- **Multi-Drive Scoped Clearing:** Resolved scan root scoping bug where scanning secondary drives previously cleared the entire index (`TC-DRV-01`, `TC-DRV-02`).
- **Persistent Scan History:** Added automatic persistence and restoration of the last successful scan session upon application launch (`TC-HIS-01` through `TC-HIS-04`).
- **Sprout Scout Companion:** Integrated the friendly offline mascot companion into the sidebar and empty states.
- **Update Verification Pipeline:** Cryptographic SHA-256 download integrity check and non-disruptive update banner with scan deferral (`TC-UPD-01` through `TC-UPD-04`).

### Fixed
- Fixed directory rollup calculation edge cases for large directory trees.
- Hardened protected system and root path deletion shield (`TC-DEL-05`).
