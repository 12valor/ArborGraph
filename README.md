# ArborGraph

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![Latest Release: v1.3.0](https://img.shields.io/github/v/release/12valor/ArborGraph?label=Release&color=0078D4)](https://github.com/12valor/ArborGraph/releases/latest)
[![Runtime: .NET 8.0](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Database: SQLite WAL](https://img.shields.io/badge/Database-SQLite%20(WAL)-003B57?logo=sqlite&logoColor=white)](https://sqlite.org)
[![Privacy: 100% Offline](https://img.shields.io/badge/Privacy-100%25%20Offline%20%2F%20Zero%20Telemetry-107C41)](LICENSE.md#3-local-architecture--privacy-commitment)
[![License: EULA](https://img.shields.io/badge/License-ArborGraph%20EULA-gray)](LICENSE.md)

**ArborGraph** is a high-performance Windows desktop filesystem analytics and storage utility built with C# and WPF on .NET 8. It catalogs local drives and folders into an embedded SQLite database in WAL mode, streams live traversal telemetry through an interactive directory feed, visualizes storage distribution with squarified treemaps, identifies duplicate files via cryptographic SHA-256 hashing, cleans system, browser, and developer cache stores, and features persistent last-scan history with the friendly **Sprout Scout** desktop companion.

ArborGraph operates 100% offline with zero telemetry, zero cloud dependencies, and zero background services.

---

## Table of Contents

- [System Requirements](#system-requirements)
- [Quick Start](#quick-start)
  - [Launchers](#launchers)
  - [Command-Line Options](#command-line-options)
- [Workspace Views](#workspace-views)
- [Core Architecture](#core-architecture)
- [Repository Structure](#repository-structure)
- [Building from Source](#building-from-source)
- [Publishing Standalone Releases](#publishing-standalone-releases)
- [Building the Windows Installer](#building-the-windows-installer)
- [Running Automated Tests](#running-automated-tests)
- [Data & Configuration Locations](#data--configuration-locations)
- [Troubleshooting](#troubleshooting)
- [Documentation & Legal](#documentation--legal)

---

## System Requirements

- **Operating System:** Windows 10 (version 1809 or later) or Windows 11, 64-bit (`x64`)
- **For standalone binaries / installer:** No external dependencies required (self-contained single-file binary with bundled runtime)
- **For building from source:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)

---

## Quick Start

### Launchers

The repository includes launcher scripts (`start.bat` and `start.ps1`) that automatically locate your .NET 8 SDK or fall back to precompiled standalone binaries:

**Command Prompt / File Explorer:**
```cmd
start.bat
```

**PowerShell:**
```powershell
.\start.ps1
```

### Command-Line Options

| Command (CMD) | Command (PowerShell) | Description |
| :--- | :--- | :--- |
| `start.bat` | `.\start.ps1` | Standard launch: compiles incremental changes and launches GUI |
| `start.bat --fast` | `.\start.ps1 -Fast` | Skip build step; launch precompiled binary immediately |
| `start.bat --dev` | `.\start.ps1 -Dev` | Run attached to console with live output (`dotnet run`) |
| `start.bat --publish` | `.\start.ps1 -Publish` | Build and run a self-contained, compressed single-file release |
| `start.bat --site` | `.\start.ps1 -Site` | Open the companion documentation portal in your browser |
| `start.bat --test` | `.\start.ps1 -Test` | Run the automated 30-stage regression suite and 9 security audits |
| `start.bat --help` | `.\start.ps1 -?` | Display command-line usage information |

---

## Workspace Views

ArborGraph is organized into 9 dedicated workspace panels accessible from the sidebar:

1. **Overview:**
   - Total, used, and available volume storage summary.
   - Real-time CPU and RAM sparkline monitors with 60-second history, bounded memory ring buffers, and dynamic RAM headroom ceiling calculation.
   - Interactive hierarchical directory drilldown with instant breadcrumb reset.
   - Embedded real-time scan traversal feed.

2. **Scanner:**
   - **Target Configuration & Controls:** Drive selector checkboxes (`C:\`, etc.), custom target folder input, folder browse dialog, and responsive **Start Scan** / **Stop Scan** controls integrated directly into the view.
   - Real-time disk indexing console showing current target, active scan state, and progress indicator.
   - Live metrics: files indexed, folders processed, traversal speed (files/sec), and elapsed time.
   - Dedicated **Live Directory Feed** displaying active folder path and rolling monospace traversal history buffer.
   - Stabilized viewport dimensions preventing container width jitter during active scanning.

3. **Files:**
   - Searchable filesystem explorer backed by indexed SQLite queries.
   - Sortable columns: Name, Path, Size, Extension, Category, and Modified Date.
   - Context actions: reveal in Windows Explorer, copy file path, send to Recycle Bin, or delete permanently.

4. **Treemap:**
   - Proportional squarified visual space map powered by the Bruls-Huizing-van Wijk layout algorithm with aspect ratio guards.
   - **Multi-Mode Color System:** Switch dynamically between **Category Mode** (vibrant modern gradients and glowing borders for Code, Videos, Audio, Images, Design, Executables, Archives, and distinct jewel-toned folder cards), **Space Heatmap Mode** (hot/cold density scaling from cool cyan to hot crimson), and **Depth Mode** (view share hierarchy).
   - Rich block typography with category icon glyphs, Cascadia Code size badges, view share percentages, and high-contrast dark tile backing.
   - Interactive drilldown into nested folders with instant breadcrumb trail navigation, double-click zoom, and dedicated inspector actions.
   - Comprehensive bottom inspector card with category badge chips, exact sizes, file counts, and one-click "Open in Explorer" or "Copy Path" commands.
   - Empty state guidance featuring the Sprout Scout desktop companion.

5. **Duplicates:**
   - 3-tier detection pipeline: exact file size match &rarr; 4 KB header verification &rarr; full SHA-256 chunked cryptographic hash.
   - Group-based duplicate inspection with automated selection rules (keep newest, keep oldest).
   - Direct Recycle Bin removal of redundant files.

6. **Cleanup Center:**
   - Unified cleaner for Windows user temporary files, crash dumps, and Windows Update cache.
   - Browser cache discovery and cleaning for Google Chrome, Microsoft Edge, Mozilla Firefox, and Brave.
   - Safe in-use skip logic: files locked by running applications are bypassed without interrupting cleanup.

7. **Developer Storage:**
   - Contextual workspace analyzer for software engineering caches:
     - **Node.js:** `node_modules` folders and `%LocalAppData%\npm-cache`
     - **.NET:** Project `bin` / `obj` directories and `~/.nuget/packages`
     - **Rust:** `target` directories (verified by `Cargo.toml`) and `~/.cargo`
     - **Gradle:** `build` folders and Gradle cache stores
     - **Python:** `%LocalAppData%\pip\cache`
     - **Docker:** Docker Desktop local container and build caches

8. **Photoshop Inspector:**
   - Dedicated inspector for Adobe Photoshop (`.psd`) and Large Document (`.psb`) assets.
   - Categorized audit of heavy working files, backup saves, and Adobe scratch file disk usage.

9. **Settings & Exports:**
   - Excluded directories and file extension filter rules.
   - Toggles for hidden files, system files, and NTFS junction / reparse point traversal.
   - Database WAL integrity checker and manual index reset tools.
   - **Consolidated Export & Reporting Center:** Export scan analytics in multiple portable formats:
     - **Interactive HTML Audit Report:** Self-contained dashboard with dark/light themes, category breakdown distribution bar, and largest file tables.
     - **Full JSON Snapshot:** Complete structured filesystem snapshot dump.
     - **Tabular CSV Exports:** Dedicated CSV data exports for indexed files, duplicate groups, and cleanable junk candidates.

---

## Core Architecture

ArborGraph features a dual presentation architecture sharing a high-performance local SQLite storage core:

1. **WPF Desktop Application (Production):**
   - Built on C# and WPF on .NET 8 (`net8.0-windows`, x64).
   - Modern Windows 11 Fluent 2 design language (`WPF-UI`) featuring native Mica backdrop, integrated title bar, SkiaSharp-backed LiveCharts2 distribution series, and real-time GPU-accelerated CPU and RAM monitors.
   - Bounded `System.Threading.Channels` traversal pipeline streaming filesystem records directly into high-throughput SQLite WAL bulk insert transactions.

2. **Electron Desktop Preview & Out-of-Process Engine Host (v1.3.0 Preview):**
   - **Modern Dark Blue Identity:** Styled with an elegant dark blue theme (`#0f1e36` midnight shell, `#005fb8` Fluent blue accents, `#93c5fd` & `#bfdbfe` treemap tiles, and slate `#64748b` neutral tones).
   - **Airy Non-Bold Typography:** Refined typography powered by `Segoe UI Semilight` and `Segoe UI Light` with light `font-weight: 300` display metrics and standard `font-weight: 400` body elements.
   - **Engine Host (`engine/`):** Standalone .NET 8 engine (`ArborGraph.EngineHost.exe`) communicating with the Electron main process over stdin/stdout using a structured JSON-lines protocol with automated protocol test coverage (`scripts/test-engine-host.ps1`).
   - **Comprehensive Workspace Views:** Features Overview, Scanner with live feed, Files, Treemap, Analytics, Scan History comparison, Duplicate verification, Developer Storage, File Age, Photoshop Inspector, Settings, Diagnostics, and full HTML, JSON, and CSV report exports.
   - **Distribution:** Packaged via `scripts/build-desktop-preview.ps1 -BuildInstaller` into a side-by-side Inno Setup installer (`ArborGraph-Desktop-Preview-Setup-v1.3.0-x64.exe`).

```
+-------------------------------------------------------------------------------+
|                       WPF Desktop UI (Production XAML)                        |
|   Overview  | Scanner | Files | Treemap | Duplicates | Cleanup | Dev | PS     |
+-------------------------------------------------------------------------------+
                                        |
                                        v
+-------------------------------------------------------------------------------+
|                          MVVM Presentation Layer                              |
|          ObservableObject | RelayCommand | MainViewModel & ViewModels         |
+-------------------------------------------------------------------------------+
                                        |
                                        v
+-------------------------------------------------------------------------------+
|                            Core Service Engine                                |
|   ScannerService        | UsnJournalService      | DuplicateAnalyzer          |
|   JunkCleanerService    | DeveloperStorageService| FileActionService          |
|   DiskService           | ExportService          | SettingsService            |
+-------------------------------------------------------------------------------+
                 |                                              |
                 v                                              v
+---------------------------------+            +--------------------------------+
|       Local SQLite Index        |            |        Windows OS APIs         |
|   WAL Mode, Memory Pragmas,     |            |   Kernel32, Shell32            |
|   Indexed Directory Rollups     |            |   Recycle Bin, NTFS Journal    |
+---------------------------------+            +--------------------------------+
                 ^
                 |  (Shared Local Database)
                 v
+-------------------------------------------------------------------------------+
|             Out-of-Process Engine Host (ArborGraph.EngineHost.exe)            |
|                JSON-lines Protocol over Stdin / Stdout (IPC)                  |
+-------------------------------------------------------------------------------+
                                        ^
                                        |  (Preload ContextBridge)
                                        v
+-------------------------------------------------------------------------------+
|                 Electron Desktop Preview (React 19 + TypeScript)              |
|        Dark Blue Theme | Segoe UI Semilight | Treemap | Audit Exports         |
+-------------------------------------------------------------------------------+
```

- **Runtime:** .NET 8.0 Windows Desktop SDK (`net8.0-windows`, x64).
- **Presentation:** Windows Presentation Foundation (WPF) with Windows 11 Fluent 2 design language (`WPF-UI`), Mica material window backdrop, vector symbol glyphs, LiveCharts2 analytical series, and real-time GPU-accelerated metric graph rendering.
- **Desktop Preview:** Modern dark blue React 19 + TypeScript shell with airy Segoe UI Semilight typography, powered out-of-process by `ArborGraph.EngineHost.exe`.
- **Persistence:** Local SQLite database via `Microsoft.Data.Sqlite` in Write-Ahead Logging (`WAL`) mode with `synchronous=NORMAL` and cache sizing pragmas.
- **Concurrency:** Bounded `System.Threading.Channels` pipeline streaming file records from multi-threaded traversal tasks directly into bulk SQLite transaction workers.

---

## Repository Structure

```
arborgraph/
├── App.xaml / App.xaml.cs       # Application entry point and exception logging
├── DiskScope.csproj             # .NET 8 WPF project definition (Produces ArborGraph.exe)
├── ArborGraph.exe               # Precompiled compressed standalone production binary
├── CHANGELOG.md                 # Semantic version release log
├── index.html                   # Web portal redirect
├── LICENSE.md                   # End User License Agreement
├── README.md                    # Project documentation and architecture guide
├── start.bat / start.ps1        # Universal CLI and GUI launchers
├── THIRD_PARTY_LICENSES.md      # Open source license notices
├── Controls/                    # Custom WPF controls (Treemap canvas, Metric graphs)
├── desktop/                     # Electron 45 / React 19 desktop preview application
│   ├── main/                    # Electron main process, IPC handlers, validation contracts
│   └── renderer/                # React 19 + TypeScript views (Dark Blue theme)
├── docs/                        # Architecture and testing documentation
│   ├── architecture/            # Migration notes and design contracts
│   └── testing/                 # Test plans, bug logs, and reports/
├── engine/                      # Standalone .NET 8 headless engine host (IPC protocol)
├── Infrastructure/              # Base classes (RelayCommand, ObservableObject)
├── installer/                   # Inno Setup 6 packaging scripts:
│   ├── installer.iss            # WPF production installer configuration
│   └── desktop-preview.iss      # Electron preview installer configuration
├── Models/                      # File records, drive metrics, duplicate groups
├── Resources/                   # Vector assets, themes, and icons:
│   ├── brand/                   # Official logo files, icons, and mascot graphics
│   └── Fonts/                   # Bundled fonts and typography assets
├── scripts/                     # Packaging, test, and release automation scripts
├── Services/                    # Core business logic and database engine
├── site/                        # Offline documentation and setup wizard portal
├── tests/                       # Automated 47-stage integration test suite
├── ViewModels/                  # MVVM view models for all workspace views
└── Views/                       # WPF XAML views for all workspace panels
```

---

## Building from Source

Ensure the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) is installed and in your `PATH`.

```cmd
git clone https://github.com/12valor/ArborGraph.git
cd ArborGraph
dotnet build DiskScope.csproj -c Release
```

The compiled binary will be placed at:
```
bin\Release\net8.0-windows\ArborGraph.exe
```

---

## Publishing Standalone Releases

To produce a self-contained, single-file executable that runs on any 64-bit Windows machine without requiring an external .NET installation:

```cmd
dotnet publish DiskScope.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o dist
```

> [!NOTE]
> Setting `/p:EnableCompressionInSingleFile=true` compresses the embedded runtime assemblies, keeping the final standalone executable around **70 MB** (well below GitHub's 100 MB file limit).

Alternatively, use the launcher:
```cmd
start.bat --publish
```

The resulting binary is created at `dist\ArborGraph.exe`.

---

## Building the Windows Installers

ArborGraph provides automated Inno Setup 6 packaging scripts for both production and preview distributions:

### 1. Supported WPF Windows Installer
```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-release.ps1 -Version 1.3.0
```
- Compiles the self-contained production binary `dist\ArborGraph.exe`.
- Compiles the official Windows installer via Inno Setup into `dist\setup\ArborGraph-Setup-v1.3.0-x64.exe`.
- Packages portable ZIP archives (`dist\ArborGraph.zip` and `dist\ArborGraph-v1.3.0-portable.zip`).
- Generates cryptographic SHA-256 digest manifest (`dist\SHA256SUMS.txt`).

### 2. Side-by-Side Electron Desktop Preview Installer
```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-desktop-preview.ps1 -BuildInstaller
```
- Bundles the React 19 + TypeScript frontend with Electron 45.
- Publishes the self-contained out-of-process `ArborGraph.EngineHost.exe` runtime into `resources/engine/`.
- Generates side-by-side installer `dist\desktop-setup\ArborGraph-Desktop-Preview-Setup-v1.3.0-x64.exe`.

---

## Running Automated Tests

ArborGraph includes comprehensive test harnesses verifying functional, architectural, security, and protocol integrity:

### 1. 47-Stage Integration & QA Suite (WPF & Database Engine)
Validates SQLite WAL ingestion, multi-drive scoping, directory rollups, cryptographic SHA-256 duplicate pipelines, cancellation, junk cleaning, treemap algorithms, EULA gates, and persistent scan history:
```powershell
start.bat --test
```
Or directly via .NET:
```powershell
dotnet run --project tests\DiskScope.Tests.csproj -c Release
```

### 2. Dedicated Security & Data-Safety Audits
Validates system root deletion shields, relative path traversal guards, prefix collision boundaries, DOS device name safety, and SQL injection immunization:
```powershell
dotnet run --project tests\DiskScope.Tests.csproj -c Release --no-build -- --security
```

### 3. Out-of-Process Engine Host Protocol Suite
Tests stdin/stdout JSON-lines IPC protocol, validation limits, scan cancellation, rollups, and graceful shutdown:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\test-engine-host.ps1
```

### 4. Desktop Client Contract Tests
Runs TypeScript compiler verification and Node test runner contract tests:
```powershell
cd desktop
npm test
```

---

## Data & Configuration Locations

All runtime files are kept in standard local app data directories:

- **Database Index:** `%LOCALAPPDATA%\ArborGraph\scan_index.db`
- **Scan History Metadata:** `%LOCALAPPDATA%\ArborGraph\last_scan.json`
- **Scan History Snapshot:** `%LOCALAPPDATA%\ArborGraph\last_scan.db`
- **Application Log:** `%LOCALAPPDATA%\ArborGraph\app.log`
- **Settings:** `%LOCALAPPDATA%\ArborGraph\settings.json`

> [!NOTE]
> **Automatic Migration & Persistence:** On first run, ArborGraph automatically detects and migrates legacy databases and configuration from `%LOCALAPPDATA%\DiskScope` or `%LOCALAPPDATA%\DiskScopePro`. When reopening the application, previous completed scans are automatically restored from the local snapshot without requiring a rescan.

---

## Troubleshooting

### Multiple .NET SDKs in PATH
If `dotnet run` complains that a matching SDK could not be found, an older runtime in `C:\Program Files\dotnet` may have priority. Both `start.bat` and `start.ps1` handle this automatically by prioritizing `%USERPROFILE%\.dotnet`.

To set this manually in Command Prompt:
```cmd
set PATH=%USERPROFILE%\.dotnet;%PATH%
dotnet run --project DiskScope.csproj
```

### In-Use Files During Cleanup
System temporary files and browser cache databases locked by active processes (e.g. Chrome, Edge) cannot be deleted while those applications are open. ArborGraph skips locked files cleanly without aborting. Close web browsers before cleaning to maximize reclaimed space.

---

## Documentation & Legal

- **End User License Agreement:** See [LICENSE.md](LICENSE.md).
- **Third-Party Notices:** See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md).
- **Web Portal:** Run `start.bat --site` or open `site/index.html` in your browser.
