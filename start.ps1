<#
.SYNOPSIS
    Launcher script for ArborGraph Filesystem Analytics & Visualization.

.DESCRIPTION
    Builds and starts the ArborGraph desktop application. Automatically configures
    the .NET 8 SDK environment or falls back to precompiled standalone executables.

.PARAMETER Fast
    Launches the compiled executable immediately without rebuilding.

.PARAMETER Dev
    Runs the application attached to the console with live logging (dotnet run).

.PARAMETER Publish
    Runs the standalone self-contained publish build in bin\publish\ArborGraph.exe or dist\ArborGraph.exe.

.PARAMETER Test
    Runs the integration test suite in tests\DiskScope.Tests.csproj.

.PARAMETER Site
    Opens the product website and documentation portal in the default web browser.

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
    Write-Host "        Running ArborGraph Integration Tests       " -ForegroundColor White
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
        Write-Host "[*] Opening ArborGraph documentation in default browser..." -ForegroundColor Green
        Start-Process $siteHtml
        return
    } else {
        Write-Error "Website file not found at: $siteHtml"
        return
    }
}

$rootExe = Join-Path $ScriptDir "ArborGraph.exe"
$distExe = Join-Path $ScriptDir "dist\ArborGraph.exe"
$debugExe = Join-Path $ScriptDir "bin\Debug\net8.0-windows\ArborGraph.exe"
$publishExe = Join-Path $ScriptDir "bin\publish\ArborGraph.exe"

# Legacy backwards-compatibility fallbacks
$legacyRootExe = Join-Path $ScriptDir "DiskScope.exe"
$legacyDistExe = Join-Path $ScriptDir "dist\DiskScope.exe"
$legacyDebugExe = Join-Path $ScriptDir "bin\Debug\net8.0-windows\DiskScope.exe"
$fallbackRootExe = Join-Path $ScriptDir "PrismDrive.exe"

# 2. Publish Mode
if ($Publish) {
    if (Test-Path $rootExe) {
        Write-Host "[*] Launching standalone ArborGraph..." -ForegroundColor Green
        Start-Process -FilePath $rootExe
        return
    } elseif (Test-Path $distExe) {
        Write-Host "[*] Launching standalone published ArborGraph..." -ForegroundColor Green
        Start-Process -FilePath $distExe
        return
    } else {
        Write-Host "[*] Building single-file release package (ArborGraph)..." -ForegroundColor Yellow
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
    foreach ($exe in @($rootExe, $distExe, $debugExe, $publishExe, $legacyRootExe, $legacyDistExe, $legacyDebugExe, $fallbackRootExe)) {
        if (Test-Path $exe) {
            Write-Host "[*] Fast launch: starting $exe..." -ForegroundColor Green
            Start-Process -FilePath $exe
            return
        }
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
    Write-Host "[*] Starting ArborGraph in attached console mode..." -ForegroundColor Cyan
    & dotnet run -c $Configuration --project (Join-Path $ScriptDir "DiskScope.csproj")
    return
}

# 6. Standard Launch: Incremental build + detached GUI launch
if ($hasNet8) {
    Write-Host "[*] Building ArborGraph ($Configuration)..." -ForegroundColor Cyan
    & dotnet build DiskScope.csproj -c $Configuration --nologo -v quiet
    foreach ($exe in @($rootExe, $distExe, $debugExe, $legacyDebugExe, $legacyRootExe)) {
        if (Test-Path $exe) {
            Write-Host "[*] Starting ArborGraph..." -ForegroundColor Green
            Start-Process -FilePath $exe
            return
        }
    }
}

# 7. Fallback to existing binaries if SDK not found
foreach ($exe in @($rootExe, $distExe, $debugExe, $publishExe, $legacyRootExe, $legacyDistExe, $legacyDebugExe, $fallbackRootExe)) {
    if (Test-Path $exe) {
        Write-Host "[*] Launching ArborGraph executable: $exe..." -ForegroundColor Green
        Start-Process -FilePath $exe
        return
    }
}

Write-Error "Could not start ArborGraph. Please ensure .NET 8 SDK is installed (https://dotnet.microsoft.com/download/dotnet/8.0)."
