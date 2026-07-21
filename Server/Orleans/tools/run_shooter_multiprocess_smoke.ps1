param(
    [switch]$NoBuild,
    [string]$Configuration = 'Debug',
    [int]$TcpPort = 41001,
    [int]$SiloPort = 12111,
    [int]$OrleansGatewayPort = 31001,
    [string]$ArtifactRoot = 'artifacts\shooter_multiprocess_smoke',
    [string]$RunId = '',
    [string]$ReplayExtension = '.record.bin',
    [int]$JoinClients = 1,
    [int]$Inputs = 3,
    [int]$Seed = 20260610,
    [int]$TimeoutSeconds = 30,
    [int]$StartupTimeoutSeconds = 60,
    [int]$SetupTimeoutSeconds = 60,
    [switch]$WaitForMatchEnd,
    [switch]$ReconnectJoinClient,
    [int]$ReconnectCount = 0,
    [int]$ReconnectDelayMs = 500,
    [int]$RecoverableFailureCount = 0,
    [int]$RetryBackoffMaxMs = 2000,
    [ValidateSet('minimal', 'full', 'compatibility', 'soak', 'custom')]
    [string]$Profile = 'minimal',
    [ValidateSet('', 'slow-consumer', 'gateway-offline', 'recoverable-retry', 'reconnect-cycles', 'observer-reactivation', 'soak-16', 'soak-64')]
    [string]$Scenario = '',
    [int]$ScenarioTimeoutSeconds = 45,
    [int]$GlobalTimeoutSeconds = 0,
    [int]$SoakDurationSeconds = 1800,
    [int]$SoakMetricsSampleIntervalMs = 1000,
    [int]$SoakResourceSampleIntervalSeconds = 5,
    [double]$SoakRecoveryP95LimitMs = 15000,
    [double]$SoakRecoveryP99LimitMs = 30000,
    [double]$SoakObserverSentBytesFairnessMin = 0.70,
    [double]$SoakMemoryGrowthLimitMb = 512,
    [switch]$PlanOnly,
    [int]$ConditionLatencyMs = 0,
    [int]$ConditionJitterMs = 0,
    [double]$ConditionPacketLossRate = 0,
    [int]$ConditionSeed = 20260610,
    [switch]$NoReplay,
    [switch]$NoCleanup,
    [switch]$OwnershipCleanupProbe,
    [ValidateSet('packed', 'pure-state')]
    [string]$PayloadMode = 'packed'
)

$ErrorActionPreference = 'Stop'

if ($TimeoutSeconds -le 0) {
    throw 'TimeoutSeconds must be > 0.'
}
if ($StartupTimeoutSeconds -le 0) {
    throw 'StartupTimeoutSeconds must be > 0.'
}
if ($SetupTimeoutSeconds -le 0) {
    throw 'SetupTimeoutSeconds must be > 0.'
}
if ($ScenarioTimeoutSeconds -le 5) {
    throw 'ScenarioTimeoutSeconds must be > 5 to reserve convergence time.'
}
if ($GlobalTimeoutSeconds -lt 0) {
    throw 'GlobalTimeoutSeconds must be >= 0; use 0 for an automatically derived matrix budget.'
}
if ($SoakDurationSeconds -lt 30) {
    throw 'SoakDurationSeconds must be >= 30.'
}
if ($SoakMetricsSampleIntervalMs -lt 100) {
    throw 'SoakMetricsSampleIntervalMs must be >= 100.'
}
if ($SoakResourceSampleIntervalSeconds -lt 1) {
    throw 'SoakResourceSampleIntervalSeconds must be >= 1.'
}
if ($SoakRecoveryP95LimitMs -le 0 -or $SoakRecoveryP99LimitMs -lt $SoakRecoveryP95LimitMs) {
    throw 'Soak recovery percentile limits must be positive and P99 must be >= P95.'
}
if ($SoakObserverSentBytesFairnessMin -le 0 -or $SoakObserverSentBytesFairnessMin -gt 1) {
    throw 'SoakObserverSentBytesFairnessMin must be in (0, 1].'
}
if ($SoakMemoryGrowthLimitMb -le 0) {
    throw 'SoakMemoryGrowthLimitMb must be > 0.'
}

. (Join-Path $PSScriptRoot 'abilitykit_process_utils.ps1')

function Get-ShooterFaultMatrixPlan {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('minimal', 'full', 'compatibility', 'soak', 'custom')]
        [string]$SelectedProfile,
        [string]$SelectedScenario,
        [string]$SelectedPayloadMode,
        [int]$SelectedJoinClients,
        [int]$SelectedConditionLatencyMs,
        [int]$SelectedConditionJitterMs,
        [double]$SelectedConditionPacketLossRate,
        [int]$BaseTcpPort,
        [int]$BaseSiloPort,
        [int]$BaseOrleansGatewayPort,
        [int]$StartupTimeoutSeconds,
        [int]$SetupTimeoutSeconds,
        [int]$PerScenarioTimeoutSeconds,
        [int]$SelectedSoakDurationSeconds
    )

    $cases = if (-not [string]::IsNullOrWhiteSpace($SelectedScenario)) {
        @([pscustomobject]@{
            caseId = [string]$SelectedScenario
            name = [string]$SelectedScenario
            payloadMode = $SelectedPayloadMode
            joinClients = $SelectedJoinClients
            conditionLatencyMs = $SelectedConditionLatencyMs
            conditionJitterMs = $SelectedConditionJitterMs
            conditionPacketLossRate = $SelectedConditionPacketLossRate
        })
    }
    elseif ($SelectedProfile -eq 'compatibility') {
        @(
            [pscustomobject]@{ caseId = 'packed-recoverable-single'; name = 'recoverable-retry'; payloadMode = 'packed'; joinClients = 1; conditionLatencyMs = 0; conditionJitterMs = 0; conditionPacketLossRate = 0 },
            [pscustomobject]@{ caseId = 'pure-state-slow-consumer-fanout'; name = 'slow-consumer'; payloadMode = 'pure-state'; joinClients = 2; conditionLatencyMs = 0; conditionJitterMs = 0; conditionPacketLossRate = 0 },
            [pscustomobject]@{ caseId = 'packed-gateway-offline-fanout'; name = 'gateway-offline'; payloadMode = 'packed'; joinClients = 2; conditionLatencyMs = 0; conditionJitterMs = 0; conditionPacketLossRate = 0 },
            [pscustomobject]@{ caseId = 'pure-state-reconnect-cycles-fanout'; name = 'reconnect-cycles'; payloadMode = 'pure-state'; joinClients = 2; conditionLatencyMs = 20; conditionJitterMs = 5; conditionPacketLossRate = 0 },
            [pscustomobject]@{ caseId = 'pure-state-observer-reactivation'; name = 'observer-reactivation'; payloadMode = 'pure-state'; joinClients = 1; conditionLatencyMs = 0; conditionJitterMs = 0; conditionPacketLossRate = 0 }
        )
    }
    elseif ($SelectedProfile -eq 'soak') {
        @(
            [pscustomobject]@{ caseId = 'soak-16'; name = 'soak-16'; payloadMode = 'pure-state'; joinClients = 15; conditionLatencyMs = 0; conditionJitterMs = 0; conditionPacketLossRate = 0 },
            [pscustomobject]@{ caseId = 'soak-64'; name = 'soak-64'; payloadMode = 'pure-state'; joinClients = 63; conditionLatencyMs = 0; conditionJitterMs = 0; conditionPacketLossRate = 0 }
        )
    }
    else {
        [string[]]$names = if ($SelectedProfile -eq 'full') {
            @('slow-consumer', 'gateway-offline', 'recoverable-retry', 'reconnect-cycles', 'observer-reactivation')
        }
        else {
            @('recoverable-retry')
        }
        @($names | ForEach-Object {
            [pscustomobject]@{
                caseId = $_
                name = $_
                payloadMode = if ($_ -eq 'slow-consumer') { 'pure-state' } else { $SelectedPayloadMode }
                joinClients = $SelectedJoinClients
                conditionLatencyMs = $SelectedConditionLatencyMs
                conditionJitterMs = $SelectedConditionJitterMs
                conditionPacketLossRate = $SelectedConditionPacketLossRate
            }
        })
    }

    $cases = @($cases)
    $plans = @()
    for ($i = 0; $i -lt $cases.Count; $i++) {
        $case = $cases[$i]
        $name = [string]$case.name
        $isSoak = $name -eq 'soak-16' -or $name -eq 'soak-64'
        $activeTimeoutSeconds = if ($isSoak) { $SelectedSoakDurationSeconds } else { $PerScenarioTimeoutSeconds }
        $convergenceTimeoutSeconds = [Math]::Max(5, [Math]::Min(30, $activeTimeoutSeconds - 5))
        $resultTimeoutSeconds = if ($isSoak) { 30 } else { 0 }
        $executionTimeoutSeconds = $StartupTimeoutSeconds + $SetupTimeoutSeconds + $activeTimeoutSeconds + 15
        if ($isSoak) {
            $executionTimeoutSeconds += $resultTimeoutSeconds + $convergenceTimeoutSeconds
        }
        $plans += [pscustomobject][ordered]@{
            caseId = [string]$case.caseId
            name = $name
            payloadMode = [string]$case.payloadMode
            joinClients = [int]$case.joinClients
            conditionLatencyMs = [int]$case.conditionLatencyMs
            conditionJitterMs = [int]$case.conditionJitterMs
            conditionPacketLossRate = [double]$case.conditionPacketLossRate
            offsetSeconds = $i * $executionTimeoutSeconds
            startupTimeoutSeconds = $StartupTimeoutSeconds
            setupTimeoutSeconds = $SetupTimeoutSeconds
            timeoutSeconds = $activeTimeoutSeconds
            resultTimeoutSeconds = $resultTimeoutSeconds
            executionTimeoutSeconds = $executionTimeoutSeconds
            tcpPort = $BaseTcpPort + ($i * 10)
            siloPort = $BaseSiloPort + ($i * 10)
            orleansGatewayPort = $BaseOrleansGatewayPort + ($i * 10)
            reconnectCount = if ($name -eq 'reconnect-cycles') { 3 } elseif ($name -eq 'slow-consumer' -or $isSoak) { 0 } else { 1 }
            recoverableFailureCount = if ($name -eq 'recoverable-retry') { 3 } else { 0 }
            gatewayOffline = $name -eq 'gateway-offline'
            observerReactivation = $name -eq 'observer-reactivation'
            slowConsumer = $name -eq 'slow-consumer'
            soak = $isSoak
            observerCount = [int]$case.joinClients + 1
            convergenceTimeoutSeconds = $convergenceTimeoutSeconds
        }
    }

    return @($plans)
}

$matrixPlan = @(Get-ShooterFaultMatrixPlan `
    -SelectedProfile $Profile `
    -SelectedScenario $Scenario `
    -SelectedPayloadMode $PayloadMode `
    -SelectedJoinClients $JoinClients `
    -SelectedConditionLatencyMs $ConditionLatencyMs `
    -SelectedConditionJitterMs $ConditionJitterMs `
    -SelectedConditionPacketLossRate $ConditionPacketLossRate `
    -BaseTcpPort $TcpPort `
    -BaseSiloPort $SiloPort `
    -BaseOrleansGatewayPort $OrleansGatewayPort `
    -StartupTimeoutSeconds $StartupTimeoutSeconds `
    -SetupTimeoutSeconds $SetupTimeoutSeconds `
    -PerScenarioTimeoutSeconds $ScenarioTimeoutSeconds `
    -SelectedSoakDurationSeconds $SoakDurationSeconds)
$effectiveGlobalTimeoutSeconds = if ($GlobalTimeoutSeconds -gt 0) {
    $GlobalTimeoutSeconds
}
else {
    [int](($matrixPlan | Measure-Object -Property executionTimeoutSeconds -Sum).Sum)
}

if ($PlanOnly) {
    [ordered]@{
        schemaVersion = 3
        profile = $Profile
        globalTimeoutSeconds = $effectiveGlobalTimeoutSeconds
        globalTimeoutIsAutomatic = $GlobalTimeoutSeconds -le 0
        scenarios = $matrixPlan
    } | ConvertTo-Json -Depth 6
    return
}

function Read-JsonFileIfPresent {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        Write-Warning "Could not read JSON file '$Path': $($_.Exception.Message)"
        return $null
    }
}

function Invoke-MatrixTimeoutOwnedCleanup {
    param(
        [Parameter(Mandatory = $true)][string]$ChildManifestPath,
        [Parameter(Mandatory = $true)][object]$ChildProcessIdentity,
        [Parameter(Mandatory = $true)][int[]]$Ports
    )

    $manifestSnapshot = Read-JsonFileIfPresent -Path $ChildManifestPath
    $candidates = @()
    if ($null -ne $manifestSnapshot -and $manifestSnapshot.PSObject.Properties['processes']) {
        $candidates = @($manifestSnapshot.processes)
    }

    $orchestratorCandidate = @($candidates | Where-Object { [string]$_.role -eq 'orchestrator' } | Select-Object -First 1)
    $orchestratorIdentity = if ($orchestratorCandidate.Count -gt 0) { $orchestratorCandidate[0] } else { $ChildProcessIdentity }
    $processResults = @(
        Stop-AbilityKitOwnedProcess -ExpectedIdentity $orchestratorIdentity -Role 'orchestrator'
    )

    foreach ($candidate in @($candidates | Where-Object { [string]$_.role -ne 'orchestrator' })) {
        $processResults += Stop-AbilityKitOwnedProcess -ExpectedIdentity $candidate -Role ([string]$candidate.role)
    }

    $unsafeIdentityResults = @($processResults | Where-Object { $_.status -in @('identity-mismatch', 'termination-failed') })
    if ($unsafeIdentityResults.Count -eq 0) {
        Stop-AbilityKitServices -Ports $Ports -GraceSeconds 1
    }
    else {
        Write-Warning 'Skipping port-owner termination because at least one manifest-owned process could not be safely identified or terminated.'
    }
    $portResults = @($Ports | ForEach-Object {
        [ordered]@{
            port = $_
            released = -not (Test-AbilityKitTcpPort -HostName '127.0.0.1' -Port $_ -TimeoutMilliseconds 250)
        }
    })

    return [pscustomobject][ordered]@{
        startedAtUtc = [DateTime]::UtcNow.ToString('O')
        source = 'running-manifest'
        candidatesObserved = $candidates.Count
        processes = @($processResults)
        ports = @($portResults)
        completedAtUtc = [DateTime]::UtcNow.ToString('O')
    }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir '..\..\..')
