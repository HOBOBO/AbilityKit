[CmdletBinding()]
param(
    [string]$OwnerProject,
    [string]$MemberProject,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe',
    [string]$GatewayHost = '127.0.0.1',
    [int]$GatewayPort = 4000,
    [string]$GatewayRegion = 'dev',
    [string]$GatewayServerId = 'local',
    [string]$SyncTemplateId = 'mass-battle-lod-aoi',
    [int]$SyncModel = 5,
    [ValidateSet('template', 'ideal', 'lan', 'mobile4g', 'crossregion', 'poorwifi', 'limitedbw')]
    [string]$NetworkEnvironmentId = 'template',
    [ValidateRange(1, 20000)]
    [int]$EnemyBudget = 512,
    [ValidateSet('gameobject', 'gpu')]
    [string]$ViewBackend = 'gameobject',
    [int]$TimeoutSeconds = 300,
    [ValidateRange(250, 10000)]
    [int]$SevereLatencyThresholdMs = 2000,
    [ValidateRange(1, 1000)]
    [double]$P95SyncFrameThresholdMs = 50,
    [ValidateRange(1, 1000)]
    [double]$P99PushApplyThresholdMs = 25,
    [ValidateRange(1, 5000)]
    [double]$P99QueueWaitThresholdMs = 500,
    [ValidateRange(1, 5000)]
    [double]$P95SnapshotSourceAgeThresholdMs = 1000,
    [ValidateRange(1, 256)]
    [int]$PeakQueueDepthThreshold = 8,
    [ValidateRange(0.001, 1.0)]
    [double]$MaxHitchRate = 0.10,
    [ValidateRange(0, 10485760)]
    [long]$AverageGcBytesPerFrameThreshold = 524288,
    [ValidateRange(1, 5)]
    [int]$CompileWarmupAttempts = 3,
    [string]$OutputRoot,
    [switch]$SkipCompileWarmup
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OwnerProject)) {
    $OwnerProject = Join-Path $PSScriptRoot '..\Unity'
}
if ([string]::IsNullOrWhiteSpace($MemberProject)) {
    $MemberProject = Join-Path $PSScriptRoot '..\..\Unity-Instance2'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $PSScriptRoot '..\artifacts\shooter-unity-headless'
}
$OwnerProject = (Resolve-Path $OwnerProject).Path
$MemberProject = (Resolve-Path $MemberProject).Path
$UnityExe = (Resolve-Path $UnityExe).Path
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)

if ($OwnerProject -eq $MemberProject) {
    throw 'OwnerProject and MemberProject must have independent Library directories.'
}
if (-not (Test-Path (Join-Path $OwnerProject 'Assets') -PathType Container) -or
    -not (Test-Path (Join-Path $MemberProject 'Assets') -PathType Container)) {
    throw 'Both Unity project paths must contain an Assets directory.'
}
if (-not (Get-NetTCPConnection -State Listen -LocalPort $GatewayPort -ErrorAction SilentlyContinue)) {
    throw "No Gateway listener was found on port $GatewayPort."
}

$usesPureStateAoi = $SyncTemplateId -eq 'mass-battle-lod-aoi' -or $SyncModel -eq 5
$networkEnvironmentArgument = if ($NetworkEnvironmentId -eq 'template') { '' } else { $NetworkEnvironmentId }
$runId = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss-fff')
$runDirectory = Join-Path $OutputRoot $runId
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
Write-Host "Run directory: $runDirectory" -ForegroundColor Cyan

$roomPath = Join-Path $runDirectory 'room.json'
$movementSignalPath = Join-Path $runDirectory 'start-movement.signal'
$finalizePath = Join-Path $runDirectory 'finalize.json'
$ownerStatePath = Join-Path $runDirectory 'owner-state.json'
$memberStatePath = Join-Path $runDirectory 'member-state.json'
$ownerResultPath = Join-Path $runDirectory 'owner-result.json'
$memberResultPath = Join-Path $runDirectory 'member-result.json'
$ownerLogPath = Join-Path $runDirectory 'owner-unity.log'
$memberLogPath = Join-Path $runDirectory 'member-unity.log'
$ownerCompileLogPath = Join-Path $runDirectory 'owner-compile.log'
$memberCompileLogPath = Join-Path $runDirectory 'member-compile.log'
$ownerAccount = "shooter-unity-owner-$runId"
$memberAccount = "shooter-unity-member-$runId"
$executeMethod = 'AbilityKit.Demo.Shooter.View.Editor.ShooterMultiplayerHeadlessClientCommand.Run'

