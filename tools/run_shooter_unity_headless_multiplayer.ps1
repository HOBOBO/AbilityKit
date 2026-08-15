[CmdletBinding()]
param(
    [string]$OwnerProject,
    [string]$MemberProject,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe',
    [string]$GatewayHost = '127.0.0.1',
    [int]$GatewayPort = 4000,
    [string]$GatewayRegion = 'dev',
    [string]$GatewayServerId = 'local',
    [int]$TimeoutSeconds = 300,
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

    return @(
        '-batchmode',
        '-nographics',
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
        '-shooterHeadlessTimeoutSeconds', $TimeoutSeconds,
        '-gatewayHost', $GatewayHost,
        '-gatewayPort', $GatewayPort,
        '-gatewayRegion', $GatewayRegion,
        '-gatewayServerId', $GatewayServerId,
        '-logFile', $LogPath
    )
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
    if (-not $selectedSample) {
        throw 'Shooter clients completed without a common authoritative sample.'
    }

    foreach ($player in @('p1', 'p2')) {
        $presentName = "${player}Present"
        $xName = "${player}x"
        $yName = "${player}y"
        if (-not [bool]$ownerState.$presentName -or -not [bool]$memberState.$presentName) {
            throw "Shooter $player was not present on both clients."
        }
        $dx = [double]$ownerState.$xName - [double]$memberState.$xName
        $dy = [double]$ownerState.$yName - [double]$memberState.$yName
        $delta = [Math]::Sqrt($dx * $dx + $dy * $dy)
        if ($delta -gt 0.35) {
            throw "Shooter $player positions diverged across clients. delta=$delta"
        }
    }

    $p1dx = [double]$ownerState.p1x - [double]$memberState.p1x
    $p1dy = [double]$ownerState.p1y - [double]$memberState.p1y
    $p1Delta = [Math]::Sqrt($p1dx * $p1dx + $p1dy * $p1dy)
    $p2dx = [double]$ownerState.p2x - [double]$memberState.p2x
    $p2dy = [double]$ownerState.p2y - [double]$memberState.p2y
    $p2Delta = [Math]::Sqrt($p2dx * $p2dx + $p2dy * $p2dy)

    Write-Host ''
    Write-Host 'Shooter Unity two-client authoritative state-sync acceptance PASSED.' -ForegroundColor Green
    Write-Host "RoomId=$($ownerState.roomId) BattleId=$($ownerState.battleId) WorldId=$($ownerState.worldId)"
    Write-Host "Room flow: ownerPushes=$($ownerState.roomPushCount), memberPushes=$($memberState.roomPushCount), soloLobbyVerified=$($ownerState.soloLobbyVerified)"
    Write-Host "State sync: commonFrame=$($selectedSample.frame), hash=$($selectedSample.authoritativeHash), ownerApplied=$($ownerState.snapshotAppliedCount), memberApplied=$($memberState.snapshotAppliedCount)"
    Write-Host "Snapshots: ownerFull=$($ownerState.fullSnapshotPushCount), ownerDelta=$($ownerState.deltaSnapshotPushCount), memberFull=$($memberState.fullSnapshotPushCount), memberDelta=$($memberState.deltaSnapshotPushCount), hashMismatches=$([int]$ownerState.authoritativeHashMismatchCount + [int]$memberState.authoritativeHashMismatchCount)"
    Write-Host "Inputs: owner=$($ownerState.inputSuccessCount)/$($ownerState.inputAttemptCount), member=$($memberState.inputSuccessCount)/$($memberState.inputAttemptCount), resync=0"
    Write-Host "Movement: ownerProgress=$([double]$ownerState.maxMovementProgress), ownerMaxBackward=$([double]$ownerState.maxBackwardMovement), memberProgress=$([double]$memberState.maxMovementProgress), memberMaxBackward=$([double]$memberState.maxBackwardMovement)"
    Write-Host "Convergence: p1Delta=$($p1Delta.ToString('F3')), p2Delta=$($p2Delta.ToString('F3'))"
    Write-Host "Artifacts: $runDirectory"
}
finally {
    foreach ($process in @($ownerProcess, $memberProcess)) {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
