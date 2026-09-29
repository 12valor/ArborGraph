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

.PARAMETER Configuration
    Build configuration to use (Debug or Release). Defaults to Debug.

.EXAMPLE
    .\start.ps1
    .\start.ps1 -Fast
    .\start.ps1 -Dev
    .\start.ps1 -Test
    .\start.ps1 -Publish
#>

[CmdletBinding()]
param(
    [switch]$Fast,
    [switch]$Dev,
    [switch]$Publish,
    [switch]$Test,
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

$debugExe = Join-Path $ScriptDir "bin\Debug\net8.0-windows\DiskScope.exe"
$publishExe = Join-Path $ScriptDir "bin\publish\DiskScope.exe"

# 2. Publish Mode
if ($Publish) {
    if (Test-Path $publishExe) {
        Write-Host "[*] Launching published standalone DiskScope..." -ForegroundColor Green
        Start-Process -FilePath $publishExe
        return
    } else {
        Write-Host "[*] Building self-contained release package..." -ForegroundColor Yellow
        & dotnet publish DiskScope.csproj -c Release -o bin\publish --self-contained true -r win-x64
        if (Test-Path $publishExe) {
            Start-Process -FilePath $publishExe
            return
        }
    }
}

# 3. Fast Mode
if ($Fast) {
    if (Test-Path $debugExe) {
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
    Write-Host "[*] Starting DiskScope in attached console mode..." -ForegroundColor Cyan
    & dotnet run -c $Configuration --project (Join-Path $ScriptDir "DiskScope.csproj")
    return
}

# 6. Standard Launch: Incremental build + detached GUI launch
if ($hasNet8) {
    Write-Host "[*] Building DiskScope ($Configuration)..." -ForegroundColor Cyan
    & dotnet build DiskScope.csproj -c $Configuration --nologo -v quiet
    if (Test-Path $debugExe) {
        Write-Host "[*] Starting DiskScope..." -ForegroundColor Green
        Start-Process -FilePath $debugExe
        return
    }
}

# 7. Fallback to existing binaries if SDK not found
if (Test-Path $publishExe) {
    Write-Host "[*] Launching standalone DiskScope..." -ForegroundColor Green
    Start-Process -FilePath $publishExe
    return
}

if (Test-Path $debugExe) {
    Write-Host "[*] Launching existing DiskScope binary..." -ForegroundColor Green
    Start-Process -FilePath $debugExe
    return
}

Write-Error "Could not start DiskScope. Please ensure .NET 8 SDK is installed (https://dotnet.microsoft.com/download/dotnet/8.0)."