function New-ClientArguments {
    param(
        [string]$ProjectPath,
        [string]$Role,
        [string]$Account,
        [string]$StatePath,
        [string]$ResultPath,
        [string]$LogPath
    )

    $arguments = @(
        '-batchmode',
        '-projectPath', $ProjectPath,
        '-executeMethod', $executeMethod,
        '-shooterHeadlessRole', $Role,
        '-shooterHeadlessAccount', $Account,
        '-shooterHeadlessRunId', $runId,
        '-shooterHeadlessRoomPath', $roomPath,
        '-shooterHeadlessMovementSignal', $movementSignalPath,
        '-shooterHeadlessFinalize', $finalizePath,
        '-shooterHeadlessState', $StatePath,
        '-shooterHeadlessResult', $ResultPath,
        '-shooterHeadlessSyncTemplate', $SyncTemplateId,
        '-shooterHeadlessSyncModel', $SyncModel,
        '-shooterHeadlessNetworkEnvironment', $networkEnvironmentArgument,
        '-shooterHeadlessEnemyBudget', $EnemyBudget,
        '-shooterHeadlessViewBackend', $ViewBackend,
        '-shooterHeadlessTimeoutSeconds', $TimeoutSeconds,
        '-gatewayHost', $GatewayHost,
        '-gatewayPort', $GatewayPort,
        '-gatewayRegion', $GatewayRegion,
        '-gatewayServerId', $GatewayServerId,
        '-logFile', $LogPath
    )
    if ($ViewBackend -eq 'gameobject') {
        $arguments = @('-nographics') + $arguments
    }
    return $arguments
}

function Read-JsonFile {
    param([string]$Path)
    if (-not (Test-Path $Path -PathType Leaf)) { return $null }
    try { return Get-Content $Path -Raw | ConvertFrom-Json }
    catch { return $null }
}

