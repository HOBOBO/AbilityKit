[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaselineOwnerResultPath,
    [Parameter(Mandatory = $true)]
    [string]$BaselineMemberResultPath,
    [Parameter(Mandatory = $true)]
    [string]$CandidateOwnerResultPath,
    [Parameter(Mandatory = $true)]
    [string]$CandidateMemberResultPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,
    [switch]$EnableGate,
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
$assertions = [System.Collections.Generic.List[object]]::new()

function Read-ClientState {
    param([string]$Path, [string]$Label)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label result was not found: $Path"
    }

    $result = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if (-not $result -or -not [bool]$result.success -or -not $result.state) {
        throw "$Label result was not successful: $Path"
    }

    return $result.state
}

function Get-Number {
    param($State, [string]$Name, [double]$Default = 0.0)
    $property = $State.PSObject.Properties[$Name]
    if (-not $property -or $null -eq $property.Value) { return $Default }
    return [double]$property.Value
}

function Get-Text {
    param($State, [string]$Name)
    $property = $State.PSObject.Properties[$Name]
    if (-not $property -or $null -eq $property.Value) { return '' }
    return [string]$property.Value
}

function Get-AveragePayloadBytes {
    param($State)
    $reported = Get-Number $State 'battlePushAveragePayloadBytes'
    if ($reported -gt 0.0) { return $reported }
    $count = Get-Number $State 'battlePushReceivedCount'
    $bytes = Get-Number $State 'battlePushPayloadBytes'
    if ($count -gt 0.0) { return $bytes / $count }
    return 0.0
}

