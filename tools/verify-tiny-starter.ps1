[CmdletBinding()]
param(
    [switch]$SkipUnity,
    [string]$UnityExe = 'C:\Software\Unity 2022.3.62f3\Editor\Unity.exe',
    [int]$SiloPort = 11170,
    [int]$OrleansGatewayPort = 30070,
    [int]$TcpPort = 4058,
    [int]$HttpPort = 5058
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$siloProcess = $null
$gatewayProcess = $null
$project = $null
$previousEnvironment = $env:DOTNET_ENVIRONMENT
$previousArtifactsPath = $env:ArtifactsPath
$previousUseArtifactsOutput = $env:UseArtifactsOutput
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$outputRoot = Join-Path $repositoryRoot "local/Logs/tiny-acceptance-$runId"
[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$env:ArtifactsPath = Join-Path $outputRoot 'artifacts'
$env:UseArtifactsOutput = 'true'
$phaseResults = [ordered]@{}

function Write-Phase([string]$Name, [string]$Status) {
    $phaseResults[$Name] = [ordered]@{
        status = $Status
        updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    $phaseResults | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $outputRoot 'phases.json') -Encoding UTF8
}

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed ($LASTEXITCODE): $($Arguments -join ' ')"
    }
}

function Invoke-TinySmoke([string]$Mode, [string]$Prefix) {
    $logPath = Join-Path $outputRoot "$Mode.log"
    & dotnet run --no-build --project 'src/AbilityKit.Demo.Tiny.Client' -- `
        '127.0.0.1' $TcpPort $Prefix $Mode 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) { throw "Tiny $Mode smoke failed. Log: $logPath" }
}

function Invoke-TinyHybridMismatch([string]$Prefix) {
    $logPath = Join-Path $outputRoot 'hybrid-mismatch.log'
    $evidencePath = Join-Path $outputRoot 'hybrid-mismatch.json'
    & dotnet run --no-build --project 'src/AbilityKit.Demo.Tiny.Client' -- `
        '127.0.0.1' $TcpPort $Prefix 'session-hybrid-mismatch' $evidencePath `
        2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) {
        throw "Tiny Hybrid mismatch smoke failed. Log: $logPath Evidence: $evidencePath"
    }
    if (-not (Test-Path -LiteralPath $evidencePath)) {
        throw "Tiny Hybrid mismatch evidence missing: $evidencePath"
    }
    $evidence = Get-Content -LiteralPath $evidencePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $evidence.passed -or $evidence.correctionsAfterInjection -ne 1 -or
        $evidence.ownerHash -ne $evidence.guestHash -or
        $evidence.ownerHash -ne $evidence.authenticHash) {
        throw "Tiny Hybrid mismatch evidence is incomplete: $evidencePath"
    }
}

function Invoke-StateSample([string]$Prefix) {
    $logPath = Join-Path $outputRoot 'state-sample.log'
    & dotnet run --no-build --project 'src/AbilityKit.Demo.Tiny.StateSample' -- `
        '127.0.0.1' $TcpPort $Prefix 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) { throw "Tiny State sample failed. Log: $logPath" }
}

function Invoke-RoomSample([string]$Prefix) {
    $logPath = Join-Path $outputRoot 'room-sample.log'
    & dotnet run --no-build --project 'src/AbilityKit.Demo.Tiny.RoomSample' -- `
        '127.0.0.1' $TcpPort $Prefix 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) { throw "Tiny Room sample failed. Log: $logPath" }
}

function Invoke-SyncChapterSample([string]$Chapter, [string]$Prefix) {
    $project = "src/AbilityKit.Demo.Tiny.$($Chapter)Sample"
    $logPath = Join-Path $outputRoot "$($Chapter.ToLowerInvariant())-sample.log"
    & dotnet run --no-build --project $project -- `
        '127.0.0.1' $TcpPort $Prefix 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) { throw "Tiny $Chapter chapter sample failed. Log: $logPath" }
}

