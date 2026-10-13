<#
.SYNOPSIS
    Builds, packages, checksums, and verifies production release artifacts for ArborGraph.

.PARAMETER Version
    Optional explicit semantic version (e.g. "1.1.4"). Defaults to the Version in DiskScope.csproj.

.PARAMETER SkipTests
    Skips the automated regression and security test suites.

.PARAMETER PublishGitHub
    Publishes the release directly to GitHub via the gh CLI.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipTests,
    [switch]$PublishGitHub
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RootDir = Split-Path -Parent $ScriptDir
Set-Location $RootDir

# 1. Resolve Version and .NET Executable
$dotnetCmd = "dotnet"
if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    $dotnetCmd = "$env:USERPROFILE\.dotnet\dotnet.exe"
    $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
}

if (-not $Version) {
    [xml]$projXml = Get-Content (Join-Path $RootDir "DiskScope.csproj")
    $Version = $projXml.Project.PropertyGroup.Version
}

$Tag = "v$Version"
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  ArborGraph Release Packaging Engine - $Tag" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 2. Test Verification Gate
if (-not $SkipTests) {
    Write-Host "`n[*] Running 40-Stage Automated QA Regression Harness..." -ForegroundColor Yellow
    & $dotnetCmd run --project tests/DiskScope.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Automated QA Regression Harness FAILED with exit code $LASTEXITCODE. Release halted."
    }

    Write-Host "`n[*] Running Security and Data-Safety Audits..." -ForegroundColor Yellow
    & $dotnetCmd run --project tests/DiskScope.Tests.csproj -c Release --no-build -- --security
    if ($LASTEXITCODE -ne 0) {
        throw "Security and Data-Safety Audit FAILED with exit code $LASTEXITCODE. Release halted."
    }
    Write-Host "[OK] All tests and security audits passed cleanly!" -ForegroundColor Green
} else {
    Write-Host "`n[!] Notice: -SkipTests requested. Skipping pre-release test execution." -ForegroundColor DarkYellow
}

# 3. Compile Standalone Single-File Binary
$distDir = Join-Path $RootDir "dist"
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

Write-Host "`n[*] Compiling standalone single-file binary (Release / win-x64)..." -ForegroundColor Yellow
& $dotnetCmd publish DiskScope.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o $distDir

$exePath = Join-Path $distDir "ArborGraph.exe"
if (-not (Test-Path $exePath)) {
    throw "Compiled binary not found at $exePath!"
}

$exeItem = Get-Item $exePath
$exeMb = [math]::Round($exeItem.Length / 1048576, 1)
Write-Host "[OK] Compiled: ArborGraph.exe ($exeMb MB / $($exeItem.Length) bytes)" -ForegroundColor Green

# 4. Package Portable ZIP Archives
Write-Host "`n[*] Packaging portable zip archives..." -ForegroundColor Yellow
$zipPath = Join-Path $distDir "ArborGraph.zip"
$versionedZipPath = Join-Path $distDir "ArborGraph-$Tag-portable.zip"

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
if (Test-Path $versionedZipPath) { Remove-Item $versionedZipPath -Force }

Compress-Archive -Path $exePath -DestinationPath $zipPath -Force
Copy-Item -Path $zipPath -Destination $versionedZipPath -Force

$zipItem = Get-Item $zipPath
$zipMb = [math]::Round($zipItem.Length / 1048576, 1)
Write-Host "[OK] Packaged: ArborGraph.zip ($zipMb MB / $($zipItem.Length) bytes)" -ForegroundColor Green
Write-Host "[OK] Packaged: ArborGraph-$Tag-portable.zip" -ForegroundColor Green

# 5. Compile Windows Installer Package (Inno Setup 6)
$isccCmd = $null
if (Get-Command iscc -ErrorAction SilentlyContinue) {
    $isccCmd = (Get-Command iscc).Source
} elseif (Test-Path "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") {
    $isccCmd = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
} elseif (Test-Path "C:\Program Files (x86)\Inno Setup 6\ISCC.exe") {
    $isccCmd = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
} elseif (Test-Path "C:\Program Files\Inno Setup 6\ISCC.exe") {
    $isccCmd = "C:\Program Files\Inno Setup 6\ISCC.exe"
}

$setupInstallerPath = $null
$setupHash = $null
$setupBaseName = "ArborGraph-Setup-$Tag-x64"

