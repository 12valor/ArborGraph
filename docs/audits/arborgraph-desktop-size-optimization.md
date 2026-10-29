# Desktop package optimization measurements

Baseline: commit 267303a, fresh isolated copy built with `scripts/build-desktop-preview.ps1 -BuildInstaller`, same Node/.NET/Windows and Inno Setup 6.6.1 as candidate. Candidate: shared core split, same script and machine. No configuration or compression change. NuGet's vulnerability endpoint was unavailable; cached packages restored. Bytes use binary MiB below.

| Metric | Baseline | Candidate | Change |
|---|---:|---:|---:|
| Installer | 172,953,564 | 140,207,972 | -32,745,592 bytes (31.23 MiB; 18.93%) |
| Staged preview | 574,251,112 | 451,034,715 | -123,216,397 bytes (117.51 MiB; 21.46%) |
| Engine directory | 199,798,459 | 76,582,062 | -123,216,397 bytes (117.51 MiB; 61.67%) |
| Staged files | 572 | 279 | -293 |
| Engine DLL files | 479 | 188 | -291 |
| Renderer HTML/CSS/JS | 307,108 | 307,108 | unchanged |

EngineHost previously referenced `DiskScope.csproj`, which enables WPF and references WPF-UI and LiveChartsCore.SkiaSharpView.WPF. These are still needed by WPF. The new core project compiles the existing UI-independent models, services and helpers as one shared assembly; EngineHost and WPF reference it. WPF-only file actions, updater and treemap presentation types remain in WPF. The Junk Cleaner XAML explicitly resolves its moved model in the core assembly. The SQLite package is referenced by the core. No source logic was duplicated and no DLLs were manually stripped.

Naturally absent from the engine publication: PresentationFramework.dll, PresentationCore.dll, Wpf.Ui.dll, libSkiaSharp.dll, libHarfBuzzSharp.dll, OpenTK.dll, wpfgfx_cor3.dll and System.Windows.Forms.Design.dll. WindowsBase.dll remains as a .NET framework assembly/facade; its presence alone is not proof WPF is active. The candidate remains self-contained win-x64.

One controlled framework-dependent publish produced 2,653,648 bytes in 12 files, but requires a compatible installed .NET runtime; installer, startup and functional comparisons for that variant were not completed. It was not adopted. Trimming, single-file engine publication, locale/graphics removal and renderer splitting were not adopted or fully evaluated; they need compatibility testing and measured benefit. Electron files, security configuration and installer compression are unchanged.

## Runtime observations

Engine process start to ping, measured on one machine with a dedicated test data directory: baseline 4,626 / 145 / 139 ms (first run created data and coincided with build work), candidate 186 / 148 / 155 ms. After two seconds, engine working sets were baseline 37,044,224 / 35,512,320 / 35,508,224 bytes and candidate 35,266,560 / 35,090,432 / 35,000,320 bytes. This small uncontrolled sample does not establish an improvement; neither run is a controlled cold-cache study. One baseline window-handle observation was 811 ms and 94,068,736-byte main-process working set. A local DevTools observation of first non-loading workspace text was 569 ms baseline and 580 ms candidate, each one run and not a full interactive/idle-memory benchmark. Windows may resolve known-folder locations independently of APPDATA environment overrides; graphical measurements are not proof of isolated user data. No scan, search, rollup, duplicate, large-scan memory, GPU or frame-time before/after benchmarks were performed. No runtime speedup is claimed.

## Verification

- Clean candidate WPF build and self-contained single-file publish succeeded; Electron production build and six IPC validation tests passed.
- Fresh candidate engine passed the existing process integration test: scan, indexed queries, rollups, insights, history comparison, cancellation and shutdown. This does not establish every workflow or an old-database migration.
- Candidate EngineHost opened a copy of a pre-existing audit fixture database, answered overview and history requests, and shut down cleanly. This is not a full user-database upgrade test.
- WPF automated QA: 53/53 passed with normal permissions. A sandbox run failed two history tests; normal-permission rerun passed. Security suite: 9/9 passed.
- First workspace text was observed in the candidate packaged Electron preview. It is not a full interactive regression. Native dialogs, report/export UI, restart with a pre-existing user database, WPF visual interaction, clean install/upgrade/uninstall, low-end graphics and representative performance tests remain unverified.
- Existing correctness audit findings remain outside this packaging change; size improvements do not repair them.

## Largest candidate files

Electron executable 238.23 MiB; dxcompiler.dll 24.59 MiB; resources.pak 12.75 MiB; System.Private.CoreLib.dll 12.56 MiB; icudtl.dat 10.32 MiB; System.Private.Xml.dll 7.63 MiB; LICENSES.chromium.html 5.44 MiB; vk_swiftshader.dll 5.20 MiB; coreclr.dll 4.76 MiB; d3dcompiler_47.dll 4.53 MiB; System.Linq.Expressions.dll 3.51 MiB; ffmpeg.dll 2.95 MiB; System.Data.Common.dll 2.73 MiB; Microsoft.DiaSymReader.Native.amd64.dll 2.27 MiB; System.Private.DataContractSerialization.dll 1.99 MiB; System.Security.Cryptography.dll 1.95 MiB; locales/ml.pak 1.70 MiB; clrjit.dll 1.70 MiB; locales/ta.pak 1.69 MiB; locales/kn.pak 1.66 MiB. Preserve licenses and graphics fallbacks without compatibility evidence.

## Remaining work

1. Validate installed upgrade, existing database/history, report/export, and WPF visual workflows before release (high priority, medium risk).
2. Collect repeatable startup, process-tree idle/peak memory and representative scan/search/rollup benchmarks before pursuing runtime changes (high priority, low risk).
3. Measure publish strategies and supported locale packaging with SQLite, software graphics and remote-desktop compatibility tests (medium priority, high risk).
4. Profile the bounded treemap and renderer before considering code splitting (low priority, medium risk).
