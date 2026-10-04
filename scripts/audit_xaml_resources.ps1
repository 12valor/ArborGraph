$colors = Get-Content Resources/Colors.xaml
$styles = Get-Content Resources/Styles.xaml

$defined = @{}
foreach ($line in ($colors + $styles)) {
    if ($line -match 'x:Key="([^"]+)"') {
        $defined[$matches[1]] = $true
    }
}

$files = Get-ChildItem -Filter *.xaml -Recurse | Where-Object { 
    $_.FullName -notmatch 'Colors\.xaml' -and 
    $_.FullName -notmatch 'Styles\.xaml' -and 
    $_.FullName -notmatch 'App\.xaml' 
}

$missingCount = 0
foreach ($f in $files) {
    $text = Get-Content $f.FullName -Raw
    $local = @{}
    foreach ($m in [regex]::Matches($text, 'x:Key="([^"]+)"')) {
        $local[$m.Groups[1].Value] = $true
    }
    foreach ($m in [regex]::Matches($text, '\{StaticResource\s+([^}]+)\}')) {
        $res = $m.Groups[1].Value.Trim()
        if (-not $defined.ContainsKey($res) -and -not $local.ContainsKey($res)) {
            Write-Host "$($f.Name): $res"
            $missingCount++
        }
    }
}

Write-Host "Total missing resources: $missingCount"
