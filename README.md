# DiskScope — Windows Storage Analyzer

DiskScope is a high-performance Windows desktop storage analyzer and disk cleaner built with WPF, .NET 8, and SQLite.

---

## Quick Start

You can start DiskScope immediately using the provided start scripts:

### Using Batch (Command Prompt or Double-Click in File Explorer)

Simply double-click **`start.bat`** or run:

```cmd
start.bat
```

#### Launch Options:
- `start.bat` — Incremental build (if SDK is present) and launches DiskScope GUI
- `start.bat --fast` — Instantly launches the precompiled executable without checking for updates/rebuilding
- `start.bat --dev` — Runs attached to console with live output (`dotnet run`)
- `start.bat --publish` — Launches the self-contained standalone release build in `bin\publish\`
- `start.bat --test` — Runs the automated integration test suite
- `start.bat --help` — Shows command-line help

---

### Using PowerShell

Run **`start.ps1`**:

```powershell
.\start.ps1
```

#### Launch Options:
- `.\start.ps1` — Standard build and launch
- `.\start.ps1 -Fast` — Quick launch precompiled binary
- `.\start.ps1 -Dev` — Run attached to console with live logs
- `.\start.ps1 -Publish` — Launch standalone published build
- `.\start.ps1 -Test` — Run integration tests

---

## Features

- **High-Throughput Scanner Engine**: Multi-threaded traversal with real-time statistics and SQLite WAL indexing.
- **Drive & Folder Overview**: Storage breakdown across drives, file types, and directory trees.
- **Photoshop Intelligence**: Special asset analyzer for `.psd` and `.psb` working files and cache.
- **Duplicate Detection**: 3-stage cryptographic duplicate detection (Size -> Quick Hash -> Full SHA-256) with zero false positives.
- **Old & Large Files**: Filter and inspect stale data and largest disk space consumers.
- **Safe File Management**: Recycle bin trash, secure deletion, and path exploration.
