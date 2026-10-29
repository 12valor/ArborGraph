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

## Additional isolated measurements

An Electron development-mode harness uses the production renderer and each built engine, a dedicated copied 10,000-file index and separate Chromium profiles. `ARBORGRAPH_DEV_DATA_DIR` is used only in development mode; the installed preview and its default engine data directory are not used. Each launch waits for an Overview containing `10,000`, then closes normally. The first launch of each build is excluded; ten later launches alternate order. This measures launch to rendered indexed overview, not a distinct app-ready/spawn/ping/interactive breakdown or controlled cold startup. The DevTools connection and polling add overhead.

| Build | First ms | Min ms | Median ms | Mean ms | Max ms | Sample SD ms |
|---|---:|---:|---:|---:|---:|---:|
| Pre-split | 1714.4 | 411.8 | 499.7 | 502.6 | 669.1 | 69.9 |
| Shared-core | 719.4 | 407.5 | 473.9 | 474.4 | 528.5 | 35.5 |

Warm baseline samples: 540.3, 460.4, 512.9, 518.3, 447.5, 411.8, 497.5, 466.1, 669.1, 501.8 ms. Candidate: 484.6, 407.5, 479.3, 468.4, 436.1, 465.4, 486.7, 519.5, 467.5, 528.5 ms. Small samples, warm caches and DevTools make causation uncertain; no startup improvement is claimed.

A single run per build scanned the same 100,000-file synthetic fixture (200 directories, 500 small text files each) with separate new indexes. Both completed and indexed 100,000 files. Timings include scan, database writes and rollup; each query median covers ten IPC round trips. Baseline ran first; this is not cold-disk or a statistically robust comparison.

| Test | Pre-split | Shared-core |
|---|---:|---:|
| Scan including indexing and rollup | 1,572.8 ms | 1,523.0 ms |
| Overview median | 11.92 ms | 10.12 ms |
| Filename search `item050` median | 25.67 ms | 24.72 ms |
| Path search `dir099` median | 31.67 ms | 29.75 ms |
| Root directory contents median | 0.84 ms | 0.64 ms |

Process-tree memory is the sum of Windows working sets, including potentially shared pages; it is not unique private memory. One development-mode run per build, sampled after three idle seconds per screen with the 10,000-file index. This is not a leak test or peak scan measurement.

| Screen | Pre-split bytes | Shared-core bytes |
|---|---:|---:|
| Initial overview | 411,680,768 | 411,709,440 |
| Files | 432,357,376 | 431,955,968 |
| Treemap | 432,713,728 | 428,097,536 |
| Duplicates page | 433,885,184 | 429,305,856 |
| Return to overview | 436,203,520 | 432,668,672 |

At initial overview, baseline main/other, GPU, utility, engine, renderer and secondary process working sets were 98,643,968; 136,540,160; 48,271,360; 38,047,744; 82,378,752; and 7,798,784 bytes respectively. Candidate values were 98,287,616; 137,265,152; 48,472,064; 37,658,624; 82,223,104; and 7,802,880 bytes. Both builds navigated Files, Treemap, Duplicates, History and Overview with no visible `role=alert` errors. This checks navigation, not all operations on those pages.

A 500,000-file test was not attempted given the limited available disk space and the need to retain both build artifacts and indexes. Peak scan and duplicate-analysis process-tree memory, independent rollup timing, CPU and I/O, installed application behavior and real upgrade/uninstall testing remain unmeasured. The release gate remains open.

## Repeated 100,000-file rescan profile

Three rescans per build used the same controlled 100,000-file fixture and each build's existing test index; launch order alternated. Engine process working set was sampled every 20 ms during scan and is a sampled peak, not process-tree peak or managed-heap size. CPU is total engine process CPU seconds, not system utilization. Windows process I/O transfer counters include cached operations and are not physical-disk throughput. Both builds reported 100,000 files and Completed in all runs.

| Build | Scan ms | Sampled peak engine working set bytes | CPU seconds | Read transfer bytes | Write transfer bytes |
|---|---:|---:|---:|---:|---:|
| Pre-split 1 | 1,673.0 | 171,995,136 | 1.641 | 132,714,615 | 145,986,615 |
| Pre-split 2 | 1,393.0 | 170,651,648 | 1.516 | 133,263,479 | 146,014,444 |
| Pre-split 3 | 1,403.1 | 172,322,816 | 1.594 | 133,263,479 | 146,014,445 |
| Shared-core 1 | 1,414.3 | 168,534,016 | 1.562 | 132,714,615 | 145,985,646 |
| Shared-core 2 | 1,321.4 | 170,676,224 | 1.484 | 133,263,479 | 146,013,793 |
| Shared-core 3 | 1,345.8 | 169,971,712 | 1.516 | 133,263,479 | 146,014,115 |

Median elapsed times were 1,403.1 ms and 1,345.8 ms; median sampled peaks were 171,995,136 and 169,971,712 bytes. These are small samples of warm synthetic rescans, not evidence that the architecture change improves production performance. Individual algorithms, database-write timing and queue depth were not isolated.
