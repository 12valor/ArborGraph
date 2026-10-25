[CmdletBinding()]
param([switch]$BuildInstaller)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$desktop = Join-Path $repo 'desktop'
$distribution = Join-Path $repo 'dist'
$stage = Join-Path $distribution 'desktop-preview'
$electronDistribution = Join-Path $desktop 'node_modules\electron\dist'
$dotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }
if (-not (Test-Path -LiteralPath (Join-Path $electronDistribution 'electron.exe'))) {
    throw 'Install desktop dependencies with npm ci before building the preview.'
}

Push-Location $desktop
try {
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
} finally { Pop-Location }

# Restrict cleanup to the exact ignored preview staging folder.
$expectedStage = [IO.Path]::GetFullPath((Join-Path $repo 'dist\desktop-preview'))
$stage = [IO.Path]::GetFullPath($stage)
if (-not [string]::Equals($stage, $expectedStage, [StringComparison]::OrdinalIgnoreCase) -or
    -not $stage.StartsWith(([IO.Path]::GetFullPath($repo) + [IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unexpected preview staging path.'
}
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
[IO.Directory]::CreateDirectory($stage) | Out-Null

Copy-Item -Path (Join-Path $electronDistribution '*') -Destination $stage -Recurse -Force
Move-Item -LiteralPath (Join-Path $stage 'electron.exe') -Destination (Join-Path $stage 'ArborGraph Desktop Preview.exe')

$resources = Join-Path $stage 'resources'
$app = Join-Path $resources 'app'
$engine = Join-Path $resources 'engine'
[IO.Directory]::CreateDirectory($app) | Out-Null
Copy-Item -LiteralPath (Join-Path $desktop 'package.json') -Destination $app
Copy-Item -LiteralPath (Join-Path $desktop 'dist') -Destination $app -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'Resources\ArborGraph.ico') -Destination $resources
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repo 'THIRD_PARTY_LICENSES.md') -Destination $stage

& $dotnet publish (Join-Path $repo 'engine\ArborGraph.EngineHost.csproj') -c Release -r win-x64 --self-contained true -o $engine
if ($LASTEXITCODE -ne 0) { throw 'Self-contained engine publish failed.' }
# The referenced WPF project also publishes its launcher. Keep its DLLs for engine reuse,
# but omit that extra executable from the preview package.
$wpfLauncher = Join-Path $engine 'ArborGraph.exe'
if (Test-Path -LiteralPath $wpfLauncher) { Remove-Item -LiteralPath $wpfLauncher -Force }

$required = @(
    (Join-Path $stage 'ArborGraph Desktop Preview.exe'),
    (Join-Path $app 'package.json'),
    (Join-Path $app 'dist\main\main.js'),
    (Join-Path $app 'dist\main\preload.js'),
    (Join-Path $app 'dist\renderer\index.html'),
    (Join-Path $engine 'ArborGraph.EngineHost.exe'),
    (Join-Path $resources 'ArborGraph.ico')
)
foreach ($file in $required) { if (-not (Test-Path -LiteralPath $file)) { throw "Missing preview file: $file" } }
Write-Output "Desktop preview staged at: $stage"
Write-Output 'The WPF release installer and artifacts are unchanged.'

if ($BuildInstaller) {
    $compiler = (Get-Command iscc -ErrorAction SilentlyContinue).Source
    if (-not $compiler) {
        foreach ($candidate in @(
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
            'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
            'C:\Program Files\Inno Setup 6\ISCC.exe'
        )) {
            if (Test-Path -LiteralPath $candidate) { $compiler = $candidate; break }
        }
    }
    if (-not $compiler) { throw 'Inno Setup 6 ISCC.exe is required to build the preview installer.' }
    & $compiler (Join-Path $repo 'installer\desktop-preview.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Preview installer compilation failed.' }
}
