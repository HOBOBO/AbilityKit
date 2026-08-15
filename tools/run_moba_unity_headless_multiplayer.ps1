[CmdletBinding()]
param(
    [string]$OwnerProject,
    [string]$MemberProject,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe',
    [string]$GatewayHost = '127.0.0.1',
    [int]$GatewayPort = 4000,
    [string]$GatewayRegion = 'dev',
    [string]$GatewayServerId = 'local',
    [int]$TimeoutSeconds = 240,
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
    $OutputRoot = Join-Path $PSScriptRoot '..\artifacts\moba-unity-headless'
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

$listener = Get-NetTCPConnection -State Listen -LocalPort $GatewayPort -ErrorAction SilentlyContinue
if (-not $listener) {
    throw "No Gateway listener was found on port $GatewayPort. Start the Orleans development services first."
}

$runId = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss-fff')
$runDirectory = Join-Path $OutputRoot $runId
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$roomPath = Join-Path $runDirectory 'room.json'
$movementSignalPath = Join-Path $runDirectory 'start-movement.signal'
$skillSignalPath = Join-Path $runDirectory 'start-skill.signal'
$ownerObservedSignalPath = Join-Path $runDirectory 'owner-observed.signal'
$memberObservedSignalPath = Join-Path $runDirectory 'member-observed.signal'
$ownerStatePath = Join-Path $runDirectory 'owner-state.json'
$memberStatePath = Join-Path $runDirectory 'member-state.json'
$ownerEventsPath = Join-Path $runDirectory 'owner-events.jsonl'
$memberEventsPath = Join-Path $runDirectory 'member-events.jsonl'
$ownerResultPath = Join-Path $runDirectory 'owner-result.json'
$memberResultPath = Join-Path $runDirectory 'member-result.json'
$ownerLogPath = Join-Path $runDirectory 'owner-unity.log'
$memberLogPath = Join-Path $runDirectory 'member-unity.log'
$ownerCompileLogPath = Join-Path $runDirectory 'owner-compile.log'
$memberCompileLogPath = Join-Path $runDirectory 'member-compile.log'
$ownerAccount = "moba-unity-owner-$runId"
$memberAccount = "moba-unity-member-$runId"
$executeMethod = 'AbilityKit.Game.Test.UnitTest.MobaMultiplayerHeadlessClientCommand.Run'

function New-ClientArguments {
    param(
        [string]$ProjectPath,
        [string]$Role,
        [string]$Account,
        [string]$StatePath,
        [string]$EventsPath,
        [string]$ResultPath,
        [string]$LogPath
    )

    return @(
        '-batchmode',
        '-nographics',
        '-projectPath', $ProjectPath,
        '-executeMethod', $executeMethod,
        '-mobaHeadlessRole', $Role,
        '-mobaHeadlessAccount', $Account,
        '-mobaHeadlessRoomPath', $roomPath,
        '-mobaHeadlessMovementSignal', $movementSignalPath,
        '-mobaHeadlessSkillSignal', $skillSignalPath,
        '-mobaHeadlessOwnerObservedSignal', $ownerObservedSignalPath,
        '-mobaHeadlessMemberObservedSignal', $memberObservedSignalPath,
        '-mobaHeadlessState', $StatePath,
        '-mobaHeadlessEvents', $EventsPath,
        '-mobaHeadlessResult', $ResultPath,
        '-mobaHeadlessTimeoutSeconds', $TimeoutSeconds,
        '-gatewayHost', $GatewayHost,
        '-gatewayPort', $GatewayPort,
        '-gatewayRegion', $GatewayRegion,
        '-gatewayServerId', $GatewayServerId,
        '-logFile', $LogPath
    )
}

function Read-ClientState {
    param([string]$Path)
    if (-not (Test-Path $Path -PathType Leaf)) { return $null }
    try {
        return Get-Content $Path -Raw | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Get-OwnerActor {
    param($State, [string]$OwnerAccountId)
    if (-not $State -or -not $State.actors) { return $null }
    return $State.actors | Where-Object { $_.accountId -eq $OwnerAccountId } | Select-Object -First 1
}

function Wait-UnityProjectAvailable {
    param(
        [string]$ProjectPath,
        [string]$Label
    )

    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $reportedOwners = ''
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $owners = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
            Where-Object {
                $_.CommandLine -and
                $_.CommandLine.IndexOf($ProjectPath, [StringComparison]::OrdinalIgnoreCase) -ge 0
            })
        if ($owners.Count -eq 0) {
            if ($reportedOwners) {
                Write-Host "Unity project became available for $Label. Project=$ProjectPath" -ForegroundColor DarkCyan
            }
            return
        }

        $ownerIds = ($owners | ForEach-Object { $_.ProcessId }) -join ','
        if ($ownerIds -ne $reportedOwners) {
            Write-Host "Waiting for Unity project $Label to become available. pid=$ownerIds, project=$ProjectPath" -ForegroundColor Yellow
            $reportedOwners = $ownerIds
        }
        Start-Sleep -Milliseconds 500
    }

    throw "Unity project remained in use for $Label after $TimeoutSeconds seconds. pid=$reportedOwners, project=$ProjectPath"
}

function Invoke-CompileWarmup {
    param(
        [string]$ProjectPath,
        [string]$LogPath,
        [string]$Label
    )

    for ($attempt = 1; $attempt -le $CompileWarmupAttempts; $attempt++) {
        $attemptLogPath = if ($attempt -eq 1) {
            $LogPath
        }
        else {
            $directory = Split-Path -Parent $LogPath
            $name = [System.IO.Path]::GetFileNameWithoutExtension($LogPath)
            $extension = [System.IO.Path]::GetExtension($LogPath)
            Join-Path $directory "$name-attempt$attempt$extension"
        }

        Stop-OrphanedUnityCompilerServers
        Write-Host "Warming Unity script compilation for $Label (attempt $attempt/$CompileWarmupAttempts). Project=$ProjectPath" -ForegroundColor DarkCyan
        $process = Start-Process -FilePath $UnityExe -ArgumentList @(
            '-batchmode',
            '-nographics',
            '-projectPath', $ProjectPath,
            '-quit',
            '-logFile', $attemptLogPath
        ) -PassThru -WindowStyle Hidden
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            Stop-OrphanedUnityCompilerServers
            throw "Unity compile warmup timed out for $Label after $TimeoutSeconds seconds."
        }
        $process.Refresh()
        if ($process.ExitCode -eq 0) {
            return
        }

        $logText = Read-WarmupLog -Path $attemptLogPath
        $hasCompilerErrors = $logText -match '(?m): error CS\d+:'
        $isSharingViolation = $logText.Contains('-1073741757')
        if ($hasCompilerErrors -or -not $isSharingViolation -or $attempt -eq $CompileWarmupAttempts) {
            throw "Unity compile warmup failed for $Label. exit=$($process.ExitCode), log=$attemptLogPath"
        }

        Write-Warning "Unity compiler sharing violation during $Label warmup; retrying with a fresh compiler server. log=$attemptLogPath"
    }
}

