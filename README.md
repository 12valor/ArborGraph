# DiskScope

DiskScope is a Windows desktop application that scans drives and folders, charts disk usage, finds duplicates, and flags space hogs. It is written in C# and WPF on .NET 8, backed by an embedded SQLite database in WAL mode.

## System requirements

- Windows 10 (version 1809+) or Windows 11, 64-bit (x64)
- For source builds: .NET 8.0 SDK
- For standalone release binaries: no dependencies required (the .NET runtime is bundled)

## Quick start

### Double-click launcher

Double-click `start.bat` in File Explorer.

If the .NET 8 SDK is installed, `start.bat` runs an incremental build and launches the app in the background. If the SDK is not present, it launches the precompiled standalone executable from `bin\publish\DiskScope.exe`.

### Command-line options

From Command Prompt:

```cmd
start.bat            :: Build (if SDK found) and launch GUI
start.bat --fast     :: Launch existing executable immediately without building
start.bat --dev      :: Run attached to the console with live logs (dotnet run)
start.bat --publish  :: Launch the standalone self-contained release build
start.bat --test     :: Run the automated integration test suite
start.bat --help     :: Print usage information
```

From PowerShell:

```powershell
.\start.ps1           # Standard launch
.\start.ps1 -Fast     # Skip build, launch precompiled binary
.\start.ps1 -Dev      # Attached console mode
.\start.ps1 -Test     # Run integration tests
.\start.ps1 -Publish  # Launch standalone build
```

## Features

- **Drive and folder analysis:** Scans selected drives or custom folders, showing directory size trees and capacity bars.
- **File category breakdown:** Groups storage by type (documents, media, archives, code, executables).
- **Largest files and folders:** Sortable lists ranking the heaviest space consumers.
- **Duplicate finder:** Three-stage detection (size filter, header check, full SHA-256 hash) to avoid false positives.
- **System and developer junk cleaner:** Scans and purges user temp files, crash dumps, Windows Update downloads, web browser caches (Chrome, Edge, Firefox, Brave), and developer stores (NuGet, npm, pip, Cargo) with automatic locked-file skipping.
- **Photoshop inspector:** Dedicated views for `.psd` and `.psb` working files and cache buildup.
- **Old files filter:** Lists files untouched for 1, 2, or 3+ years.
- **Safe deletion:** Sends files to the Windows Recycle Bin or deletes permanently on confirmation.
- **Analytics:** Tracks scan history and calculates reclaimable disk space across scans.

## Building from source

Clone the repository and build with the .NET CLI:

```cmd
git clone https://github.com/12valor/C-file-scanner.git
cd C-file-scanner
dotnet build DiskScope.csproj -c Release
```

The compiled binary will be located at:
`bin\Release\net8.0-windows\DiskScope.exe`

## Publishing a standalone release

To generate a self-contained release folder that runs on any 64-bit Windows machine without requiring .NET 8 to be pre-installed:

```cmd
dotnet publish DiskScope.csproj -c Release -r win-x64 --self-contained true -o bin\publish
```

The output in `bin\publish\` contains `DiskScope.exe` and all needed runtime libraries. You can zip this folder for distribution.

## Running tests

The test project validates scanner traversal, SQLite index creation, SHA-256 duplicate detection, cancellation tokens, and analytics queries.

Run tests using the launcher:

```cmd
start.bat --test
```

Or directly via dotnet:

```cmd
dotnet run --project tests\DiskScope.Tests.csproj
```

## Data and log locations

DiskScope stores its database and logs under `%LOCALAPPDATA%\DiskScopePro`:

- Database: `%LOCALAPPDATA%\DiskScopePro\scan_index.db`
- Application log: `%LOCALAPPDATA%\DiskScopePro\app.log`

To reset scan data, delete `scan_index.db` while the application is closed.

## Troubleshooting

### Multiple .NET versions in PATH
If `dotnet run` complains that no .NET Core SDK could be found, you may have an older global runtime in `C:\Program Files\dotnet` taking precedence over a user-level .NET 8 install. `start.bat` and `start.ps1` resolve this automatically by prepending `%USERPROFILE%\.dotnet` to `PATH`.

If running manually in a standard command prompt, add your .NET 8 directory first:

```cmd
set PATH=%USERPROFILE%\.dotnet;%PATH%
dotnet run
```