function Invoke-UnityBatch([string[]]$Arguments) {
    $unity = Start-Process -FilePath $UnityExe -WindowStyle Hidden -PassThru `
        -ArgumentList $Arguments
    if (-not $unity.WaitForExit(300000)) {
        if (-not $unity.HasExited) { Stop-Process -Id $unity.Id -Force }
        throw "Unity batchmode timed out after five minutes."
    }
    return $unity.ExitCode
}

function Assert-TinyPackageDistribution([string]$Project) {
    $unsafePackage = Join-Path $Project `
        'Packages/com.abilitykit.thirdparty.unsafe/Runtime/System.Runtime.CompilerServices.Unsafe.dll'
    if (-not (Test-Path -LiteralPath $unsafePackage)) {
        throw "Tiny project is missing its packaged Unsafe dependency: $Project"
    }
    $guide = Join-Path $Project `
        'Packages/com.abilitykit.demo.tiny/Documentation~/IntegrationGuide.md'
    if (-not (Test-Path -LiteralPath $guide)) {
        throw "Tiny project is missing its package integration guide: $Project"
    }
    $assetDlls = @(Get-ChildItem -LiteralPath (Join-Path $Project 'Assets') `
        -Filter '*.dll' -File -Recurse)
    if ($assetDlls.Count -gt 0) {
        throw "Tiny project copied DLLs into Assets outside the package closure: $Project"
    }
}

function Test-Port([int]$Port) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $attempt = $client.BeginConnect('127.0.0.1', $Port, $null, $null)
        if (-not $attempt.AsyncWaitHandle.WaitOne(250)) { return $false }
        $client.EndConnect($attempt)
        return $true
    }
    catch { return $false }
    finally { $client.Dispose() }
}

function Assert-PortAvailable([int]$Port) {
    $connections = @(Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue)
    if ($connections.Count -gt 0 -or (Test-Port $Port)) {
        throw "Port $Port is already in use."
    }
}

function Wait-Port([int]$Port, [System.Diagnostics.Process]$Process) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($Process.HasExited) { throw "Process exited before port $Port was ready." }
        if (Test-Port $Port) { return }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for port $Port."
}

