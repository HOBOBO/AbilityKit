[CmdletBinding()]
param(
    [string]$OwnerProject,
    [string]$MemberProject,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe',
    [string]$GatewayHost = '127.0.0.1',
    [int]$GatewayPort = 4000,
    [string]$GatewayRegion = 'dev',
    [string]$GatewayServerId = 'local',
    [int]$SyncModel = 5,
    [int[]]$EnemyBudgets = @(1000, 2000),
    [ValidateSet('gameobject', 'gpu')]
    [string]$ViewBackend = 'gameobject',
    [ValidateSet('ideal', 'lan', 'mobile4g', 'crossregion', 'poorwifi', 'limitedbw')]
    [string[]]$NetworkEnvironments = @('ideal', 'limitedbw'),
    [ValidateRange(1, 10)]
    [int]$Repetitions = 1,
    [int]$TimeoutSeconds = 300,
    [string]$ServerLogPath,
    [string]$OutputRoot,
    [switch]$SkipCompileWarmup,
    [switch]$EnableGate,
    [switch]$PlanOnly,
    [ValidateRange(1.0, 10.0)]
    [double]$MaxAveragePayloadAmplification = 1.75,
    [ValidateRange(0.0, 1.0)]
    [double]$MaxStarvationRatioIncrease = 0.02,
    [ValidateRange(0.0, 1.0)]
    [double]$MaxHeldRatioIncrease = 0.02,
    [ValidateRange(0.0, 5000.0)]
    [double]$MaxP99ArrivalGapIncreaseMs = 50.0,
    [ValidateRange(0.0, 5000.0)]
    [double]$MaxP99QueueWaitIncreaseMs = 50.0,
    [ValidateRange(0.0, 5000.0)]
    [double]$MaxP99ApplyIncreaseMs = 50.0,
    [ValidateRange(0.0, 100.0)]
    [double]$MaxUnexplainedBackwardIncrease = 0.10,
    [ValidateRange(0, 10485760)]
    [double]$MaxAverageGcBytesIncrease = 65536,
    [ValidateRange(0, 100)]
    [int]$MaxResyncNeededIncrease = 4,
    [ValidateRange(0, 100)]
    [int]$MaxFullSnapshotIncrease = 4,
    [ValidateRange(1, 4096)]
    [int]$MaxTransformSamplesPerBlock = 32
)

$ErrorActionPreference = 'Stop'
$baselineTemplate = 'mass-battle-lod-aoi'
$candidateTemplate = 'mass-battle-lod-aoi-sample-block'
$runner = Join-Path $PSScriptRoot 'run_shooter_unity_headless_multiplayer.ps1'
$comparator = Join-Path $PSScriptRoot 'compare_shooter_pure_state_sample_block_ab.ps1'

$planCases = [System.Collections.Generic.List[object]]::new()
foreach ($enemyBudget in $EnemyBudgets) {
    foreach ($networkEnvironment in $NetworkEnvironments) {
        for ($repetition = 1; $repetition -le $Repetitions; $repetition++) {
            $planCases.Add([pscustomobject]@{
                caseId = "enemies-$enemyBudget-$networkEnvironment-run-$repetition"
                enemyBudget = $enemyBudget
                networkEnvironment = $networkEnvironment
                repetition = $repetition
                baselineTemplateId = $baselineTemplate
                candidateTemplateId = $candidateTemplate
                syncModel = $SyncModel
                viewBackend = $ViewBackend
            })
        }
    }
}