function Get-Aggregate {
    param($Owner, $Member)
    $states = @($Owner, $Member)
    $historicalTransforms = ($states | ForEach-Object { Get-Number $_ 'pureStatePlaybackReceivedTransformSampleCount' } | Measure-Object -Sum).Sum
    $authoritativeTransforms = ($states | ForEach-Object { Get-Number $_ 'pureStatePlaybackReceivedAuthoritativeTransformCount' } | Measure-Object -Sum).Sum
    return [pscustomobject]@{
        averagePayloadBytes = [Math]::Round((($states | ForEach-Object { Get-AveragePayloadBytes $_ } | Measure-Object -Average).Average), 3)
        maxPayloadBytes = [Math]::Round((($states | ForEach-Object { Get-Number $_ 'battlePushMaxPayloadBytes' } | Measure-Object -Maximum).Maximum), 3)
        starvationRatio = [Math]::Round((($states | ForEach-Object { Get-Number $_ 'pureStatePlaybackStarvationRatio' } | Measure-Object -Average).Average), 6)
        heldRatio = [Math]::Round((($states | ForEach-Object { Get-Number $_ 'pureStatePlaybackHeldRatio' } | Measure-Object -Average).Average), 6)
        p99ArrivalGapMs = [Math]::Round((($states | ForEach-Object { Get-Number $_ 'p99SnapshotArrivalGapMs' } | Measure-Object -Maximum).Maximum), 3)
        p95SourceAgeMs = [Math]::Round((($states | ForEach-Object { Get-Number $_ 'p95SnapshotSourceAgeMs' } | Measure-Object -Maximum).Maximum), 3)
        p99QueueWaitMs = [Math]::Round((($states | ForEach-Object { Get-Number $_ 'p99BattlePushQueueWaitMs' } | Measure-Object -Maximum).Maximum), 3)
        p99ApplyMs = [Math]::Round((($states | ForEach-Object { Get-Number $_ 'p99BattlePushApplyMs' } | Measure-Object -Maximum).Maximum), 3)
        maxBackwardMovement = [Math]::Round((($states | ForEach-Object { Get-Number $_ 'maxBackwardMovement' } | Measure-Object -Maximum).Maximum), 6)
        maxUnexplainedBackwardMovement = [Math]::Round((($states | ForEach-Object { Get-Number $_ 'maxUnexplainedBackwardMovement' } | Measure-Object -Maximum).Maximum), 6)
        averageGcBytesPerFrame = [Math]::Round((($states | ForEach-Object { Get-Number $_ 'averageGcBytesPerFrame' } | Measure-Object -Average).Average), 3)
        peakQueueDepth = [int](($states | ForEach-Object { Get-Number $_ 'battlePushPeakQueueDepth' } | Measure-Object -Maximum).Maximum)
        enqueuedPushCount = [long](($states | ForEach-Object { Get-Number $_ 'battlePushEnqueuedCount' } | Measure-Object -Sum).Sum)
        processedPushCount = [long](($states | ForEach-Object { Get-Number $_ 'battlePushProcessedCount' } | Measure-Object -Sum).Sum)
        coalescedSnapshotCount = [long](($states | ForEach-Object { Get-Number $_ 'battlePushCoalescedSnapshotCount' } | Measure-Object -Sum).Sum)
        drainCount = [long](($states | ForEach-Object { Get-Number $_ 'battlePushDrainCount' } | Measure-Object -Sum).Sum)
        budgetLimitedDrainCount = [long](($states | ForEach-Object { Get-Number $_ 'battlePushBudgetLimitedDrainCount' } | Measure-Object -Sum).Sum)
        budgetLimitedDrainRatio = if (($states | ForEach-Object { Get-Number $_ 'battlePushDrainCount' } | Measure-Object -Sum).Sum -gt 0) {
            [Math]::Round(
                (($states | ForEach-Object { Get-Number $_ 'battlePushBudgetLimitedDrainCount' } | Measure-Object -Sum).Sum) /
                (($states | ForEach-Object { Get-Number $_ 'battlePushDrainCount' } | Measure-Object -Sum).Sum),
                6)
        } else { 0.0 }
        receivedSampleBlockCount = [long](($states | ForEach-Object { Get-Number $_ 'pureStatePlaybackReceivedSampleBlockCount' } | Measure-Object -Sum).Sum)
        receivedFrameSampleCount = [long](($states | ForEach-Object { Get-Number $_ 'pureStatePlaybackReceivedFrameSampleCount' } | Measure-Object -Sum).Sum)
        rejectedFrameSampleCount = [long](($states | ForEach-Object { Get-Number $_ 'pureStatePlaybackRejectedFrameSampleCount' } | Measure-Object -Sum).Sum)
        staleFrameSampleCount = [long](($states | ForEach-Object { Get-Number $_ 'pureStatePlaybackStaleFrameSampleCount' } | Measure-Object -Sum).Sum)
        invalidFrameSampleCount = [long](($states | ForEach-Object { Get-Number $_ 'pureStatePlaybackInvalidFrameSampleCount' } | Measure-Object -Sum).Sum)
        maxTransformSamplesPerBlock = [int](($states | ForEach-Object { Get-Number $_ 'pureStatePlaybackMaxTransformSampleCountPerBlock' } | Measure-Object -Maximum).Maximum)
        historicalTransformCount = [long]$historicalTransforms
        authoritativeTransformCount = [long]$authoritativeTransforms
        resyncNeededCount = [long](($states | ForEach-Object { Get-Number $_ 'snapshotResyncNeededCount' } | Measure-Object -Sum).Sum)
        automaticFullStateSyncCoalescedRequestCount = [long](($states | ForEach-Object { Get-Number $_ 'automaticFullStateSyncCoalescedRequestCount' } | Measure-Object -Sum).Sum)
        fullSnapshotAppliedCount = [long](($states | ForEach-Object { Get-Number $_ 'pureStateFullAppliedCount' } | Measure-Object -Sum).Sum)
        historicalTransformAmplificationRatio = if ($authoritativeTransforms -gt 0.0) {
            [Math]::Round($historicalTransforms / $authoritativeTransforms, 6)
        } else { 0.0 }
    }
}

function Add-Assertion {
    param([string]$Name, [bool]$Passed, [string]$Kind, [string]$Detail)
    $assertions.Add([pscustomobject]@{
        name = $Name
        passed = $Passed
        kind = $Kind
        detail = $Detail
    })
}

function Get-Ratio {
    param([double]$Candidate, [double]$Baseline)
    if ($Baseline -gt 0.0) { return [Math]::Round($Candidate / $Baseline, 6) }
    if ($Candidate -le 0.0) { return 1.0 }
    return $null
}

$baselineOwner = Read-ClientState $BaselineOwnerResultPath 'Baseline owner'
$baselineMember = Read-ClientState $BaselineMemberResultPath 'Baseline member'
$candidateOwner = Read-ClientState $CandidateOwnerResultPath 'Candidate owner'
$candidateMember = Read-ClientState $CandidateMemberResultPath 'Candidate member'

foreach ($entry in @(
    @{ Label = 'baseline owner'; State = $baselineOwner; Template = $baselineTemplate },
    @{ Label = 'baseline member'; State = $baselineMember; Template = $baselineTemplate },
    @{ Label = 'candidate owner'; State = $candidateOwner; Template = $candidateTemplate },
    @{ Label = 'candidate member'; State = $candidateMember; Template = $candidateTemplate }
)) {
    $actual = Get-Text $entry.State 'syncTemplateId'
    Add-Assertion "template-$($entry.Label.Replace(' ', '-'))" ($actual -eq $entry.Template) 'contract' "expected=$($entry.Template), actual=$actual"
}

