param(
    [string]$OutputPath = 'artifacts/moba-content/modifier-impact-report.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $repoRoot $OutputPath
}

& python (Join-Path $repoRoot 'LubanConfig/Moba/tools/modifier_impact_report.py') --output $resolvedOutput
if ($LASTEXITCODE -ne 0) {
    throw "MOBA modifier impact report failed with exit code $LASTEXITCODE."
}
Write-Host "MOBA modifier impact report: $resolvedOutput"
