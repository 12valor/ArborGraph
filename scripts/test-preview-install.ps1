# This test must run on an ephemeral GitHub-hosted Windows runner, never on a user's PC.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or -not $env:RUNNER_TEMP) {
    throw 'Run only on an ephemeral GitHub-hosted runner.'
}
$root = Join-Path $env:RUNNER_TEMP ('arborgraph-installer-' + [guid]::NewGuid().ToString('N'))
$install = Join-Path $root 'app'
$data = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ArborGraph'
if (Test-Path -LiteralPath $data) { throw 'Existing ArborGraph data found; refusing to run.' }
$appid = '{58A5B2A8-FA36-4C8E-9737-69D6EA0184B0}_is1'
foreach ($hive in @('HKCU:', 'HKLM:')) {
    if (Test-Path "$hive\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appid") { throw 'Existing preview installation found; refusing to run.' }
}
New-Item -ItemType Directory -Path $root | Out-Null
$downloads = @{}
foreach ($version in @('1.3.0', '1.3.2')) {
    $name = "ArborGraph-Desktop-Preview-Setup-v$version-x64.exe"
    $base = "https://github.com/12valor/ArborGraph/releases/download/v$version"
    $setup = Join-Path $root $name
    Invoke-WebRequest "$base/$name" -OutFile $setup
    $sumsPath = Join-Path $root "SHA256SUMS-$version.txt"
    Invoke-WebRequest "$base/SHA256SUMS.txt" -OutFile $sumsPath
    $sums = Get-Content -LiteralPath $sumsPath -Raw
    $entry = @($sums -split "`n" | Where-Object { $_ -match [regex]::Escape($name) })
    if ($entry.Count -ne 1 -or $entry[0] -notmatch '^([0-9a-fA-F]{64})\s+\*?(.+?)\s*$') { throw "Invalid checksum for $name" }
    if ((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash -ne $Matches[1].ToUpperInvariant()) { throw "Checksum mismatch for $name" }
    $downloads[$version] = $setup
}
function Run-Setup([string]$exe) {
    $args = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER',"/DIR=`"$install`"")
    $p = Start-Process -FilePath $exe -ArgumentList $args -PassThru -Wait
    if ($p.ExitCode -ne 0) { throw "Installer failed with exit code $($p.ExitCode)" }
}
function Invoke-Engine([string]$exe, [string]$phase) {
    $si = [Diagnostics.ProcessStartInfo]::new()
    $si.FileName = $exe; $si.UseShellExecute = $false; $si.CreateNoWindow = $true
    $si.RedirectStandardInput = $true; $si.RedirectStandardOutput = $true; $si.RedirectStandardError = $true
    $p = [Diagnostics.Process]::Start($si)
    try {
        function Request([hashtable]$value) {
            $p.StandardInput.WriteLine(($value | ConvertTo-Json -Compress -Depth 8)); $p.StandardInput.Flush()
            while ($true) {
                $read = $p.StandardOutput.ReadLineAsync()
                if (-not $read.Wait([TimeSpan]::FromSeconds(30)) -or $null -eq $read.Result) { throw "Engine response timeout in $phase" }
                $message = $read.Result | ConvertFrom-Json
                if ($message.id -eq $value.id) {
                    if (-not $message.ok) { throw "Engine request $($value.command) failed: $($message.error.message)" }
                    return $message.data
                }
            }
        }
        $null = Request @{id='legal';command='acceptEula';accepted=$true}
        if ($phase -eq 'before') {
            $fixture = Join-Path $root 'fixture'; New-Item -ItemType Directory -Path $fixture | Out-Null
            1..20 | ForEach-Object { [IO.File]::WriteAllText((Join-Path $fixture "file-$_.txt"), "fixture $_") }
            $null = Request @{id='scan';command='startScan';roots=@($fixture)}
            do {
                $read = $p.StandardOutput.ReadLineAsync()
                if (-not $read.Wait([TimeSpan]::FromSeconds(30)) -or $null -eq $read.Result) { throw 'Scan timed out' }
                $message = $read.Result | ConvertFrom-Json
                if ($message.eventName -eq 'scanFailed') { throw 'Fixture scan failed' }
            } until ($message.eventName -eq 'scanFinished')
            if ($message.state -ne 'Completed' -or $message.filesIndexed -ne 20) { throw 'Fixture scan did not index 20 files.' }
        }
        $overview = Request @{id='overview';command='getOverview'}
        $history = Request @{id='history';command='getScanHistory'}
        $files = Request @{id='files';command='getFiles';search='file-'}
        if ($overview.indexedFiles -ne 20 -or $history.Count -lt 1 -or $files.total -ne 20) { throw "Data unavailable $phase upgrade" }
        $null = Request @{id='shutdown';command='shutdown'}
        if (-not $p.WaitForExit(10000) -or $p.ExitCode -ne 0) { throw 'Engine shutdown failed.' }
    } finally { if (-not $p.HasExited) { $p.Kill(); $p.WaitForExit() }; $p.Dispose() }
}
Run-Setup $downloads['1.3.0']
$engine = Join-Path $install 'resources\engine\ArborGraph.EngineHost.exe'
foreach ($p in @($engine, (Join-Path $install 'ArborGraph Desktop Preview.exe'), (Join-Path $install 'LICENSE.md'), (Join-Path $install 'THIRD_PARTY_LICENSES.md'), (Join-Path $install 'resources\ArborGraph.ico'))) {
    if (-not (Test-Path -LiteralPath $p)) { throw "Installed file missing: $p" }
}
Invoke-Engine $engine 'before'
$oldHash = (Get-FileHash (Join-Path $data 'scan_index.db') -Algorithm SHA256).Hash
Run-Setup $downloads['1.3.2']
if ((Get-FileHash (Join-Path $data 'scan_index.db') -Algorithm SHA256).Hash -ne $oldHash) { throw 'Installer changed the existing database.' }
Invoke-Engine $engine 'after'
$uninstaller = Join-Path $install 'unins000.exe'
$p = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait -PassThru
if ($p.ExitCode -ne 0 -or (Test-Path -LiteralPath $engine)) { throw 'Uninstall did not remove the installed engine.' }
if (-not (Test-Path -LiteralPath (Join-Path $data 'scan_index.db'))) { throw 'Uninstall removed user scan data.' }
Run-Setup $downloads['1.3.2']
if (-not (Test-Path -LiteralPath $engine)) { throw 'Reinstall failed.' }
Invoke-Engine $engine 'after reinstall'
Write-Output 'PASS: verified release checksums, clean install, fixture scan, upgrade, search/history, uninstall data preservation and reinstall.'
