@echo off
setlocal enabledelayedexpansion

:: Set window title and current directory
title DiskScope Launcher
cd /d "%~dp0"

:: Ensure user .dotnet SDK directory is prioritized if present
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "PATH=%USERPROFILE%\.dotnet;%PATH%"
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
)

:: Check arguments
if "%~1"=="--help" goto :ShowHelp
if "%~1"=="-h" goto :ShowHelp
if "%~1"=="/?" goto :ShowHelp
if "%~1"=="--test" goto :RunTests
if "%~1"=="test" goto :RunTests
if "%~1"=="--fast" goto :RunFast
if "%~1"=="fast" goto :RunFast
if "%~1"=="--dev" goto :RunDev
if "%~1"=="dev" goto :RunDev
if "%~1"=="--console" goto :RunDev
if "%~1"=="console" goto :RunDev
if "%~1"=="--publish" goto :RunPublish
if "%~1"=="publish" goto :RunPublish

:: Default launch flow:
:: 1. If .NET 8 SDK is available, do a quick incremental build so latest changes are included
where dotnet >nul 2>&1
if %ERRORLEVEL% equ 0 (
    dotnet --list-sdks 2>nul | findstr /R "^8\." >nul 2>&1
    if !ERRORLEVEL! equ 0 (
        echo [*] Building DiskScope...
        dotnet build DiskScope.csproj -c Debug --nologo -v quiet
        if !ERRORLEVEL! neq 0 (
            echo [!] Build warning: falling back to existing executable...
        )
    )
)

:: 2. Launch root standalone executable if available
if exist "DiskScope.exe" (
    echo [*] Starting standalone DiskScope...
    start "" "DiskScope.exe" %*
    goto :Success
)

:: 3. Launch dist standalone executable if available
if exist "dist\DiskScope.exe" (
    echo [*] Starting standalone DiskScope...
    start "" "dist\DiskScope.exe" %*
    goto :Success
)

:: 4. Launch the Debug executable if available
if exist "bin\Debug\net8.0-windows\DiskScope.exe" (
    echo [*] Starting DiskScope...
    start "" "bin\Debug\net8.0-windows\DiskScope.exe" %*
    goto :Success
)

:: 5. Launch self-contained publish build if available
if exist "bin\publish\DiskScope.exe" (
    echo [*] Starting published standalone DiskScope...
    start "" "bin\publish\DiskScope.exe" %*
    goto :Success
)

:: 6. If neither binary exists, try dotnet run directly
where dotnet >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo [*] Launching with dotnet run...
    dotnet run --project DiskScope.csproj -- %*
    if !ERRORLEVEL! equ 0 goto :Success
)

:: Error state
echo.
echo [ERROR] Could not start DiskScope!
echo Neither a precompiled executable nor a compatible .NET 8 SDK was found.
echo Please install the .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
echo.
if "%~1"=="" pause
exit /b 1

:RunFast
if exist "DiskScope.exe" (
    echo [*] Fast launch: starting DiskScope.exe...
    start "" "DiskScope.exe" %2 %3 %4 %5 %6
    goto :Success
)
if exist "dist\DiskScope.exe" (
    echo [*] Fast launch: starting dist\DiskScope.exe...
    start "" "dist\DiskScope.exe" %2 %3 %4 %5 %6
    goto :Success
)
if exist "bin\Debug\net8.0-windows\DiskScope.exe" (
    echo [*] Fast launch: starting DiskScope...
    start "" "bin\Debug\net8.0-windows\DiskScope.exe" %2 %3 %4 %5 %6
    goto :Success
)
if exist "bin\publish\DiskScope.exe" (
    echo [*] Fast launch: starting published standalone DiskScope...
    start "" "bin\publish\DiskScope.exe" %2 %3 %4 %5 %6
    goto :Success
)
echo [!] Precompiled binary not found. Running normal startup...
goto :EOF

:RunDev
echo [*] Starting DiskScope in attached console mode...
dotnet run --project DiskScope.csproj -- %2 %3 %4 %5 %6
exit /b %ERRORLEVEL%

:RunPublish
if exist "DiskScope.exe" (
    echo [*] Launching standalone build...
    start "" "DiskScope.exe" %2 %3 %4 %5 %6
    goto :Success
)
if exist "dist\DiskScope.exe" (
    echo [*] Launching standalone published build...
    start "" "dist\DiskScope.exe" %2 %3 %4 %5 %6
    goto :Success
)
if exist "bin\publish\DiskScope.exe" (
    echo [*] Launching standalone published build...
    start "" "bin\publish\DiskScope.exe" %2 %3 %4 %5 %6
    goto :Success
)
echo [*] Standalone build not found. Publishing single-file executable now (Release / win-x64)...
dotnet publish DiskScope.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o dist
if exist "dist\DiskScope.exe" (
    copy /y "dist\DiskScope.exe" "DiskScope.exe" >nul
    start "" "DiskScope.exe"
    goto :Success
)
echo [ERROR] Publish failed.
if "%~1"=="" pause
exit /b 1

:RunTests
echo [*] Running DiskScope Integration Tests...
echo.
if exist "tests\DiskScope.Tests.csproj" (
    dotnet run --project tests\DiskScope.Tests.csproj
    exit /b !ERRORLEVEL!
)
echo [!] Test project not found at tests\DiskScope.Tests.csproj
exit /b 1

:ShowHelp
echo ===================================================
echo             DiskScope Storage Analyzer            
echo ===================================================
echo.
echo Usage:
echo   start.bat            Build (if SDK available) and launch DiskScope GUI
echo   start.bat --fast     Launch compiled executable immediately without build
echo   start.bat --dev      Run attached to console with live output (dotnet run)
echo   start.bat --publish  Launch standalone self-contained build (bin\publish)
echo   start.bat --test     Run the automated integration test suite
echo   start.bat --help     Display this help message
echo.
exit /b 0

:Success
exit /b 0