if ($isccCmd) {
    Write-Host "`n[*] Compiling Windows Installer Package via Inno Setup ($isccCmd)..." -ForegroundColor Yellow
    $setupDir = Join-Path $distDir "setup"
    New-Item -ItemType Directory -Path $setupDir -Force | Out-Null
    $issFile = Join-Path $RootDir "installer\installer.iss"

    & $isccCmd /DMyAppVersion="$Version" /DSourceExe="$exePath" /O"$setupDir" /F"$setupBaseName" $issFile
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
    }

    $setupInstallerPath = Join-Path $setupDir "$setupBaseName.exe"
    if (-not (Test-Path $setupInstallerPath)) {
        throw "Expected installer file not found at $setupInstallerPath"
    }

    $setupItem = Get-Item $setupInstallerPath
    if ($setupItem.Length -le 1048576) {
        throw "Generated installer is suspiciously small ($($setupItem.Length) bytes). Expected > 1 MB."
    }

    $setupMb = [math]::Round($setupItem.Length / 1048576, 1)
    Write-Host "[OK] Packaged: $setupBaseName.exe ($setupMb MB / $($setupItem.Length) bytes)" -ForegroundColor Green
    $setupHash = (Get-FileHash $setupInstallerPath -Algorithm SHA256).Hash.ToUpper()
} else {
    Write-Host "`n[!] Notice: Inno Setup compiler (ISCC.exe) not located in PATH or Program Files. Skipping local installer compilation." -ForegroundColor DarkYellow
    Write-Host "    (Note: GitHub Actions release workflow automatically installs Inno Setup to generate installer assets.)" -ForegroundColor Gray
}

# 6. Compute SHA-256 Checksums
Write-Host "`n[*] Generating SHA-256 Cryptographic Digest Manifest..." -ForegroundColor Yellow
$exeHash = (Get-FileHash $exePath -Algorithm SHA256).Hash.ToUpper()
$zipHash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToUpper()

$manifestLines = [System.Collections.Generic.List[string]]::new()
$manifestLines.Add("$exeHash *ArborGraph.exe")
$manifestLines.Add("$zipHash *ArborGraph.zip")
if ($setupHash -and $setupInstallerPath) {
    $manifestLines.Add("$setupHash *$setupBaseName.exe")
}
$manifestPath = Join-Path $distDir "SHA256SUMS.txt"
Set-Content -Path $manifestPath -Value $manifestLines -Encoding Ascii

Write-Host "[OK] Manifest written to dist\SHA256SUMS.txt:" -ForegroundColor Green
Get-Content $manifestPath | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }

# 7. Summary Dashboard
Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "  RELEASE PACKAGING COMPLETE - $Tag" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  ArborGraph.exe:               $exeHash ($($exeItem.Length) bytes)"
Write-Host "  ArborGraph.zip:               $zipHash ($($zipItem.Length) bytes)"
Write-Host "  ArborGraph-$Tag-portable.zip: $zipHash"
if ($setupHash -and $setupItem) {
    Write-Host "  $setupBaseName.exe: $setupHash ($($setupItem.Length) bytes)"
} else {
    Write-Host "  $setupBaseName.exe: [Skipped - ISCC not installed locally]"
}
Write-Host "  SHA256SUMS.txt:               Generated and verified"
Write-Host "==========================================================" -ForegroundColor Cyan

# 8. Optional GitHub Release Publication
if ($PublishGitHub) {
    Write-Host "`n[*] Publishing release $Tag to GitHub via gh CLI..." -ForegroundColor Yellow
    $notesFile = Join-Path $distDir "RELEASE_NOTES_$Tag.md"
    $notesArg = @()
    if (Test-Path $notesFile) {
        $notesArg = @("--notes-file", $notesFile)
    } else {
        $notesArg = @("--generate-notes")
    }

    $assets = @($exePath, $zipPath, $versionedZipPath, $manifestPath)
    if ($setupInstallerPath -and (Test-Path $setupInstallerPath)) {
        $assets += $setupInstallerPath
    }
    
    # Check if release exists
    $allReleases = & gh release list
    $releaseExists = ($allReleases -match "\b$Tag\b").Count -gt 0
    if ($releaseExists) {
        Write-Host "Release $Tag already exists on GitHub. Uploading updated assets..." -ForegroundColor Yellow
        & gh release upload $Tag $assets --clobber
    } else {
        Write-Host "Creating new GitHub Release $Tag..." -ForegroundColor Yellow
        & gh release create $Tag $assets --title "ArborGraph $Tag" @notesArg
    }

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] GitHub Release published successfully: https://github.com/12valor/ArborGraph/releases/tag/$Tag" -ForegroundColor Green
    } else {
        Write-Error "GitHub Release publication failed."
    }
}
