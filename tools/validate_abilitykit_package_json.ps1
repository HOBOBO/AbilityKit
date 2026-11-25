$ErrorActionPreference = 'Stop'

$errors = @()
$packageDirs = Get-ChildItem -Path 'Unity\Packages' -Directory -Filter 'com.abilitykit*'
foreach ($dir in $packageDirs) {
    $packagePath = Join-Path $dir.FullName 'package.json'
    if (-not (Test-Path -LiteralPath $packagePath)) {
        continue
    }

    try {
        Get-Content -LiteralPath $packagePath -Raw -Encoding UTF8 | ConvertFrom-Json | Out-Null
    }
    catch {
        $errors += "$packagePath :: $($_.Exception.Message)"
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host '全部 AbilityKit package.json 文件均为有效 JSON。'