$project = Join-Path $repoRoot 'Server\Orleans\src\AbilityKit.Orleans.ShooterSmoke\AbilityKit.Orleans.ShooterSmoke.csproj'
$projectDirectory = Split-Path -Parent $project
$applicationDll = Join-Path $projectDirectory "bin\$Configuration\net10.0\AbilityKit.Orleans.ShooterSmoke.dll"
if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = '{0:yyyyMMdd-HHmmss-fff}-{1}' -f [DateTime]::UtcNow, $PID
}

if ([string]::IsNullOrWhiteSpace($Scenario)) {
    $matrixRoot = if ([System.IO.Path]::IsPathRooted($ArtifactRoot)) {
        [System.IO.Path]::GetFullPath($ArtifactRoot)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactRoot))
    }
    New-Item -ItemType Directory -Force -Path $matrixRoot | Out-Null
    $matrixResults = @()
    $matrixFailure = $null
    $matrixCleanupEvidence = @()
    if (-not $NoBuild) {
        Write-Host 'Building Shooter smoke project once for the fault matrix...' -ForegroundColor Cyan
        dotnet build $project -c $Configuration '-p:UseSharedCompilation=false' '-p:nodeReuse=false'
        if ($LASTEXITCODE -ne 0) {
            throw "Shooter smoke project build failed with exit code $LASTEXITCODE."
        }
    }

    $matrixStartedAtUtc = [DateTime]::UtcNow
    $matrixDeadlineUtc = $matrixStartedAtUtc.AddSeconds($effectiveGlobalTimeoutSeconds)
    foreach ($plan in $matrixPlan) {
        $remainingGlobalSeconds = [int][Math]::Floor(($matrixDeadlineUtc - [DateTime]::UtcNow).TotalSeconds)
        if ($remainingGlobalSeconds -le 0) {
            $matrixFailure = "Shooter fault matrix exceeded global timeout of $effectiveGlobalTimeoutSeconds seconds before case '$($plan.caseId)' started."
            break
        }

        $childRunId = "$RunId-$($plan.caseId)"
        $childManifestPath = Join-Path (Join-Path $matrixRoot $childRunId) 'manifest.json'
        $childArguments = @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $MyInvocation.MyCommand.Path,
            '-NoBuild', '-Configuration', $Configuration,
            '-TcpPort', $plan.tcpPort, '-SiloPort', $plan.siloPort,
            '-OrleansGatewayPort', $plan.orleansGatewayPort,
            '-ArtifactRoot', $ArtifactRoot, '-RunId', $childRunId,
            '-ReplayExtension', $ReplayExtension, '-JoinClients', $plan.joinClients,
            '-Inputs', $Inputs, '-Seed', $Seed,
            '-TimeoutSeconds', $TimeoutSeconds,
            '-StartupTimeoutSeconds', $StartupTimeoutSeconds,
            '-SetupTimeoutSeconds', $SetupTimeoutSeconds,
            '-ScenarioTimeoutSeconds', $ScenarioTimeoutSeconds,
            '-GlobalTimeoutSeconds', $effectiveGlobalTimeoutSeconds,
            '-SoakDurationSeconds', $SoakDurationSeconds,
            '-SoakMetricsSampleIntervalMs', $SoakMetricsSampleIntervalMs,
            '-SoakResourceSampleIntervalSeconds', $SoakResourceSampleIntervalSeconds,
            '-SoakRecoveryP95LimitMs', $SoakRecoveryP95LimitMs,
            '-SoakRecoveryP99LimitMs', $SoakRecoveryP99LimitMs,
            '-SoakObserverSentBytesFairnessMin', $SoakObserverSentBytesFairnessMin,
            '-SoakMemoryGrowthLimitMb', $SoakMemoryGrowthLimitMb,
            '-Profile', 'custom', '-Scenario', $plan.name,
            '-ReconnectDelayMs', $ReconnectDelayMs,
            '-RetryBackoffMaxMs', $RetryBackoffMaxMs,
            '-ConditionLatencyMs', $plan.conditionLatencyMs,
            '-ConditionJitterMs', $plan.conditionJitterMs,
            '-ConditionPacketLossRate', $plan.conditionPacketLossRate,
            '-ConditionSeed', $ConditionSeed,
            '-PayloadMode', $plan.payloadMode)
        if ($NoReplay) { $childArguments += '-NoReplay' }
        if ($NoCleanup) { $childArguments += '-NoCleanup' }
        if ($WaitForMatchEnd) { $childArguments += '-WaitForMatchEnd' }
        if ($OwnershipCleanupProbe) { $childArguments += '-OwnershipCleanupProbe' }

        $childStartedAtUtc = [DateTime]::UtcNow
        $child = Start-Process -FilePath 'powershell.exe' -ArgumentList $childArguments -PassThru -NoNewWindow
        $childProcessIdentity = Get-AbilityKitProcessIdentity -ProcessId $child.Id
        if ($null -eq $childProcessIdentity) {
            throw "Could not capture the child orchestrator identity for PID $($child.Id)."
        }
        $childTimeoutSeconds = [Math]::Min($remainingGlobalSeconds, $plan.executionTimeoutSeconds)
        $childTimedOut = -not $child.WaitForExit($childTimeoutSeconds * 1000)
        $cleanupEvidence = $null
        if ($childTimedOut) {
            $matrixFailure = "Shooter fault matrix case '$($plan.caseId)' exceeded its bounded execution timeout of $childTimeoutSeconds seconds."
            $cleanupEvidence = Invoke-MatrixTimeoutOwnedCleanup `
                -ChildManifestPath $childManifestPath `
                -ChildProcessIdentity $childProcessIdentity `
                -Ports @($plan.tcpPort, $plan.siloPort, $plan.orleansGatewayPort)
            $matrixCleanupEvidence += $cleanupEvidence
            $null = $child.WaitForExit(5000)
        }
        $child.Refresh()
        $childExitCode = if ($childTimedOut) { -1 } else { [int]$child.ExitCode }
        $childManifest = Read-JsonFileIfPresent -Path $childManifestPath
        if ($childTimedOut -and $null -ne $childManifest) {
            $childManifest.status = 'failed'
            $childManifest.completedAtUtc = [DateTime]::UtcNow.ToString('O')
            $childManifest.error = $matrixFailure
            $childManifest.failure = [pscustomobject][ordered]@{
                category = 'FaultRecoveryFailed'
                stage = 'matrix-timeout'
                message = $matrixFailure
            }
            $childManifest | Add-Member -NotePropertyName cleanupEvidence -NotePropertyValue $cleanupEvidence -Force
            $childManifest.assertions = @($childManifest.assertions) + [pscustomobject][ordered]@{
                name = 'scenario-completed'
                passed = $false
                details = $matrixFailure
            }
            $childManifestTemporaryPath = "$childManifestPath.tmp"
            $childManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $childManifestTemporaryPath -Encoding utf8
            Move-Item -LiteralPath $childManifestTemporaryPath -Destination $childManifestPath -Force
        }
        $childManifestStatus = if ($null -eq $childManifest) { 'missing' } else { [string]$childManifest.status }
        $matrixResults += [ordered]@{
            caseId = $plan.caseId
            scenario = $plan.name
            payloadMode = $plan.payloadMode
            joinClients = $plan.joinClients
            runId = $childRunId
            processId = $child.Id
            startedAtUtc = $childStartedAtUtc.ToString('O')
            completedAtUtc = [DateTime]::UtcNow.ToString('O')
            exitCode = $childExitCode
            manifestPath = "$childRunId/manifest.json"
            manifestStatus = $childManifestStatus
            cleanupEvidence = $cleanupEvidence
        }
        if ($childExitCode -ne 0 -or $childManifestStatus -ne 'passed') {
            if ($null -eq $matrixFailure) {
                $matrixFailure = "Shooter fault matrix case '$($plan.caseId)' failed. ExitCode=$childExitCode, ManifestStatus=$childManifestStatus."
            }
            break
        }
    }

    $matrixManifestPath = Join-Path $matrixRoot "$RunId-matrix.json"
    [ordered]@{
        schemaVersion = 3
        runId = $RunId
        profile = $Profile
        status = if ($null -eq $matrixFailure) { 'passed' } else { 'failed' }
        error = $matrixFailure
        startedAtUtc = $matrixStartedAtUtc.ToString('O')
        completedAtUtc = [DateTime]::UtcNow.ToString('O')
        scenarios = $matrixResults
        cleanupEvidence = @($matrixCleanupEvidence)
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $matrixManifestPath -Encoding utf8
    if ($null -ne $matrixFailure) {
        [Console]::Error.WriteLine("$matrixFailure Manifest=$matrixManifestPath")
        exit 1
    }
    Write-Host "Shooter fault matrix passed. Manifest=$matrixManifestPath" -ForegroundColor Green
    return
}

$activePlan = $matrixPlan[0]
$isSoakRun = [bool]$activePlan.soak
if (-not [string]::IsNullOrWhiteSpace($Scenario)) {
    $ReconnectCount = if ($ReconnectJoinClient -and $Profile -eq 'custom') { [Math]::Max(1, $ReconnectCount) } else { $activePlan.reconnectCount }
    $RecoverableFailureCount = $activePlan.recoverableFailureCount
    if ($Scenario -eq 'slow-consumer') {
        $PayloadMode = 'pure-state'
    }
}
if ($RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
    throw 'RunId must start with an alphanumeric character and contain only alphanumeric characters, dot, underscore, or hyphen.'
}

$artifactRootPath = if ([System.IO.Path]::IsPathRooted($ArtifactRoot)) {
    [System.IO.Path]::GetFullPath($ArtifactRoot)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactRoot))
}
$logDir = Join-Path $artifactRootPath $RunId
if (Test-Path $logDir) {
    throw "Smoke run directory already exists: $logDir"
}
$serverLog = Join-Path $logDir 'server.log'
$replayDir = Join-Path $logDir 'records'
$diagnosticDir = Join-Path $logDir 'diagnostics'
$soakControlDir = Join-Path $logDir 'control'
$soakTelemetryDir = Join-Path $logDir 'telemetry'
$soakResourcePath = Join-Path $soakTelemetryDir 'process-resources.jsonl'
$soakSummaryPath = Join-Path $soakTelemetryDir 'soak-summary.json'
$manifestPath = Join-Path $logDir 'manifest.json'
$manifestStartedAtUtc = [DateTime]::UtcNow
$manifestStatus = 'running'
$manifestError = $null
$manifestFailureCategory = $null
$manifestFailureStage = $null
$scenarioEstablished = $false
$faultTimeline = @()
$assertionResults = @()
$firstDivergence = $null
$convergenceSummaries = @()
$processTimeline = @()
$faultControlPath = Join-Path $logDir 'gateway-fault-command.json'
$reconnectReleasePath = Join-Path $logDir 'gateway-reconnect.release'
$completionReleasePath = Join-Path $logDir 'scenario-completion.release'
$soakSummary = $null
$soakResourcePrevious = @{}
$scenarioDeadlineUtc = $null
$timeoutPhase = 'startup'
$timeoutBudgetSeconds = $StartupTimeoutSeconds
$roomId = $null
$server = $null
$serverCorrelationId = "$RunId/shooter-mp-server"
$clientLogs = @()
$manifestClients = @()
$manifestProcesses = @()
$startedProcesses = New-Object System.Collections.Generic.List[System.Diagnostics.Process]

function Register-RunProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Role,
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [Parameter(Mandatory = $true)][string]$CorrelationId,
        [string]$StdOutPath = '',
        [string]$StdErrPath = ''
    )

    $identity = Get-AbilityKitProcessIdentity -ProcessId $ProcessId
    if ($null -eq $identity) {
        throw "Could not capture process identity for role '$Role', PID $ProcessId."
    }

    $script:manifestProcesses += [ordered]@{
        role = $Role
        processId = $identity.processId
        creationTimeUtc = $identity.creationTimeUtc
        executablePath = $identity.executablePath
        commandLine = $identity.commandLine
        correlationId = $CorrelationId
        stdoutPath = if ([string]::IsNullOrWhiteSpace($StdOutPath)) { $null } else { ConvertTo-RunRelativePath -Path $StdOutPath }
        stderrPath = if ([string]::IsNullOrWhiteSpace($StdErrPath)) { $null } else { ConvertTo-RunRelativePath -Path $StdErrPath }
    }
}

function Write-RunManifest {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('running', 'passed', 'failed')]
        [string]$Status,
        [string]$ErrorMessage = ''
    )

    $artifacts = @()
    if ($Status -ne 'running' -and (Test-Path $logDir)) {
        $artifacts = @(Get-ChildItem -LiteralPath $logDir -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -ne $manifestPath -and $_.FullName -ne "$manifestPath.tmp" } |
            Sort-Object FullName |
            ForEach-Object {
                [ordered]@{
                    path = ConvertTo-RunRelativePath -Path $_.FullName
                    bytes = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            })
    }

    $manifest = [ordered]@{
        schemaVersion = 2
        runId = $RunId
        status = $Status
        failure = if ($null -eq $manifestFailureCategory) { $null } else { [ordered]@{ category = $manifestFailureCategory; stage = $manifestFailureStage; message = $ErrorMessage } }
        startedAtUtc = $manifestStartedAtUtc.ToString('O')
        completedAtUtc = if ($Status -eq 'running') { $null } else { [DateTime]::UtcNow.ToString('O') }
        artifactDirectory = '.'
        manifestPath = 'manifest.json'
        processId = $PID
        machineName = [Environment]::MachineName
        configuration = $Configuration
        scenario = [ordered]@{
            name = if ([string]::IsNullOrWhiteSpace($Scenario)) { 'custom' } else { $Scenario }
            profile = $Profile
            mode = if ($isSoakRun) { 'soak' } elseif ($WaitForMatchEnd) { 'end-to-end' } elseif ($ReconnectCount -gt 0 -or $ConditionLatencyMs -gt 0 -or $ConditionJitterMs -gt 0 -or $ConditionPacketLossRate -gt 0) { 'resilience' } else { 'sync' }
            payloadMode = $PayloadMode
            joinClients = $JoinClients
            observerCount = $JoinClients + 1
            soak = $isSoakRun
            inputs = $Inputs
            seed = $Seed
            waitForMatchEnd = [bool]$WaitForMatchEnd
            reconnectJoinClient = $ReconnectCount -gt 0
            reconnectCount = $ReconnectCount
            reconnectDelayMs = $ReconnectDelayMs
            recoverableFailureCount = $RecoverableFailureCount
            retryBackoffMaxMs = $RetryBackoffMaxMs
            operationTimeoutSeconds = $TimeoutSeconds
            startupTimeoutSeconds = $StartupTimeoutSeconds
            setupTimeoutSeconds = $SetupTimeoutSeconds
            timeoutSeconds = $activePlan.timeoutSeconds
            executionTimeoutSeconds = $activePlan.executionTimeoutSeconds
            convergenceTimeoutSeconds = $activePlan.convergenceTimeoutSeconds
            metricsSampleIntervalMs = if ($isSoakRun) { $SoakMetricsSampleIntervalMs } else { $null }
            resourceSampleIntervalSeconds = if ($isSoakRun) { $SoakResourceSampleIntervalSeconds } else { $null }
            networkCondition = [ordered]@{
                latencyMs = $ConditionLatencyMs
                jitterMs = $ConditionJitterMs
                packetLossRate = $ConditionPacketLossRate
                seed = $ConditionSeed
            }
        }
        ports = [ordered]@{
            tcpGateway = $TcpPort
            silo = $SiloPort
            orleansGateway = $OrleansGatewayPort
        }
        roomId = $roomId
        replayEnabled = -not [bool]$NoReplay
        error = if ([string]::IsNullOrWhiteSpace($ErrorMessage)) { $null } else { $ErrorMessage }
        processes = @($manifestProcesses)
        clients = @($manifestClients)
        processTimeline = @($processTimeline)
        faultTimeline = @($faultTimeline)
        assertions = @($assertionResults)
        firstDivergence = $firstDivergence
        healthSummary = @($convergenceSummaries | ForEach-Object { $_.health })
        observerSummary = [ordered]@{
            slowConsumer = [bool]$activePlan.slowConsumer
            bytesPerSecond = if ($activePlan.slowConsumer) { 256 } else { $null }
            burstBytes = if ($activePlan.slowConsumer) { 32768 } else { $null }
            maxQueueLength = if ($activePlan.slowConsumer) { 1 } else { $null }
            maxQueueAgeMs = if ($activePlan.slowConsumer) { 100 } else { $null }
            drainIntervalMs = if ($activePlan.slowConsumer) { 250 } else { $null }
            clients = @($convergenceSummaries)
        }
        soakSummary = $soakSummary
        artifacts = $artifacts
    }

    $temporaryPath = "$manifestPath.tmp"
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $temporaryPath -Encoding utf8
    Move-Item -LiteralPath $temporaryPath -Destination $manifestPath -Force
}

function Stop-StartedProcesses {
    param([System.Collections.Generic.List[System.Diagnostics.Process]]$Processes)

    for ($i = $Processes.Count - 1; $i -ge 0; $i--) {
        $process = $Processes[$i]
        if ($null -eq $process) {
            continue
        }

        try {
            $live = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
            if ($live) {
                Write-Host "Stopping spawned PID $($process.Id) ($($live.ProcessName))" -ForegroundColor Yellow
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
            Write-Warning "Failed to stop spawned PID $($process.Id): $($_.Exception.Message)"
        }
    }
}

function ConvertTo-ProcessArgumentString {
    param([string[]]$Arguments)

    return ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + (($_ -replace '"', '\"')) + '"'
        }
        else {
            $_
        }
    }) -join ' '
}

function Start-DotnetProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$StdOut,
        [Parameter(Mandatory = $true)]
        [string]$StdErr
    )

    $workingDirectory = [System.IO.Path]::GetDirectoryName($project)
    $argumentString = ConvertTo-ProcessArgumentString -Arguments $Arguments
    $process = Start-Process -FilePath 'dotnet' `
        -ArgumentList $argumentString `
        -WorkingDirectory $workingDirectory `
        -RedirectStandardOutput $StdOut `
        -RedirectStandardError $StdErr `
        -PassThru

    return $process
}