foreach ($name in @('syncModel', 'networkEnvironmentId', 'enemyBudget', 'viewBackend')) {
    $baselineValues = @((Get-Text $baselineOwner $name), (Get-Text $baselineMember $name))
    $candidateValues = @((Get-Text $candidateOwner $name), (Get-Text $candidateMember $name))
    $allValues = @($baselineValues + $candidateValues)
    $distinct = @($allValues | Select-Object -Unique)
    Add-Assertion "matched-$name" ($distinct.Count -eq 1 -and -not [string]::IsNullOrWhiteSpace($distinct[0])) 'contract' "values=$($allValues -join ',')"
}

$baseline = Get-Aggregate $baselineOwner $baselineMember
$candidate = Get-Aggregate $candidateOwner $candidateMember
Add-Assertion 'baseline-has-no-sample-blocks' ($baseline.receivedSampleBlockCount -eq 0 -and $baseline.historicalTransformCount -eq 0) 'contract' "blocks=$($baseline.receivedSampleBlockCount), transforms=$($baseline.historicalTransformCount)"
Add-Assertion 'candidate-has-sample-blocks' ($candidate.receivedSampleBlockCount -gt 0 -and $candidate.receivedFrameSampleCount -ge $candidate.receivedSampleBlockCount -and $candidate.historicalTransformCount -gt 0) 'contract' "blocks=$($candidate.receivedSampleBlockCount), frames=$($candidate.receivedFrameSampleCount), transforms=$($candidate.historicalTransformCount)"
Add-Assertion 'candidate-has-no-invalid-frame-samples' ($candidate.invalidFrameSampleCount -eq 0) 'contract' "invalid=$($candidate.invalidFrameSampleCount), stale=$($candidate.staleFrameSampleCount), rejectedTotal=$($candidate.rejectedFrameSampleCount)"
Add-Assertion 'candidate-respects-transform-block-budget' ($candidate.maxTransformSamplesPerBlock -gt 0 -and $candidate.maxTransformSamplesPerBlock -le $MaxTransformSamplesPerBlock) 'contract' "actual=$($candidate.maxTransformSamplesPerBlock), max=$MaxTransformSamplesPerBlock"

$payloadRatio = Get-Ratio $candidate.averagePayloadBytes $baseline.averagePayloadBytes
$deltas = [pscustomobject]@{
    averagePayloadBytes = [Math]::Round($candidate.averagePayloadBytes - $baseline.averagePayloadBytes, 3)
    averagePayloadAmplification = $payloadRatio
    starvationRatio = [Math]::Round($candidate.starvationRatio - $baseline.starvationRatio, 6)
    heldRatio = [Math]::Round($candidate.heldRatio - $baseline.heldRatio, 6)
    p99ArrivalGapMs = [Math]::Round($candidate.p99ArrivalGapMs - $baseline.p99ArrivalGapMs, 3)
    p95SourceAgeMs = [Math]::Round($candidate.p95SourceAgeMs - $baseline.p95SourceAgeMs, 3)
    p99QueueWaitMs = [Math]::Round($candidate.p99QueueWaitMs - $baseline.p99QueueWaitMs, 3)
    p99ApplyMs = [Math]::Round($candidate.p99ApplyMs - $baseline.p99ApplyMs, 3)
    maxBackwardMovement = [Math]::Round($candidate.maxBackwardMovement - $baseline.maxBackwardMovement, 6)
    maxUnexplainedBackwardMovement = [Math]::Round($candidate.maxUnexplainedBackwardMovement - $baseline.maxUnexplainedBackwardMovement, 6)
    averageGcBytesPerFrame = [Math]::Round($candidate.averageGcBytesPerFrame - $baseline.averageGcBytesPerFrame, 3)
    peakQueueDepth = $candidate.peakQueueDepth - $baseline.peakQueueDepth
    coalescedSnapshotCount = $candidate.coalescedSnapshotCount - $baseline.coalescedSnapshotCount
    budgetLimitedDrainRatio = [Math]::Round($candidate.budgetLimitedDrainRatio - $baseline.budgetLimitedDrainRatio, 6)
    resyncNeededCount = $candidate.resyncNeededCount - $baseline.resyncNeededCount
    automaticFullStateSyncCoalescedRequestCount = $candidate.automaticFullStateSyncCoalescedRequestCount - $baseline.automaticFullStateSyncCoalescedRequestCount
    fullSnapshotAppliedCount = $candidate.fullSnapshotAppliedCount - $baseline.fullSnapshotAppliedCount
}

