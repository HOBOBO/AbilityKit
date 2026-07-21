param(
    [switch]$NoBuild,
    [string]$Configuration = 'Release',
    [int]$TcpPort = 44301,
    [int]$SiloPort = 15311,
    [int]$OrleansGatewayPort = 34301,
    [string]$ArtifactRoot = 'artifacts\shooter_multiprocess_ownership_cleanup',
    [int]$GlobalTimeoutSeconds = 35
)

$ErrorActionPreference = 'Stop'

if ($GlobalTimeoutSeconds -lt 20) {
    throw 'GlobalTimeoutSeconds must be at least 20 seconds so all owned roles can register before timeout.'
}

. (Join-Path $PSScriptRoot 'abilitykit_process_utils.ps1')

$runnerPath = Join-Path $PSScriptRoot 'run_shooter_multiprocess_smoke.ps1'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$projectPath = Join-Path $repoRoot 'Server\Orleans\src\AbilityKit.Orleans.ShooterSmoke\AbilityKit.Orleans.ShooterSmoke.csproj'
$artifactRootPath = if ([System.IO.Path]::IsPathRooted($ArtifactRoot)) {
    [System.IO.Path]::GetFullPath($ArtifactRoot)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactRoot))
}
$runId = 'test-01c-{0:yyyyMMdd-HHmmss-fff}-{1}' -f [DateTime]::UtcNow, $PID
$childRunId = "$runId-recoverable-retry"
$childManifestPath = Join-Path (Join-Path $artifactRootPath $childRunId) 'manifest.json'
$matrixManifestPath = Join-Path $artifactRootPath "$runId-matrix.json"
$reportPath = Join-Path $artifactRootPath "$runId-acceptance.json"
$protectionMarker = "abilitykit-test-01c-protected-$runId"
$protectedProcess = $null
$runnerProcess = $null
$assertions = [System.Collections.Generic.List[object]]::new()

function Add-ProbeAssertion {
    param([string]$Name, [bool]$Passed, [string]$Details)

    $assertions.Add([pscustomobject][ordered]@{
        name = $Name
        passed = $Passed
        details = $Details
    })
}

function Test-ProcessExited {
    param([int]$ProcessId)

    return $null -eq (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)
}

New-Item -ItemType Directory -Force -Path $artifactRootPath | Out-Null