function Get-BoundedTimeoutSeconds {
    param([int]$RequestedSeconds)

    if ($null -eq $scenarioDeadlineUtc) {
        return [Math]::Max(1, $RequestedSeconds)
    }

    $remaining = [int][Math]::Ceiling(($scenarioDeadlineUtc - [DateTime]::UtcNow).TotalSeconds)
    if ($remaining -le 0) {
        throw "Scenario '$Scenario' exceeded $timeoutPhase timeout of $timeoutBudgetSeconds seconds."
    }
    return [Math]::Max(1, [Math]::Min($RequestedSeconds, $remaining))
}

function Wait-ForPort {
    param(
        [int]$Port,
        [int]$TimeoutSeconds
    )

    $TimeoutSeconds = Get-BoundedTimeoutSeconds -RequestedSeconds $TimeoutSeconds
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-AbilityKitTcpPort -HostName '127.0.0.1' -Port $Port -TimeoutMilliseconds 500) {
            return
        }

        Start-Sleep -Milliseconds 250
    }

    throw "TCP Gateway did not listen on 127.0.0.1:$Port within $TimeoutSeconds seconds."
}

function Wait-ForPortClosed {
    param(
        [int]$Port,
        [int]$TimeoutSeconds
    )

    $TimeoutSeconds = Get-BoundedTimeoutSeconds -RequestedSeconds $TimeoutSeconds
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (-not (Test-AbilityKitTcpPort -HostName '127.0.0.1' -Port $Port -TimeoutMilliseconds 250)) {
            return
        }

        Start-Sleep -Milliseconds 100
    }

    throw "TCP Gateway remained reachable on 127.0.0.1:$Port for $TimeoutSeconds seconds after the offline fault."
}

function Wait-ForResultLine {
    param(
        [string]$Path,
        [string]$Prefix,
        [int]$TimeoutSeconds
    )

    $TimeoutSeconds = Get-BoundedTimeoutSeconds -RequestedSeconds $TimeoutSeconds
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $Path) {
            $lines = @(Get-Content $Path -ErrorAction SilentlyContinue)
            $line = $lines | Where-Object { $_ -like "$Prefix*" } | Select-Object -Last 1
            if ($line) {
                return $line
            }

            $failure = $lines | Where-Object { $_ -like 'SHOOTER_MP_CLIENT_RESULT status=fail*' } | Select-Object -Last 1
            if ($failure) {
                throw "Client failed before '$Prefix': $failure"
            }
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Timed out waiting for '$Prefix' in $Path."
}

function ConvertFrom-ClientResultLine {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Line
    )

    $fields = @{}
    $matches = [regex]::Matches($Line, '(?<name>[A-Za-z][A-Za-z0-9_]*)=(?<value>"(?:\\.|[^"])*"|[^\s]+)')
    foreach ($match in $matches) {
        $name = $match.Groups['name'].Value
        $value = $match.Groups['value'].Value
        if ($value.StartsWith('"') -and $value.EndsWith('"')) {
            $value = $value.Substring(1, $value.Length - 2).Replace('\"', '"').Replace('\\', '\')
        }

        $fields[$name] = $value
    }

    return $fields
}

function Read-ResultValue {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Fields,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if (-not $Fields.ContainsKey($Name)) {
        throw "Could not read $Name from result fields."
    }

    return [string]$Fields[$Name]
}