function Wait-UnityProjectAvailable {
    param([string]$ProjectPath, [string]$Label)
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $reportedOwners = ''
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $owners = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($ProjectPath, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
        if ($owners.Count -eq 0) { return }
        $ownerIds = ($owners | ForEach-Object { $_.ProcessId }) -join ','
        if ($ownerIds -ne $reportedOwners) {
            Write-Host "Waiting for Unity project $Label. pid=$ownerIds, project=$ProjectPath" -ForegroundColor Yellow
            $reportedOwners = $ownerIds
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Unity project remained in use for $Label after $TimeoutSeconds seconds. pid=$reportedOwners"
}

function Read-WarmupLog {
    param([string]$Path)
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        try {
            if (Test-Path $Path -PathType Leaf) { return [System.IO.File]::ReadAllText($Path) }
        }
        catch [System.IO.IOException] { }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Unity warmup log was not readable: $Path"
}

function Stop-OrphanedUnityCompilerServers {
    $servers = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -and
            $_.CommandLine -like '*Unity*VBCSCompiler.dll*' -and
            -not (Get-Process -Id $_.ParentProcessId -ErrorAction SilentlyContinue)
        })
    foreach ($server in $servers) {
        Stop-Process -Id $server.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-CompileWarmup {
    param([string]$ProjectPath, [string]$LogPath, [string]$Label)
    for ($attempt = 1; $attempt -le $CompileWarmupAttempts; $attempt++) {
        $attemptLogPath = if ($attempt -eq 1) { $LogPath } else {
            Join-Path (Split-Path -Parent $LogPath) "$([IO.Path]::GetFileNameWithoutExtension($LogPath))-attempt$attempt$([IO.Path]::GetExtension($LogPath))"
        }
        Stop-OrphanedUnityCompilerServers
        Write-Host "Warming Unity compilation for $Label ($attempt/$CompileWarmupAttempts)..." -ForegroundColor DarkCyan
        $process = Start-Process -FilePath $UnityExe -ArgumentList @(
            '-batchmode', '-nographics', '-projectPath', $ProjectPath, '-quit', '-logFile', $attemptLogPath
        ) -PassThru -WindowStyle Hidden
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            throw "Unity compile warmup timed out for $Label."
        }
        $process.Refresh()
        $logText = Read-WarmupLog $attemptLogPath
        $hasCompilerErrors = $logText -match '(?m): error CS\d+:'
        if ($process.ExitCode -eq 0 -and -not $hasCompilerErrors) { return }
        $isSharingViolation = $logText.Contains('-1073741757')
        if ($hasCompilerErrors -or -not $isSharingViolation -or $attempt -eq $CompileWarmupAttempts) {
            throw "Unity compile warmup failed for $Label. exit=$($process.ExitCode), log=$attemptLogPath"
        }
        Write-Warning "Unity compiler sharing violation during $Label warmup; retrying."
    }
}

function Find-CommonAuthoritativeSample {
    param($OwnerState, $MemberState)
    if (-not $OwnerState -or -not $MemberState -or -not $OwnerState.samples -or -not $MemberState.samples) { return $null }
    $memberByFrame = @{}
    foreach ($sample in @($MemberState.samples)) {
        $memberByFrame[[int]$sample.frame] = $sample
    }
    foreach ($sample in @($OwnerState.samples | Sort-Object { [int]$_.frame } -Descending)) {
        $frame = [int]$sample.frame
        if ($memberByFrame.ContainsKey($frame) -and
            [string]$memberByFrame[$frame].authoritativeHash -eq [string]$sample.authoritativeHash) {
            return $sample
        }
    }
    return $null
}

$ownerProcess = $null
$memberProcess = $null
$selectedSample = $null
try {
    if (-not $SkipCompileWarmup) {
        Wait-UnityProjectAvailable $OwnerProject 'owner warmup'
        Invoke-CompileWarmup $OwnerProject $ownerCompileLogPath 'owner'
        Wait-UnityProjectAvailable $MemberProject 'member warmup'
        Invoke-CompileWarmup $MemberProject $memberCompileLogPath 'member'
    }

    Wait-UnityProjectAvailable $OwnerProject 'owner client'
    Wait-UnityProjectAvailable $MemberProject 'member client'
    Write-Host 'Starting Shooter Unity owner...' -ForegroundColor Cyan
    $ownerProcess = Start-Process -FilePath $UnityExe -ArgumentList (New-ClientArguments `
        -ProjectPath $OwnerProject -Role owner -Account $ownerAccount `
        -StatePath $ownerStatePath -ResultPath $ownerResultPath -LogPath $ownerLogPath) -PassThru -WindowStyle Hidden
    Write-Host 'Starting Shooter Unity member...' -ForegroundColor Cyan
    $memberProcess = Start-Process -FilePath $UnityExe -ArgumentList (New-ClientArguments `
        -ProjectPath $MemberProject -Role member -Account $memberAccount `
        -StatePath $memberStatePath -ResultPath $memberResultPath -LogPath $memberLogPath) -PassThru -WindowStyle Hidden

    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $movementSignaled = $false
    $finalizeSignaled = $false
    $lastOwnerStage = ''
    $lastMemberStage = ''
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $ownerProcess.Refresh()
        $memberProcess.Refresh()
        $ownerState = Read-JsonFile $ownerStatePath
        $memberState = Read-JsonFile $memberStatePath
        if ($ownerState -and $ownerState.stage -ne $lastOwnerStage) {
            $lastOwnerStage = $ownerState.stage
            Write-Host "Owner: $($ownerState.stage) - $($ownerState.detail)"
        }
        if ($memberState -and $memberState.stage -ne $lastMemberStage) {
            $lastMemberStage = $memberState.stage
            Write-Host "Member: $($memberState.stage) - $($memberState.detail)"
        }

        if (-not $movementSignaled -and $ownerState.stage -eq 'BattleReady' -and $memberState.stage -eq 'BattleReady') {
            New-Item -ItemType File -Path $movementSignalPath -Force | Out-Null
            $movementSignaled = $true
            Write-Host 'Both Shooter clients are battle-ready; movement probe started.' -ForegroundColor Cyan
        }

        if (-not $finalizeSignaled -and $ownerState.stage -eq 'AwaitFinalize' -and $memberState.stage -eq 'AwaitFinalize') {
            if ($usesPureStateAoi) {
                $finalizeFrame = [Math]::Min([int]$ownerState.frame, [int]$memberState.frame)
                @{
                    frame = [Math]::Max(1, $finalizeFrame)
                    authoritativeHash = 'observer-specific-aoi'
                } | ConvertTo-Json | Set-Content -Path $finalizePath -Encoding UTF8
                $finalizeSignaled = $true
                Write-Host "Both observers completed AOI lifecycle evidence; finalizing at frame=$finalizeFrame." -ForegroundColor Cyan
            }
            else {
                $selectedSample = Find-CommonAuthoritativeSample $ownerState $memberState
                if ($selectedSample) {
                    @{
                        frame = [int]$selectedSample.frame
                        authoritativeHash = [string]$selectedSample.authoritativeHash
                    } | ConvertTo-Json | Set-Content -Path $finalizePath -Encoding UTF8
                    $finalizeSignaled = $true
                    Write-Host "Selected common authoritative frame=$($selectedSample.frame), hash=$($selectedSample.authoritativeHash)." -ForegroundColor Cyan
                }
            }
        }

        $ownerResultProbe = Read-JsonFile $ownerResultPath
        $memberResultProbe = Read-JsonFile $memberResultPath
        if ($ownerResultProbe -and $memberResultProbe) { break }
        if ($ownerProcess.HasExited -and $memberProcess.HasExited) { break }
        if (($ownerProcess.HasExited -and $ownerProcess.ExitCode -ne 0) -or
            ($memberProcess.HasExited -and $memberProcess.ExitCode -ne 0)) { break }
        Start-Sleep -Milliseconds 500
    }

    $ownerProcess.Refresh()
    $memberProcess.Refresh()
    $ownerResult = Read-JsonFile $ownerResultPath
    $memberResult = Read-JsonFile $memberResultPath
    if ((-not $ownerProcess.HasExited -or -not $memberProcess.HasExited) -and
        (-not $ownerResult -or -not $memberResult)) {
        throw "Shooter Unity clients did not finish within $TimeoutSeconds seconds."
    }
    if (($ownerProcess.HasExited -and $ownerProcess.ExitCode -ne 0) -or
        ($memberProcess.HasExited -and $memberProcess.ExitCode -ne 0)) {
        throw "Shooter Unity client failed. ownerExit=$($ownerProcess.ExitCode), memberExit=$($memberProcess.ExitCode)."
    }

    if (-not $ownerResult -or -not $memberResult -or -not $ownerResult.success -or -not $memberResult.success) {
        throw "Shooter client assertion failed. owner='$($ownerResult.message)', member='$($memberResult.message)'."
    }
    $ownerState = $ownerResult.state
    $memberState = $memberResult.state
    if ($ownerState.roomId -ne $memberState.roomId -or
        $ownerState.battleId -ne $memberState.battleId -or
        [string]$ownerState.worldId -ne [string]$memberState.worldId) {
        throw 'Shooter clients did not converge on the same room, battle, and world.'
    }
    if (-not $ownerState.soloLobbyVerified) {
        throw 'Shooter owner did not verify the one-player lobby gate.'
    }
    if ([string]$ownerState.syncTemplateId -ne $SyncTemplateId -or
        [string]$memberState.syncTemplateId -ne $SyncTemplateId -or
        [int]$ownerState.syncModel -ne $SyncModel -or
        [int]$memberState.syncModel -ne $SyncModel) {
        throw "Shooter clients used an unexpected sync configuration. expected=$SyncTemplateId/$SyncModel, owner=$($ownerState.syncTemplateId)/$($ownerState.syncModel), member=$($memberState.syncTemplateId)/$($memberState.syncModel)"
    }
    if ([string]$ownerState.battleHandoffMode -ne 'RestoreOnly' -or
        [string]$memberState.battleHandoffMode -ne 'RestoreOnly') {
        throw "Shooter clients did not use the formal RestoreOnly battle handoff. owner=$($ownerState.battleHandoffMode), member=$($memberState.battleHandoffMode)"
    }
    if (-not [bool]$ownerState.hostRunning -or -not [bool]$memberState.hostRunning -or
        [long]$ownerState.hostRenderCount -lt 1 -or [long]$memberState.hostRenderCount -lt 1) {
        throw "Shooter battle hosts did not remain active and render. ownerRunning=$($ownerState.hostRunning), ownerRenders=$($ownerState.hostRenderCount), memberRunning=$($memberState.hostRunning), memberRenders=$($memberState.hostRenderCount)"
    }
    $expectedViewBackend = if ($ViewBackend -eq 'gpu') { 'GpuInstancedDotsReady' } else { 'GameObject' }
    if ([string]$ownerState.viewBackend -ne $expectedViewBackend -or
        [string]$memberState.viewBackend -ne $expectedViewBackend) {
        throw "Shooter clients used an unexpected view backend. expected=$expectedViewBackend, owner=$($ownerState.viewBackend), member=$($memberState.viewBackend)"
    }
    foreach ($entry in @(@{ Label = 'owner'; State = $ownerState }, @{ Label = 'member'; State = $memberState })) {
        $state = $entry.State
        if ($ViewBackend -eq 'gpu') {
            if ([long]$state.viewFullRebuildCount -lt 1 -or [long]$state.viewIncrementalBatchCount -lt 1) {
                throw "Shooter $($entry.Label) GPU view did not exercise full and incremental projection paths. rebuilds=$($state.viewFullRebuildCount), incremental=$($state.viewIncrementalBatchCount)"
            }
            if ([bool]$state.viewUsesIndirectRendering -and
                ([long]$state.viewIndirectUploadPassCount -lt 1 -or
                 [long]$state.viewMatrixUploadCallCount -lt 1 -or
                 [long]$state.viewUploadedMatrixCount -lt 1)) {
                throw "Shooter $($entry.Label) indirect GPU view produced no matrix uploads. passes=$($state.viewIndirectUploadPassCount), calls=$($state.viewMatrixUploadCallCount), matrices=$($state.viewUploadedMatrixCount)"
            }
        }
        if ([double]$state.maxInputRoundTripMs -ge $SevereLatencyThresholdMs -or
            [double]$state.firstMovementResponseMs -lt 0 -or
            [double]$state.firstMovementResponseMs -ge $SevereLatencyThresholdMs -or
            [double]$state.maxEditorUpdateGapMs -ge $SevereLatencyThresholdMs -or
            [double]$state.maxSnapshotGapMs -ge $SevereLatencyThresholdMs) {
            throw "Shooter $($entry.Label) observed severe latency or a stall. thresholdMs=$SevereLatencyThresholdMs, inputMax=$($state.maxInputRoundTripMs), movementFirst=$($state.firstMovementResponseMs), editorGapMax=$($state.maxEditorUpdateGapMs), snapshotGapMax=$($state.maxSnapshotGapMs)"
        }
        $frameCount = [long]$state.syncFrameCount
        $hitchRate = if ($frameCount -gt 0) { [double]$state.syncHitchCount / $frameCount } else { 1.0 }
        if ($frameCount -le 0 -or
            [double]$state.p95SyncFrameMs -gt $P95SyncFrameThresholdMs -or
            [double]$state.p99BattlePushApplyMs -gt $P99PushApplyThresholdMs -or
            [double]$state.p99BattlePushQueueWaitMs -gt $P99QueueWaitThresholdMs -or
            [double]$state.p95SnapshotSourceAgeMs -gt $P95SnapshotSourceAgeThresholdMs -or
            [int]$state.battlePushPeakQueueDepth -gt $PeakQueueDepthThreshold -or
            $hitchRate -gt $MaxHitchRate -or
            [double]$state.averageGcBytesPerFrame -gt $AverageGcBytesPerFrameThreshold) {
            throw "Shooter $($entry.Label) exceeded sync performance budget. frameP95=$($state.p95SyncFrameMs)/$P95SyncFrameThresholdMs, applyP99=$($state.p99BattlePushApplyMs)/$P99PushApplyThresholdMs, queueWaitP99=$($state.p99BattlePushQueueWaitMs)/$P99QueueWaitThresholdMs, sourceAgeP95=$($state.p95SnapshotSourceAgeMs)/$P95SnapshotSourceAgeThresholdMs, peakQueue=$($state.battlePushPeakQueueDepth)/$PeakQueueDepthThreshold, hitchRate=$([Math]::Round($hitchRate, 4))/$MaxHitchRate, gcAvg=$([Math]::Round([double]$state.averageGcBytesPerFrame, 0))/$AverageGcBytesPerFrameThreshold"
        }
    }

    if ($usesPureStateAoi) {
        foreach ($entry in @(@{ Label = 'owner'; State = $ownerState }, @{ Label = 'member'; State = $memberState })) {
            $state = $entry.State
            if ([int]$state.aoiInitialPlayerViewCount -lt 2 -or [int]$state.aoiMaxPlayerViewCount -lt 2) {
                throw "Shooter $($entry.Label) did not observe local and remote player views inside AOI. initial=$($state.aoiInitialPlayerViewCount), max=$($state.aoiMaxPlayerViewCount)"
            }
            if (-not [bool]$state.remotePlayerViewObserved -or -not [bool]$state.remotePlayerViewRemoved -or
                [bool]$state.remotePlayerViewActive -or [int]$state.playerViewCount -ne 1) {
                throw "Shooter $($entry.Label) remote player GameObject lifecycle failed. observed=$($state.remotePlayerViewObserved), removed=$($state.remotePlayerViewRemoved), active=$($state.remotePlayerViewActive), finalPlayers=$($state.playerViewCount)"
            }
            if ([int]$state.remotePlayerSpawnFrame -le 0 -or [int]$state.remotePlayerDespawnFrame -le [int]$state.remotePlayerSpawnFrame) {
                throw "Shooter $($entry.Label) protocol lifecycle was incomplete. spawn=$($state.remotePlayerSpawnFrame), despawn=$($state.remotePlayerDespawnFrame)"
            }
            if ([int]$state.pureStateFullAppliedCount -lt 1 -or [int]$state.pureStateDeltaAppliedCount -lt 1 -or
                [int]$state.pureStateLowFrequencyUpdateCount -lt 1) {
                throw "Shooter $($entry.Label) pure-state/LOD evidence was incomplete. full=$($state.pureStateFullAppliedCount), delta=$($state.pureStateDeltaAppliedCount), lowFrequency=$($state.pureStateLowFrequencyUpdateCount)"
            }
            if ([double]$state.aoiVisibleRadius -ne 24 -or [double]$state.aoiBoundaryRadius -ne 30 -or
                [int]$state.nearLodIntervalFrames -ne 10 -or [int]$state.midLodIntervalFrames -ne 30 -or [int]$state.farLodIntervalFrames -ne 90) {
                throw "Shooter $($entry.Label) AOI/LOD settings were unexpected. radius=$($state.aoiVisibleRadius)/$($state.aoiBoundaryRadius), lod=$($state.nearLodIntervalFrames)/$($state.midLodIntervalFrames)/$($state.farLodIntervalFrames)"
            }
        }

        Write-Host ''
        Write-Host 'Shooter Unity two-observer AOI/LOD acceptance PASSED.' -ForegroundColor Green
        Write-Host "RoomId=$($ownerState.roomId) BattleId=$($ownerState.battleId) WorldId=$($ownerState.worldId)"
        Write-Host "AOI config: visible=$($ownerState.aoiVisibleRadius), boundary=$($ownerState.aoiBoundaryRadius), LOD=$($ownerState.nearLodIntervalFrames)/$($ownerState.midLodIntervalFrames)/$($ownerState.farLodIntervalFrames) frames"
        Write-Host "Owner lifecycle: views=$($ownerState.aoiInitialPlayerViewCount)->$($ownerState.aoiMaxPlayerViewCount)->$($ownerState.playerViewCount), remoteSpawn=$($ownerState.remotePlayerSpawnFrame), remoteDespawn=$($ownerState.remotePlayerDespawnFrame), viewLeave=$($ownerState.remotePlayerViewLeaveFrame), inactive=$(-not [bool]$ownerState.remotePlayerViewActive)"
        Write-Host "Member lifecycle: views=$($memberState.aoiInitialPlayerViewCount)->$($memberState.aoiMaxPlayerViewCount)->$($memberState.playerViewCount), remoteSpawn=$($memberState.remotePlayerSpawnFrame), remoteDespawn=$($memberState.remotePlayerDespawnFrame), viewLeave=$($memberState.remotePlayerViewLeaveFrame), inactive=$(-not [bool]$memberState.remotePlayerViewActive)"
        Write-Host "Pure-state: ownerFull/Delta=$($ownerState.pureStateFullAppliedCount)/$($ownerState.pureStateDeltaAppliedCount), memberFull/Delta=$($memberState.pureStateFullAppliedCount)/$($memberState.pureStateDeltaAppliedCount), lowFrequency=$($ownerState.pureStateLowFrequencyUpdateCount)/$($memberState.pureStateLowFrequencyUpdateCount)"
        Write-Host "Protocol lifecycle totals: ownerSpawn/Update/Despawn=$($ownerState.pureStateSpawnCount)/$($ownerState.pureStateUpdateCount)/$($ownerState.pureStateDespawnCount), memberSpawn/Update/Despawn=$($memberState.pureStateSpawnCount)/$($memberState.pureStateUpdateCount)/$($memberState.pureStateDespawnCount)"
        Write-Host "Inputs: owner=$($ownerState.inputSuccessCount)/$($ownerState.inputAttemptCount), member=$($memberState.inputSuccessCount)/$($memberState.inputAttemptCount), resync=0"
        Write-Host "Latency ms (avg/max input, first movement): owner=$([Math]::Round([double]$ownerState.averageInputRoundTripMs, 1))/$([Math]::Round([double]$ownerState.maxInputRoundTripMs, 1))/$([Math]::Round([double]$ownerState.firstMovementResponseMs, 1)), member=$([Math]::Round([double]$memberState.averageInputRoundTripMs, 1))/$([Math]::Round([double]$memberState.maxInputRoundTripMs, 1))/$([Math]::Round([double]$memberState.firstMovementResponseMs, 1))"
        Write-Host "Stall ms (max editor update/max snapshot gap): owner=$([Math]::Round([double]$ownerState.maxEditorUpdateGapMs, 1))/$([Math]::Round([double]$ownerState.maxSnapshotGapMs, 1)), member=$([Math]::Round([double]$memberState.maxEditorUpdateGapMs, 1))/$([Math]::Round([double]$memberState.maxSnapshotGapMs, 1)), severeThreshold=$SevereLatencyThresholdMs"
        Write-Host "Sync P95/P99 ms (frame/apply/queue): owner=$($ownerState.p95SyncFrameMs)/$($ownerState.p99BattlePushApplyMs)/$($ownerState.p99BattlePushQueueWaitMs), member=$($memberState.p95SyncFrameMs)/$($memberState.p99BattlePushApplyMs)/$($memberState.p99BattlePushQueueWaitMs)"
        Write-Host "Snapshot source-to-apply P95/P99 ms: owner=$($ownerState.p95SnapshotSourceAgeMs)/$($ownerState.p99SnapshotSourceAgeMs), member=$($memberState.p95SnapshotSourceAgeMs)/$($memberState.p99SnapshotSourceAgeMs)"
        Write-Host "Load/network: enemies=$EnemyBudget, network=$NetworkEnvironmentId, GC avg bytes/frame=$([Math]::Round([double]$ownerState.averageGcBytesPerFrame, 0))/$([Math]::Round([double]$memberState.averageGcBytesPerFrame, 0))"
        if ($ViewBackend -eq 'gpu') {
            Write-Host "GPU view owner/member: indirect=$($ownerState.viewUsesIndirectRendering)/$($memberState.viewUsesIndirectRendering), rebuild=$($ownerState.viewFullRebuildCount)/$($memberState.viewFullRebuildCount), incremental=$($ownerState.viewIncrementalBatchCount)/$($memberState.viewIncrementalBatchCount), uploadPass=$($ownerState.viewIndirectUploadPassCount)/$($memberState.viewIndirectUploadPassCount), matrices=$($ownerState.viewUploadedMatrixCount)/$($memberState.viewUploadedMatrixCount), full/ranges=$($ownerState.viewFullBufferUploadCount)/$($ownerState.viewPartialUploadRangeCount)|$($memberState.viewFullBufferUploadCount)/$($memberState.viewPartialUploadRangeCount)"
        }
        Write-Host "Artifacts: $runDirectory"
        return
    }

    if ([int]$ownerState.playerViewCount -lt 2 -or [int]$memberState.playerViewCount -lt 2 -or
        [int]$ownerState.enemyViewCount -lt 1 -or [int]$memberState.enemyViewCount -lt 1) {
        throw "Shooter Unity views were incomplete. ownerPlayers=$($ownerState.playerViewCount), ownerEnemies=$($ownerState.enemyViewCount), memberPlayers=$($memberState.playerViewCount), memberEnemies=$($memberState.enemyViewCount)"
    }
    if (-not $selectedSample) {
        throw 'Shooter clients completed without a common authoritative sample.'
    }

    $selectedFrame = [int]$selectedSample.frame
    $ownerSelectedSample = @($ownerState.samples) |
        Where-Object { [int]$_.frame -eq $selectedFrame } |
        Select-Object -First 1
    $memberSelectedSample = @($memberState.samples) |
        Where-Object { [int]$_.frame -eq $selectedFrame } |
        Select-Object -First 1
    if (-not $ownerSelectedSample -or -not $memberSelectedSample -or
        [string]$ownerSelectedSample.authoritativeHash -ne [string]$memberSelectedSample.authoritativeHash) {
        throw "Shooter clients did not retain the selected authoritative sample. frame=$selectedFrame"
    }

    $authoritativeDeltas = @{}
    foreach ($player in @('p1', 'p2')) {
        $presentName = "${player}Present"
        $xName = "${player}x"
        $yName = "${player}y"
        if (-not [bool]$ownerSelectedSample.$presentName -or
            -not [bool]$memberSelectedSample.$presentName) {
            throw "Shooter $player was not present in both authoritative samples."
        }
        $dx = [double]$ownerSelectedSample.$xName - [double]$memberSelectedSample.$xName
        $dy = [double]$ownerSelectedSample.$yName - [double]$memberSelectedSample.$yName
        $delta = [Math]::Sqrt($dx * $dx + $dy * $dy)
        $authoritativeDeltas[$player] = $delta
        if ($delta -gt 0.35) {
            throw "Shooter $player authoritative positions diverged across clients. frame=$selectedFrame, delta=$delta"
        }
    }

    Write-Host ''
    Write-Host 'Shooter Unity two-client authoritative state-sync acceptance PASSED.' -ForegroundColor Green
    Write-Host "RoomId=$($ownerState.roomId) BattleId=$($ownerState.battleId) WorldId=$($ownerState.worldId)"
    Write-Host "Room flow: ownerPushes=$($ownerState.roomPushCount), memberPushes=$($memberState.roomPushCount), soloLobbyVerified=$($ownerState.soloLobbyVerified)"
    Write-Host "GUI handoff: mode=RestoreOnly, ownerRunning=$($ownerState.hostRunning), memberRunning=$($memberState.hostRunning), ownerRenders=$($ownerState.hostRenderCount), memberRenders=$($memberState.hostRenderCount)"
    Write-Host "Unity views: ownerPlayers=$($ownerState.playerViewCount), ownerEnemies=$($ownerState.enemyViewCount), memberPlayers=$($memberState.playerViewCount), memberEnemies=$($memberState.enemyViewCount)"
    if ($ViewBackend -eq 'gpu') {
        Write-Host "GPU view owner/member: indirect=$($ownerState.viewUsesIndirectRendering)/$($memberState.viewUsesIndirectRendering), rebuild=$($ownerState.viewFullRebuildCount)/$($memberState.viewFullRebuildCount), incremental=$($ownerState.viewIncrementalBatchCount)/$($memberState.viewIncrementalBatchCount), uploadPass=$($ownerState.viewIndirectUploadPassCount)/$($memberState.viewIndirectUploadPassCount), matrices=$($ownerState.viewUploadedMatrixCount)/$($memberState.viewUploadedMatrixCount), full/ranges=$($ownerState.viewFullBufferUploadCount)/$($ownerState.viewPartialUploadRangeCount)|$($memberState.viewFullBufferUploadCount)/$($memberState.viewPartialUploadRangeCount)"
    }
    Write-Host "State sync: template=$SyncTemplateId, model=$SyncModel, commonFrame=$selectedFrame, hash=$($ownerSelectedSample.authoritativeHash), ownerApplied=$($ownerState.snapshotAppliedCount), memberApplied=$($memberState.snapshotAppliedCount)"
    Write-Host "Snapshots: ownerFull=$($ownerState.fullSnapshotPushCount), ownerDelta=$($ownerState.deltaSnapshotPushCount), memberFull=$($memberState.fullSnapshotPushCount), memberDelta=$($memberState.deltaSnapshotPushCount), hashMismatches=$([int]$ownerState.authoritativeHashMismatchCount + [int]$memberState.authoritativeHashMismatchCount)"
    Write-Host "Inputs: owner=$($ownerState.inputSuccessCount)/$($ownerState.inputAttemptCount), member=$($memberState.inputSuccessCount)/$($memberState.inputAttemptCount), resync=0"
    Write-Host "Movement: ownerProgress=$([double]$ownerState.maxMovementProgress), ownerMaxBackward=$([double]$ownerState.maxBackwardMovement), memberProgress=$([double]$memberState.maxMovementProgress), memberMaxBackward=$([double]$memberState.maxBackwardMovement)"
    Write-Host "Authoritative convergence: p1Delta=$($authoritativeDeltas.p1.ToString('F3')), p2Delta=$($authoritativeDeltas.p2.ToString('F3'))"
    Write-Host "Runtime diagnostics: ownerP1=($($ownerState.p1x),$($ownerState.p1y)), memberP1=($($memberState.p1x),$($memberState.p1y)), ownerP2=($($ownerState.p2x),$($ownerState.p2y)), memberP2=($($memberState.p2x),$($memberState.p2y))"
    Write-Host "Artifacts: $runDirectory"
}
finally {
    foreach ($process in @($ownerProcess, $memberProcess)) {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
