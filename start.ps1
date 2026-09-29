<#
.SYNOPSIS
    Launcher script for DiskScope Windows Storage Analyzer.

.DESCRIPTION
    Builds and starts the DiskScope desktop application. Automatically configures
    the .NET 8 SDK environment or falls back to precompiled standalone executables.

.PARAMETER Fast
    Launches the compiled executable immediately without rebuilding.

.PARAMETER Dev
    Runs the application attached to the console with live logging (dotnet run).

.PARAMETER Publish
    Runs the standalone self-contained publish build in bin\publish\DiskScope.exe.

.PARAMETER Test
    Runs the integration test suite in tests\DiskScope.Tests.csproj.

.PARAMETER Site
    Opens the product website and download page in the default web browser.

.PARAMETER Configuration
    Build configuration to use (Debug or Release). Defaults to Debug.

.EXAMPLE
    .\start.ps1
    .\start.ps1 -Fast
    .\start.ps1 -Dev
    .\start.ps1 -Test
    .\start.ps1 -Publish
    .\start.ps1 -Site
#>

[CmdletBinding()]
param(
    [switch]$Fast,
    [switch]$Dev,
    [switch]$Publish,
    [switch]$Test,
    [switch]$Site,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $ScriptDir

# Ensure user .dotnet directory is prioritized if present
$UserDotnet = Join-Path $env:USERPROFILE ".dotnet"
if (Test-Path (Join-Path $UserDotnet "dotnet.exe")) {
    $env:DOTNET_ROOT = $UserDotnet
    $env:PATH = "$UserDotnet;$($env:PATH)"
}

# 1. Test Mode
if ($Test) {
    Write-Host "===================================================" -ForegroundColor Cyan
    Write-Host "         Running DiskScope Integration Tests       " -ForegroundColor White
    Write-Host "===================================================" -ForegroundColor Cyan
    $testProj = Join-Path $ScriptDir "tests\DiskScope.Tests.csproj"
    if (Test-Path $testProj) {
        & dotnet run --project $testProj
        return
    } else {
        Write-Error "Test project not found at: $testProj"
        return
    }
}

# 2. Site Mode
if ($Site) {
    $siteHtml = Join-Path $ScriptDir "site\index.html"
    if (Test-Path $siteHtml) {
        Write-Host "[*] Opening DiskScope website in default browser..." -ForegroundColor Green
        Start-Process $siteHtml
        return
    } else {
        Write-Error "Website file not found at: $siteHtml"
        return
    }
}

$rootExe = Join-Path $ScriptDir "PrismDrive.exe"
$distExe = Join-Path $ScriptDir "dist\PrismDrive.exe"
$debugExe = Join-Path $ScriptDir "bin\Debug\net8.0-windows\PrismDrive.exe"
$publishExe = Join-Path $ScriptDir "bin\publish\PrismDrive.exe"

$legacyRootExe = Join-Path $ScriptDir "DiskScope.exe"
$legacyDistExe = Join-Path $ScriptDir "dist\DiskScope.exe"
$legacyDebugExe = Join-Path $ScriptDir "bin\Debug\net8.0-windows\DiskScope.exe"

# 2. Publish Mode
if ($Publish) {
    if (Test-Path $rootExe) {
        Write-Host "[*] Launching standalone PrismDrive..." -ForegroundColor Green
        Start-Process -FilePath $rootExe
        return
    } elseif (Test-Path $distExe) {
        Write-Host "[*] Launching standalone published PrismDrive..." -ForegroundColor Green
        Start-Process -FilePath $distExe
        return
    } elseif (Test-Path $legacyRootExe) {
        Write-Host "[*] Launching standalone PrismDrive..." -ForegroundColor Green
        Start-Process -FilePath $legacyRootExe
        return
    } else {
        Write-Host "[*] Building single-file release package (PrismDrive)..." -ForegroundColor Yellow
        & dotnet publish DiskScope.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o dist
        if (Test-Path $distExe) {
            Copy-Item $distExe -Destination $rootExe -Force
            Start-Process -FilePath $rootExe
            return
        }
    }
}

# 3. Fast Mode
if ($Fast) {
    if (Test-Path $rootExe) {
        Write-Host "[*] Fast launch: starting $rootExe..." -ForegroundColor Green
        Start-Process -FilePath $rootExe
        return
    } elseif (Test-Path $distExe) {
        Write-Host "[*] Fast launch: starting $distExe..." -ForegroundColor Green
        Start-Process -FilePath $distExe
        return
    } elseif (Test-Path $debugExe) {
        Write-Host "[*] Fast launch: starting $debugExe..." -ForegroundColor Green
        Start-Process -FilePath $debugExe
        return
    } elseif (Test-Path $publishExe) {
        Write-Host "[*] Fast launch: starting $publishExe..." -ForegroundColor Green
        Start-Process -FilePath $publishExe
        return
    }
    Write-Host "[!] Precompiled binary not found. Proceeding with standard build..." -ForegroundColor Yellow
}

# 4. Check for .NET 8 SDK
$hasNet8 = $false
try {
    $sdks = & dotnet --list-sdks 2>$null
    if ($sdks -match "^8\.") {
        $hasNet8 = $true
    }
} catch {}

# 5. Dev Mode (Attached to console)
if ($Dev) {
    Write-Host "[*] Starting PrismDrive in attached console mode..." -ForegroundColor Cyan
    & dotnet run -c $Configuration --project (Join-Path $ScriptDir "DiskScope.csproj")
    return
}

# 6. Standard Launch: Incremental build + detached GUI launch
if ($hasNet8) {
    Write-Host "[*] Building PrismDrive ($Configuration)..." -ForegroundColor Cyan
    & dotnet build DiskScope.csproj -c $Configuration --nologo -v quiet
    if (Test-Path $debugExe) {
        Write-Host "[*] Starting PrismDrive..." -ForegroundColor Green
        Start-Process -FilePath $debugExe
        return
    } elseif (Test-Path $legacyDebugExe) {
        Write-Host "[*] Starting PrismDrive..." -ForegroundColor Green
        Start-Process -FilePath $legacyDebugExe
        return
    }
}

# 7. Fallback to existing binaries if SDK not found
if (Test-Path $rootExe) {
    Write-Host "[*] Launching standalone PrismDrive..." -ForegroundColor Green
    Start-Process -FilePath $rootExe
    return
}

if (Test-Path $distExe) {
    Write-Host "[*] Launching standalone PrismDrive..." -ForegroundColor Green
    Start-Process -FilePath $distExe
    return
}

if (Test-Path $legacyRootExe) {
    Write-Host "[*] Launching standalone PrismDrive..." -ForegroundColor Green
    Start-Process -FilePath $legacyRootExe
    return
}

if (Test-Path $publishExe) {
    Write-Host "[*] Launching standalone PrismDrive..." -ForegroundColor Green
    Start-Process -FilePath $publishExe
    return
}

if (Test-Path $debugExe) {
    Write-Host "[*] Launching existing PrismDrive binary..." -ForegroundColor Green
    Start-Process -FilePath $debugExe
    return
}

Write-Error "Could not start PrismDrive. Please ensure .NET 8 SDK is installed (https://dotnet.microsoft.com/download/dotnet/8.0)."

