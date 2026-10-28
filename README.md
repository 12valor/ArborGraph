# ArborGraph

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![Latest Release: v1.3.1](https://img.shields.io/github/v/release/12valor/ArborGraph?label=Release&color=0078D4)](https://github.com/12valor/ArborGraph/releases/latest)
[![Framework: Electron 45](https://img.shields.io/badge/Framework-Electron%2045%20%2B%20React%2019-47848F?logo=electron&logoColor=white)](https://www.electronjs.org)
[![Engine: .NET 8.0](https://img.shields.io/badge/Engine-.NET%208.0%20x64-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Database: SQLite WAL](https://img.shields.io/badge/Database-SQLite%20(WAL)-003B57?logo=sqlite&logoColor=white)](https://sqlite.org)
[![Privacy: 100% Offline](https://img.shields.io/badge/Privacy-100%25%20Offline%20%2F%20Zero%20Telemetry-107C41)](LICENSE.md#3-local-architecture--privacy-commitment)
[![License: EULA](https://img.shields.io/badge/License-ArborGraph%20EULA-gray)](LICENSE.md)

**ArborGraph** is a high-performance Windows desktop filesystem analytics and storage optimization utility. Featuring a modern Dark Blue user interface powered by Electron and React 19, backed by an out-of-process .NET 8 engine host, ArborGraph catalogs local drives and folders into an embedded SQLite database in WAL mode, streams live traversal telemetry through an interactive directory feed, visualizes storage distribution with squarified treemaps, identifies duplicate files via cryptographic SHA-256 hashing, cleans system, browser, and developer cache stores, and features persistent scan history comparison.

ArborGraph operates 100% offline with zero telemetry, zero cloud dependencies, and zero background services.

---

## Table of Contents

- [Visual Identity & Design System](#visual-identity--design-system)
- [System Requirements](#system-requirements)
- [Quick Start](#quick-start)
- [Workspace Views](#workspace-views)
- [Core Architecture](#core-architecture)
- [Repository Structure](#repository-structure)
- [Building the Windows Installer](#building-the-windows-installer)
- [Running Automated Tests](#running-automated-tests)
- [Data & Configuration Locations](#data--configuration-locations)
- [Documentation & Legal](#documentation--legal)

---

## Visual Identity & Design System

- **Dark Blue Palette:** Built upon an intentional dark blue aesthetic featuring a deep midnight navy shell (`#0f1e36`), subtle container cards (`#142542`), Fluent blue primary accents (`#005fb8`), soft blue treemap tiles (`#93c5fd` & `#bfdbfe`), and clean slate neutral dividers (`#64748b`).
- **Airy Non-Bold Typography:** High-legibility, modern typography utilizing `Segoe UI Semilight` and `Segoe UI Light` with light `font-weight: 300` display metrics and standard `font-weight: 400` body elements, avoiding artificial heavy font weights.

---

## System Requirements

- **Operating System:** Windows 10 (version 1809 or later) or Windows 11, 64-bit (`x64`)
- **For Installer:** No external dependencies required (bundled Electron runtime and self-contained .NET 8 engine)
- **For Building from Source:**
  - [Node.js](https://nodejs.org) (v20+ recommended)
  - [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)
  - [Inno Setup 6](https://jrsoftware.org/isdl.php) (for compiling installer packages)

---

## Quick Start

### 1. Download Official Installer

Download the official setup package directly from [GitHub Releases](https://github.com/12valor/ArborGraph/releases/latest):
- **Installer:** `ArborGraph-Desktop-Preview-Setup-v1.3.1-x64.exe` (164.8 MiB)
- **SHA-256:** `3156F60381C3C11A92BB14CE5130942394560CDBF82DD96B5C0DFE711FEC19F9`

### 2. Run from Source

To build and launch ArborGraph locally from source:

1. **Build the Engine Host:**
   ```powershell
   dotnet publish engine/ArborGraph.EngineHost.csproj -c Release -r win-x64 --self-contained true
   ```

2. **Launch the Desktop Application:**
   ```powershell
   cd desktop
   npm ci
   npm run build
   npm start
   ```

---

## Workspace Views

ArborGraph organizes storage inspection into dedicated workspace panels:

1. **Overview:**
   - Total, used, and available volume storage summary.
   - Interactive hierarchical directory drilldown with instant breadcrumb reset.
   - Embedded real-time scan traversal feed.

2. **Scanner:**
   - **Target Configuration & Controls:** Drive selector checkboxes (`C:\`, etc.), custom target folder input, folder browse dialog, and responsive **Start Scan** / **Stop Scan** controls.
   - Real-time disk indexing console showing current target, active scan state, and progress indicator.
   - Live metrics: files indexed, folders processed, traversal speed (files/sec), and elapsed time.
   - **Live Directory Feed:** Rolling monospace traversal history buffer.

3. **Files:**
   - Searchable filesystem explorer backed by indexed SQLite queries.
   - Sortable columns: Name, Path, Size, Extension, Category, and Modified Date.
   - Context actions: reveal in Windows Explorer, copy file path, or send to Recycle Bin.

4. **Treemap:**
   - Proportional squarified visual space map powered by the Bruls-Huizing-van Wijk layout algorithm.
   - Interactive drilldown into nested folders with instant breadcrumb trail navigation.
   - Category badges, exact file sizes, and high-contrast dark blue tile backing.

5. **Analytics & File Age:**
   - Category-based disk space breakdown and file age distribution analysis.
   - Identification of dormant or forgotten assets across selected drives.

6. **Duplicates:**
   - 3-tier detection pipeline: exact file size match &rarr; header verification &rarr; full SHA-256 chunked cryptographic hash.
   - Redundant file group review and safe deletion.

7. **Developer Storage:**
   - Contextual workspace analyzer for software engineering caches:
     - **Node.js:** `node_modules` folders and npm cache
     - **.NET:** Project `bin` / `obj` directories and NuGet packages
     - **Rust:** `target` directories (verified by `Cargo.toml`) and Cargo cache
     - **Gradle & Python:** Build directories and pip caches

8. **Photoshop Inspector:**
   - Dedicated inspector for Adobe Photoshop (`.psd`) and Large Document (`.psb`) assets.
   - Audit of heavy working files, backup saves, and Adobe scratch file disk usage.

9. **Scan History & Comparison:**
   - Compare storage deltas between historical scan runs across identical roots.

10. **Reports & Exports:**
    - Export scan analytics in multiple portable formats:
      - **Interactive HTML Audit Report:** Self-contained dashboard with dark/light themes.
      - **Full JSON Snapshot:** Complete structured filesystem snapshot dump.
      - **Tabular CSV Exports:** Dedicated CSV data exports for indexed files.

---

## Core Architecture

ArborGraph separates presentation from data indexing via an out-of-process engine architecture:

```
+-------------------------------------------------------------------------------+
|                 Electron Desktop Application (React 19 + TypeScript)          |
|      Modern Dark Blue Theme | Segoe UI Semilight | Treemap | Live Scanner     |
+-------------------------------------------------------------------------------+
                                        ^
                                        |  (Preload ContextBridge / Stdin-Stdout IPC)
                                        v
+-------------------------------------------------------------------------------+
|             Out-of-Process Engine Host (ArborGraph.EngineHost.exe)            |
|                JSON-lines Protocol over Stdin / Stdout (IPC)                  |
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

- **Frontend:** Electron 45, React 19, TypeScript, and Vite.
- **Engine Host:** Standalone .NET 8 engine (`ArborGraph.EngineHost.exe`) communicating over standard input/output with structured JSON-lines messages.
- **Persistence:** Local SQLite database via `Microsoft.Data.Sqlite` in Write-Ahead Logging (`WAL`) mode with `synchronous=NORMAL`.
- **Concurrency:** Bounded `System.Threading.Channels` pipeline streaming file records from multi-threaded traversal tasks directly into bulk SQLite transaction workers.

---

## Repository Structure

```
arborgraph/
├── desktop/                     # Electron 45 / React 19 desktop application
│   ├── main/                    # Electron main process, IPC handlers, validation contracts
│   ├── renderer/                # React 19 + TypeScript views (Dark Blue theme)
│   └── package.json             # Desktop dependencies and build scripts
├── engine/                      # Standalone .NET 8 headless engine host (IPC protocol)
│   ├── ArborGraph.EngineHost.csproj
│   └── Program.cs               # JSON-lines stdio protocol server
├── installer/                   # Inno Setup 6 packaging scripts:
│   └── desktop-preview.iss      # Desktop installer configuration
├── Models/                      # File records, drive metrics, duplicate groups
├── Resources/                   # Vector assets, themes, and icons:
│   ├── brand/                   # Official logo files, icons, and mascot graphics
│   └── Fonts/                   # Bundled fonts and typography assets
├── scripts/                     # Packaging, test, and release automation scripts:
│   ├── build-desktop-preview.ps1 # Electron packaging & installer build script
│   └── test-engine-host.ps1     # Automated IPC protocol test harness
├── Services/                    # Core business logic and SQLite database engine
├── site/                        # Official web portal and documentation
├── tests/                       # Automated QA integration test suite
├── CHANGELOG.md                 # Semantic version release log
├── LICENSE.md                   # End User License Agreement
├── README.md                    # Project documentation and architecture guide
└── THIRD_PARTY_LICENSES.md      # Open source license notices
```

---

## Building the Windows Installer

To build the self-contained Windows installer package:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-desktop-preview.ps1 -BuildInstaller
```

This automated pipeline:
1. Compiles the TypeScript and Vite React frontend in `desktop/`.
2. Packages the Electron shell runtime.
3. Publishes the self-contained out-of-process `ArborGraph.EngineHost.exe` runtime into `resources/engine/`.
4. Invokes Inno Setup 6 to compile the installer into `dist\desktop-setup\ArborGraph-Desktop-Preview-Setup-v1.3.1-x64.exe`.

---

## Running Automated Tests

### 1. Desktop Client Contract Tests
Runs TypeScript compiler verification and Node test runner contract tests:
```powershell
npm test --prefix desktop
```

### 2. Engine Host IPC Protocol Suite
Tests stdin/stdout JSON-lines IPC protocol, validation limits, scan cancellation, rollups, and graceful shutdown:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\test-engine-host.ps1
```

### 3. Core Engine & SQLite QA Suite
Validates SQLite WAL ingestion, multi-drive scoping, directory rollups, cryptographic SHA-256 duplicate pipelines, cancellation, junk cleaning, and treemap algorithms:
```powershell
dotnet run --project tests\DiskScope.Tests.csproj -c Release
```

### 4. Security & Data-Safety Audits
Validates system root deletion shields, relative path traversal guards, prefix collision boundaries, and DOS device name safety:
```powershell
dotnet run --project tests\DiskScope.Tests.csproj -c Release --no-build -- --security
```

---

## Data & Configuration Locations

All runtime files are kept in standard local app data directories:

- **Database Index:** `%LOCALAPPDATA%\ArborGraph\scan_index.db`
- **Scan History Metadata:** `%LOCALAPPDATA%\ArborGraph\last_scan.json`
- **Scan History Snapshot:** `%LOCALAPPDATA%\ArborGraph\last_scan.db`
- **Application Log:** `%LOCALAPPDATA%\ArborGraph\app.log`
- **Settings:** `%LOCALAPPDATA%\ArborGraph\settings.json`

---

## Documentation & Legal

- **End User License Agreement:** See [LICENSE.md](LICENSE.md).
- **Third-Party Notices:** See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md).
- **Web Portal:** Open `site/index.html` in your browser.
