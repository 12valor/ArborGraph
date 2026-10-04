$installedApp = Join-Path $env:LOCALAPPDATA "Programs\ArborGraph\ArborGraph.exe"
if (Test-Path $installedApp) {
    $settingsFolder = Join-Path $env:LOCALAPPDATA "ArborGraph"
    if (-not (Test-Path $settingsFolder)) { 
        New-Item -ItemType Directory -Path $settingsFolder -Force | Out-Null
    }
    $settingsPath = Join-Path $settingsFolder "settings.json"
    $json = '{"HasAcceptedEula": true, "EulaAcceptedVersion": "1.0.0", "EulaAcceptedDate": "2026-10-05T02:00:00Z"}'
    Set-Content -Path $settingsPath -Value $json -Force

    $proc = Start-Process -FilePath $installedApp -PassThru
    Start-Sleep -Seconds 3

    Add-Type -AssemblyName System.Drawing, System.Windows.Forms
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $outputPath = "c:\Users\evang\Downloads\diskscope\docs\testing\evidence\desktop_running_arborgraph.png"
    $bmp.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose()
    $bmp.Dispose()
    Write-Host "Captured desktop screenshot to: $outputPath"

    Stop-Process -Id $proc.Id -Force
    Write-Host "ArborGraph launched and exited cleanly."
} else {
    Write-Warning "Installed app not found at $installedApp"
}
