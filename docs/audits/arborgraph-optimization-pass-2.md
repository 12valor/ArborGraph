# Optimization pass 2: validation and measurements

30 October 2026. Baseline is the clean pre-split build at 267303a in `dist/perf-baseline`; candidate is the shared-core build at 2114971 in `dist/perf-after`. Existing sizes: installer 172,953,564 vs 140,207,972 bytes; stage 574,251,112 vs 451,034,715; engine 199,798,459 vs 76,582,062. This pass makes no application or packaging change, so additional savings are zero. These are existing built artifacts, not newly built installers during pass 2.

## Release gate

NOT PASSED. A previous ArborGraph Desktop Preview 1.3.0 is registered in `C:\Program Files\ArborGraph Desktop Preview`; candidate installer uses the same AppId. Running its upgrade/uninstaller on this machine risks changing that installation. No isolated Windows VM or Windows Sandbox executable was found in the available environment. Clean install, upgrade, shortcuts, uninstall, reinstall and installed-app functional testing were therefore not attempted. Prior staged tests include 53 WPF automated tests, nine security tests, six IPC tests and EngineHost fixture integration; see the pass 1 report. WPF visual workflows, native dialogs, removable drives and real inaccessible files remain unverified. Known unrelated scanner/duplicate findings in `arborgraph-audit-2026-10-29.md` are not resolved.

## Existing-database compatibility

Copied four prior synthetic fixture directories (`audit-sample`, `audit-edge`, `audit-hardlink`, `audit-hidden`) separately for each engine. Identical baseline and candidate JSON responses were obtained for overview, default file listing, partial search, root directory contents, largest folders, history, file-age and Photoshop insights, duplicate overview and shutdown: 10 successful requests per fixture. SQLite `user_version` and schema definitions were unchanged on each copy. The originals were not modified. This does not test all database versions, data migration, duplicate deletion, export dialogs or installed upgrade behavior.

## Engine startup measurements

Python started each EngineHost with a dedicated data directory, sent ping, waited for its response, and shut it down. Eleven sequential runs per build; first is reported separately, followed by ten warm runs. These timings include process launch and ping, not Electron, window creation or usable workspace. Baseline was always run before candidate; caches, load and ordering are uncontrolled, so this is not evidence of a speedup.

| Build | First ms | Min ms | Median ms | Mean ms | Max ms | Sample SD ms |
|---|---:|---:|---:|---:|---:|---:|
| Pre-split | 361.6 | 103.6 | 125.8 | 134.4 | 201.4 | 29.4 |
| Shared-core | 133.4 | 102.7 | 108.0 | 109.3 | 122.5 | 5.9 |

Baseline samples: 201.4, 168.1, 135.9, 103.6, 123.8, 114.4, 124.2, 127.4, 111.1, 133.7 ms. Candidate: 107.4, 105.0, 104.2, 111.8, 110.4, 122.5, 105.6, 102.7, 108.6, 114.8 ms. Full startup stages and process-tree memory were not instrumented.

## Small workload

Created the same synthetic fixture of 100 directories with 100 small text files each. Each build received its own clean data directory, accepted the EULA and scanned the same fixture once; the elapsed wall time runs from `startScan` request through `scanFinished` and includes indexing and rollup. Both reported Completed and 10,000 indexed files. Each query was then issued ten times through the engine, measured round-trip; medians below include serialization and IPC. Baseline always ran first; filesystem cache, single-run scanning and synthetic workload limit comparisons.

| Measurement | Pre-split | Shared-core |
|---|---:|---:|
| Scan including rollup, one run | 296.2 ms | 274.8 ms |
| Overview median, 10 queries | 1.28 ms | 1.18 ms |
| Partial filename search `item050`, median | 4.22 ms | 6.89 ms |
| Root directory contents, median | 0.78 ms | 1.02 ms |

These are not representative cold-disk benchmarks and do not establish improvements or regressions. Medium/large datasets, CPU, I/O, peak and process-tree memory, separate rollup timing, duplicate hashing and interaction latency were not measured. No scanner, query, treemap or renderer algorithm was changed.

## Publish and locale experiments

No additional production-size experiments were started: the request puts installed regression before further size reduction, and a safe test Windows installation environment is unavailable. The previous pass published a 2,653,648-byte framework-dependent engine without full installation, missing-runtime and compatibility tests; it is not adopted. Trimming, single-file, ReadyToRun and locale restrictions remain untested. The current self-contained build and all Chromium locale, graphics fallback and license files are retained. Largest remaining files are recorded in `arborgraph-desktop-size-optimization.md`.

## Next step

Run clean install, upgrade, functional checks and uninstall in a disposable Windows VM or Windows Sandbox with a copy of representative user data. Only after that release gate should controlled publish and locale experiments begin. This pass does not certify the build release-ready.
