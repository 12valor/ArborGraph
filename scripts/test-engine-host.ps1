[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo 'engine\bin\Release\net8.0-windows\ArborGraph.EngineHost.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Build the Release engine host first: $exe" }

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("ArborGraph_EngineHost_" + [guid]::NewGuid().ToString('N'))
$scanRoot = Join-Path $testRoot 'target'
$dataRoot = Join-Path $testRoot 'data'
$nestedRoot = Join-Path $scanRoot 'node_modules'
[IO.Directory]::CreateDirectory($scanRoot) | Out-Null
[IO.Directory]::CreateDirectory($dataRoot) | Out-Null
[IO.Directory]::CreateDirectory($nestedRoot) | Out-Null
for ($i = 0; $i -lt 1200; $i++) {
    $parent = if ($i -lt 20) { $nestedRoot } else { $scanRoot }
    $extension = if ($i -eq 0) { '.psd' } elseif ($i -eq 1) { '.psb' } elseif ($i -eq 2) { '.abr' } else { '.txt' }
    $filePath = Join-Path $parent "file_$i$extension"
    [IO.File]::WriteAllText($filePath, "ArborGraph engine test $i")
    if ($i -lt 5) { [IO.File]::SetLastWriteTimeUtc($filePath, [DateTime]::UtcNow.AddDays(-400)) }
}
[IO.File]::WriteAllText((Join-Path $scanRoot 'file_100.txt'), ('D' * 2048))
[IO.File]::WriteAllText((Join-Path $scanRoot 'file_101.txt'), ('D' * 2048))
[IO.File]::WriteAllText((Join-Path $scanRoot 'file_102.txt'), ('E' * 2048))
[IO.File]::WriteAllText((Join-Path $scanRoot 'package.json'), '{}')

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

    Send-Request @{ id = 'legal'; command = 'getLegal' }
    $reply = Expect-Response 'legal'
    if (-not $reply.ok -or $reply.data.accepted -or $reply.data.version -ne '1.0' -or
        $reply.data.text -notlike '*License Grant*') { throw 'Embedded first-run EULA is unavailable.' }

    Send-Request @{ id = 'beforeConsent'; command = 'getOverview' }
    $reply = Expect-Response 'beforeConsent'
    if ($reply.ok -or $reply.error.code -ne 'EULA_REQUIRED') { throw 'Analysis was available before EULA acceptance.' }

    Send-Request @{ id = 'invalidConsent'; command = 'acceptEula'; accepted = $false }
    $reply = Expect-Response 'invalidConsent'
    if ($reply.ok -or $reply.error.code -ne 'INVALID_ARGUMENT') { throw 'False EULA consent was accepted.' }

    Send-Request @{ id = 'accept'; command = 'acceptEula'; accepted = $true }
    $reply = Expect-Response 'accept'
    if (-not $reply.ok -or -not $reply.data.accepted) { throw 'First-run EULA acceptance failed.' }
    $savedSettings = Get-Content -LiteralPath (Join-Path $dataRoot 'settings.json') -Raw | ConvertFrom-Json
    if (-not $savedSettings.HasAcceptedEula -or $savedSettings.EulaAcceptedVersion -ne '1.0') {
        throw 'EULA consent was not persisted in the existing settings format.'
    }

    Send-Request @{ id = 'settings'; command = 'getSettings' }
    $reply = Expect-Response 'settings'
    if (-not $reply.ok -or -not $reply.data.includeHiddenFiles) { throw 'Shared scan settings were not returned.' }
    Send-Request @{ id = 'setSettings'; command = 'setScanSettings'; workerCount = 2; followJunctions = $false; includeHiddenFiles = $false; includeSystemFiles = $true }
    $reply = Expect-Response 'setSettings'
    if (-not $reply.ok -or $reply.data.workerCount -ne 2 -or $reply.data.includeHiddenFiles) { throw 'Scan settings update failed.' }
    $savedSettings = Get-Content -LiteralPath (Join-Path $dataRoot 'settings.json') -Raw | ConvertFrom-Json
    if (-not $savedSettings.HasAcceptedEula -or $savedSettings.WorkerCount -ne 2) { throw 'Saving scan settings lost EULA consent.' }

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
        $finished.filesDiscovered -ne 1201 -or $finished.filesIndexed -ne 1201 -or
        $finished.directoriesProcessed -lt 1) {
        throw 'Scan completion totals did not match the test data.'
    }

    Send-Request @{ id = 'addExclusion'; command = 'addExclusion'; path = $nestedRoot }
    $reply = Expect-Response 'addExclusion'
    if (-not $reply.ok -or $nestedRoot -notin $reply.data.excludedPaths) { throw 'Selected exclusion was not saved.' }
    Send-Request @{ id = 'removeExclusion'; command = 'removeExclusion'; path = $nestedRoot }
    $reply = Expect-Response 'removeExclusion'
    if (-not $reply.ok -or $nestedRoot -in $reply.data.excludedPaths) { throw 'Exclusion was not removed.' }

    Send-Request @{ id = 'overview'; command = 'getOverview' }
    $reply = Expect-Response 'overview'
    if (-not $reply.ok -or $reply.data.indexedFiles -ne 1201) { throw 'Overview index count is incorrect.' }

    Send-Request @{ id = 'developer'; command = 'getIndexedDeveloperStorage' }
    $reply = Expect-Response 'developer'
    $nodeSummary = @($reply.data | Where-Object { $_.name -like 'Node.js*' })
    if (-not $reply.ok -or $nodeSummary.Count -ne 1 -or $nodeSummary[0].itemCount -ne 1 -or
        $nodeSummary[0].items[0].path -ne $nestedRoot) { throw 'Indexed Node.js dependencies were not recognized.' }

    Send-Request @{ id = 'files'; command = 'getFiles'; limit = 25; search = 'file_'; sortBy = 'size' }
    $reply = Expect-Response 'files'
    if (-not $reply.ok -or $reply.data.total -ne 1200 -or $reply.data.files.Count -ne 25) {
        throw 'Paged file query did not match the index.'
    }

    $csvPath = Join-Path $testRoot 'matching.csv'
    Send-Request @{ id = 'exportCsv'; command = 'exportFilesCsv'; filePath = $csvPath; search = 'file_10'; sortBy = 'name'; sortDesc = $false }
    $reply = Expect-Response 'exportCsv'
    if (-not $reply.ok -or -not $reply.data.saved -or -not (Test-Path -LiteralPath $csvPath)) { throw 'Filtered CSV export failed.' }
    $csvRows = @(Import-Csv -LiteralPath $csvPath)
    if ($csvRows.Count -ne 111 -or ($csvRows | Where-Object { $_.Name -notlike '*file_10*' }).Count -ne 0) {
        throw 'CSV rows did not match the selected file filter.'
    }
    Send-Request @{ id = 'badExport'; command = 'exportFilesCsv'; filePath = 'relative.csv' }
    $reply = Expect-Response 'badExport'
    if ($reply.ok -or $reply.error.code -ne 'INVALID_ARGUMENT') { throw 'Relative CSV export path was accepted.' }

    $jsonReportPath = Join-Path $testRoot 'audit.json'
    Send-Request @{ id = 'jsonReport'; command = 'exportAuditReport'; format = 'json'; filePath = $jsonReportPath }
    $reply = Expect-Response 'jsonReport'
    if (-not $reply.ok -or -not $reply.data.saved) { throw 'JSON audit report failed.' }
    $jsonReport = Get-Content -LiteralPath $jsonReportPath -Raw | ConvertFrom-Json
    if ($jsonReport.TotalFilesIndexed -ne 1201 -or $jsonReport.DuplicateBytesAreVerified -or $jsonReport.JunkTargetsAnalyzed -or
        $jsonReport.TargetRoots -ne $scanRoot) { throw 'JSON audit report metadata or duplicate status was incorrect.' }
    $htmlReportPath = Join-Path $testRoot 'audit.html'
    Send-Request @{ id = 'htmlReport'; command = 'exportAuditReport'; format = 'html'; filePath = $htmlReportPath }
    $reply = Expect-Response 'htmlReport'
    if (-not $reply.ok -or -not $reply.data.saved) { throw 'HTML audit report failed.' }
    $htmlReport = Get-Content -LiteralPath $htmlReportPath -Raw
    if ($htmlReport -notlike '*Possible Duplicate Redundancy*' -or $htmlReport -notlike '*Not analyzed*' -or $htmlReport -notlike '*1,201*') {
        throw 'HTML audit report omitted index or verification context.'
    }
    Send-Request @{ id = 'badReport'; command = 'exportAuditReport'; format = 'html'; filePath = $jsonReportPath }
    $reply = Expect-Response 'badReport'
    if ($reply.ok -or $reply.error.code -ne 'INVALID_ARGUMENT') { throw 'Mismatched report extension was accepted.' }

    Send-Request @{ id = 'folders'; command = 'getLargestFolders'; limit = 5 }
    $reply = Expect-Response 'folders'
    if (-not $reply.ok -or $reply.data.Count -lt 1) { throw 'Largest folder query returned no index data.' }

    Send-Request @{ id = 'directory'; command = 'getDirectoryContents'; path = $scanRoot; limit = 100 }
    $reply = Expect-Response 'directory'
    if (-not $reply.ok -or $reply.data.totalItems -ne 1182 -or $reply.data.items.Count -ne 100 -or
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

    Send-Request @{ id = 'ages'; command = 'getFileAgeInsights'; daysOld = 180; limit = 100 }
    $reply = Expect-Response 'ages'
    if (-not $reply.ok -or $reply.data.oldFiles.Count -lt 5 -or
        ($reply.data.ageBuckets | Measure-Object -Property fileCount -Sum).Sum -ne 1201) {
        throw 'File age index summaries are incorrect.'
    }

    Send-Request @{ id = 'photoshop'; command = 'getPhotoshopInsights'; limit = 100 }
    $reply = Expect-Response 'photoshop'
    if (-not $reply.ok -or $reply.data.photoshop.Count -ne 3 -or
        $reply.data.photoshopSummary.psdCount -ne 1 -or $reply.data.photoshopSummary.psbCount -ne 1 -or
        $reply.data.photoshopSummary.otherCount -ne 1 -or
        ($reply.data.photoshop | Where-Object { $_.name -like '*.abr' })[0].type -ne 'Preset / Asset') {
        throw 'Photoshop index summaries are incorrect.'
    }

    Send-Request @{ id = 'badInsights'; command = 'getFileAgeInsights'; daysOld = -1 }
    $reply = Expect-Response 'badInsights'
    if ($reply.ok -or $reply.error.code -ne 'INVALID_ARGUMENT') { throw 'Invalid file age threshold was accepted.' }

    Send-Request @{ id = 'duplicateOverview'; command = 'getDuplicateOverview' }
    $reply = Expect-Response 'duplicateOverview'
    if (-not $reply.ok -or $reply.data.candidateGroups -lt 1 -or $reply.data.candidateFiles -lt 3) {
        throw 'Duplicate size candidates were not reported.'
    }
    Send-Request @{ id = 'duplicateStart'; command = 'startDuplicateAnalysis' }
    $reply = Expect-Response 'duplicateStart'
    if (-not $reply.ok -or -not $reply.data.accepted) { throw 'Duplicate analysis was not accepted.' }
    $duplicateFinished = $null
    for ($attempt = 0; $attempt -lt 1000; $attempt++) {
        $message = Read-Message
        if ($message.eventName -eq 'duplicateFinished') { $duplicateFinished = $message; break }
        if ($message.eventName -eq 'duplicateFailed') { throw 'Duplicate analysis failed.' }
    }
    if ($null -eq $duplicateFinished -or $duplicateFinished.state -ne 'Completed' -or $duplicateFinished.groupCount -lt 1) {
        throw 'Verified duplicate result was not reported.'
    }
    Send-Request @{ id = 'duplicateResults'; command = 'getDuplicateResults'; offset = 0; limit = 25 }
    $reply = Expect-Response 'duplicateResults'
    $verified = @($reply.data.groups | Where-Object { $_.exactSize -eq 2048 -and $_.fileCount -eq 2 })
    if (-not $reply.ok -or $verified.Count -ne 1 -or $verified[0].potentialRedundantBytes -ne 2048) {
        throw 'Full-hash duplicate verification did not separate same-size different files.'
    }

    Send-Request @{ id = 'history'; command = 'getScanHistory'; limit = 5 }
    $reply = Expect-Response 'history'
    if (-not $reply.ok -or $reply.data.Count -ne 1 -or $reply.data[0].filesIndexed -ne 1201) {
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
    Write-Output 'Engine host process test passed: validation, scan totals, indexed queries and rollups, file insights, history comparison, event order, duplicate scan rejection, cancellation, shutdown.'
}
finally {
    if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    $process.Dispose()
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