function Read-ResultInt {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Fields,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = Read-ResultValue -Fields $Fields -Name $Name
    return [int]::Parse($value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Read-ResultInt64 {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Fields,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = Read-ResultValue -Fields $Fields -Name $Name
    return [long]::Parse($value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Read-ResultBool {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Fields,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = Read-ResultValue -Fields $Fields -Name $Name
    return [bool]::Parse($value)
}

function Read-ResultDouble {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Fields,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = Read-ResultValue -Fields $Fields -Name $Name
    return [double]::Parse($value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function ConvertTo-RunRelativePath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ''
    }

    $trimChars = [char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $rootPath = [System.IO.Path]::GetFullPath($logDir).TrimEnd($trimChars) + [System.IO.Path]::DirectorySeparatorChar
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootUri = New-Object System.Uri($rootPath)
    $pathUri = New-Object System.Uri($fullPath)
    $relativeUri = $rootUri.MakeRelativeUri($pathUri)
    $relative = [System.Uri]::UnescapeDataString($relativeUri.ToString())
    if ($relativeUri.IsAbsoluteUri -or $relative -eq '..' -or $relative.StartsWith('../')) {
        throw "Artifact path is outside the run root: $Path"
    }

    return $relative -replace '\\', '/'
}

function Get-ProcessExitCode {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,
        [int]$RetryCount = 20,
        [int]$DelayMilliseconds = 100
    )

    for ($i = 0; $i -lt $RetryCount; $i++) {
        try {
            $null = $Process.Handle
            $Process.Refresh()
            return [int]$Process.ExitCode
        }
        catch {
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }

    throw "Could not read exit code for process $($Process.Id)."
}

function Add-AssertionResult {
    param([string]$Name, [bool]$Passed, [string]$Details = '')

    $script:assertionResults += [ordered]@{
        name = $Name
        passed = $Passed
        checkedAtUtc = [DateTime]::UtcNow.ToString('O')
        details = $Details
    }
    if (-not $Passed -and $null -eq $script:firstDivergence) {
        $script:firstDivergence = [ordered]@{
            assertion = $Name
            observedAtUtc = [DateTime]::UtcNow.ToString('O')
            details = $Details
        }
    }
}

function Get-PercentileValue {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][double[]]$Values,
        [Parameter(Mandatory = $true)][double]$Percentile
    )

    if ($Values.Count -eq 0) {
        return $null
    }

    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 1) {
        return [double]$sorted[0]
    }

    $position = ($sorted.Count - 1) * $Percentile
    $lower = [int][Math]::Floor($position)
    $upper = [int][Math]::Ceiling($position)
    if ($lower -eq $upper) {
        return [double]$sorted[$lower]
    }

    $weight = $position - $lower
    return ([double]$sorted[$lower] * (1 - $weight)) + ([double]$sorted[$upper] * $weight)
}

function Write-SoakResourceSample {
    param([Parameter(Mandatory = $true)][object[]]$RunProcesses)

    $sampledAtUtc = [DateTime]::UtcNow
    foreach ($runProcess in $RunProcesses) {
        $processId = [int]$runProcess.processId
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($null -eq $process) {
            continue
        }

        $process.Refresh()
        $cpuTotalMs = [double]$process.TotalProcessorTime.TotalMilliseconds
        $previous = $script:soakResourcePrevious[$processId]
        $cpuUtilizationPercent = $null
        if ($null -ne $previous) {
            $elapsedMs = ($sampledAtUtc - $previous.sampledAtUtc).TotalMilliseconds
            if ($elapsedMs -gt 0) {
                $cpuUtilizationPercent = (($cpuTotalMs - $previous.cpuTotalMs) / $elapsedMs) * 100.0 / [Environment]::ProcessorCount
            }
        }
        $script:soakResourcePrevious[$processId] = [pscustomobject]@{
            sampledAtUtc = $sampledAtUtc
            cpuTotalMs = $cpuTotalMs
        }

        [ordered]@{
            type = 'process-resource-sample'
            timestampUtc = $sampledAtUtc.ToString('O')
            role = [string]$runProcess.role
            processId = $processId
            cpuTotalMs = $cpuTotalMs
            cpuUtilizationPercent = $cpuUtilizationPercent
            workingSetBytes = [long]$process.WorkingSet64
            privateBytes = [long]$process.PrivateMemorySize64
            gc = [ordered]@{
                available = $false
                reason = 'CLR GC counters are not exposed by System.Diagnostics.Process; recorded as unavailable.'
            }
        } | ConvertTo-Json -Depth 5 -Compress | Add-Content -LiteralPath $soakResourcePath -Encoding utf8
    }
}

function Write-SoakNetworkConditionCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Command
    )

    $temporaryPath = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"
    $backupPath = "$Path.$([Guid]::NewGuid().ToString('N')).bak"
    try {
        $Command | ConvertTo-Json -Depth 5 -Compress | Set-Content -LiteralPath $temporaryPath -Encoding utf8
        $replaceDeadline = [DateTime]::UtcNow.AddSeconds(2)
        while ($true) {
            try {
                if ([System.IO.File]::Exists($Path)) {
                    [System.IO.File]::Replace($temporaryPath, $Path, $backupPath)
                }
                else {
                    [System.IO.File]::Move($temporaryPath, $Path)
                }
                break
            }
            catch [System.IO.IOException] {
                if ([DateTime]::UtcNow -ge $replaceDeadline) {
                    throw
                }
                Start-Sleep -Milliseconds 10
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path -LiteralPath $backupPath) {
            Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Send-SoakNetworkCondition {
    param(
        [Parameter(Mandatory = $true)][object[]]$Clients,
        [Parameter(Mandatory = $true)][string]$Phase,
        [int]$LatencyMs = 0,
        [int]$JitterMs = 0,
        [double]$PacketLossRate = 0,
        [int]$BandwidthBytesPerSecond = 0,
        [bool]$ExpectRecovery = $false
    )

    $commandId = '{0}-{1:D2}' -f $Phase, ($script:faultTimeline.Count + 1)
    $requestedAtUtc = [DateTime]::UtcNow
    foreach ($client in $Clients) {
        Remove-Item -LiteralPath $client.NetworkControlAckPath -Force -ErrorAction SilentlyContinue
        $command = [ordered]@{
            id = $commandId
            phase = $Phase
            inboundLatencyMs = $LatencyMs
            inboundJitterMs = $JitterMs
            inboundPacketLossRate = $PacketLossRate
            inboundBandwidthBytesPerSecond = $BandwidthBytesPerSecond
            seed = $ConditionSeed + $client.PlayerId
            expectRecovery = $ExpectRecovery
        }
        Write-SoakNetworkConditionCommand -Path $client.NetworkControlPath -Command $command
    }

    $pending = @($Clients)
    $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Min(15, (Get-BoundedTimeoutSeconds -RequestedSeconds 15)))
    while ($pending.Count -gt 0 -and [DateTime]::UtcNow -lt $deadline) {
        $remaining = @()
        foreach ($client in $pending) {
            $ack = Read-JsonFileIfPresent -Path $client.NetworkControlAckPath
            if ($null -eq $ack -or [string]$ack.id -ne $commandId) {
                $remaining += $client
            }
        }
        $pending = @($remaining)
        if ($pending.Count -gt 0) {
            Start-Sleep -Milliseconds 50
        }
    }

    if ($pending.Count -gt 0) {
        throw "Timed out waiting for soak network-condition acknowledgements. Phase=$Phase, Pending=$(@($pending | ForEach-Object { $_.ClientId }) -join ',')"
    }

    $script:faultTimeline += [ordered]@{
        action = 'network-condition'
        commandId = $commandId
        phase = $Phase
        requestedAtUtc = $requestedAtUtc.ToString('O')
        completedAtUtc = [DateTime]::UtcNow.ToString('O')
        clientCount = $Clients.Count
        latencyMs = $LatencyMs
        jitterMs = $JitterMs
        packetLossRate = $PacketLossRate
        bandwidthBytesPerSecond = $BandwidthBytesPerSecond
        expectRecovery = $ExpectRecovery
    }
    Write-RunManifest -Status 'running'
}

function Invoke-ShooterSoakPhases {
    param([Parameter(Mandatory = $true)][object[]]$Clients)

    $phaseDefinitions = @(
        [pscustomobject]@{ name = 'ideal'; latencyMs = 0; jitterMs = 0; loss = 0.0; bandwidth = 0; recovery = $false },
        [pscustomobject]@{ name = 'latency-jitter'; latencyMs = 120; jitterMs = 40; loss = 0.0; bandwidth = 0; recovery = $false },
        [pscustomobject]@{ name = 'packet-loss'; latencyMs = 40; jitterMs = 10; loss = 0.08; bandwidth = 0; recovery = $false },
        [pscustomobject]@{ name = 'limited-bandwidth'; latencyMs = 40; jitterMs = 10; loss = 0.01; bandwidth = 32768; recovery = $false },
        [pscustomobject]@{ name = 'disruptive-pressure'; latencyMs = 250; jitterMs = 100; loss = 0.20; bandwidth = 8192; recovery = $false },
        [pscustomobject]@{ name = 'recovery-ideal'; latencyMs = 0; jitterMs = 0; loss = 0.0; bandwidth = 0; recovery = $true }
    )
    $runStartedAtUtc = [DateTime]::UtcNow
    $phaseSeconds = [double]$activePlan.timeoutSeconds / $phaseDefinitions.Count
    $nextResourceSampleUtc = [DateTime]::MinValue

    for ($phaseIndex = 0; $phaseIndex -lt $phaseDefinitions.Count; $phaseIndex++) {
        $phase = $phaseDefinitions[$phaseIndex]
        Send-SoakNetworkCondition -Clients $Clients -Phase $phase.name -LatencyMs $phase.latencyMs -JitterMs $phase.jitterMs -PacketLossRate $phase.loss -BandwidthBytesPerSecond $phase.bandwidth -ExpectRecovery $phase.recovery
        $phaseDeadlineUtc = if ($phaseIndex -eq $phaseDefinitions.Count - 1) {
            $runStartedAtUtc.AddSeconds($activePlan.timeoutSeconds)
        }
        else {
            $runStartedAtUtc.AddSeconds($phaseSeconds * ($phaseIndex + 1))
        }
        while ([DateTime]::UtcNow -lt $phaseDeadlineUtc) {
            if ([DateTime]::UtcNow -ge $nextResourceSampleUtc) {
                Write-SoakResourceSample -RunProcesses @($manifestProcesses)
                $nextResourceSampleUtc = [DateTime]::UtcNow.AddSeconds($SoakResourceSampleIntervalSeconds)
            }
            $sleepMs = [Math]::Min(250, [Math]::Max(1, [int](($phaseDeadlineUtc - [DateTime]::UtcNow).TotalMilliseconds)))
            Start-Sleep -Milliseconds $sleepMs
        }
    }

    New-Item -ItemType File -Path $completionReleasePath -Force | Out-Null
    Add-AssertionResult -Name 'soak-phases-completed' -Passed $true -Details "Phases=$($phaseDefinitions.Count); DurationSeconds=$($activePlan.timeoutSeconds)"
}

function Get-ShooterSoakSummary {
    param([Parameter(Mandatory = $true)][object[]]$Clients)

    $clientSummaries = @()
    $recoveryDurations = @()
    foreach ($client in $Clients) {
        if (-not (Test-Path -LiteralPath $client.MetricsOutputPath -PathType Leaf)) {
            throw "Soak metrics artifact was not created. Client=$($client.ClientId), Path=$($client.MetricsOutputPath)"
        }
        $events = @(Get-Content -LiteralPath $client.MetricsOutputPath | ForEach-Object {
            if (-not [string]::IsNullOrWhiteSpace($_)) { $_ | ConvertFrom-Json }
        })
        $delivery = @($events | Where-Object { $_.type -eq 'delivery-sample' })
        $recoveries = @($events | Where-Object { $_.type -eq 'recovery-completed' })
        if ($delivery.Count -lt 2) {
            throw "Soak telemetry has fewer than two delivery samples. Client=$($client.ClientId), Samples=$($delivery.Count)"
        }
        $sentDelta = [long]$delivery[-1].delivery.sentBytes - [long]$delivery[0].delivery.sentBytes
        $maxQueueLength = [int](($delivery | ForEach-Object { [int]$_.delivery.queueLength } | Measure-Object -Maximum).Maximum)
        $recoveryDurations += @($recoveries | ForEach-Object { [double]$_.recovery.durationMs })
        $clientSummaries += [pscustomobject][ordered]@{
            clientId = $client.ClientId
            deliverySamples = $delivery.Count
            sentBytesDelta = $sentDelta
            producedBytesDelta = [long]$delivery[-1].delivery.producedBytes - [long]$delivery[0].delivery.producedBytes
            droppedBytesDelta = [long]$delivery[-1].delivery.droppedBytes - [long]$delivery[0].delivery.droppedBytes
            mergedBytesDelta = [long]$delivery[-1].delivery.mergedBytes - [long]$delivery[0].delivery.mergedBytes
            maxQueueLength = $maxQueueLength
            recoveryCount = $recoveries.Count
        }
    }

    $sentValues = @($clientSummaries | ForEach-Object { [double]$_.sentBytesDelta })
    $sentMedian = Get-PercentileValue -Values $sentValues -Percentile 0.5
    $fairness = if ($sentMedian -le 0) { 0.0 } else { [double](($sentValues | Measure-Object -Minimum).Minimum) / $sentMedian }
    $resourceEvents = if (Test-Path -LiteralPath $soakResourcePath) {
        @(Get-Content -LiteralPath $soakResourcePath | ForEach-Object { if (-not [string]::IsNullOrWhiteSpace($_)) { $_ | ConvertFrom-Json } })
    }
    else { @() }
    $processResourceSummaries = @($resourceEvents | Group-Object processId | ForEach-Object {
        $samples = @($_.Group | Sort-Object timestampUtc)
        $memoryValues = @($samples | ForEach-Object { [double]$_.privateBytes / 1MB })
        $windowSize = [Math]::Max(1, [int][Math]::Ceiling($memoryValues.Count * 0.1))
        $memoryStartMedian = Get-PercentileValue -Values @($memoryValues | Select-Object -First $windowSize) -Percentile 0.5
        $memoryEndMedian = Get-PercentileValue -Values @($memoryValues | Select-Object -Last $windowSize) -Percentile 0.5
        [pscustomobject][ordered]@{
            role = [string]$samples[0].role
            processId = [int]$samples[0].processId
            sampleCount = $samples.Count
            peakWorkingSetMb = [double](($samples | Measure-Object -Property workingSetBytes -Maximum).Maximum) / 1MB
            peakPrivateMb = [double](($samples | Measure-Object -Property privateBytes -Maximum).Maximum) / 1MB
            peakCpuUtilizationPercent = [double](($samples | Where-Object { $null -ne $_.cpuUtilizationPercent } | Measure-Object -Property cpuUtilizationPercent -Maximum).Maximum)
            privateMemoryStartMedianMb = $memoryStartMedian
            privateMemoryEndMedianMb = $memoryEndMedian
            privateMemoryGrowthMb = $memoryEndMedian - $memoryStartMedian
        }
    })
    $maxMemoryGrowthMb = if ($processResourceSummaries.Count -gt 0) {
        [double](($processResourceSummaries | Measure-Object -Property privateMemoryGrowthMb -Maximum).Maximum)
    }
    else { 0.0 }

    return [pscustomobject][ordered]@{
        schemaVersion = 1
        observerCount = $Clients.Count
        durationSeconds = $activePlan.timeoutSeconds
        phaseCount = 6
        recovery = [ordered]@{
            count = $recoveryDurations.Count
            p50Ms = Get-PercentileValue -Values @($recoveryDurations) -Percentile 0.50
            p95Ms = Get-PercentileValue -Values @($recoveryDurations) -Percentile 0.95
            p99Ms = Get-PercentileValue -Values @($recoveryDurations) -Percentile 0.99
        }
        fairness = [ordered]@{
            definition = 'minimum observer sent-byte delta divided by median observer sent-byte delta'
            minimumToMedian = $fairness
            threshold = $SoakObserverSentBytesFairnessMin
        }
        resources = [ordered]@{
            sampleCount = $resourceEvents.Count
            peakWorkingSetMb = if ($resourceEvents.Count -gt 0) { [double](($resourceEvents | Measure-Object -Property workingSetBytes -Maximum).Maximum) / 1MB } else { 0.0 }
            peakPrivateMb = if ($resourceEvents.Count -gt 0) { [double](($resourceEvents | Measure-Object -Property privateBytes -Maximum).Maximum) / 1MB } else { 0.0 }
            maximumProcessPrivateMemoryGrowthMb = $maxMemoryGrowthMb
            gcCountersAvailable = $false
            processes = $processResourceSummaries
        }
        clients = $clientSummaries
    }
}

function Assert-ShooterSoakSummary {
    param([Parameter(Mandatory = $true)][object]$Summary)

    $expectedObservers = [int]$activePlan.observerCount
    if ($Summary.observerCount -ne $expectedObservers) {
        throw "Soak observer count mismatch. Expected=$expectedObservers, Actual=$($Summary.observerCount)"
    }
    if ($Summary.recovery.count -lt $expectedObservers) {
        throw "Soak recovery evidence is incomplete. ExpectedAtLeast=$expectedObservers, Actual=$($Summary.recovery.count)"
    }
    if ([double]$Summary.recovery.p95Ms -gt $SoakRecoveryP95LimitMs -or [double]$Summary.recovery.p99Ms -gt $SoakRecoveryP99LimitMs) {
        throw "Soak recovery percentile exceeded limits. P95=$($Summary.recovery.p95Ms)/$SoakRecoveryP95LimitMs, P99=$($Summary.recovery.p99Ms)/$SoakRecoveryP99LimitMs"
    }
    if ([double]$Summary.fairness.minimumToMedian -lt $SoakObserverSentBytesFairnessMin) {
        throw "Soak observer sent-byte fairness is below threshold. Actual=$($Summary.fairness.minimumToMedian), Minimum=$SoakObserverSentBytesFairnessMin"
    }
    if ([double]$Summary.resources.maximumProcessPrivateMemoryGrowthMb -gt $SoakMemoryGrowthLimitMb) {
        throw "Soak private-memory growth exceeded limit. ActualMb=$($Summary.resources.maximumProcessPrivateMemoryGrowthMb), LimitMb=$SoakMemoryGrowthLimitMb"
    }

    Add-AssertionResult -Name 'soak-observer-count' -Passed $true -Details "Observers=$expectedObservers"
    Add-AssertionResult -Name 'soak-recovery-percentiles' -Passed $true -Details ($Summary.recovery | ConvertTo-Json -Compress)
    Add-AssertionResult -Name 'soak-observer-fairness' -Passed $true -Details ($Summary.fairness | ConvertTo-Json -Compress)
    Add-AssertionResult -Name 'soak-resource-trend' -Passed $true -Details ($Summary.resources | ConvertTo-Json -Compress)
}

function Invoke-GatewayFaultCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Action,
        [string]$ObserverKey = '',
        [int]$TimeoutSeconds = 10
    )

    $commandId = [Guid]::NewGuid().ToString('N')
    $ackPath = "$faultControlPath.ack.json"
    Remove-Item -LiteralPath $ackPath -Force -ErrorAction SilentlyContinue
    $requestedAtUtc = [DateTime]::UtcNow
    [ordered]@{
        Id = $commandId
        Action = $Action
        RequestedAtUtc = $requestedAtUtc.ToString('O')
        ObserverKey = $ObserverKey
    } | ConvertTo-Json | Set-Content -LiteralPath $faultControlPath -Encoding utf8

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path $ackPath) {
            try {
                $ack = Get-Content -LiteralPath $ackPath -Raw | ConvertFrom-Json
                if ($ack.Id -eq $commandId) {
                    $script:faultTimeline += [ordered]@{
                        action = $Action
                        commandId = $commandId
                        requestedAtUtc = $requestedAtUtc.ToString('O')
                        receivedAtUtc = $ack.ReceivedAtUtc
                        completedAtUtc = $ack.CompletedAtUtc
                        status = $ack.Status
                        error = $ack.Error
                        observerReactivation = $ack.ObserverReactivation
                    }
                    if ($ack.Status -ne 'completed') {
                        throw "Gateway fault command '$Action' failed: $($ack.Error)"
                    }
                    return $ack
                }
            }
            catch [System.ArgumentException] {
            }
        }
        Start-Sleep -Milliseconds 50
    }

    throw "Timed out waiting for Gateway fault acknowledgement. Action=$Action"
}

