[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo 'engine\bin\Release\net8.0-windows\ArborGraph.EngineHost.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Build the Release engine host first: $exe" }

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("ArborGraph_EngineHost_" + [guid]::NewGuid().ToString('N'))
$scanRoot = Join-Path $testRoot 'target'
$dataRoot = Join-Path $testRoot 'data'
$nestedRoot = Join-Path $scanRoot 'nested'
[IO.Directory]::CreateDirectory($scanRoot) | Out-Null
[IO.Directory]::CreateDirectory($dataRoot) | Out-Null
[IO.Directory]::CreateDirectory($nestedRoot) | Out-Null
for ($i = 0; $i -lt 1200; $i++) {
    $parent = if ($i -lt 20) { $nestedRoot } else { $scanRoot }
    [IO.File]::WriteAllText((Join-Path $parent "file_$i.txt"), "ArborGraph engine test $i")
}

$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $exe
$start.ArgumentList.Add('--data-dir')
$start.ArgumentList.Add($dataRoot)
$start.UseShellExecute = $false
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.CreateNoWindow = $true
$process = [Diagnostics.Process]::Start($start)

function Send-Request($value) {
    $process.StandardInput.WriteLine(($value | ConvertTo-Json -Compress -Depth 8))
    $process.StandardInput.Flush()
}

function Read-Message {
    $read = $process.StandardOutput.ReadLineAsync()
    if (-not $read.Wait([TimeSpan]::FromSeconds(20))) { throw 'Timed out waiting for engine host response.' }
    if ($null -eq $read.Result) { throw "Engine host closed stdout: $($process.StandardError.ReadToEnd())" }
    return ($read.Result | ConvertFrom-Json)
}

function Expect-Response($id) {
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        $message = Read-Message
        if ($message.type -eq 'response' -and $message.id -eq $id) { return $message }
    }
    throw "No response for $id"
}

