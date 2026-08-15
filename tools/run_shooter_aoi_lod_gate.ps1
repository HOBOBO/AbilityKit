param(
    [ValidateSet('smoke', 'full')]
    [string]$Profile = 'smoke',
    [string]$OutputPath = 'artifacts\test-gates\shooter-performance.json',
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
    '--', '--profile', $Profile,
    '--output', $resolvedOutputPath
)

Write-Host "dotnet $($arguments -join ' ')" -ForegroundColor DarkGray
& dotnet @arguments
exit $LASTEXITCODE