function Get-FailureClassification {
    param([string]$Message, [bool]$Established)

    if (-not $Established -or $Message -match '(?i)(status|http|gatewaystatuscode)[^\r\n]*409|\b409\b|conflict|already exists|port.*(used|listen|bind)') {
        return 'PreconditionFailed'
    }
    return 'FaultRecoveryFailed'
}

function Assert-BoundedConvergence {
    param([Parameter(Mandatory = $true)][object[]]$ClientResults)

    $deadline = [DateTime]::UtcNow.AddSeconds($activePlan.convergenceTimeoutSeconds)
    $summaries = @()
    foreach ($clientResult in $ClientResults) {
        if ([DateTime]::UtcNow -ge $deadline) {
            throw "Convergence diagnostics exceeded timeout of $($activePlan.convergenceTimeoutSeconds) seconds."
        }

        $diagnosticPath = Read-ResultValue -Fields $clientResult.Fields -Name 'diagnosticArtifactPath'
        $resolvedDiagnosticPath = if ([System.IO.Path]::IsPathRooted($diagnosticPath)) {
            $diagnosticPath
        }
        else {
            Join-Path $logDir $diagnosticPath
        }
        if (-not (Test-Path -LiteralPath $resolvedDiagnosticPath)) {
            throw "Client diagnostic artifact was not created: $diagnosticPath"
        }
        $diagnostic = Get-Content -LiteralPath $resolvedDiagnosticPath -Raw | ConvertFrom-Json
        if (-not $diagnostic.diff.matched) {
            throw "Client authoritative diff did not converge. Client=$($clientResult.Client.ClientId), Status=$($diagnostic.diff.status), Reason=$($diagnostic.diff.reason)"
        }
        if ($diagnostic.reliableEvents.needsResync) {
            throw "Client reliable-event cursor still requires resync. Client=$($clientResult.Client.ClientId)"
        }
        if ((Read-ResultBool -Fields $clientResult.Fields -Name 'pureStateLastResyncNeeded')) {
            throw "Client baseline remained pending after recovery. Client=$($clientResult.Client.ClientId)"
        }

        if ($activePlan.slowConsumer -and
            ($null -eq $diagnostic.observer.serverDroppedItems -or
             $null -eq $diagnostic.observer.serverCoalescedItems -or
             $null -eq $diagnostic.observer.serverBaselineInvalidations)) {
            throw "Slow-consumer server delivery metrics were not captured. Client=$($clientResult.Client.ClientId)"
        }

        $summaries += [PSCustomObject][ordered]@{
            clientId = $clientResult.Client.ClientId
            stateHash = Read-ResultValue -Fields $clientResult.Fields -Name 'stateHash'
            baselineFrame = Read-ResultInt -Fields $clientResult.Fields -Name 'baselineFrame'
            reliableEventEpoch = [string]$diagnostic.reliableEvents.epoch
            reliableEventCursor = [long]$diagnostic.reliableEvents.lastAcknowledgedSequence
            reliableEventNeedsResync = [bool]$diagnostic.reliableEvents.needsResync
            diffStatus = [string]$diagnostic.diff.status
            serverQueueLength = [int]$diagnostic.observer.serverQueueLength
            serverDroppedItems = [long]$diagnostic.observer.serverDroppedItems
            serverCoalescedItems = [long]$diagnostic.observer.serverCoalescedItems
            serverBaselineInvalidations = [long]$diagnostic.observer.serverBaselineInvalidations
            fullBaselinesApplied = [int]$diagnostic.observer.pureStateFullBaselinesApplied
            health = [ordered]@{
                total = [long]$diagnostic.health.totalCount
                warnings = [long]$diagnostic.health.warningCount
                critical = [long]$diagnostic.health.criticalCount
                highestSeverity = [string]$diagnostic.health.highestSeverity
            }
        }
    }

    if ($activePlan.slowConsumer) {
        $pressureCount = [long](($summaries | Measure-Object -Property serverDroppedItems -Sum).Sum) +
            [long](($summaries | Measure-Object -Property serverCoalescedItems -Sum).Sum)
        $baselineCount = [long](($summaries | Measure-Object -Property fullBaselinesApplied -Sum).Sum)
        if ($pressureCount -le 0) {
            throw 'Slow-consumer scenario did not observe server queue drop or coalescing evidence.'
        }
        if ($baselineCount -lt $ClientResults.Count) {
            throw "Slow-consumer scenario did not restore a full baseline for every client. Expected=$($ClientResults.Count), Actual=$baselineCount."
        }
    }

    $script:convergenceSummaries = @($summaries)
    Add-AssertionResult -Name 'bounded-convergence' -Passed $true -Details ($summaries | ConvertTo-Json -Depth 4 -Compress)
}

function New-ClientArguments {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('create', 'join')]
        [string]$ClientMode,
        [Parameter(Mandatory = $true)]
        [string]$ClientId,
        [Parameter(Mandatory = $true)]
        [int]$PlayerId,
        [string]$RoomId,
        [int]$RoomMaxPlayers = 0,
        [int]$BattleDurationFrames = 0,
        [int]$ClientReconnectCount = 0,
        [int]$ClientRecoverableFailureCount = 0,
        [int]$LatencyMs = 0,
        [int]$JitterMs = 0,
        [double]$PacketLossRate = 0,
        [int]$BandwidthBytesPerSecond = 0,
        [int]$NetworkSeed = 0,
        [string]$ReplayOutputPath = '',
        [string]$ReconnectReleasePath = '',
        [string]$CompletionReleasePath = '',
        [string]$NetworkControlPath = '',
        [string]$MetricsOutputPath = '',
        [int]$MetricsSampleIntervalMs = 1000,
        [int]$ClientTimeoutSeconds = $TimeoutSeconds,
        [Parameter(Mandatory = $true)]
        [string]$CorrelationId,
        [Parameter(Mandatory = $true)]
        [string]$DiagnosticOutputPath
    )

    $arguments = @($applicationDll)
    $arguments += @(
        '--client',
        '--state-sync-payload-mode', $PayloadMode,
        '--client-mode', $ClientMode,
        '--tcp-port', $TcpPort,
        '--client-id', $ClientId,
        '--player-id', $PlayerId,
        '--inputs', $Inputs,
        '--seed', $Seed,
        '--timeout-seconds', $ClientTimeoutSeconds,
        '--run-id', $RunId,
        '--correlation-id', $CorrelationId,
        '--run-root', $logDir,
        '--diagnostic-output', $DiagnosticOutputPath)

    if ($ClientMode -eq 'create' -and $RoomMaxPlayers -gt 0) {
        $arguments += @('--room-max-players', $RoomMaxPlayers)
    }

    if ($ClientMode -eq 'create' -and $BattleDurationFrames -gt 0) {
        $arguments += @('--battle-duration-frames', $BattleDurationFrames)
    }

    if ($WaitForMatchEnd) {
        $arguments += @('--wait-for-match-end')
    }

    if ($ClientReconnectCount -gt 0) {
        $clientReconnectDelayMs = if ($Scenario -eq 'gateway-offline') { [Math]::Max(1500, $ReconnectDelayMs) } else { $ReconnectDelayMs }
        $arguments += @(
            '--reconnect-count', $ClientReconnectCount,
            '--reconnect-delay-ms', $clientReconnectDelayMs,
            '--recoverable-failure-count', $ClientRecoverableFailureCount,
            '--retry-backoff-max-ms', $RetryBackoffMaxMs)
    }

    if ($LatencyMs -gt 0 -or $JitterMs -gt 0 -or $PacketLossRate -gt 0 -or $BandwidthBytesPerSecond -gt 0) {
        $arguments += @(
            '--condition-latency-ms', $LatencyMs,
            '--condition-jitter-ms', $JitterMs,
            '--condition-packet-loss-rate', ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0}', $PacketLossRate)),
            '--condition-bandwidth-bytes-per-second', $BandwidthBytesPerSecond,
            '--condition-seed', $NetworkSeed)
    }

    if (-not [string]::IsNullOrWhiteSpace($ReplayOutputPath)) {
        $arguments += @('--input-state-replay-output', $ReplayOutputPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($ReconnectReleasePath)) {
        $arguments += @('--reconnect-release-path', $ReconnectReleasePath)
    }

    if (-not [string]::IsNullOrWhiteSpace($CompletionReleasePath)) {
        $arguments += @('--completion-release-path', $CompletionReleasePath)
    }

    if (-not [string]::IsNullOrWhiteSpace($NetworkControlPath)) {
        $arguments += @('--network-control-path', $NetworkControlPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($MetricsOutputPath)) {
        $arguments += @(
            '--metrics-output', $MetricsOutputPath,
            '--metrics-sample-interval-ms', $MetricsSampleIntervalMs)
    }

    if ($ClientMode -eq 'join') {
        if ([string]::IsNullOrWhiteSpace($RoomId)) {
            throw 'RoomId is required for join client mode.'
        }

        $arguments += @('--room-id', $RoomId)
    }

    return $arguments
}

function Start-SmokeClient {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('create', 'join')]
        [string]$ClientMode,
        [Parameter(Mandatory = $true)]
        [string]$ClientId,
        [Parameter(Mandatory = $true)]
        [int]$PlayerId,
        [string]$RoomId,
        [int]$RoomMaxPlayers = 0,
        [int]$BattleDurationFrames = 0,
        [Parameter(Mandatory = $true)]
        [string]$LogPath,
        [Parameter(Mandatory = $true)]
        [string]$ErrLogPath,
        [int]$ClientReconnectCount = 0,
        [int]$ClientRecoverableFailureCount = 0,
        [int]$LatencyMs = 0,
        [int]$JitterMs = 0,
        [double]$PacketLossRate = 0,
        [int]$BandwidthBytesPerSecond = 0,
        [int]$NetworkSeed = 0,
        [string]$ReplayOutputPath = '',
        [string]$ReconnectReleasePath = '',
        [string]$CompletionReleasePath = '',
        [string]$NetworkControlPath = '',
        [string]$MetricsOutputPath = '',
        [int]$MetricsSampleIntervalMs = 1000,
        [int]$ClientTimeoutSeconds = $TimeoutSeconds,
        [Parameter(Mandatory = $true)]
        [string]$CorrelationId,
        [Parameter(Mandatory = $true)]
        [string]$DiagnosticOutputPath
    )

    $arguments = New-ClientArguments -ClientMode $ClientMode -ClientId $ClientId -PlayerId $PlayerId -RoomId $RoomId -RoomMaxPlayers $RoomMaxPlayers -BattleDurationFrames $BattleDurationFrames -ClientReconnectCount $ClientReconnectCount -ClientRecoverableFailureCount $ClientRecoverableFailureCount -LatencyMs $LatencyMs -JitterMs $JitterMs -PacketLossRate $PacketLossRate -BandwidthBytesPerSecond $BandwidthBytesPerSecond -NetworkSeed $NetworkSeed -ReplayOutputPath $ReplayOutputPath -ReconnectReleasePath $ReconnectReleasePath -CompletionReleasePath $CompletionReleasePath -NetworkControlPath $NetworkControlPath -MetricsOutputPath $MetricsOutputPath -MetricsSampleIntervalMs $MetricsSampleIntervalMs -ClientTimeoutSeconds $ClientTimeoutSeconds -CorrelationId $CorrelationId -DiagnosticOutputPath $DiagnosticOutputPath
    $startedAtUtc = [DateTime]::UtcNow
    $process = Start-DotnetProcess -Arguments $arguments -StdOut $LogPath -StdErr $ErrLogPath
    $startedProcesses.Add($process)
    Register-RunProcess -Role "client-$ClientMode-$ClientId" -ProcessId $process.Id -CorrelationId $CorrelationId -StdOutPath $LogPath -StdErrPath $ErrLogPath
    Write-RunManifest -Status 'running'

    return [pscustomobject]@{
        Mode = $ClientMode
        ClientId = $ClientId
        CorrelationId = $CorrelationId
        PlayerId = $PlayerId
        LogPath = $LogPath
        Process = $process
        StartedAtUtc = $startedAtUtc
        ReconnectCount = $ClientReconnectCount
        RecoverableFailureCount = $ClientRecoverableFailureCount
        LatencyMs = $LatencyMs
        JitterMs = $JitterMs
        PacketLossRate = $PacketLossRate
        BandwidthBytesPerSecond = $BandwidthBytesPerSecond
        ReplayOutputPath = $ReplayOutputPath
        DiagnosticOutputPath = $DiagnosticOutputPath
        NetworkControlPath = $NetworkControlPath
        NetworkControlAckPath = if ([string]::IsNullOrWhiteSpace($NetworkControlPath)) { '' } else { "$NetworkControlPath.ack.json" }
        MetricsOutputPath = $MetricsOutputPath
    }
}

function Wait-ForClientReconnectReady {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Client
    )

    $line = Wait-ForResultLine -Path $Client.LogPath -Prefix 'SHOOTER_MP_CLIENT_RECONNECT_READY' -TimeoutSeconds $TimeoutSeconds
    return [pscustomobject]@{
        Client = $Client
        Line = $line
        Fields = ConvertFrom-ClientResultLine -Line $line
    }
}