try {
    Push-Location $repositoryRoot
    try {
        Write-Phase 'protocol' 'running'
        & (Join-Path $PSScriptRoot 'compile-protocol-catalogs.ps1') -Check
        if ($LASTEXITCODE -ne 0) { throw 'Protocol catalog check failed.' }
        & (Join-Path $PSScriptRoot 'export-protocol-wire.ps1') -Projects room -Check
        if ($LASTEXITCODE -ne 0) { throw 'Room wire schema check failed.' }
        Write-Phase 'protocol' 'passed'

        Write-Phase 'dotnet' 'running'
        Invoke-Dotnet @('test', 'src/AbilityKit.Network.Room.Tests/AbilityKit.Network.Room.Tests.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('test', 'src/AbilityKit.Demo.Tiny.Replication.Tests/AbilityKit.Demo.Tiny.Replication.Tests.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('test', 'Server/Orleans/src/AbilityKit.Orleans.Grains.Tests/AbilityKit.Orleans.Grains.Tests.csproj',
            '--filter', 'FullyQualifiedName~Tiny|FullyQualifiedName~RoomStateStoreDeepCopyTests',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('test', 'Server/Orleans/src/AbilityKit.Orleans.Gateway.Tests/AbilityKit.Orleans.Gateway.Tests.csproj',
            '-c', 'Release', '--filter', 'FullyQualifiedName~RoomMembershipIntegrationTests',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.Logic.Sample',
            '--no-launch-profile', '--nologo', '-v:q')
        Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.ChapterSamples',
            '--no-launch-profile', '--nologo', '-v:q')
        Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.Client/AbilityKit.Demo.Tiny.Client.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.StateSample/AbilityKit.Demo.Tiny.StateSample.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.RoomSample/AbilityKit.Demo.Tiny.RoomSample.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.FrameSample/AbilityKit.Demo.Tiny.FrameSample.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.HybridSample/AbilityKit.Demo.Tiny.HybridSample.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.RecoverySample/AbilityKit.Demo.Tiny.RecoverySample.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Write-Phase 'dotnet' 'passed'

        Write-Phase 'tcp' 'running'
        foreach ($port in @($SiloPort, $OrleansGatewayPort, $TcpPort, $HttpPort)) {
            Assert-PortAvailable $port
        }
        $siloOutput = Join-Path $outputRoot 'host'
        $gatewayOutput = Join-Path $outputRoot 'gateway'
        Invoke-Dotnet @('build', 'Server/Orleans/src/AbilityKit.Orleans.Host/AbilityKit.Orleans.Host.csproj',
            '-c:Debug', "-p:OutDir=$siloOutput\", '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('build', 'Server/Orleans/src/AbilityKit.Orleans.Gateway/AbilityKit.Orleans.Gateway.csproj',
            '-c:Debug', "-p:OutDir=$gatewayOutput\", '--nologo', '-clp:ErrorsOnly')

        $env:DOTNET_ENVIRONMENT = 'Development'
        $siloProcess = Start-Process -FilePath 'dotnet' -WindowStyle Hidden -PassThru `
            -WorkingDirectory (Join-Path $repositoryRoot 'Server/Orleans/src/AbilityKit.Orleans.Host') `
            -ArgumentList @((Join-Path $siloOutput 'AbilityKit.Orleans.Host.dll'),
                '--AbilityKit:Orleans:SiloPort', $SiloPort,
                '--AbilityKit:Orleans:PrimarySiloPort', $SiloPort,
                '--AbilityKit:Orleans:GatewayPort', $OrleansGatewayPort) `
            -RedirectStandardOutput (Join-Path $outputRoot 'host.out.log') `
            -RedirectStandardError (Join-Path $outputRoot 'host.err.log')
        Wait-Port $OrleansGatewayPort $siloProcess

        $gatewayProcess = Start-Process -FilePath 'dotnet' -WindowStyle Hidden -PassThru `
            -WorkingDirectory (Join-Path $repositoryRoot 'Server/Orleans/src/AbilityKit.Orleans.Gateway') `
            -ArgumentList @((Join-Path $gatewayOutput 'AbilityKit.Orleans.Gateway.dll'),
                '--AbilityKit:Orleans:GatewayPort', $OrleansGatewayPort,
                '--AbilityKit:Gateway:Tcp:Port', $TcpPort,
                '--TcpGateway:Port', $TcpPort,
                '--AbilityKit:Gateway:Http:Port', $HttpPort) `
            -RedirectStandardOutput (Join-Path $outputRoot 'gateway.out.log') `
            -RedirectStandardError (Join-Path $outputRoot 'gateway.err.log')
        Wait-Port $TcpPort $gatewayProcess

        Invoke-RoomSample "tiny-$runId-room-sample"
        Invoke-StateSample "tiny-$runId-state-sample"
        Invoke-SyncChapterSample 'Frame' "tiny-$runId-frame-sample"
        Invoke-SyncChapterSample 'Hybrid' "tiny-$runId-hybrid-sample"
        Invoke-SyncChapterSample 'Recovery' "tiny-$runId-recovery-sample"
        foreach ($mode in @('state', 'frame', 'hybrid')) {
            Invoke-TinySmoke $mode "tiny-$runId-$mode"
            Invoke-TinySmoke "session-$mode" "tiny-$runId-session-$mode"
        }
        Invoke-TinyHybridMismatch "tiny-$runId-hybrid-mismatch"
        Write-Phase 'tcp' 'passed'

        if (-not $SkipUnity) {
            Write-Phase 'unity-network-playmode' 'running'
            if (-not (Test-Path -LiteralPath $UnityExe)) { throw "Unity editor not found: $UnityExe" }
            $project = Join-Path $outputRoot 'unity-project'
            & (Join-Path $PSScriptRoot 'create-tiny-validation-project.ps1') -OutputPath $project
            Assert-TinyPackageDistribution $project
            $networkResults = Join-Path $outputRoot 'tiny-network-playmode.xml'
            $networkLog = Join-Path $outputRoot 'tiny-network-playmode-unity.log'
            $networkEvidencePath = Join-Path $outputRoot 'tiny-network-playmode.json'
            $previousUnityPort = $env:ABILITYKIT_TINY_UNITY_PORT
            $previousUnityPrefix = $env:ABILITYKIT_TINY_UNITY_PREFIX
            $previousUnityEvidence = $env:ABILITYKIT_TINY_UNITY_EVIDENCE
            try {
                $env:ABILITYKIT_TINY_UNITY_PORT = [string]$TcpPort
                $env:ABILITYKIT_TINY_UNITY_PREFIX = "tiny-$runId-unity"
                $env:ABILITYKIT_TINY_UNITY_EVIDENCE = $networkEvidencePath
                $networkArguments = @(
                    '-batchmode', '-nographics', '-projectPath', $project,
                    '-runTests', '-testPlatform', 'PlayMode',
                    '-assemblyNames', 'AbilityKit.Demo.Tiny.Network.PlayMode.Tests',
                    '-testResults', $networkResults,
                    '-logFile', $networkLog)
                for ($attempt = 1; $attempt -le 4; $attempt++) {
                    $networkExitCode = Invoke-UnityBatch $networkArguments
                    if ($networkExitCode -eq 0 -and (Test-Path -LiteralPath $networkResults)) { break }
                    $logText = if (Test-Path -LiteralPath $networkLog) {
                        Get-Content -LiteralPath $networkLog -Raw -Encoding UTF8
                    } else { '' }
                    if ($attempt -lt 4 -and $logText.Contains('MovedFromExtractor') -and
                        $logText.Contains('-1073741757')) {
                        Copy-Item -LiteralPath $networkLog -Destination `
                            (Join-Path $outputRoot "tiny-network-import-attempt-$attempt.log")
                        Write-Warning "Tiny Unity import helper failed on attempt $attempt; retrying."
                        continue
                    }
                    throw "Tiny Unity network PlayMode failed. Log: $networkLog"
                }
            }
            finally {
                $env:ABILITYKIT_TINY_UNITY_PORT = $previousUnityPort
                $env:ABILITYKIT_TINY_UNITY_PREFIX = $previousUnityPrefix
                $env:ABILITYKIT_TINY_UNITY_EVIDENCE = $previousUnityEvidence
            }
            [xml]$networkXml = Get-Content -LiteralPath $networkResults -Raw -Encoding UTF8
            $networkRun = $networkXml.'test-run'
            if ($null -eq $networkRun -or [int]$networkRun.total -ne 1 -or
                [int]$networkRun.passed -ne 1 -or [int]$networkRun.failed -ne 0 -or
                -not (Test-Path -LiteralPath $networkEvidencePath)) {
                throw "Tiny Unity network evidence is incomplete: $networkResults"
            }
            $networkEvidence = Get-Content -LiteralPath $networkEvidencePath -Raw -Encoding UTF8 |
                ConvertFrom-Json
            if (@($networkEvidence.modes).Count -ne 3 -or
                @($networkEvidence.modes | Where-Object {
                    -not $_.ownerProjected -or -not $_.guestProjected -or -not $_.guestRestored -or
                    [string]::IsNullOrWhiteSpace($_.roomId)
                }).Count -gt 0) {
                throw "Tiny Unity network mode evidence is incomplete: $networkEvidencePath"
            }
            Write-Host "Tiny Unity network PlayMode passed: $($networkRun.passed)/$($networkRun.total)"
            Write-Phase 'unity-network-playmode' 'passed'
        }
    }
    finally { Pop-Location }
}
finally {
    foreach ($started in @($gatewayProcess, $siloProcess)) {
        if ($null -ne $started -and -not $started.HasExited) {
            Stop-Process -Id $started.Id -Force
            $started.WaitForExit(5000) | Out-Null
        }
    }
    $env:DOTNET_ENVIRONMENT = $previousEnvironment
    $env:ArtifactsPath = $previousArtifactsPath
    $env:UseArtifactsOutput = $previousUseArtifactsOutput
}

if (-not $SkipUnity) {
    Write-Phase 'unity-editmode' 'running'
    if (-not (Test-Path -LiteralPath $UnityExe)) { throw "Unity editor not found: $UnityExe" }
    if ($null -eq $project) { throw 'Tiny Unity project was not created.' }
    Assert-TinyPackageDistribution $project
    $projectManifest = Get-Content -LiteralPath (Join-Path $project 'Packages/manifest.json') -Raw -Encoding UTF8
    if ($projectManifest.Contains($repositoryRoot) -or $projectManifest.Contains('file:')) {
        throw "Tiny Unity project still references the source checkout: $project"
    }
    if (Test-Path -LiteralPath (Join-Path $project 'Temp/UnityLockfile')) {
        throw "Tiny validation project is open in Unity: $project"
    }
    $resultPath = Join-Path $outputRoot 'tiny-editmode.xml'
    $unityLog = Join-Path $outputRoot 'unity.log'
    $unityArguments = @(
        '-batchmode', '-nographics', '-projectPath', $project,
        '-runTests', '-testPlatform', 'EditMode',
        '-assemblyNames', 'AbilityKit.Demo.Tiny.Editor.Tests',
        '-testResults', $resultPath,
        '-logFile', $unityLog)
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        $unityExitCode = Invoke-UnityBatch $unityArguments
        if ($unityExitCode -eq 0 -and (Test-Path -LiteralPath $resultPath)) { break }
        $logText = if (Test-Path -LiteralPath $unityLog) {
            Get-Content -LiteralPath $unityLog -Raw -Encoding UTF8
        } else { '' }
        if ($attempt -lt 4 -and $logText.Contains('MovedFromExtractorCombine') -and
            $logText.Contains('-1073741757')) {
            Copy-Item -LiteralPath $unityLog -Destination (Join-Path $outputRoot "unity-import-attempt-$attempt.log")
            Write-Warning "Unity import helper failed on attempt $attempt; retrying the same project."
            continue
        }
        throw "Tiny Unity EditMode verification failed. Log: $unityLog"
    }
    [xml]$testResults = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
    $run = $testResults.'test-run'
    if ($null -eq $run -or [int]$run.total -lt 1 -or [int]$run.failed -ne 0 -or
        [int]$run.passed -ne [int]$run.total) {
        throw "Tiny Unity EditMode tests did not all pass: $resultPath"
    }
    Write-Host "Tiny Unity EditMode tests passed: $($run.passed)/$($run.total)"
    Write-Phase 'unity-editmode' 'passed'

    Write-Phase 'unity-playmode' 'running'
    $playModeResults = Join-Path $outputRoot 'tiny-playmode.xml'
    $playModeLog = Join-Path $outputRoot 'tiny-playmode-unity.log'
    $playModeArguments = @(
        '-batchmode', '-nographics', '-projectPath', $project,
        '-runTests', '-testPlatform', 'PlayMode',
        '-assemblyNames', 'AbilityKit.Demo.Tiny.PlayMode.Tests',
        '-testResults', $playModeResults,
        '-logFile', $playModeLog)
    $playModeExitCode = Invoke-UnityBatch $playModeArguments
    if ($playModeExitCode -ne 0 -or -not (Test-Path -LiteralPath $playModeResults)) {
        throw "Tiny Unity PlayMode verification failed. Log: $playModeLog"
    }
    [xml]$playModeXml = Get-Content -LiteralPath $playModeResults -Raw -Encoding UTF8
    $playModeSummary = $playModeXml.'test-run'
    if ($null -eq $playModeSummary -or [int]$playModeSummary.total -lt 1 -or
        [int]$playModeSummary.failed -ne 0 -or
        [int]$playModeSummary.passed -ne [int]$playModeSummary.total) {
        throw "Tiny Unity PlayMode tests did not all pass: $playModeResults"
    }
    Write-Host "Tiny Unity PlayMode tests passed: $($playModeSummary.passed)/$($playModeSummary.total)"
    Write-Phase 'unity-playmode' 'passed'

    Write-Phase 'unity-consumer-playmode' 'running'
    $consumerProject = Join-Path $outputRoot 'consumer-project'
    & (Join-Path $PSScriptRoot 'create-tiny-validation-project.ps1') `
        -OutputPath $consumerProject -Standalone
    Assert-TinyPackageDistribution $consumerProject
    $consumerManifest = Get-Content -LiteralPath (Join-Path $consumerProject 'Packages/manifest.json') `
        -Raw -Encoding UTF8
    if ($consumerManifest.Contains($repositoryRoot) -or $consumerManifest.Contains('file:') -or
        $consumerManifest.Contains('com.abilitykit.demo.starter') -or
        (Test-Path -LiteralPath (Join-Path $consumerProject 'Assets/Scenes/StarterScene.unity'))) {
        throw "Tiny consumer project is not independent of Starter and the source checkout: $consumerProject"
    }
    $consumerResults = Join-Path $outputRoot 'tiny-consumer-playmode.xml'
    $consumerLog = Join-Path $outputRoot 'tiny-consumer-unity.log'
    $consumerArguments = @(
        '-batchmode', '-nographics', '-projectPath', $consumerProject,
        '-runTests', '-testPlatform', 'PlayMode',
        '-assemblyNames', 'TinyConsumer.PlayMode.Tests',
        '-testResults', $consumerResults,
        '-logFile', $consumerLog)
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        $consumerExitCode = Invoke-UnityBatch $consumerArguments
        if ($consumerExitCode -eq 0 -and (Test-Path -LiteralPath $consumerResults)) { break }
        $logText = if (Test-Path -LiteralPath $consumerLog) {
            Get-Content -LiteralPath $consumerLog -Raw -Encoding UTF8
        } else { '' }
        if ($attempt -lt 4 -and $logText.Contains('MovedFromExtractor') -and
            $logText.Contains('-1073741757')) {
            Copy-Item -LiteralPath $consumerLog -Destination (Join-Path $outputRoot "tiny-consumer-import-attempt-$attempt.log")
            Write-Warning "Tiny consumer Unity import helper failed on attempt $attempt; retrying the same project."
            continue
        }
        throw "Tiny consumer Unity PlayMode verification failed. Log: $consumerLog"
    }
    [xml]$consumerXml = Get-Content -LiteralPath $consumerResults -Raw -Encoding UTF8
    $consumerRun = $consumerXml.'test-run'
    if ($null -eq $consumerRun -or [int]$consumerRun.total -lt 1 -or
        [int]$consumerRun.failed -ne 0 -or
        [int]$consumerRun.passed -ne [int]$consumerRun.total) {
        throw "Tiny consumer Unity PlayMode tests did not all pass: $consumerResults"
    }
    Write-Host "Tiny consumer Unity PlayMode tests passed: $($consumerRun.passed)/$($consumerRun.total)"
    Write-Phase 'unity-consumer-playmode' 'passed'

    Write-Phase 'unity-logic-editmode' 'running'
    $logicProject = Join-Path $outputRoot 'logic-project'
    & (Join-Path $PSScriptRoot 'create-tiny-logic-validation-project.ps1') -OutputPath $logicProject
    $logicManifest = Get-Content -LiteralPath (Join-Path $logicProject 'Packages/manifest.json') -Raw -Encoding UTF8
    if ($logicManifest.Contains($repositoryRoot) -or $logicManifest.Contains('file:')) {
        throw "Tiny Logic project still references the source checkout: $logicProject"
    }
    $logicResults = Join-Path $outputRoot 'tiny-logic-editmode.xml'
    $logicLog = Join-Path $outputRoot 'tiny-logic-unity.log'
    $logicArguments = @(
        '-batchmode', '-nographics', '-projectPath', $logicProject,
        '-runTests', '-testPlatform', 'EditMode',
        '-assemblyNames', 'AbilityKit.Demo.Tiny.Logic.Editor.Tests',
        '-testResults', $logicResults,
        '-logFile', $logicLog)
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        $unityExitCode = Invoke-UnityBatch $logicArguments
        if ($unityExitCode -eq 0 -and (Test-Path -LiteralPath $logicResults)) { break }
        $logText = if (Test-Path -LiteralPath $logicLog) {
            Get-Content -LiteralPath $logicLog -Raw -Encoding UTF8
        } else { '' }
        if ($attempt -lt 4 -and $logText.Contains('MovedFromExtractor') -and
            $logText.Contains('-1073741757')) {
            Copy-Item -LiteralPath $logicLog -Destination (Join-Path $outputRoot "tiny-logic-import-attempt-$attempt.log")
            Write-Warning "Tiny Logic Unity import helper failed on attempt $attempt; retrying the same project."
            continue
        }
        throw "Tiny Logic Unity EditMode verification failed. Log: $logicLog"
    }
    [xml]$logicTestResults = Get-Content -LiteralPath $logicResults -Raw -Encoding UTF8
    $logicRun = $logicTestResults.'test-run'
    if ($null -eq $logicRun -or [int]$logicRun.total -lt 1 -or [int]$logicRun.failed -ne 0 -or
        [int]$logicRun.passed -ne [int]$logicRun.total) {
        throw "Tiny Logic Unity EditMode tests did not all pass: $logicResults"
    }
    Write-Host "Tiny Logic Unity EditMode tests passed: $($logicRun.passed)/$($logicRun.total)"
    Write-Phase 'unity-logic-editmode' 'passed'
} else {
    Write-Phase 'unity-network-playmode' 'skipped'
    Write-Phase 'unity-editmode' 'skipped'
    Write-Phase 'unity-playmode' 'skipped'
    Write-Phase 'unity-consumer-playmode' 'skipped'
    Write-Phase 'unity-logic-editmode' 'skipped'
}

Write-Host "Tiny starter verification passed. Evidence: $outputRoot"
exit 0
