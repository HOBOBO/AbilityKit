param(
    [ValidateSet('full', 'delta')]
    [string]$Snapshot = 'delta',
    [int]$Entities = 1000,
    [int]$Warmup = 5,
    [int]$Measurement = 64,
    [double]$MaxP99Milliseconds = 16.7,
    [long]$MaxAllocatedBytes = 4194304,
    [string]$OutputPath = 'artifacts\test-gates\shooter-sync-pipeline.json',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $repoRoot 'src\AbilityKit.Demo.Shooter.AoiLodBenchmarks\AbilityKit.Demo.Shooter.AoiLodBenchmarks.csproj'
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
}

$null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolvedOutputPath)
$arguments = @(
    'run', '--project', $project,
    '--configuration', $Configuration,
    '--', '--pipeline', 'true',
    '--snapshot', $Snapshot,
    '--entities', $Entities,
    '--warmup', $Warmup,
    '--measurement', $Measurement,
    '--max-p99-ms', $MaxP99Milliseconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--max-alloc-bytes', $MaxAllocatedBytes,
    '--output', $resolvedOutputPath
)

Write-Host "dotnet $($arguments -join ' ')" -ForegroundColor DarkGray
& dotnet @arguments
exit $LASTEXITCODE