function Wait-ForClientReady {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Client
    )

    $line = Wait-ForResultLine -Path $Client.LogPath -Prefix 'SHOOTER_MP_CLIENT_READY' -TimeoutSeconds $SetupTimeoutSeconds
    return [pscustomobject]@{
        Client = $Client
        Line = $line
        Fields = ConvertFrom-ClientResultLine -Line $line
    }
}

function Wait-ForClientCompletionReady {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Client
    )

    return Wait-ForResultLine -Path $Client.LogPath -Prefix 'SHOOTER_MP_CLIENT_COMPLETION_READY' -TimeoutSeconds $SetupTimeoutSeconds
}

function Wait-ForClientResult {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Client
    )

    $line = Wait-ForResultLine -Path $Client.LogPath -Prefix 'SHOOTER_MP_CLIENT_RESULT' -TimeoutSeconds $TimeoutSeconds
    return [pscustomobject]@{
        Client = $Client
        Line = $line
        Fields = ConvertFrom-ClientResultLine -Line $line
    }
}

function Assert-ClientResult {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$ClientResult
    )

    $line = $ClientResult.Line
    $fields = $ClientResult.Fields
    $client = $ClientResult.Client
    $expectedEntryKind = if ($client.Mode -eq 'create') { 'TeamLobby' } elseif ($client.ReconnectCount -gt 0) { 'Reconnect' } else { 'LateJoin' }

    if ((Read-ResultValue -Fields $fields -Name 'status') -ne 'pass') {
        throw "Client result did not pass: $line"
    }

    if ((Read-ResultValue -Fields $fields -Name 'mode') -ne $client.Mode) {
        throw "Client mode mismatch. Expected=$($client.Mode), Line=$line"
    }

    if ((Read-ResultValue -Fields $fields -Name 'clientId') -ne $client.ClientId) {
        throw "Client id mismatch. Expected=$($client.ClientId), Line=$line"
    }

    $actualPlayerId = Read-ResultInt -Fields $fields -Name 'playerId'
    if ($isSoakRun) {
        if ($actualPlayerId -lt 1 -or $actualPlayerId -gt ($JoinClients + 1)) {
            throw "Soak client player id is outside the room range. ExpectedRange=1..$($JoinClients + 1), Actual=$actualPlayerId, Line=$line"
        }
    }
    elseif ($actualPlayerId -ne $client.PlayerId) {
        throw "Player id mismatch. Expected=$($client.PlayerId), Actual=$actualPlayerId, Line=$line"
    }

    if ((Read-ResultValue -Fields $fields -Name 'entryKind') -ne $expectedEntryKind) {
        throw "Client entry kind mismatch. Expected=$expectedEntryKind, Line=$line"
    }

    if (-not $WaitForMatchEnd -and $PayloadMode -eq 'packed' -and -not (Read-ResultBool -Fields $fields -Name 'snapshotHashMatched')) {
        throw "Client snapshot hash validation failed: $line"
    }

    if ((Read-ResultValue -Fields $fields -Name 'payloadMode') -ne $PayloadMode) {
        throw "Client payload mode mismatch. Expected=$PayloadMode, Line=$line"
    }

    Assert-ClientPayloadResult -ClientResult $ClientResult
    Assert-ClientTimeAnchorResult -ClientResult $ClientResult
    Assert-ClientLagCompensationResult -ClientResult $ClientResult

    if (Read-ResultBool -Fields $fields -Name 'shouldResync') {
        throw "Client requested resync during multiprocess sync acceptance: $line"
    }

    if ((Read-ResultInt -Fields $fields -Name 'runtimeFrame') -le 0 -or (Read-ResultInt -Fields $fields -Name 'viewFrame') -le 0) {
        throw "Client runtime/presentation did not advance: $line"
    }

    if ((Read-ResultInt -Fields $fields -Name 'localRuntimeFrame') -ne (Read-ResultInt -Fields $fields -Name 'runtimeFrame') -or (Read-ResultInt -Fields $fields -Name 'localViewFrame') -ne (Read-ResultInt -Fields $fields -Name 'viewFrame')) {
        throw "Client local runtime/view frame aliases diverged from final frame fields: $line"
    }

    $actualInputs = Read-ResultInt -Fields $fields -Name 'inputs'
    if ($WaitForMatchEnd -and $client.Mode -eq 'create') {
        if ($actualInputs -lt $Inputs) {
            throw "Client input count is lower than expected. ExpectedAtLeast=$Inputs, Line=$line"
        }
    }
    elseif ($actualInputs -ne $Inputs) {
        throw "Client input count mismatch. Expected=$Inputs, Line=$line"
    }

    if ($Inputs -gt 0 -and -not (Read-ResultBool -Fields $fields -Name 'lastInputSuccess')) {
        throw "Client last input did not succeed: $line"
    }

    if ($Inputs -gt 0 -and (Read-ResultInt -Fields $fields -Name 'lastAcceptedFrame') -lt (Read-ResultInt -Fields $fields -Name 'lastRequestedFrame')) {
        throw "Client accepted frame regressed: $line"
    }

    if ($Inputs -gt 0 -and (Read-ResultInt64 -Fields $fields -Name 'lastServerTicks') -le 0) {
        throw "Client input response did not include positive server ticks: $line"
    }

    if ((Read-ResultInt -Fields $fields -Name 'entities') -lt $actualPlayerId) {
        throw "Client snapshot entity count is lower than expected player visibility. ExpectedAtLeast=$actualPlayerId, Line=$line"
    }

    if ($client.ReconnectCount -gt 0) {
        if ((Read-ResultInt -Fields $fields -Name 'reconnectCount') -ne $client.ReconnectCount) {
            throw "Client reconnect count mismatch. Expected=$($client.ReconnectCount), Line=$line"
        }

        $expectedAttempts = $client.ReconnectCount + $client.RecoverableFailureCount
        if ((Read-ResultInt -Fields $fields -Name 'retryAttemptCount') -ne $expectedAttempts) {
            throw "Client retry attempt count mismatch. Expected=$expectedAttempts, Line=$line"
        }

        if ((Read-ResultInt -Fields $fields -Name 'injectedFailureCount') -ne $client.RecoverableFailureCount) {
            throw "Client injected failure count mismatch. Expected=$($client.RecoverableFailureCount), Line=$line"
        }

        if ((Read-ResultValue -Fields $fields -Name 'reconnectEntryKind') -ne 'Reconnect') {
            throw "Client reconnect entry kind mismatch: $line"
        }

        if ((Read-ResultInt -Fields $fields -Name 'reconnectPushesAfter') -le (Read-ResultInt -Fields $fields -Name 'reconnectPushesBefore')) {
            throw "Client did not receive a snapshot after reconnect: $line"
        }
    }
    else {
        if ((Read-ResultInt -Fields $fields -Name 'reconnectCount') -ne 0 -or
            (Read-ResultInt -Fields $fields -Name 'retryAttemptCount') -ne 0 -or
            (Read-ResultInt -Fields $fields -Name 'injectedFailureCount') -ne 0) {
            throw "Client unexpectedly entered reconnect/retry flow: $line"
        }
    }

    if ($client.LatencyMs -gt 0 -or $client.JitterMs -gt 0) {
        if ((Read-ResultInt -Fields $fields -Name 'conditionInboundDelayed') -le 0) {
            throw "Client network latency/jitter condition did not delay any inbound push: $line"
        }
    }

    if ($client.PacketLossRate -gt 0) {
        if ((Read-ResultInt -Fields $fields -Name 'conditionInboundDropped') -le 0) {
            throw "Client network packet loss condition did not drop any inbound push: $line"
        }
    }

    $replayPath = Read-ResultValue -Fields $fields -Name 'inputStateReplayPath'
    $minimizedReplayPath = Read-ResultValue -Fields $fields -Name 'minimizedInputStateReplayPath'
    if (-not $NoReplay) {
        if ([string]::IsNullOrWhiteSpace($replayPath)) {
            throw "Client did not report replay path: $line"
        }

        if ([string]::IsNullOrWhiteSpace($minimizedReplayPath)) {
            throw "Client did not report minimized replay path: $line"
        }

        if (-not (Test-Path $replayPath)) {
            throw "Client replay record file was not created: $replayPath"
        }

        if (-not (Test-Path $minimizedReplayPath)) {
            throw "Client minimized replay record file was not created: $minimizedReplayPath"
        }

        $replayFile = Get-Item -LiteralPath $replayPath
        if ($replayFile.Length -le 0) {
            throw "Client replay record file is empty: $replayPath"
        }

        $minimizedReplayFile = Get-Item -LiteralPath $minimizedReplayPath
        if ($minimizedReplayFile.Length -le 0) {
            throw "Client minimized replay record file is empty: $minimizedReplayPath"
        }

        if (-not (Read-ResultBool -Fields $fields -Name 'inputStateReplayConsumed')) {
            throw "Input-state replay record was not consumed by validation: $line"
        }

        if ((Read-ResultInt -Fields $fields -Name 'inputStateReplaySnapshots') -le 0) {
            throw "Input-state replay record did not include snapshots: $line"
        }

        if ((Read-ResultInt -Fields $fields -Name 'inputStateReplayHashes') -ne 0) {
            throw "Minimized input-state replay record should not include state hashes: $line"
        }
    }

    if ($WaitForMatchEnd) {
        if (-not (Read-ResultBool -Fields $fields -Name 'matchFinal')) {
            throw "Client did not observe final match state: $line"
        }

        $matchState = Read-ResultInt -Fields $fields -Name 'matchState'
        if ($matchState -ne 2 -and $matchState -ne 3 -and $matchState -ne 4) {
            throw "Client final match state is invalid: $line"
        }

        if ((Read-ResultInt -Fields $fields -Name 'matchCompletedFrame') -le 0) {
            throw "Client final match completed frame is invalid: $line"
        }

        if ((Read-ResultInt -Fields $fields -Name 'timeLimitFrames') -le 0) {
            throw "Client final match time limit is invalid: $line"
        }

        if ((Read-ResultInt -Fields $fields -Name 'pushes') -le 1) {
            throw "Client did not receive continuous snapshot pushes before final state: $line"
        }
    }

    Write-Host $line -ForegroundColor Green
}

function Assert-ClientPayloadResult {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$ClientResult
    )

    $line = $ClientResult.Line
    $fields = $ClientResult.Fields
    $payloadOpCode = Read-ResultInt -Fields $fields -Name 'payloadOpCode'
    $payloadKind = Read-ResultInt -Fields $fields -Name 'payloadKind'
    $sourceFrame = Read-ResultInt -Fields $fields -Name 'sourceFrame'
    $baselineFrame = Read-ResultInt -Fields $fields -Name 'baselineFrame'
    $lastPushFrame = Read-ResultInt -Fields $fields -Name 'lastPushFrame'
    $pushes = Read-ResultInt -Fields $fields -Name 'pushes'
    $visibilityHints = Read-ResultInt -Fields $fields -Name 'visibilityHints'
    $entities = Read-ResultInt -Fields $fields -Name 'entities'
    $fullBaselinesApplied = Read-ResultInt -Fields $fields -Name 'pureStateFullBaselinesApplied'
    $deltasApplied = Read-ResultInt -Fields $fields -Name 'pureStateDeltasApplied'
    $resyncRequests = Read-ResultInt -Fields $fields -Name 'pureStateResyncRequests'
    $lastResyncNeeded = Read-ResultBool -Fields $fields -Name 'pureStateLastResyncNeeded'
    $snapshotHashMatched = Read-ResultBool -Fields $fields -Name 'snapshotHashMatched'
    $diffStatus = Read-ResultValue -Fields $fields -Name 'diffStatus'

    if ($PayloadMode -eq 'pure-state') {
        if ($payloadOpCode -ne 5207 -and $payloadOpCode -ne 5208) {
            throw "PureState payload op code mismatch: $line"
        }

        if ($payloadKind -ne 1 -and $payloadKind -ne 2 -and $payloadKind -ne 3) {
            throw "PureState payload kind is invalid: $line"
        }

        if ($payloadOpCode -eq 5207 -and $payloadKind -ne 1) {
            throw "PureState full payload did not report full baseline kind: $line"
        }

        if ($payloadOpCode -eq 5208 -and $payloadKind -ne 2 -and $payloadKind -ne 3) {
            throw "PureState delta payload did not report delta or low-frequency kind: $line"
        }

        if ($sourceFrame -le 0) {
            throw "PureState source frame is invalid: $line"
        }

        if ($baselineFrame -lt 0) {
            throw "PureState baseline frame is invalid: $line"
        }

        if (($payloadKind -eq 2 -or $payloadKind -eq 3) -and $baselineFrame -le 0) {
            throw "PureState delta baseline frame was not reported: $line"
        }

        if ($visibilityHints -lt 0) {
            throw "PureState visibility hint count is negative: $line"
        }

        if ($visibilityHints -ne $entities) {
            throw "PureState visibility hints should match exported entity count for current payload logic: $line"
        }

        if ($fullBaselinesApplied -lt 1) {
            throw "PureState full baseline was not applied: $line"
        }

        $hasStrictPureStateActivity =
            ($deltasApplied + $resyncRequests + $fullBaselinesApplied) -ge 2
        $hasConvergedSoakFinalFrame =
            $isSoakRun `
            -and $pushes -ge 2 `
            -and -not $lastResyncNeeded `
            -and $snapshotHashMatched `
            -and $diffStatus -eq 'Identical' `
            -and $sourceFrame -eq $baselineFrame `
            -and $sourceFrame -eq $lastPushFrame

        if (-not $hasStrictPureStateActivity -and
            -not $hasConvergedSoakFinalFrame) {
            throw "PureState did not apply later state or prove repeated delivery and convergence at the soak final frame: $line"
        }

        if ($lastResyncNeeded -and $resyncRequests -le 0) {
            throw "PureState last resync state was reported without any resync request: $line"
        }
    }
    else {
        if ($payloadOpCode -ne 5204) {
            throw "Packed payload op code mismatch: $line"
        }

        if ($payloadKind -ne 0 -or $visibilityHints -ne 0) {
            throw "Packed payload should not report PureState metadata: $line"
        }
    }
}

