param(
    [int]$Entities = 2000,
    [int]$Warmup = 8,
    [int]$Measurement = 64,
    [double]$MaxP99Milliseconds = 16.7,
    [string]$OutputDirectory = 'artifacts\test-gates\shooter-sync-allocation',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$pipelineGate = Join-Path $PSScriptRoot 'run_shooter_sync_pipeline_gate.ps1'
$resolvedOutputDirectory = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
}

$scenarios = @(
    @{
        Name = 'empty-delta'
        ChangedEntityFraction = 0.0
        RefreshIntervalFrames = 10000
    },
    @{
        Name = 'five-percent-changed'
        ChangedEntityFraction = 0.05
        RefreshIntervalFrames = 10000
    },
    @{
        Name = 'periodic-refresh'
        ChangedEntityFraction = 0.0
        RefreshIntervalFrames = 8
    }
)

foreach ($scenario in $scenarios) {
    $outputPath = Join-Path $resolvedOutputDirectory ($scenario.Name + '.json')
    & $pipelineGate `
        -Snapshot delta `
        -Entities $Entities `
        -Warmup $Warmup `
        -Measurement $Measurement `
        -ChangedEntityFraction $scenario.ChangedEntityFraction `
        -RefreshIntervalFrames $scenario.RefreshIntervalFrames `
        -MaxP99Milliseconds $MaxP99Milliseconds `
        -MaxAllocatedBytes 0 `
        -OutputPath $outputPath `
        -Configuration $Configuration

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Host "Shooter sync allocation gate: PASS ($($scenarios.Count) scenarios, $Entities entities, 0 B steady-state allocation)." -ForegroundColor Green