function Read-WarmupLog {
    param([string]$Path)

    $deadline = [DateTime]::UtcNow + [TimeSpan]::FromSeconds(10)
    do {
        try {
            if (Test-Path $Path -PathType Leaf) {
                return [System.IO.File]::ReadAllText($Path)
            }
        }
        catch [System.IO.IOException] {
            Start-Sleep -Milliseconds 200
            continue
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Unity warmup log was not readable after 10 seconds. log=$Path"
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
        Write-Host "Stopped orphaned Unity compiler server. pid=$($server.ProcessId)" -ForegroundColor DarkGray
    }
}

$ownerProcess = $null
$memberProcess = $null
try {
    if (-not $SkipCompileWarmup) {
        Wait-UnityProjectAvailable -ProjectPath $OwnerProject -Label 'owner warmup'
        Invoke-CompileWarmup -ProjectPath $OwnerProject -LogPath $ownerCompileLogPath -Label 'owner'
        Wait-UnityProjectAvailable -ProjectPath $MemberProject -Label 'member warmup'
        Invoke-CompileWarmup -ProjectPath $MemberProject -LogPath $memberCompileLogPath -Label 'member'
    }

    Wait-UnityProjectAvailable -ProjectPath $OwnerProject -Label 'owner client'
    Wait-UnityProjectAvailable -ProjectPath $MemberProject -Label 'member client'

    Write-Host "Starting Unity owner client. Project=$OwnerProject" -ForegroundColor Cyan
    $ownerProcess = Start-Process -FilePath $UnityExe -ArgumentList (New-ClientArguments `
        -ProjectPath $OwnerProject -Role 'owner' -Account $ownerAccount `
        -StatePath $ownerStatePath -EventsPath $ownerEventsPath `
        -ResultPath $ownerResultPath -LogPath $ownerLogPath) -PassThru -WindowStyle Hidden

    Write-Host "Starting Unity member client. Project=$MemberProject" -ForegroundColor Cyan
    $memberProcess = Start-Process -FilePath $UnityExe -ArgumentList (New-ClientArguments `
        -ProjectPath $MemberProject -Role 'member' -Account $memberAccount `
        -StatePath $memberStatePath -EventsPath $memberEventsPath `
        -ResultPath $memberResultPath -LogPath $memberLogPath) -PassThru -WindowStyle Hidden

    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $movementSignaled = $false
    $skillSignaled = $false
    $lastOwnerStage = ''
    $lastMemberStage = ''

    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $ownerProcess.Refresh()
        $memberProcess.Refresh()
        $ownerState = Read-ClientState $ownerStatePath
        $memberState = Read-ClientState $memberStatePath

        if ($ownerState -and $ownerState.stage -ne $lastOwnerStage) {
            $lastOwnerStage = $ownerState.stage
            Write-Host "Owner: $($ownerState.stage) - $($ownerState.detail)"
        }
        if ($memberState -and $memberState.stage -ne $lastMemberStage) {
            $lastMemberStage = $memberState.stage
            Write-Host "Member: $($memberState.stage) - $($memberState.detail)"
        }

        if (-not $movementSignaled -and
            $ownerState.stage -eq 'BattleReady' -and
            $memberState.stage -eq 'BattleReady') {
            New-Item -ItemType File -Path $movementSignalPath -Force | Out-Null
            $movementSignaled = $true
            Write-Host 'Both clients are battle-ready; movement synchronization probe started.' -ForegroundColor Cyan
        }

        if (-not $skillSignaled -and
            $ownerState.stage -eq 'SkillReady' -and
            $memberState.stage -eq 'SkillReady') {
            New-Item -ItemType File -Path $skillSignalPath -Force | Out-Null
            $skillSignaled = $true
            Write-Host 'Both clients completed movement validation; skill synchronization probe started.' -ForegroundColor Cyan
        }

        if ($ownerProcess.HasExited -and $memberProcess.HasExited) { break }
        if ($ownerProcess.HasExited -and $ownerProcess.ExitCode -ne 0) { break }
        if ($memberProcess.HasExited -and $memberProcess.ExitCode -ne 0) { break }
        Start-Sleep -Milliseconds 500
    }

    $ownerProcess.Refresh()
    $memberProcess.Refresh()
    $clientFailed = ($ownerProcess.HasExited -and $ownerProcess.ExitCode -ne 0) -or
                    ($memberProcess.HasExited -and $memberProcess.ExitCode -ne 0)
    if ($clientFailed) {
        foreach ($process in @($ownerProcess, $memberProcess)) {
            if ($process -and -not $process.HasExited) {
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
                $process.WaitForExit(10000) | Out-Null
                $process.Refresh()
            }
        }
    }
    if (-not $ownerProcess.HasExited -or -not $memberProcess.HasExited) {
        throw "Unity headless clients did not finish within $TimeoutSeconds seconds."
    }
    if ($ownerProcess.ExitCode -ne 0 -or $memberProcess.ExitCode -ne 0) {
        throw "Unity client failed. ownerExit=$($ownerProcess.ExitCode), memberExit=$($memberProcess.ExitCode)."
    }
    if (-not (Test-Path $ownerResultPath) -or -not (Test-Path $memberResultPath)) {
        throw 'One or both Unity result files are missing.'
    }

    $ownerResult = Get-Content $ownerResultPath -Raw | ConvertFrom-Json
    $memberResult = Get-Content $memberResultPath -Raw | ConvertFrom-Json
    if (-not $ownerResult.success -or -not $memberResult.success) {
        throw "Client assertion failed. owner='$($ownerResult.message)', member='$($memberResult.message)'."
    }

    $ownerState = $ownerResult.state
    $memberState = $memberResult.state
    if ($ownerState.roomId -ne $memberState.roomId -or
        $ownerState.battleId -ne $memberState.battleId -or
        $ownerState.worldId -ne $memberState.worldId) {
        throw 'Clients did not converge on the same room, battle, and world identifiers.'
    }
    if ($ownerState.playerCount -lt 2 -or $memberState.playerCount -lt 2) {
        throw 'Both clients must observe two authoritative room members.'
    }
    if (-not [bool]$ownerState.soloLobbyVerified) {
        throw 'Owner did not verify that the room stayed in Lobby before the second client joined.'
    }
    if (-not [bool]$ownerState.skillValidated -or -not [bool]$memberState.skillValidated) {
        throw 'Both clients must observe the owner skill synchronization probe.'
    }
    if ([long]$ownerState.roomPushCount -lt 1 -or [long]$memberState.roomPushCount -lt 1) {
        throw "Both clients must receive authoritative room pushes. owner=$($ownerState.roomPushCount), member=$($memberState.roomPushCount)"
    }
    if ([long]$ownerState.roomRefreshFallbackCount -ne 0 -or
        [long]$memberState.roomRefreshFallbackCount -ne 0) {
        throw "Room push sequence required a snapshot fallback. owner=$($ownerState.roomRefreshFallbackCount), member=$($memberState.roomRefreshFallbackCount)"
    }
    if ([int]$ownerState.skillSubmitAttemptCount -lt 1 -or
        [int]$ownerState.skillSubmitSuccessCount -lt 1) {
        throw "Owner skill input was not submitted successfully. attempts=$($ownerState.skillSubmitAttemptCount), successes=$($ownerState.skillSubmitSuccessCount)"
    }

    $ownerActorFromOwner = Get-OwnerActor $ownerState $ownerAccount
    $ownerActorFromMember = Get-OwnerActor $memberState $ownerAccount
    if (-not $ownerActorFromOwner -or -not $ownerActorFromMember -or
        -not $ownerActorFromOwner.hasPosition -or -not $ownerActorFromMember.hasPosition) {
        throw 'Owner actor position was not observable from both clients.'
    }

    $dx = [double]$ownerActorFromOwner.x - [double]$ownerActorFromMember.x
    $dy = [double]$ownerActorFromOwner.y - [double]$ownerActorFromMember.y
    $dz = [double]$ownerActorFromOwner.z - [double]$ownerActorFromMember.z
    $positionDelta = [Math]::Sqrt($dx * $dx + $dy * $dy + $dz * $dz)
    if ($positionDelta -gt 0.35) {
        throw "Final owner actor positions diverged across clients. delta=$positionDelta"
    }

    $runtimeDx = [double]$ownerActorFromOwner.runtimeX - [double]$ownerActorFromMember.runtimeX
    $runtimeDy = [double]$ownerActorFromOwner.runtimeY - [double]$ownerActorFromMember.runtimeY
    $runtimeDz = [double]$ownerActorFromOwner.runtimeZ - [double]$ownerActorFromMember.runtimeZ
    $runtimePositionDelta = [Math]::Sqrt(
        $runtimeDx * $runtimeDx +
        $runtimeDy * $runtimeDy +
        $runtimeDz * $runtimeDz)
    if ($runtimePositionDelta -gt 0.35) {
        throw "Predicted logic worlds diverged for the owner actor. delta=$runtimePositionDelta"
    }
    if ([long]$ownerState.predictionDroppedLocalInputBatches -ne 0 -or
        [long]$memberState.predictionDroppedLocalInputBatches -ne 0) {
        throw "Prediction dropped local input. owner=$($ownerState.predictionDroppedLocalInputBatches), member=$($memberState.predictionDroppedLocalInputBatches)"
    }
    if ([bool]$ownerState.predictionReplaying -or [bool]$memberState.predictionReplaying) {
        throw "Prediction replay did not settle. owner=$($ownerState.predictionReplaying), member=$($memberState.predictionReplaying)"
    }
    if ([long]$ownerState.predictionRollbackRestoreFailed -ne 0 -or
        [long]$memberState.predictionRollbackRestoreFailed -ne 0) {
        throw "Prediction rollback restore failed. owner=$($ownerState.predictionRollbackRestoreFailed), member=$($memberState.predictionRollbackRestoreFailed)"
    }
    if ([long]$ownerState.predictionReplayTimeoutCount -ne 0 -or
        [long]$memberState.predictionReplayTimeoutCount -ne 0) {
        throw "Prediction replay timed out. owner=$($ownerState.predictionReplayTimeoutCount), member=$($memberState.predictionReplayTimeoutCount)"
    }

    Write-Host ''
    Write-Host 'MOBA Unity two-client headless acceptance PASSED.' -ForegroundColor Green
    Write-Host "RoomId=$($ownerState.roomId) BattleId=$($ownerState.battleId) WorldId=$($ownerState.worldId)"
    Write-Host "Lobby gate: soloLobbyVerified=$($ownerState.soloLobbyVerified), finalPlayers=$($ownerState.playerCount)"
    Write-Host "Room push: owner=$($ownerState.roomPushAppliedCount)/$($ownerState.roomPushCount), member=$($memberState.roomPushAppliedCount)/$($memberState.roomPushCount), fallback=0"
    Write-Host "Frames: owner=$($ownerState.frame), member=$($memberState.frame), positionDelta=$($positionDelta.ToString('F3')), runtimeDelta=$($runtimePositionDelta.ToString('F3'))"
    Write-Host "Trajectory: ownerSamples=$($ownerState.movementSampleCount), ownerMaxBackward=$([double]$ownerState.maxBackwardMovement), memberSamples=$($memberState.movementSampleCount), memberMaxBackward=$([double]$memberState.maxBackwardMovement)"
    Write-Host "Skill: ownerDisplacement=$([double]$ownerState.maxSkillDisplacement), memberDisplacement=$([double]$memberState.maxSkillDisplacement), submits=$($ownerState.skillSubmitSuccessCount)/$($ownerState.skillSubmitAttemptCount)"
    Write-Host "Prediction: ownerRollbacks=$($ownerState.predictionRollbackCount), memberRollbacks=$($memberState.predictionRollbackCount), ownerMismatches=$($ownerState.predictionMismatchCount), memberMismatches=$($memberState.predictionMismatchCount), ownerDroppedLocal=$($ownerState.predictionDroppedLocalInputBatches), memberDroppedLocal=$($memberState.predictionDroppedLocalInputBatches), settled=$(-not $ownerState.predictionReplaying -and -not $memberState.predictionReplaying)"
    Write-Host "Artifacts: $runDirectory"
}
finally {
    foreach ($process in @($ownerProcess, $memberProcess)) {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