function Assert-ClientTimeAnchorResult {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$ClientResult
    )

    $line = $ClientResult.Line
    $fields = $ClientResult.Fields
    $remoteAnchorValid = Read-ResultBool -Fields $fields -Name 'remoteAnchorValid'
    $targetFrame = Read-ResultInt -Fields $fields -Name 'targetFrame'
    $remoteTargetFrame = Read-ResultInt -Fields $fields -Name 'remoteTargetFrame'
    $remoteCatchUpFrames = Read-ResultInt -Fields $fields -Name 'remoteCatchUpFrames'
    $remoteElapsedSeconds = Read-ResultDouble -Fields $fields -Name 'remoteElapsedSeconds'
    $remoteServerTicks = Read-ResultInt64 -Fields $fields -Name 'remoteServerTicks'
    $snapshotServerTicks = Read-ResultInt64 -Fields $fields -Name 'snapshotServerTicks'
    $lastPushServerTicks = Read-ResultInt64 -Fields $fields -Name 'lastPushServerTicks'
    $lastPushPackedServerTick = Read-ResultInt64 -Fields $fields -Name 'lastPushPackedServerTick'
    $runtimeFrame = Read-ResultInt -Fields $fields -Name 'runtimeFrame'
    $viewFrame = Read-ResultInt -Fields $fields -Name 'viewFrame'
    $timeLimitFrames = Read-ResultInt -Fields $fields -Name 'timeLimitFrames'
    $reconnectCount = Read-ResultInt -Fields $fields -Name 'reconnectCount'
    $lastPushFrame = Read-ResultInt -Fields $fields -Name 'lastPushFrame'

    if ($remoteAnchorValid) {
        if ($remoteServerTicks -le 0) {
            throw "Remote time anchor did not include positive server ticks: $line"
        }

        if ($remoteTargetFrame -lt 0) {
            throw "Remote target frame is invalid: $line"
        }

        if ($remoteCatchUpFrames -lt 0) {
            throw "Remote catch-up frame count is invalid: $line"
        }

        if ($remoteElapsedSeconds -lt 0) {
            throw "Remote elapsed seconds is invalid: $line"
        }
    }

    if ($targetFrame -ne $remoteTargetFrame) {
        throw "Remote target frame diverged from launch target frame: $line"
    }

    if ($snapshotServerTicks -le 0 -or $lastPushServerTicks -le 0) {
        throw "Snapshot push server ticks were not reported: $line"
    }

    if ($lastPushServerTicks -lt $snapshotServerTicks) {
        throw "Last push server ticks regressed behind first applied snapshot: $line"
    }

    if ($lastPushPackedServerTick -le 0) {
        throw "Last push packed/server payload tick was not reported: $line"
    }

    $reachableTargetFrame = if ($reconnectCount -gt 0) {
        $lastPushFrame
    }
    else {
        $remoteTargetFrame
    }
    if ($timeLimitFrames -gt 0 -and $timeLimitFrames -lt $reachableTargetFrame) {
        $reachableTargetFrame = $timeLimitFrames
    }

    if ($runtimeFrame -lt $reachableTargetFrame) {
        throw "Final runtime frame did not catch up to reachable target frame: $line"
    }

    # Authoritative interpolation cannot present beyond the latest received snapshot.
    $reachableViewFrame = [Math]::Min($reachableTargetFrame, $lastPushFrame)
    if ($viewFrame -lt $reachableViewFrame) {
        throw "Final view frame did not consume the reachable authoritative snapshot frame: $line"
    }
}

function Assert-ClientLagCompensationResult {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$ClientResult
    )

    $line = $ClientResult.Line
    $fields = $ClientResult.Fields
    $accepted = Read-ResultBool -Fields $fields -Name 'lagCompAccepted'
    $reason = Read-ResultValue -Fields $fields -Name 'lagCompReason'
    $requestedFrame = Read-ResultInt -Fields $fields -Name 'lagCompRequestedFrame'
    $resolvedFrame = Read-ResultInt -Fields $fields -Name 'lagCompResolvedFrame'
    $hitEntityId = Read-ResultInt -Fields $fields -Name 'lagCompHitEntityId'
    $runtimeFrame = Read-ResultInt -Fields $fields -Name 'runtimeFrame'

    $acceptableReasons = @('Hit', 'HistoryUnavailable', 'RewindWindowExceeded')
    if (-not $accepted -and -not ($acceptableReasons -contains $reason)) {
        throw "Lag compensation result was not accepted and did not report an acceptable reason: $line"
    }

    if ($requestedFrame -lt 0) {
        throw "Lag compensation requested frame is invalid: $line"
    }

    if ($accepted) {
        if ($reason -ne 'Hit') {
            throw "Accepted lag compensation result did not report Hit: $line"
        }

        if ($hitEntityId -le 0) {
            throw "Accepted lag compensation result did not report a hit entity: $line"
        }

        if ($resolvedFrame -lt 0 -or $resolvedFrame -gt $runtimeFrame) {
            throw "Accepted lag compensation resolved frame is outside the runtime window: $line"
        }
    }
}

function Assert-ClientResultSet {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$ClientResults
    )

    if ($ClientResults.Count -ne ($JoinClients + 1)) {
        throw "Client result count mismatch. Expected=$($JoinClients + 1), Actual=$($ClientResults.Count)."
    }

    if ($isSoakRun) {
        $actualPlayerIds = @(
            $ClientResults |
                ForEach-Object { Read-ResultInt -Fields $_.Fields -Name 'playerId' } |
                Sort-Object
        )
        $expectedPlayerIds = @(1..($JoinClients + 1))
        if (($actualPlayerIds -join ',') -ne ($expectedPlayerIds -join ',')) {
            throw "Soak client player ids did not uniquely cover the room player range. Expected=$($expectedPlayerIds -join ','), Actual=$($actualPlayerIds -join ',')."
        }
    }

    $roomId = Read-ResultValue -Fields $ClientResults[0].Fields -Name 'roomId'
    $battleId = Read-ResultValue -Fields $ClientResults[0].Fields -Name 'battleId'
    $worldId = Read-ResultValue -Fields $ClientResults[0].Fields -Name 'worldId'

    foreach ($clientResult in $ClientResults) {
        $line = $clientResult.Line
        if ((Read-ResultValue -Fields $clientResult.Fields -Name 'roomId') -ne $roomId) {
            throw "Client room id mismatch: $line"
        }

        if ((Read-ResultValue -Fields $clientResult.Fields -Name 'battleId') -ne $battleId) {
            throw "Client battle id mismatch: $line"
        }

        if ((Read-ResultValue -Fields $clientResult.Fields -Name 'worldId') -ne $worldId) {
            throw "Client world id mismatch: $line"
        }
    }

    if ($WaitForMatchEnd) {
        $stateHash = Read-ResultValue -Fields $ClientResults[0].Fields -Name 'stateHash'
        $runtimeFrame = Read-ResultInt -Fields $ClientResults[0].Fields -Name 'runtimeFrame'
        $matchState = Read-ResultInt -Fields $ClientResults[0].Fields -Name 'matchState'
        $matchCompletedFrame = Read-ResultInt -Fields $ClientResults[0].Fields -Name 'matchCompletedFrame'
        $matchVictory = Read-ResultBool -Fields $ClientResults[0].Fields -Name 'matchVictory'
 
        foreach ($clientResult in $ClientResults) {
            $line = $clientResult.Line
            if ((Read-ResultValue -Fields $clientResult.Fields -Name 'stateHash') -ne $stateHash) {
                throw "Client final state hash mismatch: $line"
            }

            if ((Read-ResultInt -Fields $clientResult.Fields -Name 'runtimeFrame') -ne $runtimeFrame) {
                throw "Client final runtime frame mismatch: $line"
            }

            if ((Read-ResultInt -Fields $clientResult.Fields -Name 'matchState') -ne $matchState) {
                throw "Client final match state mismatch: $line"
            }

            if ((Read-ResultInt -Fields $clientResult.Fields -Name 'matchCompletedFrame') -ne $matchCompletedFrame) {
                throw "Client final match completed frame mismatch: $line"
            }

            if ((Read-ResultBool -Fields $clientResult.Fields -Name 'matchVictory') -ne $matchVictory) {
                throw "Client final match victory mismatch: $line"
            }
        }
    }
}

function Assert-ClientExitCode {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Client
    )

    if (-not $Client.Process.WaitForExit($TimeoutSeconds * 1000)) {
        throw "Client process $($Client.Process.Id) did not exit within $TimeoutSeconds seconds."
    }

    $exitCode = Get-ProcessExitCode -Process $Client.Process
    if ($exitCode -ne 0) {
        throw "Client process $($Client.Process.Id) exited with code $exitCode."
    }
}

if (-not (Test-Path $project)) {
    throw "Shooter smoke project was not found: $project"
}

if ($JoinClients -lt 0) {
    throw 'JoinClients must be >= 0.'
}

