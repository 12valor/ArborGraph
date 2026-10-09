# ArborGraph

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![Latest Release: v1.1.1](https://img.shields.io/github/v/release/12valor/ArborGraph?label=Release&color=0078D4)](https://github.com/12valor/ArborGraph/releases/latest)
[![Runtime: .NET 8.0](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Database: SQLite WAL](https://img.shields.io/badge/Database-SQLite%20(WAL)-003B57?logo=sqlite&logoColor=white)](https://sqlite.org)
[![Privacy: 100% Offline](https://img.shields.io/badge/Privacy-100%25%20Offline%20%2F%20Zero%20Telemetry-107C41)](LICENSE.md#3-local-architecture--privacy-commitment)
[![License: EULA](https://img.shields.io/badge/License-ArborGraph%20EULA-gray)](LICENSE.md)

**ArborGraph** is a high-performance Windows desktop filesystem analytics and storage utility built with C# and WPF on .NET 8. It catalogs local drives and folders into an embedded SQLite database in WAL mode, streams live traversal telemetry through an interactive directory feed, visualizes storage distribution with squarified treemaps, identifies duplicate files via cryptographic SHA-256 hashing, and cleans system, browser, and developer cache stores.

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
   - Squarified, color-coded visual space map of directory hierarchies.
   - Interactive drilldown into nested folders with instant breadcrumb navigation.
   - Mathematically exact folder rollup aggregation.

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

```
+-------------------------------------------------------------------------------+
|                           WPF Desktop UI (XAML)                               |
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
```

- **Runtime:** .NET 8.0 Windows Desktop SDK (`net8.0-windows`, x64).
- **Presentation:** Windows Presentation Foundation (WPF) with custom styling and asynchronous UI binding.
- **Persistence:** Local SQLite database via `Microsoft.Data.Sqlite` in Write-Ahead Logging (`WAL`) mode with `synchronous=NORMAL` and cache sizing pragmas.
- **Concurrency:** Bounded `System.Threading.Channels` pipeline streaming file records from multi-threaded traversal tasks directly into bulk SQLite transaction workers.

---

## Repository Structure

```
arborgraph/
├── App.xaml / App.xaml.cs       # Application entry point and exception logging
├── DiskScope.csproj             # .NET 8 project definition (Produces ArborGraph.exe)
├── ArborGraph.exe               # Precompiled compressed standalone binary
├── start.bat / start.ps1        # Universal CLI and GUI launchers
├── Controls/                    # Custom WPF controls (Treemap canvas, Metric graphs)
├── Infrastructure/              # Base classes (RelayCommand, ObservableObject)
├── Models/                      # File records, drive metrics, duplicate groups
├── Resources/                   # Vector assets, themes, and ArborGraph icons
├── Services/                    # Core business logic:
│   ├── DatabaseService.cs       # SQLite schema, ingestion, and query engine
│   ├── DeveloperStorageService.cs# Developer workspace detection and cache clean
│   ├── DiskService.cs           # Drive enumeration and volume geometry
│   ├── DuplicateAnalyzer.cs     # 3-stage cryptographic duplicate engine
│   ├── ExportService.cs         # CSV, JSON, and HTML report generator
│   ├── FileActionService.cs     # Recycle Bin and permanent deletion
│   ├── JunkCleanerService.cs    # System and browser cache scanners
│   ├── ScannerService.cs        # BFS traversal, streaming channel, and live feed
│   ├── SettingsService.cs       # Configuration persistence and exclusions
│   └── UsnJournalService.cs     # NTFS USN Change Journal reader
├── ViewModels/                  # MVVM view models for all 9 workspace views
├── Views/                       # WPF XAML views for all workspace panels
├── installer/                   # Inno Setup 6 packaging script (`installer.iss`)
├── site/                        # Offline documentation and setup wizard portal
├── tests/                       # Automated integration test suite (`DiskScope.Tests.csproj`)
├── LICENSE.md                   # End User License Agreement
└── THIRD_PARTY_LICENSES.md      # Open source license notices
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

## Building the Windows Installer

ArborGraph includes an Inno Setup script configured for packaging a standard Windows installer:

1. Install [Inno Setup 6](https://jrsoftware.org/isdl.php).
2. Generate the single-file published binary:
   ```cmd
   dotnet publish DiskScope.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o dist
   ```
3. Compile the installer script:
   ```cmd
   iscc installer\installer.iss
   ```
4. The generated installer will be located in `dist\setup\ArborGraph-Setup-1.0.0-x64.exe`.

---

## Running Automated Tests

The test suite validates the full operational lifecycle across 32 automated test suites: SQLite ingestion in WAL mode, recursive directory rollups, cryptographic duplicate detection, safe cancellation, junk file deletion, treemap layout, live directory feed buffer, XAML StaticResource integrity, and report exports.

Run the test suite via the launcher:
```cmd
start.bat --test
```

Or directly via `dotnet`:
```cmd
dotnet run --project tests\DiskScope.Tests.csproj
```

---

## Data & Configuration Locations

All runtime files are kept in standard local app data directories:

- **Database Index:** `%LOCALAPPDATA%\ArborGraph\scan_index.db`
- **Application Log:** `%LOCALAPPDATA%\ArborGraph\app.log`
- **Settings:** `%LOCALAPPDATA%\ArborGraph\settings.json`

> [!NOTE]
> **Automatic Migration:** On first run, ArborGraph automatically detects and migrates legacy databases and configuration from `%LOCALAPPDATA%\DiskScope` or `%LOCALAPPDATA%\DiskScopePro`, preserving all scan indexes and custom settings.

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
