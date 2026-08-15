param(
    [ValidateSet('smoke', 'full')]
    [string]$Profile = 'smoke',
    [ValidateSet('all', 'attributes', 'core-collections', 'event-dispatcher', 'modifiers', 'pipeline', 'record', 'targeting', 'triggering')]
    [string]$Module = 'all',
    [ValidateSet('all', 'package', 'capability')]
    [string]$Scope = 'all',
    [string]$OutputPath = 'artifacts\runtime-benchmarks\smoke.json',
    [string]$Configuration = 'Release',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $repoRoot 'src\AbilityKit.Runtime.Benchmarks\AbilityKit.Runtime.Benchmarks.csproj'
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
}

$null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolvedOutputPath)
$arguments = @(
    'run', '--project', $project,
    '--configuration', $Configuration
)
if ($NoRestore) {
    $arguments += '--no-restore'
}
$arguments += @(
    '--', '--profile', $Profile,
    '--module', $Module,
    '--scope', $Scope,
    '--output', $resolvedOutputPath
)

Write-Host "dotnet $($arguments -join ' ')" -ForegroundColor DarkGray
& dotnet @arguments
exit $LASTEXITCODE