$ports = @($TcpPort, $SiloPort, $OrleansGatewayPort)
foreach ($port in $ports) {
    if ($port -le 0 -or $port -gt 65535) {
        throw "Smoke ports must be between 1 and 65535. Invalid port: $port"
    }
}
if (($ports | Sort-Object -Unique).Count -ne $ports.Count) {
    throw 'TcpPort, SiloPort, and OrleansGatewayPort must be distinct.'
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
New-Item -ItemType Directory -Force -Path $replayDir | Out-Null
New-Item -ItemType Directory -Force -Path $diagnosticDir | Out-Null
if ($isSoakRun) {
    New-Item -ItemType Directory -Force -Path $soakControlDir | Out-Null
    New-Item -ItemType Directory -Force -Path $soakTelemetryDir | Out-Null
}
Register-RunProcess -Role 'orchestrator' -ProcessId $PID -CorrelationId "$RunId/shooter-mp-orchestrator"
Write-RunManifest -Status 'running'

$commonArgs = @(
    '-p:UseSharedCompilation=false',
    '-p:nodeReuse=false'
)

try {
    if (-not $NoCleanup) {
        Stop-AbilityKitServices `
            -Ports $ports `
            -GraceSeconds 2
    }

    if (-not $NoBuild) {
        Write-Host 'Building Shooter smoke project...' -ForegroundColor Cyan
        dotnet build $project -c $Configuration @commonArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Shooter smoke project build failed with exit code $LASTEXITCODE."
        }
    }

    if (-not (Test-Path -LiteralPath $applicationDll -PathType Leaf)) {
        throw "Shooter smoke application artifact was not found: $applicationDll"
    }

    $serverArgs = @($applicationDll)
    $serverArgs += @(
        '--server',
        '--tcp-port', $TcpPort,
        '--state-sync-payload-mode', $PayloadMode,
        '--fault-control-path', $faultControlPath,
        '--AbilityKit:Orleans:SiloPort', $SiloPort,
        '--AbilityKit:Orleans:GatewayPort', $OrleansGatewayPort,
        '--AbilityKit:Orleans:PrimarySiloPort', $SiloPort)
    if ($activePlan.slowConsumer) {
        $serverArgs += @(
            '--AbilityKit:StateSyncObserver:BytesPerSecond', 256,
            '--AbilityKit:StateSyncObserver:BurstBytes', 32768,
            '--AbilityKit:StateSyncObserver:MaxQueueLength', 1,
            '--AbilityKit:StateSyncObserver:MaxQueueAgeMs', 100,
            '--AbilityKit:StateSyncObserver:DrainIntervalMs', 250)
    }

    Write-Host "Starting Shooter state-sync server on 127.0.0.1:$TcpPort..." -ForegroundColor Cyan
    $serverStartedAtUtc = [DateTime]::UtcNow
    $serverErrorLog = Join-Path $logDir 'server.err.log'
    $server = Start-DotnetProcess -Arguments $serverArgs -StdOut $serverLog -StdErr $serverErrorLog
    $startedProcesses.Add($server)
    Register-RunProcess -Role 'server' -ProcessId $server.Id -CorrelationId $serverCorrelationId -StdOutPath $serverLog -StdErrPath $serverErrorLog
    Write-RunManifest -Status 'running'
    $processTimeline += [ordered]@{ role = 'server'; processId = $server.Id; startedAtUtc = $serverStartedAtUtc.ToString('O'); exitedAtUtc = $null; exitCode = $null }
    Wait-ForPort -Port $TcpPort -TimeoutSeconds $StartupTimeoutSeconds
    Add-AssertionResult -Name 'server-listening' -Passed $true -Details "127.0.0.1:$TcpPort"
    $timeoutPhase = 'setup'
    $timeoutBudgetSeconds = $SetupTimeoutSeconds
    $scenarioDeadlineUtc = [DateTime]::UtcNow.AddSeconds($SetupTimeoutSeconds)
    Write-RunManifest -Status 'running'

    $clientTimeoutSeconds = if ($isSoakRun) {
        [Math]::Max($TimeoutSeconds, [int]$activePlan.executionTimeoutSeconds)
    }
    else {
        $TimeoutSeconds
    }

    Write-Host 'Starting primary create client...' -ForegroundColor Cyan
    $createReplayPath = if ($NoReplay) { '' } else { Join-Path $replayDir "input-state-create$ReplayExtension" }
    $createCorrelationId = "$RunId/shooter-mp-create"
    $createControlPath = if ($isSoakRun) { Join-Path $soakControlDir 'client-create.json' } else { '' }
    $createMetricsPath = if ($isSoakRun) { Join-Path $soakTelemetryDir 'client-create.jsonl' } else { '' }
    $createClient = Start-SmokeClient `
        -ClientMode 'create' `
        -ClientId 'shooter-mp-create' `
        -PlayerId 1 `
        -RoomMaxPlayers ($JoinClients + 1) `
        -BattleDurationFrames $(if ($isSoakRun) { [int][Math]::Min([int]::MaxValue, [long]$activePlan.executionTimeoutSeconds * 60) } else { 0 }) `
        -LogPath (Join-Path $logDir 'client-create.log') `
        -ErrLogPath (Join-Path $logDir 'client-create.err.log') `
        -ReplayOutputPath $createReplayPath `
        -CompletionReleasePath $(if ($Scenario -eq 'slow-consumer' -or $isSoakRun) { $completionReleasePath } else { '' }) `
        -NetworkControlPath $createControlPath `
        -MetricsOutputPath $createMetricsPath `
        -MetricsSampleIntervalMs $SoakMetricsSampleIntervalMs `
        -ClientTimeoutSeconds $clientTimeoutSeconds `
        -CorrelationId $createCorrelationId `
        -DiagnosticOutputPath (Join-Path $diagnosticDir 'client-create.diagnostic.json')
    $clientLogs += $createClient.LogPath

    $createReady = Wait-ForClientReady -Client $createClient
    $roomId = Read-ResultValue -Fields $createReady.Fields -Name 'roomId'
    $scenarioEstablished = $true
    Add-AssertionResult -Name 'battle-established' -Passed $true -Details "RoomId=$roomId"
    Write-RunManifest -Status 'running'
    Write-Host "Primary client ready. RoomId=$roomId" -ForegroundColor Green

    $clients = New-Object System.Collections.Generic.List[object]
    $clientResults = New-Object System.Collections.Generic.List[object]
    $clients.Add($createClient)
    for ($i = 1; $i -le $JoinClients; $i++) {
        $playerId = $i + 1
        Write-Host "Starting join client $i as player $playerId..." -ForegroundColor Cyan
        $joinReplayPath = if ($NoReplay) { '' } else { Join-Path $replayDir "input-state-join-$i$ReplayExtension" }
        $joinCorrelationId = "$RunId/shooter-mp-join-$i"
        $joinControlPath = if ($isSoakRun) { Join-Path $soakControlDir "client-join-$i.json" } else { '' }
        $joinMetricsPath = if ($isSoakRun) { Join-Path $soakTelemetryDir "client-join-$i.jsonl" } else { '' }
        $joinClient = Start-SmokeClient `
            -ClientMode 'join' `
            -ClientId "shooter-mp-join-$i" `
            -PlayerId $playerId `
            -RoomId $roomId `
            -LogPath (Join-Path $logDir "client-join-$i.log") `
            -ErrLogPath (Join-Path $logDir "client-join-$i.err.log") `
            -ClientReconnectCount $(if ($i -eq 1) { $ReconnectCount } else { 0 }) `
            -ClientRecoverableFailureCount $(if ($i -eq 1) { $RecoverableFailureCount } else { 0 }) `
            -LatencyMs $ConditionLatencyMs `
            -JitterMs $ConditionJitterMs `
            -PacketLossRate $ConditionPacketLossRate `
            -NetworkSeed ($ConditionSeed + $i) `
            -ReplayOutputPath $joinReplayPath `
            -ReconnectReleasePath $(if (($Scenario -eq 'gateway-offline' -or $Scenario -eq 'observer-reactivation') -and $i -eq 1) { $reconnectReleasePath } else { '' }) `
            -CompletionReleasePath $(if ($Scenario -eq 'slow-consumer' -or $isSoakRun) { $completionReleasePath } else { '' }) `
            -NetworkControlPath $joinControlPath `
            -MetricsOutputPath $joinMetricsPath `
            -MetricsSampleIntervalMs $SoakMetricsSampleIntervalMs `
            -ClientTimeoutSeconds $clientTimeoutSeconds `
            -CorrelationId $joinCorrelationId `
            -DiagnosticOutputPath (Join-Path $diagnosticDir "client-join-$i.diagnostic.json")
        $clientLogs += $joinClient.LogPath
        $clients.Add($joinClient)
    }

    if ($OwnershipCleanupProbe) {
        Add-AssertionResult -Name 'ownership-cleanup-probe-armed' -Passed $true -Details "Processes=$($manifestProcesses.Count)"
        Write-RunManifest -Status 'running'
        while ($true) {
            Start-Sleep -Seconds 1
        }
    }

    $joinReadyResults = @{}
    for ($i = 1; $i -lt $clients.Count; $i++) {
        $joinReady = Wait-ForClientReady -Client $clients[$i]
        $joinReadyResults[$i] = $joinReady
        Add-AssertionResult -Name "join-$i-ready" -Passed $true -Details $joinReady.Line
    }
    if ($isSoakRun) {
        foreach ($client in $clients) {
            $completionReady = Wait-ForClientCompletionReady -Client $client
            Add-AssertionResult -Name "soak-$($client.ClientId)-completion-ready" -Passed $true -Details $completionReady
        }
    }
    $timeoutPhase = 'active scenario'
    $timeoutBudgetSeconds = $activePlan.timeoutSeconds
    $scenarioDeadlineUtc = [DateTime]::UtcNow.AddSeconds($activePlan.timeoutSeconds)
    Add-AssertionResult -Name 'scenario-active-budget-started' -Passed $true -Details "TimeoutSeconds=$($activePlan.timeoutSeconds)"
    Write-RunManifest -Status 'running'

    if ($isSoakRun) {
        Invoke-ShooterSoakPhases -Clients $clients.ToArray()
        $timeoutPhase = 'soak result collection'
        $timeoutBudgetSeconds = $activePlan.resultTimeoutSeconds
        $scenarioDeadlineUtc = [DateTime]::UtcNow.AddSeconds($activePlan.resultTimeoutSeconds)
        Add-AssertionResult -Name 'soak-result-budget-started' -Passed $true -Details "TimeoutSeconds=$($activePlan.resultTimeoutSeconds)"
        Write-RunManifest -Status 'running'
    }
    elseif ($Scenario -eq 'slow-consumer') {
        Start-Sleep -Seconds 2
        New-Item -ItemType File -Path $completionReleasePath -Force | Out-Null
        Add-AssertionResult -Name 'slow-consumer-pressure-window-completed' -Passed $true -Details 'DurationSeconds=2'
        Write-RunManifest -Status 'running'
    }

    if ($Scenario -eq 'gateway-offline') {
        if ($JoinClients -lt 1) {
            throw 'Gateway offline scenario requires at least one join client.'
        }
        Add-AssertionResult -Name 'join-subscribed-before-fault' -Passed $true -Details 'join-1-ready'
        $reconnectReady = Wait-ForClientReconnectReady -Client $clients[1]
        Add-AssertionResult -Name 'join-inputs-completed-before-fault' -Passed $true -Details $reconnectReady.Line
        $null = Invoke-GatewayFaultCommand -Action 'gateway-offline'
        Add-AssertionResult -Name 'gateway-offline-acknowledged' -Passed $true
        Wait-ForPortClosed -Port $TcpPort -TimeoutSeconds 5
        Add-AssertionResult -Name 'gateway-offline-unreachable' -Passed $true -Details "127.0.0.1:$TcpPort"
        $null = Invoke-GatewayFaultCommand -Action 'gateway-online'
        Wait-ForPort -Port $TcpPort -TimeoutSeconds 10
        Add-AssertionResult -Name 'gateway-online-acknowledged' -Passed $true -Details "127.0.0.1:$TcpPort"
        New-Item -ItemType File -Path $reconnectReleasePath -Force | Out-Null
        Add-AssertionResult -Name 'join-reconnect-released-after-recovery' -Passed $true -Details $reconnectReleasePath
        Write-RunManifest -Status 'running'
    }

    if ($Scenario -eq 'observer-reactivation') {
        if ($JoinClients -lt 1) {
            throw 'Observer reactivation scenario requires at least one join client.'
        }
        $reconnectReady = Wait-ForClientReconnectReady -Client $clients[1]
        Add-AssertionResult -Name 'join-inputs-completed-before-observer-deactivation' -Passed $true -Details $reconnectReady.Line
        $joinAccountId = Read-ResultValue -Fields $joinReadyResults[1].Fields -Name 'accountId'
        $joinRoomId = Read-ResultValue -Fields $joinReadyResults[1].Fields -Name 'roomId'
        $observerKey = "${joinAccountId}:${joinRoomId}"
        $reactivationAck = Invoke-GatewayFaultCommand -Action 'observer-reactivate' -ObserverKey $observerKey
        $reactivation = $reactivationAck.ObserverReactivation
        if ($null -eq $reactivation -or
            [string]::IsNullOrWhiteSpace([string]$reactivation.BeforeActivationToken) -or
            [string]::IsNullOrWhiteSpace([string]$reactivation.AfterActivationToken) -or
            $reactivation.BeforeActivationToken -eq $reactivation.AfterActivationToken) {
            throw "Observer reactivation acknowledgement did not prove an activation-token change. ObserverKey=$observerKey"
        }
        Add-AssertionResult -Name 'observer-reactivation-token-changed' -Passed $true -Details ($reactivation | ConvertTo-Json -Compress)
        New-Item -ItemType File -Path $reconnectReleasePath -Force | Out-Null
        Add-AssertionResult -Name 'join-reconnect-released-after-observer-reactivation' -Passed $true -Details $reconnectReleasePath
        Write-RunManifest -Status 'running'
    }

    foreach ($client in $clients) {
        $clientResult = Wait-ForClientResult -Client $client
        Assert-ClientResult -ClientResult $clientResult
        $clientResults.Add($clientResult)
        $fields = $clientResult.Fields
        $manifestClients += [ordered]@{
            clientId = $client.ClientId
            processId = $client.Process.Id
            correlationId = Read-ResultValue -Fields $fields -Name 'correlationId'
            accountId = Read-ResultValue -Fields $fields -Name 'accountId'
            playerId = Read-ResultInt -Fields $fields -Name 'playerId'
            roomId = Read-ResultValue -Fields $fields -Name 'roomId'
            battleId = Read-ResultValue -Fields $fields -Name 'battleId'
            worldId = Read-ResultValue -Fields $fields -Name 'worldId'
            recordPath = ConvertTo-RunRelativePath -Path (Read-ResultValue -Fields $fields -Name 'inputStateReplayPath')
            diagnosticPath = Read-ResultValue -Fields $fields -Name 'diagnosticArtifactPath'
            diagnosticSha256 = Read-ResultValue -Fields $fields -Name 'diagnosticArtifactSha256'
            diffPath = Read-ResultValue -Fields $fields -Name 'diffPath'
            diffSha256 = Read-ResultValue -Fields $fields -Name 'diffSha256'
            diffStatus = Read-ResultValue -Fields $fields -Name 'diffStatus'
        }
        Write-RunManifest -Status 'running'
    }

    Assert-ClientResultSet -ClientResults $clientResults.ToArray()
    Assert-BoundedConvergence -ClientResults $clientResults.ToArray()
    if ($isSoakRun) {
        $soakSummary = Get-ShooterSoakSummary -Clients $clients.ToArray()
        $soakSummary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $soakSummaryPath -Encoding utf8
        Assert-ShooterSoakSummary -Summary $soakSummary
        Write-RunManifest -Status 'running'
    }

    foreach ($client in $clients) {
        Assert-ClientExitCode -Client $client
        $exitedAtUtc = [DateTime]::UtcNow
        $processTimeline += [ordered]@{
            role = "client-$($client.ClientId)"
            processId = $client.Process.Id
            startedAtUtc = $client.StartedAtUtc.ToString('O')
            exitedAtUtc = $exitedAtUtc.ToString('O')
            exitCode = Get-ProcessExitCode -Process $client.Process
        }
    }

    $mode = if ($WaitForMatchEnd) { 'end-to-end' } elseif ($ReconnectCount -gt 0 -or $ConditionLatencyMs -gt 0 -or $ConditionJitterMs -gt 0 -or $ConditionPacketLossRate -gt 0) { 'resilience' } else { 'sync' }
    $replaySummary = if ($NoReplay) { 'Replay=disabled' } else { "Replay=$replayDir" }
    Add-AssertionResult -Name 'scenario-completed' -Passed $true -Details "Mode=$mode; Clients=$($clients.Count)"
    $manifestStatus = 'passed'
    Write-Host "Shooter multiprocess $mode smoke passed. PayloadMode=$PayloadMode, RoomId=$roomId, Clients=$($clients.Count), Logs=$logDir, Manifest=$manifestPath, $replaySummary" -ForegroundColor Green
}
catch {
    $manifestStatus = 'failed'
    $manifestError = $_.Exception.Message
    Write-Warning "Shooter smoke failure location: $($_.InvocationInfo.PositionMessage)"
    $manifestFailureCategory = Get-FailureClassification -Message $manifestError -Established $scenarioEstablished
    $manifestFailureStage = if ($manifestFailureCategory -eq 'PreconditionFailed') { 'setup' } else { 'fault-recovery' }
    Add-AssertionResult -Name 'scenario-completed' -Passed $false -Details $manifestError
}
finally {
    Stop-StartedProcesses -Processes $startedProcesses
    if ($null -ne $server) {
        $serverTimeline = @($processTimeline | Where-Object { $_.role -eq 'server' } | Select-Object -First 1)
        if ($serverTimeline.Count -gt 0) {
            $server.Refresh()
            if ($server.HasExited) {
                $serverExitCode = Get-ProcessExitCode -Process $server
                $serverTimeline[0].exitedAtUtc = [DateTime]::UtcNow.ToString('O')
                $serverTimeline[0].exitCode = $serverExitCode
            }
        }
    }

    if (-not $NoCleanup) {
        Stop-AbilityKitServices `
            -Ports $ports `
            -GraceSeconds 1
    }

    try {
        Write-RunManifest -Status $manifestStatus -ErrorMessage $manifestError
    }
    catch {
        Write-Warning "Failed to write final manifest without replacing the scenario result: $($_.Exception.Message)"
    }
}

if ($manifestStatus -eq 'failed') {
    [Console]::Error.WriteLine($manifestError)
    exit 1
}
