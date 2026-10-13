# Changelog

All notable changes to **ArborGraph** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

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
