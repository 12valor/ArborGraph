# Electron migration: engine-host foundation

The first migration stage adds `engine/ArborGraph.EngineHost.csproj`. It is a separate console process that calls the current `DatabaseService`, `ScannerService`, `SettingsService`, and `DiskService`. It does not create a WPF window or write protocol diagnostics to stdout. The existing WPF app remains the supported release and fallback.

This host currently references `DiskScope.csproj`, so it still requires the Windows Desktop runtime and is **not yet a WPF-independent core library**. Moving reusable services and models into a separate library is a later stage, after parity tests cover their behavior. The host does not provide cleanup, deletion, update, export, or general file browsing commands yet.

## Run locally

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build engine/ArborGraph.EngineHost.csproj -c Release
./scripts/test-engine-host.ps1
```

For an isolated test database, launch `engine/bin/Release/net8.0-windows/ArborGraph.EngineHost.exe --data-dir <directory>`. Without `--data-dir`, the existing `%LOCALAPPDATA%\ArborGraph` database and settings are used. In that mode, the host acquires WPF's existing single-instance mutex and refuses to start if WPF owns it. Electron main must also own the host process and enforce one active application instance.

## Protocol version 1

Requests and responses are one UTF-8 JSON object per line. Each request has a string `id` of 1–128 characters and an allowlisted `command`. Maximum request length is 65,536 characters. Responses include `type: "response"`, the matching `id`, `ok`, and either `data` or an `error` with a stable `code` and readable `message`. Scanner events include `type: "event"`, `eventName`, and an increasing `sequence`. Stdout carries only these messages; stderr carries diagnostics.

| Command | Input | Result |
| --- | --- | --- |
| `ping` | None | Engine name and protocol version |
| `listDrives` | None | Available drive geometry |
| `getOverview` | None | Current indexed file count, logical bytes, and categories |
| `startScan` | `roots`: 1–16 existing, distinct directories | Acceptance response, then `scanProgress` and `scanFinished` or `scanFailed` events |
| `cancelScan` | None | Cancellation acknowledgment, or `NO_ACTIVE_SCAN` |
| `shutdown` | None | Acknowledgment; cancels an active scan and exits |

`startScan` uses a full scan. A second active scan returns `SCAN_IN_PROGRESS`. Invalid or oversized messages and unknown commands receive errors. The scan event reports logical indexed bytes, discovered/indexed counts, skipped counts, elapsed time, and state. It does not imply physical disk allocation or complete filesystem coverage.

## Next boundaries

- Extract the reusable services and transport DTOs from the WPF project while keeping existing data files and tests compatible.
- Add Electron main and a narrow preload bridge. Validate every IPC command, block arbitrary navigation and Node access, and allow scan targets only after trusted drive selection or an OS folder picker.
- Add renderer screens incrementally and compare each workflow with WPF on the same fixtures.
- Preserve the existing installer and update path until the new package passes clean-install, upgrade, and data-recovery tests.