if ($EnableGate) {
    Add-Assertion 'payload-amplification-budget' ($null -ne $payloadRatio -and $payloadRatio -le $MaxAveragePayloadAmplification) 'gate' "actual=$payloadRatio, max=$MaxAveragePayloadAmplification"
    Add-Assertion 'starvation-regression-budget' ($deltas.starvationRatio -le $MaxStarvationRatioIncrease) 'gate' "delta=$($deltas.starvationRatio), max=$MaxStarvationRatioIncrease"
    Add-Assertion 'held-regression-budget' ($deltas.heldRatio -le $MaxHeldRatioIncrease) 'gate' "delta=$($deltas.heldRatio), max=$MaxHeldRatioIncrease"
    Add-Assertion 'arrival-gap-regression-budget' ($deltas.p99ArrivalGapMs -le $MaxP99ArrivalGapIncreaseMs) 'gate' "deltaMs=$($deltas.p99ArrivalGapMs), maxMs=$MaxP99ArrivalGapIncreaseMs"
    Add-Assertion 'queue-wait-regression-budget' ($deltas.p99QueueWaitMs -le $MaxP99QueueWaitIncreaseMs) 'gate' "deltaMs=$($deltas.p99QueueWaitMs), maxMs=$MaxP99QueueWaitIncreaseMs"
    Add-Assertion 'apply-regression-budget' ($deltas.p99ApplyMs -le $MaxP99ApplyIncreaseMs) 'gate' "deltaMs=$($deltas.p99ApplyMs), maxMs=$MaxP99ApplyIncreaseMs"
    Add-Assertion 'unexplained-backward-regression-budget' ($deltas.maxUnexplainedBackwardMovement -le $MaxUnexplainedBackwardIncrease) 'gate' "delta=$($deltas.maxUnexplainedBackwardMovement), max=$MaxUnexplainedBackwardIncrease"
    Add-Assertion 'gc-regression-budget' ($deltas.averageGcBytesPerFrame -le $MaxAverageGcBytesIncrease) 'gate' "deltaBytes=$($deltas.averageGcBytesPerFrame), maxBytes=$MaxAverageGcBytesIncrease"
    Add-Assertion 'resync-regression-budget' ($deltas.resyncNeededCount -le $MaxResyncNeededIncrease) 'gate' "delta=$($deltas.resyncNeededCount), max=$MaxResyncNeededIncrease"
    Add-Assertion 'full-snapshot-regression-budget' ($deltas.fullSnapshotAppliedCount -le $MaxFullSnapshotIncrease) 'gate' "delta=$($deltas.fullSnapshotAppliedCount), max=$MaxFullSnapshotIncrease"
}

$contractFailures = @($assertions | Where-Object { $_.kind -eq 'contract' -and -not $_.passed })
$gateFailures = @($assertions | Where-Object { $_.kind -eq 'gate' -and -not $_.passed })
$summary = [pscustomobject]@{
    schemaVersion = 1
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    baselineTemplateId = $baselineTemplate
    candidateTemplateId = $candidateTemplate
    gateEnabled = $EnableGate.IsPresent
    contractPassed = $contractFailures.Count -eq 0
    gatePassed = -not $EnableGate.IsPresent -or $gateFailures.Count -eq 0
    baseline = $baseline
    candidate = $candidate
    delta = $deltas
    assertions = $assertions
    inputs = [pscustomobject]@{
        baselineOwner = [System.IO.Path]::GetFullPath($BaselineOwnerResultPath)
        baselineMember = [System.IO.Path]::GetFullPath($BaselineMemberResultPath)
        candidateOwner = [System.IO.Path]::GetFullPath($CandidateOwnerResultPath)
        candidateMember = [System.IO.Path]::GetFullPath($CandidateMemberResultPath)
    }
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path -Parent $resolvedOutput) -Force | Out-Null
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedOutput -Encoding utf8
Write-Host "Shooter PureState sample-block A/B report: $resolvedOutput"
Write-Host "Payload avg bytes: $($baseline.averagePayloadBytes) -> $($candidate.averagePayloadBytes) (x$payloadRatio)"
Write-Host "Starvation/held delta: $($deltas.starvationRatio) / $($deltas.heldRatio)"

if ($contractFailures.Count -gt 0) {
    throw "Shooter PureState A/B contract failed: $($contractFailures.name -join ', ')."
}
if ($EnableGate -and $gateFailures.Count -gt 0) {
    throw "Shooter PureState A/B gate failed: $($gateFailures.name -join ', ')."
}