try {
    if (-not $NoBuild) {
        Write-Host 'Building Shooter smoke project before the bounded ownership cleanup probe...' -ForegroundColor Cyan
        dotnet build $projectPath -c $Configuration '-p:UseSharedCompilation=false' '-p:nodeReuse=false'
        if ($LASTEXITCODE -ne 0) {
            throw "Shooter smoke project build failed with exit code $LASTEXITCODE."
        }
    }

    $protectedProcess = Start-Process -FilePath 'powershell.exe' `
        -ArgumentList @('-NoProfile', '-Command', "`$host.UI.RawUI.WindowTitle='$protectionMarker'; Start-Sleep -Seconds 300") `
        -PassThru
    $protectedIdentity = Get-AbilityKitProcessIdentity -ProcessId $protectedProcess.Id
    if ($null -eq $protectedIdentity) {
        throw 'Could not capture the unrelated protection process identity.'
    }

    $runnerArguments = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $runnerPath,
        '-Configuration', $Configuration,
        '-Profile', 'minimal',
        '-TcpPort', $TcpPort,
        '-SiloPort', $SiloPort,
        '-OrleansGatewayPort', $OrleansGatewayPort,
        '-ArtifactRoot', $artifactRootPath,
        '-RunId', $runId,
        '-JoinClients', 1,
        '-Inputs', 1,
        '-StartupTimeoutSeconds', 30,
        '-SetupTimeoutSeconds', 30,
        '-ScenarioTimeoutSeconds', 15,
        '-GlobalTimeoutSeconds', $GlobalTimeoutSeconds,
        '-OwnershipCleanupProbe',
        '-NoReplay',
        '-NoBuild')

    $runnerProcess = Start-Process -FilePath 'powershell.exe' -ArgumentList $runnerArguments -PassThru -NoNewWindow
    $outerTimeoutSeconds = $GlobalTimeoutSeconds + 45
    if (-not $runnerProcess.WaitForExit($outerTimeoutSeconds * 1000)) {
        Stop-Process -Id $runnerProcess.Id -Force -ErrorAction SilentlyContinue
        throw "Ownership cleanup probe runner exceeded its outer timeout of $outerTimeoutSeconds seconds."
    }
    $runnerProcess.Refresh()
    Add-ProbeAssertion -Name 'runner-failed-as-expected' -Passed ($runnerProcess.ExitCode -ne 0) -Details "ExitCode=$($runnerProcess.ExitCode)"

    $childManifest = if (Test-Path -LiteralPath $childManifestPath) {
        Get-Content -LiteralPath $childManifestPath -Raw | ConvertFrom-Json
    }
    else {
        $null
    }
    $matrixManifest = if (Test-Path -LiteralPath $matrixManifestPath) {
        Get-Content -LiteralPath $matrixManifestPath -Raw | ConvertFrom-Json
    }
    else {
        $null
    }

    Add-ProbeAssertion -Name 'child-manifest-exists' -Passed ($null -ne $childManifest) -Details $childManifestPath
    Add-ProbeAssertion -Name 'matrix-manifest-exists' -Passed ($null -ne $matrixManifest) -Details $matrixManifestPath
    if ($null -ne $childManifest) {
        $ownedProcesses = @($childManifest.processes)
        $roles = @($ownedProcesses | ForEach-Object { [string]$_.role })
        Add-ProbeAssertion -Name 'all-owned-roles-recorded' `
            -Passed (($roles -contains 'orchestrator') -and ($roles -contains 'server') -and ($roles -contains 'client-create-shooter-mp-create') -and ($roles -contains 'client-join-shooter-mp-join-1')) `
            -Details ($roles -join ',')
        $liveOwned = @($ownedProcesses | Where-Object { -not (Test-ProcessExited -ProcessId ([int]$_.processId)) })
        Add-ProbeAssertion -Name 'owned-processes-exited' -Passed ($liveOwned.Count -eq 0) -Details (($liveOwned.processId) -join ',')
        Add-ProbeAssertion -Name 'child-status-failed' -Passed ([string]$childManifest.status -eq 'failed') -Details ([string]$childManifest.status)
        Add-ProbeAssertion -Name 'failure-stage-preserved' -Passed ([string]$childManifest.failure.stage -eq 'matrix-timeout') -Details ([string]$childManifest.failure.stage)
        $cleanupStatuses = @($childManifest.cleanupEvidence.processes | ForEach-Object { [string]$_.status })
        Add-ProbeAssertion -Name 'cleanup-evidence-recorded' `
            -Passed ($cleanupStatuses.Count -ge 4 -and @($cleanupStatuses | Where-Object { $_ -notin @('terminated', 'already-exited') }).Count -eq 0) `
            -Details ($cleanupStatuses -join ',')
    }

    if ($null -ne $matrixManifest) {
        Add-ProbeAssertion -Name 'matrix-status-failed' -Passed ([string]$matrixManifest.status -eq 'failed') -Details ([string]$matrixManifest.status)
        Add-ProbeAssertion -Name 'matrix-cleanup-evidence-recorded' -Passed (@($matrixManifest.cleanupEvidence).Count -eq 1) -Details "Count=$(@($matrixManifest.cleanupEvidence).Count)"
    }

    foreach ($port in @($TcpPort, $SiloPort, $OrleansGatewayPort)) {
        $released = -not (Test-AbilityKitTcpPort -HostName '127.0.0.1' -Port $port -TimeoutMilliseconds 250)
        Add-ProbeAssertion -Name "port-$port-released" -Passed $released -Details "Released=$released"
    }

    $protectedAlive = -not (Test-ProcessExited -ProcessId $protectedProcess.Id)
    Add-ProbeAssertion -Name 'unrelated-process-survived' -Passed $protectedAlive -Details "PID=$($protectedProcess.Id)"
}
catch {
    Add-ProbeAssertion -Name 'probe-execution' -Passed $false -Details $_.Exception.Message
}
finally {
    if ($null -ne $protectedProcess) {
        Stop-Process -Id $protectedProcess.Id -Force -ErrorAction SilentlyContinue
    }

    $failedAssertions = @($assertions | Where-Object { -not $_.passed })
    [ordered]@{
        schemaVersion = 1
        runId = $runId
        status = if ($failedAssertions.Count -eq 0) { 'passed' } else { 'failed' }
        generatedAtUtc = [DateTime]::UtcNow.ToString('O')
        childManifestPath = $childManifestPath
        matrixManifestPath = $matrixManifestPath
        assertions = @($assertions)
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding utf8
    Write-Host "TEST-01C ownership cleanup acceptance report: $reportPath"
}

if (@($assertions | Where-Object { -not $_.passed }).Count -gt 0) {
    exit 1
}

exit 0
