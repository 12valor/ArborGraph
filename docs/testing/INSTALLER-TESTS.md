# ArborGraph Installer & Deployment Test Specification

**Document Identifier:** AG-IT-001  
**Target Installer:** Inno Setup 6 Script (`installer/installer.iss`)  
**Target Package:** `ArborGraph-Setup-1.0.0-x64.exe`  
**Execution Context:** Windows 10 & Windows 11 (64-bit x64)  
**Execution Rule:** All test cases are initialized as `NOT TESTED`. Evidence from code inspection is documented under `Notes`.  

---

## 1. Test Cases Specification

### Test ID: `TC-INS-01`
- **Title:** Clean Fresh Installation (Standard Lowest-Privilege User)
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Installer failing or demanding unnecessary Administrator elevation (`UAC` prompt) prevents standard non-admin users from installing.
- **Preconditions:** Clean test machine without prior ArborGraph or DiskScope installation. Standard user session (non-elevated).
- **Test Steps:**
  1. Launch `ArborGraph-Setup-1.0.0-x64.exe`.
  2. Verify no mandatory UAC shield prompt appears (`PrivilegesRequired=lowest`).
  3. Verify modern Inno Setup wizard displays with correct title: `"Setup - ArborGraph"`.
  4. Inspect License Agreement screen: verify `installer/eula.txt` displays cleanly.
  5. Select `"I accept the agreement"` and click `Next`.
  6. Verify default installation directory resolves to `{autopf}\ArborGraph` (typically `%LOCALAPPDATA%\Programs\ArborGraph` for standard user).
  7. Check `"Create a desktop shortcut"` task.
  8. Click `Install` and await completion.
  9. Click `Finish` with `"Launch ArborGraph"` checked.
- **Expected Result:**
  - Setup completes without errors or elevation prompts.
  - Binaries deployed to `{app}`: `ArborGraph.exe` and `eula.txt`.
  - Application launches successfully into EULA first-run gate or main window.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `installer.iss` specifies `PrivilegesRequired=lowest` and `LicenseFile=eula.txt`.

---

### Test ID: `TC-INS-02`
- **Title:** Mandatory EULA Rejection Aborts Installation
- **Priority:** P1 / CRITICAL
- **Risk:** High — Legal licensing compliance failure if user can proceed without consenting.
- **Preconditions:** Fresh installer launch.
- **Test Steps:**
  1. Launch `ArborGraph-Setup-1.0.0-x64.exe`.
  2. On the License Agreement page, select `"I do not accept the agreement"`.
  3. Attempt to click `Next`.
  4. Click `Cancel`.
- **Expected Result:**
  - `Next` button is disabled when `"I do not accept"` is selected.
  - Clicking `Cancel` prompts for confirmation and exits cleanly with zero filesystem changes.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Built-in Inno Setup behavior enforced by `LicenseFile`.

---

### Test ID: `TC-INS-03`
- **Title:** Desktop & Start Menu Shortcut Verification
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Broken shortcut targets or missing icon resources.
- **Preconditions:** Installation completed with both shortcut tasks enabled.
- **Test Steps:**
  1. Inspect Start Menu: verify `"ArborGraph"` shortcut exists under programs.
  2. Inspect Desktop: verify `"ArborGraph"` shortcut exists on user desktop.
  3. Right-click both shortcuts -> `Properties`:
     - Verify Target: `"{app}\ArborGraph.exe"`.
     - Verify Start In: `"{app}"`.
     - Verify Icon: Resolves to `ArborGraph.ico` (Green circular tree emblem).
  4. Double-click each shortcut to launch application.
- **Expected Result:** Both shortcuts launch `ArborGraph.exe` with proper application icon and working directory.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Configured in `installer.iss` lines 47-49.

---

### Test ID: `TC-INS-04`
- **Title:** Metadata & Repository URL Verification (URL Drift Check)
- **Priority:** P0 / BLOCKER
- **Risk:** Critical — Outdated links in Windows Control Panel ("Installed Apps") pointing to dead or old repository paths.
- **Preconditions:** App installed on Windows 10 or 11.
- **Test Steps:**
  1. Open Windows `Settings` -> `Apps` -> `Installed Apps` (or `appwiz.cpl`).
  2. Locate `ArborGraph`.
  3. Inspect properties:
     - Name: `ArborGraph`
     - Publisher: `AG DIAZ EVANGELISTA`
     - Version: `1.0.0`
     - Support / Update Link: Inspect URL.
- **Expected Result:**
  - Support and Publisher URLs must point to `https://github.com/12valor/ArborGraph`.
  - *(Audit finding: `installer.iss` line 9 currently hardcodes `https://github.com/12valor/C-file-scanner`. This must be updated prior to release).*
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Audit report flagged this as Blocker 3.

---

### Test ID: `TC-INS-05`
- **Title:** In-Place Upgrade from Previous DiskScope / ArborGraph Installation
- **Priority:** P1 / CRITICAL
- **Risk:** High — Overwriting previous installation corrupts user settings or wipes `%LOCALAPPDATA%\ArborGraph\scan_index.db`.
- **Preconditions:** Previous version installed with active scan index in `%LOCALAPPDATA%\ArborGraph`.
- **Test Steps:**
  1. Launch new installer build.
  2. Run setup without uninstalling previous build.
  3. Verify installer detects existing `AppId={{D37F7E1A-85F4-4BC3-9C1D-72810C24A59E}`.
  4. Complete installation.
  5. Launch ArborGraph.
- **Expected Result:**
  - Binaries are updated in-place cleanly without duplicate Start Menu entries.
  - User SQLite index database and historical scan logs are retained completely intact.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: `AppId` stability verified in `installer.iss` line 14.

---

### Test ID: `TC-INS-06`
- **Title:** Complete Clean Uninstallation
- **Priority:** P1 / CRITICAL
- **Risk:** High — Leftover orphaned registry keys, binaries, or broken shortcuts after uninstall.
- **Preconditions:** ArborGraph installed via setup.
- **Test Steps:**
  1. Open `Installed Apps` -> Select `ArborGraph` -> Click `Uninstall`.
  2. Complete uninstallation wizard.
  3. Inspect `{app}` directory: verify `ArborGraph.exe` and `eula.txt` are deleted.
  4. Verify Start Menu and Desktop shortcuts are removed.
  5. Check Windows Registry: verify `Uninstall\ArborGraph` key is purged.
- **Expected Result:** Application binaries and shortcuts are 100% removed. User database in `%LOCALAPPDATA%` is preserved according to standard Windows desktop guidelines.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Previously observed / audit evidence: Defined in `[UninstallDelete]` in `installer.iss`.

---

### Test ID: `TC-INS-07`
- **Title:** Silent Headless Deployment (`/VERYSILENT`)
- **Priority:** P2 / MAJOR
- **Risk:** Medium — Enterprise or script deployments hanging on interactive prompts.
- **Preconditions:** Command prompt session.
- **Test Steps:**
  1. Execute: `ArborGraph-Setup-1.0.0-x64.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.
  2. Monitor process termination and exit code.
  3. Check target directory for deployed binaries.
- **Expected Result:** Process executes completely headless without displaying windows; exits with code 0; app deployed properly.
- **Actual Result:** 
- **Status:** NOT TESTED
- **Notes:** Standard Inno Setup capability.
