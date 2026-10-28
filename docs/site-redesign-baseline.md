# ArborGraph website baseline (2026-10-27)

The existing public page at `/ArborGraph/site/` is a static HTML/CSS/JavaScript site. The root Pages URL currently returns a redirect page to `/site/`. The deployment workflow publishes `./site` on pushes to `main`; all site assets use relative paths. The redesign must keep the `/site/` URL working and preserve static hosting.

## Verified distribution

The latest GitHub release is [v1.3.0](https://github.com/12valor/ArborGraph/releases/tag/v1.3.0), published 2026-10-25 UTC. Its assets are:

| Asset | Release bytes | SHA-256 |
| --- | ---: | --- |
| `ArborGraph-Desktop-Preview-Setup-v1.3.0-x64.exe` | 172,933,844 (164.9 MiB) | `BD3874384F53F21AAB712652DD7C3F1B1412AB7FB99D88793641A58355B1F880` |
| `SHA256SUMS.txt` | 114 | Not published |

The installer digest matches both the repository's `site/downloads/SHA256SUMS.txt` and a local hash of `site/downloads/ArborGraph-Desktop-Preview-Setup-v1.3.0-x64.exe`. The installer is a Windows x64 package; `installer/desktop-preview.iss` allows x64-compatible architecture and uses per-user installation by default. Release source archives are provided by GitHub on the tag page, but no portable binary or separate ZIP is a release asset. No verified minimum RAM or disk-space figure is available for the Electron preview.

## Existing behavior and content

The old page contains navigation, mobile drawer, installer walkthrough, illustrative diagnostics and scanner, checksum comparison and copy, specifications, tabbed documentation and legal articles, release notes, and repository links. The walkthrough and diagnostics are browser simulations, not a scan of a visitor's disk. The site JavaScript reads GitHub release metadata at runtime. The desktop product source confirms a local .NET EngineHost with SQLite indexing, treemap, files, duplicates, developer storage, scan history, reports, and settings views. The EULA is `LICENSE.md`; third-party notices are `THIRD_PARTY_LICENSES.md`.

## Baseline issues to correct

- The existing download text suggests standalone binaries and portable archives, although the current release has only the installer and checksum manifest.
- Several old documentation statements describe the earlier WPF build or quote unverified hardware requirements. The old page calls v1.3.0 a stable channel while its asset name and release describe a desktop preview.
- Taste's baseline inspector found tiny body text, heavy boxes, low contrast, poor hierarchy, mobile clipping, and no substantial product image. Baseline lint: 108 failures and 45 warnings across `site/` (including ancillary `logo_showcase.html`).
- The legacy EULA and legal terms are retained for reference, while the new page links to the repository's canonical license and third-party notices.

## Design direction

Taste direction: **one motif**. On this page, the directory treemap's nested rectangles become the visual device, from hero field to screenshot crop and section geometry. Inter and a light neutral palette are required by the brief; the product's existing blue is the restrained accent. The useful live action is comparing a locally calculated SHA-256 to the verified installer digest. The main argument: inspect real disk data locally, understand its structure, then download a verifiable Windows package.