try {
    Send-Request @{ id = 'ping'; command = 'ping' }
    $reply = Expect-Response 'ping'
    if (-not $reply.ok -or $reply.data.protocolVersion -ne 1) { throw 'Ping contract failed.' }

    Send-Request @{ id = 'bad'; command = 'runAnything' }
    $reply = Expect-Response 'bad'
    if ($reply.ok -or $reply.error.code -ne 'UNKNOWN_COMMAND') { throw 'Command allowlist failed.' }

    $process.StandardInput.WriteLine('{not-json}')
    $process.StandardInput.Flush()
    $reply = Read-Message
    if ($reply.ok -or $reply.error.code -ne 'INVALID_JSON') { throw 'Invalid JSON was accepted.' }

    $process.StandardInput.WriteLine(('x' * 65537))
    $process.StandardInput.Flush()
    $reply = Read-Message
    if ($reply.ok -or $reply.error.code -ne 'MESSAGE_TOO_LARGE') { throw 'Oversized request was accepted.' }

    Send-Request @{ id = 'scan'; command = 'startScan'; roots = @($scanRoot) }
    $reply = Expect-Response 'scan'
    if (-not $reply.ok -or -not $reply.data.accepted) { throw 'Scan was not accepted.' }

    $finished = $null
    $lastSequence = 0
    for ($attempt = 0; $attempt -lt 10000; $attempt++) {
        $message = Read-Message
        if ($message.type -ne 'event') { continue }
        if ($message.sequence -le $lastSequence) { throw 'Progress sequence went backwards.' }
        $lastSequence = $message.sequence
        if ($message.eventName -eq 'scanFinished') { $finished = $message; break }
        if ($message.eventName -eq 'scanFailed') { throw "Scan failed: $($message.message)" }
    }
    if ($null -eq $finished -or $finished.state -ne 'Completed' -or
        $finished.filesDiscovered -ne 1200 -or $finished.filesIndexed -ne 1200 -or
        $finished.directoriesProcessed -lt 1) {
        throw 'Scan completion totals did not match the test data.'
    }

    Send-Request @{ id = 'overview'; command = 'getOverview' }
    $reply = Expect-Response 'overview'
    if (-not $reply.ok -or $reply.data.indexedFiles -ne 1200) { throw 'Overview index count is incorrect.' }

    Send-Request @{ id = 'files'; command = 'getFiles'; limit = 25; search = 'file_'; sortBy = 'size' }
    $reply = Expect-Response 'files'
    if (-not $reply.ok -or $reply.data.total -ne 1200 -or $reply.data.files.Count -ne 25) {
        throw 'Paged file query did not match the index.'
    }

    Send-Request @{ id = 'folders'; command = 'getLargestFolders'; limit = 5 }
    $reply = Expect-Response 'folders'
    if (-not $reply.ok -or $reply.data.Count -lt 1) { throw 'Largest folder query returned no index data.' }

    Send-Request @{ id = 'directory'; command = 'getDirectoryContents'; path = $scanRoot; limit = 100 }
    $reply = Expect-Response 'directory'
    if (-not $reply.ok -or $reply.data.totalItems -ne 1181 -or $reply.data.items.Count -ne 100 -or
        $reply.data.logicalBytes -ne $finished.indexedLogicalBytes) {
        throw 'Directory contents did not preserve the indexed size and item count.'
    }

    Send-Request @{ id = 'nestedDirectory'; command = 'getDirectoryContents'; path = $nestedRoot }
    $reply = Expect-Response 'nestedDirectory'
    if (-not $reply.ok -or $reply.data.totalItems -ne 20 -or $reply.data.items.Count -ne 20 -or
        $reply.data.logicalBytes -ne ($reply.data.items | Measure-Object -Property logicalBytes -Sum).Sum) {
        throw 'Nested directory rollup does not match its direct files.'
    }

    Send-Request @{ id = 'badSort'; command = 'getFiles'; sortBy = 'arbitrary_sql' }
    $reply = Expect-Response 'badSort'
    if ($reply.ok -or $reply.error.code -ne 'INVALID_ARGUMENT') { throw 'Invalid file sort was accepted.' }

    Send-Request @{ id = 'relativeDirectory'; command = 'getDirectoryContents'; path = 'relative' }
    $reply = Expect-Response 'relativeDirectory'
    if ($reply.ok -or $reply.error.code -ne 'INVALID_ARGUMENT') { throw 'Relative directory was accepted.' }

    Send-Request @{ id = 'history'; command = 'getScanHistory'; limit = 5 }
    $reply = Expect-Response 'history'
    if (-not $reply.ok -or $reply.data.Count -ne 1 -or $reply.data[0].filesIndexed -ne 1200) {
        throw 'Completed scan history did not match the test scan.'
    }
    $firstScanId = $reply.data[0].id

    Send-Request @{ id = 'repeat'; command = 'startScan'; roots = @($scanRoot) }
    $reply = Expect-Response 'repeat'
    if (-not $reply.ok) { throw 'Repeat scan was not accepted.' }
    $repeatFinished = $null
    for ($attempt = 0; $attempt -lt 10000; $attempt++) {
        $message = Read-Message
        if ($message.eventName -eq 'scanFinished') { $repeatFinished = $message; break }
        if ($message.eventName -eq 'scanFailed') { throw "Repeat scan failed: $($message.message)" }
    }
    if ($null -eq $repeatFinished -or $repeatFinished.state -ne 'Completed') { throw 'Repeat scan did not finish.' }

    Send-Request @{ id = 'historyAgain'; command = 'getScanHistory'; limit = 5 }
    $reply = Expect-Response 'historyAgain'
    if (-not $reply.ok -or $reply.data.Count -ne 2) { throw 'Repeat scan is missing from history.' }
    $secondScanId = $reply.data[0].id

    Send-Request @{ id = 'compare'; command = 'compareScans'; olderId = $firstScanId; newerId = $secondScanId }
    $reply = Expect-Response 'compare'
    if (-not $reply.ok -or $reply.data.deltaBytes -ne 0 -or $reply.data.deltaFiles -ne 0 -or
        $reply.data.coverageWarning -or $reply.data.categories.Count -lt 1) {
        throw 'Identical-target scan comparison is incorrect.'
    }

    Send-Request @{ id = 'badCompare'; command = 'compareScans'; olderId = $secondScanId; newerId = $firstScanId }
    $reply = Expect-Response 'badCompare'
    if ($reply.ok -or $reply.error.code -ne 'NOT_COMPARABLE') { throw 'Reverse-order scan comparison was accepted.' }

    Send-Request @{ id = 'secondScan'; command = 'startScan'; roots = @($scanRoot) }
    $reply = Expect-Response 'secondScan'
    if (-not $reply.ok) { throw 'Second scan was not accepted.' }

    Send-Request @{ id = 'duplicateScan'; command = 'startScan'; roots = @($scanRoot) }
    $reply = Expect-Response 'duplicateScan'
    if ($reply.ok -or $reply.error.code -ne 'SCAN_IN_PROGRESS') { throw 'Concurrent scan was accepted.' }

    Send-Request @{ id = 'stop'; command = 'cancelScan' }
    $reply = Expect-Response 'stop'
    if (-not $reply.ok -or -not $reply.data.cancelling) { throw 'Active cancellation was rejected.' }

    $cancelled = $null
    for ($attempt = 0; $attempt -lt 10000; $attempt++) {
        $message = Read-Message
        if ($message.eventName -eq 'scanFinished') { $cancelled = $message; break }
        if ($message.eventName -eq 'scanFailed') { throw "Cancelled scan failed: $($message.message)" }
    }
    if ($null -eq $cancelled -or $cancelled.state -ne 'Cancelled') { throw 'Active scan did not cancel.' }

    Send-Request @{ id = 'cancel'; command = 'cancelScan' }
    $reply = Expect-Response 'cancel'
    if ($reply.ok -or $reply.error.code -ne 'NO_ACTIVE_SCAN') { throw 'Idle cancellation contract failed.' }

    Send-Request @{ id = 'shutdown'; command = 'shutdown' }
    $reply = Expect-Response 'shutdown'
    if (-not $reply.ok) { throw 'Shutdown response failed.' }
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(10000) -or $process.ExitCode -ne 0) { throw 'Engine host did not exit cleanly.' }
    Write-Output 'Engine host process test passed: validation, scan totals, indexed queries and rollups, history comparison, event order, duplicate scan rejection, cancellation, shutdown.'
}
finally {
    if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    $process.Dispose()
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