if ($PlanOnly) {
    [pscustomobject]@{
        schemaVersion = 1
        gateEnabled = $EnableGate.IsPresent
        caseCount = $planCases.Count
        cases = $planCases
    } | ConvertTo-Json -Depth 8
    return
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $runId = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss-fff')
    $OutputRoot = Join-Path $PSScriptRoot "..\artifacts\shooter-pure-state-sample-block-ab\$runId"
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

function Get-LatestRunDirectory {
    param([string]$Root)
    return Get-ChildItem -LiteralPath $Root -Directory |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Get-Median {
    param([AllowEmptyCollection()][double[]]$Values)
    if ($null -eq $Values -or $Values.Count -eq 0) { return $null }
    $ordered = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($ordered.Count / 2)
    if (($ordered.Count % 2) -eq 1) { return [Math]::Round($ordered[$middle], 6) }
    return [Math]::Round(($ordered[$middle - 1] + $ordered[$middle]) / 2.0, 6)
}

function Invoke-HeadlessVariant {
    param(
        [string]$TemplateId,
        [string]$VariantRoot,
        [int]$EnemyBudget,
        [string]$NetworkEnvironment,
        [bool]$SkipWarmup
    )
    New-Item -ItemType Directory -Path $VariantRoot -Force | Out-Null
    $arguments = @{
        UnityExe = $UnityExe
        GatewayHost = $GatewayHost
        GatewayPort = $GatewayPort
        GatewayRegion = $GatewayRegion
        GatewayServerId = $GatewayServerId
        SyncTemplateId = $TemplateId
        SyncModel = $SyncModel
        NetworkEnvironmentId = $NetworkEnvironment
        EnemyBudget = $EnemyBudget
        ViewBackend = $ViewBackend
        TimeoutSeconds = $TimeoutSeconds
        OutputRoot = $VariantRoot
        SkipCompileWarmup = $SkipWarmup
        SkipPerformanceValidation = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($ServerLogPath)) { $arguments.ServerLogPath = $ServerLogPath }
    if (-not [string]::IsNullOrWhiteSpace($OwnerProject)) { $arguments.OwnerProject = $OwnerProject }
    if (-not [string]::IsNullOrWhiteSpace($MemberProject)) { $arguments.MemberProject = $MemberProject }
    & $runner @arguments
    return Get-LatestRunDirectory $VariantRoot
}

$caseResults = [System.Collections.Generic.List[object]]::new()
$executionIndex = 0
foreach ($case in $planCases) {
    $caseRoot = Join-Path $OutputRoot $case.caseId
    $baselineRoot = Join-Path $caseRoot 'baseline'
    $candidateRoot = Join-Path $caseRoot 'candidate'
    $comparisonPath = Join-Path $caseRoot 'comparison.json'
    $startedAt = [DateTime]::UtcNow
    $errorMessage = ''
    $comparison = $null
    try {
        $executionIndex++
        $skipBaselineWarmup = $SkipCompileWarmup.IsPresent -or $executionIndex -gt 1
        $baselineRun = Invoke-HeadlessVariant `
            -TemplateId $baselineTemplate `
            -VariantRoot $baselineRoot `
            -EnemyBudget $case.enemyBudget `
            -NetworkEnvironment $case.networkEnvironment `
            -SkipWarmup $skipBaselineWarmup
        if (-not $baselineRun) { throw "Baseline produced no run directory for $($case.caseId)." }

        $executionIndex++
        $candidateRun = Invoke-HeadlessVariant `
            -TemplateId $candidateTemplate `
            -VariantRoot $candidateRoot `
            -EnemyBudget $case.enemyBudget `
            -NetworkEnvironment $case.networkEnvironment `
            -SkipWarmup $true
        if (-not $candidateRun) { throw "Candidate produced no run directory for $($case.caseId)." }

        $compareArguments = @{
            BaselineOwnerResultPath = Join-Path $baselineRun.FullName 'owner-result.json'
            BaselineMemberResultPath = Join-Path $baselineRun.FullName 'member-result.json'
            CandidateOwnerResultPath = Join-Path $candidateRun.FullName 'owner-result.json'
            CandidateMemberResultPath = Join-Path $candidateRun.FullName 'member-result.json'
            OutputPath = $comparisonPath
            EnableGate = $EnableGate.IsPresent
            MaxAveragePayloadAmplification = $MaxAveragePayloadAmplification
            MaxStarvationRatioIncrease = $MaxStarvationRatioIncrease
            MaxHeldRatioIncrease = $MaxHeldRatioIncrease
            MaxP99ArrivalGapIncreaseMs = $MaxP99ArrivalGapIncreaseMs
            MaxP99QueueWaitIncreaseMs = $MaxP99QueueWaitIncreaseMs
            MaxP99ApplyIncreaseMs = $MaxP99ApplyIncreaseMs
            MaxUnexplainedBackwardIncrease = $MaxUnexplainedBackwardIncrease
            MaxAverageGcBytesIncrease = $MaxAverageGcBytesIncrease
            MaxResyncNeededIncrease = $MaxResyncNeededIncrease
            MaxFullSnapshotIncrease = $MaxFullSnapshotIncrease
            MaxTransformSamplesPerBlock = $MaxTransformSamplesPerBlock
        }
        & $comparator @compareArguments
        $comparison = Get-Content -LiteralPath $comparisonPath -Raw | ConvertFrom-Json
    }
    catch {
        $errorMessage = $_.Exception.Message
        if (Test-Path -LiteralPath $comparisonPath -PathType Leaf) {
            $comparison = Get-Content -LiteralPath $comparisonPath -Raw | ConvertFrom-Json
        }
    }

    $passed = $null -ne $comparison -and [bool]$comparison.contractPassed -and [bool]$comparison.gatePassed -and [string]::IsNullOrWhiteSpace($errorMessage)
    $caseResults.Add([pscustomobject]@{
        caseId = $case.caseId
        enemyBudget = $case.enemyBudget
        networkEnvironment = $case.networkEnvironment
        repetition = $case.repetition
        passed = $passed
        error = $errorMessage
        durationSeconds = [Math]::Round(([DateTime]::UtcNow - $startedAt).TotalSeconds, 3)
        comparisonPath = $comparisonPath
        comparison = $comparison
    })
}

$summaryPath = Join-Path $OutputRoot 'ab-summary.json'
$failedCases = @($caseResults | Where-Object { -not $_.passed })
$groups = [System.Collections.Generic.List[object]]::new()
$caseGroups = $caseResults | Group-Object { "$($_.enemyBudget)|$($_.networkEnvironment)" }
foreach ($caseGroup in $caseGroups) {
    $valid = @($caseGroup.Group | Where-Object {
        $null -ne $_.comparison -and [bool]$_.comparison.contractPassed
    })
    $validPayload = @($valid | Where-Object {
        $null -ne $_.comparison.delta.averagePayloadAmplification
    })
    $first = $caseGroup.Group | Select-Object -First 1
    $groups.Add([pscustomobject]@{
        enemyBudget = $first.enemyBudget
        networkEnvironment = $first.networkEnvironment
        repetitionCount = $caseGroup.Count
        validComparisonCount = $valid.Count
        passedCount = @($caseGroup.Group | Where-Object passed).Count
        medianAveragePayloadAmplification = Get-Median @($validPayload | ForEach-Object { [double]$_.comparison.delta.averagePayloadAmplification })
        medianStarvationRatioDelta = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.starvationRatio })
        medianHeldRatioDelta = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.heldRatio })
        medianP99ArrivalGapDeltaMs = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.p99ArrivalGapMs })
        medianP99QueueWaitDeltaMs = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.p99QueueWaitMs })
        medianP99ApplyDeltaMs = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.p99ApplyMs })
        medianPeakQueueDepthDelta = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.peakQueueDepth })
        medianCoalescedSnapshotCountDelta = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.coalescedSnapshotCount })
        medianBudgetLimitedDrainRatioDelta = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.budgetLimitedDrainRatio })
        medianUnexplainedBackwardDelta = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.maxUnexplainedBackwardMovement })
        medianAverageGcBytesDelta = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.averageGcBytesPerFrame })
        medianResyncNeededCountDelta = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.resyncNeededCount })
        medianFullSnapshotAppliedCountDelta = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.fullSnapshotAppliedCount })
        medianAutomaticFullStateSyncCoalescedRequestCountDelta = Get-Median @($valid | ForEach-Object { [double]$_.comparison.delta.automaticFullStateSyncCoalescedRequestCount })
    })
}
$summary = [pscustomobject]@{
    schemaVersion = 1
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    baselineTemplateId = $baselineTemplate
    candidateTemplateId = $candidateTemplate
    gateEnabled = $EnableGate.IsPresent
    syncModel = $SyncModel
    viewBackend = $ViewBackend
    caseCount = $caseResults.Count
    passedCount = $caseResults.Count - $failedCases.Count
    failedCount = $failedCases.Count
    groups = $groups
    cases = $caseResults
}
$summary | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Host "Shooter PureState sample-block A/B: $($summary.passedCount)/$($summary.caseCount) passed."
Write-Host "Summary: $summaryPath"
if ($failedCases.Count -gt 0) {
    throw "Shooter PureState sample-block A/B has $($failedCases.Count) failing case(s)."
}
