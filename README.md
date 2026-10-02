# DiskScope

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![Runtime: .NET 8.0](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Database: SQLite WAL](https://img.shields.io/badge/Database-SQLite%20(WAL)-003B57?logo=sqlite&logoColor=white)](https://sqlite.org)
[![Privacy: 100% Offline](https://img.shields.io/badge/Privacy-100%25%20Offline%20%2F%20Zero%20Telemetry-107C41)](LICENSE.md#3-local-architecture--privacy-commitment)
[![License: EULA](https://img.shields.io/badge/License-DiskScope%20EULA-gray)](LICENSE.md)

DiskScope is a high-performance Windows desktop storage analyzer and disk cleanup utility built with C# and WPF on .NET 8. It catalogs drives and directories into an embedded SQLite database in WAL mode, visualizes disk usage with interactive treemaps, flags duplicate files via SHA-256 hashing, and cleans system, browser, and developer cache stores.

DiskScope runs completely offline with zero telemetry, zero cloud dependencies, and zero background services.

---

## Table of Contents

- [System Requirements](#system-requirements)
- [Quick Start](#quick-start)
  - [Launcher Scripts](#launcher-scripts)
  - [Command-Line Options](#command-line-options)
- [Key Features](#key-features)
- [Architecture & Tech Stack](#architecture--tech-stack)
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
- **For standalone binaries / installer:** No external dependencies required (the .NET runtime is bundled)
- **For building from source:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)

---

## Quick Start

### Launcher Scripts

The repository includes convenient launcher scripts (`start.bat` and `start.ps1`) that automatically locate your .NET 8 SDK or fall back to precompiled standalone binaries.

**Command Prompt / File Explorer:**
```cmd
start.bat
```

**PowerShell:**
```powershell
.\start.ps1
```

### Command-Line Options

Both launchers support instant launch modes, live developer consoles, integration testing, and documentation viewing:

| Command (CMD) | Command (PowerShell) | Description |
| :--- | :--- | :--- |
| `start.bat` | `.\start.ps1` | Standard launch: compiles incremental changes (if SDK found) and runs GUI |
| `start.bat --fast` | `.\start.ps1 -Fast` | Skip build step; launch the precompiled executable immediately |
| `start.bat --dev` | `.\start.ps1 -Dev` | Run attached to console with real-time logs (`dotnet run`) |
| `start.bat --publish` | `.\start.ps1 -Publish` | Build and run a self-contained, single-file release |
| `start.bat --site` | `.\start.ps1 -Site` | Open the companion documentation and setup portal in your browser |
| `start.bat --test` | `.\start.ps1 -Test` | Run the automated integration test suite |
| `start.bat --help` | `.\start.ps1 -?` | Display command-line usage information |

---

## Key Features

- **High-Speed Drive & Folder Analysis:**
  - Auto-discovers all ready system volumes (capacity, free space, filesystem format).
  - Multi-threaded traversal engine using `System.Threading.Channels` producer-consumer pipeline.
  - Optional NTFS USN Journal reader for rapid MFT indexing on supported volumes.
- **Interactive Visual Treemap:**
  - Squarified, color-coded space map of folders and files.
  - Interactive drill-down navigation with breadcrumb trail and instant parent back-tracking.
- **Duplicate File Detection:**
  - Multi-tier detection: byte size match &rarr; 4 KB header verification &rarr; full SHA-256 chunked hash.
  - Side-by-side duplicate group review with one-click selection of oldest or newest files.
- **Targeted Junk Cleaner:**
  - **System:** User temporary files, crash dumps, Windows Update download cache, prefetch files.
  - **Web Browsers:** Google Chrome, Microsoft Edge, Mozilla Firefox, and Brave caches.
  - Automatic skip logic for in-use or locked system files.
- **Developer Storage Cleaner:**
  - Analyzes and clears package caches for NuGet (`~/.nuget/packages`), npm (`%LocalAppData%\npm-cache`), pip (`%LocalAppData%\pip\cache`), Cargo (`~/.cargo`), Gradle, and Docker Desktop cache.
- **Photoshop & Media Inspector:**
  - Dedicated inspector for heavy `.psd` and `.psb` working files and Adobe scratch buildup.
- **Largest Files & Folders:**
  - Sortable ranking of top storage consumers across any scanned volume or directory.
- **Old Files Filter:**
  - Flags files unmodified for 1, 2, or 3+ years to reclaim dormant drive space.
- **Safe File Operations:**
  - Shell-integrated deletion directly to the Windows Recycle Bin by default, with permanent deletion available on explicit confirmation.
  - Quick access to reveal files in Windows File Explorer or copy paths to clipboard.
- **Export & Historical Analytics:**
  - Export full scan inventories and audit lists to CSV, JSON, or standalone HTML reports.
  - Tracks scan history, directory changes, and cumulative space reclaimed over time.

---

## Architecture & Tech Stack

```
+------------------------------------------------------------------+
|                    WPF User Interface (XAML)                     |
|  Overview | Treemap | Duplicates | Junk Cleaner | Developer Hub  |
+------------------------------------------------------------------+
                                |
                                v
+------------------------------------------------------------------+
|                   MVVM Presentation Layer                        |
|        RelayCommand | ObservableObject | ViewModels              |
+------------------------------------------------------------------+
                                |
                                v
+------------------------------------------------------------------+
|                        Core Services                             |
|  ScannerService | UsnJournalService | DuplicateAnalyzer         |
|  JunkCleanerService | DeveloperStorageService | FileActionService|
+------------------------------------------------------------------+
             |                                    |
             v                                    v
+------------------------+          +------------------------------+
|     Local SQLite       |          |      Windows OS APIs         |
|  WAL Mode, In-Memory   |          |  Kernel32, Shell32           |
|  Index & Query Cache   |          |  Recycle Bin, NTFS Journal   |
+------------------------+          +------------------------------+
```

- **Runtime:** .NET 8.0 Windows Desktop SDK (`net8.0-windows`, x64).
- **Presentation:** Windows Presentation Foundation (WPF) with custom styling and asynchronous UI binding.
- **Persistence:** Embedded SQLite via `Microsoft.Data.Sqlite` (8.0.10) with Write-Ahead Logging (`WAL`), `synchronous=NORMAL`, and optimized memory cache pragmas.
- **Concurrency:** Bounded `System.Threading.Channels` streaming file records from traversal tasks into bulk SQLite transaction workers.

---

## Repository Structure

```
diskscope/
├── App.xaml / App.xaml.cs       # Application entry point, global exception handlers
├── DiskScope.csproj             # .NET 8 project definition and assembly metadata
├── DiskScope.exe                # Precompiled standalone executable
├── start.bat / start.ps1        # Universal CLI and GUI launchers
├── Controls/                    # Custom WPF controls (Treemap canvas, metric cards)
├── Infrastructure/              # Base classes (RelayCommand, ObservableObject)
├── Models/                      # File records, drive metrics, duplicate groups
├── Resources/                   # Application icons and vector assets
├── Services/                    # Core business logic:
│   ├── DatabaseService.cs       # SQLite schema, indexing, and query engine
│   ├── DeveloperStorageService.cs# Dev package cache discovery and purge
│   ├── DiskService.cs           # Drive enumeration and volume geometry
│   ├── DuplicateAnalyzer.cs     # Multi-stage SHA-256 duplicate detection
│   ├── ExportService.cs         # CSV, JSON, and HTML report generator
│   ├── FileActionService.cs     # Recycle Bin and permanent file removal
│   ├── JunkCleanerService.cs    # System and browser cache scanners
│   ├── ScannerService.cs        # BFS traversal and bounded-channel streaming
│   ├── SettingsService.cs       # Local user configuration persistence
│   └── UsnJournalService.cs     # Direct NTFS Change Journal reader
├── ViewModels/                  # MVVM view models for all tabs
├── Views/                       # WPF XAML views for all workspace panels
├── installer/                   # Inno Setup 6 script (`installer.iss`)
├── site/                        # Offline documentation and setup wizard portal
├── tests/                       # Integration test project (`DiskScope.Tests.csproj`)
├── LICENSE.md                   # End User License Agreement
├── THIRD_PARTY_LICENSES.md      # Open source license notices
└── DISKSCOPE_PRO_AUDIT_REPORT.md# Technical verification and audit report
```

---

## Building from Source

Ensure the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) is installed and available in your `PATH`.

```cmd
git clone https://github.com/12valor/C-file-scanner.git
cd C-file-scanner
dotnet build DiskScope.csproj -c Release
```

The output binary will be located at:
```
bin\Release\net8.0-windows\DiskScope.exe
```

---

## Publishing Standalone Releases

To generate a self-contained, single-file executable that runs on any 64-bit Windows machine without requiring .NET 8 to be installed:

```cmd
dotnet publish DiskScope.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o dist
```

Or via launcher:
```cmd
start.bat --publish
```

The resulting standalone executable is generated at `dist\DiskScope.exe`.

---

## Building the Windows Installer

DiskScope includes an Inno Setup script configured for standard Windows installation:

1. Install [Inno Setup 6](https://jrsoftware.org/isdl.php).
2. Generate the published binary into `dist/` or `bin\publish\`:
   ```cmd
   dotnet publish DiskScope.csproj -c Release -r win-x64 --self-contained true -o dist
   ```
3. Compile the installer script:
   ```cmd
   iscc installer\installer.iss
   ```
4. The signed installer output will be created in `installer\Output\DiskScope_Setup.exe`.

---

## Running Automated Tests

The test suite validates filesystem traversal, SQLite batch ingestion, duplicate hashing integrity, cancellation token responsiveness, and report exports.

Run tests using the launcher:
```cmd
start.bat --test
```

Or directly via `dotnet`:
```cmd
dotnet run --project tests\DiskScope.Tests.csproj
```

---

## Data & Configuration Locations

DiskScope stores its runtime files inside the user profile directory:

- **Database:** `%LOCALAPPDATA%\DiskScope\scan_index.db`
- **Application Log:** `%LOCALAPPDATA%\DiskScope\app.log`
- **Settings:** Stored alongside the local SQLite catalog

To reset all scanned data, close DiskScope and delete `%LOCALAPPDATA%\DiskScope\scan_index.db`.

---

## Troubleshooting

### Multiple .NET Versions in PATH
If `dotnet run` complains that a matching SDK could not be found, an older global runtime in `C:\Program Files\dotnet` may take precedence over your user-level .NET 8 install. `start.bat` and `start.ps1` resolve this automatically by prepending `%USERPROFILE%\.dotnet` to `PATH`.

To resolve this manually in Command Prompt:
```cmd
set PATH=%USERPROFILE%\.dotnet;%PATH%
dotnet run --project DiskScope.csproj
```

### Locked Files During Cleanup
Certain Windows temporary files or browser lockfiles (`lock`, `LOG`) cannot be deleted while their host processes (e.g., Chrome, Edge) are running. DiskScope automatically skips locked files without halting the cleanup process. For maximum space recovery, close web browsers before running the Junk Cleaner.

---

## Documentation & Legal

- **End User License Agreement:** See [LICENSE.md](LICENSE.md).
- **Third-Party Notices:** See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) for licenses covering SQLite, .NET Runtime, and dependencies.
- **Technical Audit Report:** Complete codebase verification report in [DISKSCOPE_PRO_AUDIT_REPORT.md](DISKSCOPE_PRO_AUDIT_REPORT.md).
- **Web Portal & Setup Guide:** Launch `start.bat --site` or open `site/index.html` in any browser.
