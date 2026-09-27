[CmdletBinding()]
param(
    [switch]$SkipUnity,
    [switch]$FocusTurnRecord,
    [switch]$VerifyCrossProcess,
    [ValidateSet('', '01', '02', '03', '04', '05', '06', '09', '10')]
    [string]$FocusChapter = '',
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
$consumerProject = $null
$consumerTurnProject = $null
$previousEnvironment = $env:DOTNET_ENVIRONMENT
$previousArtifactsPath = $env:ArtifactsPath
$previousUseArtifactsOutput = $env:UseArtifactsOutput
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$outputRoot = Join-Path $repositoryRoot "local/Logs/tiny-acceptance-$runId"
[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$env:ArtifactsPath = Join-Path $outputRoot 'artifacts'
$env:UseArtifactsOutput = 'true'
$phaseResults = [ordered]@{}

function Write-Phase([string]$Name, [string]$Status, [string]$Detail = '') {
    $phaseResults[$Name] = [ordered]@{
        status = $Status
        updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    if ($Detail) { $phaseResults[$Name].detail = $Detail }
    $phaseResults | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $outputRoot 'phases.json') -Encoding UTF8
}

trap {
    $failure = $_.Exception.Message
    foreach ($name in @($phaseResults.Keys)) {
        if ($phaseResults[$name].status -eq 'running') {
            Write-Phase $name 'failed' $failure
        }
    }
    throw $failure
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

function Invoke-TinyLiveRecord([string]$Prefix) {
    $recordPath = Join-Path $outputRoot 'tiny-live-record.bin'
    $logPath = Join-Path $outputRoot 'tiny-live-record.log'
    $evidencePath = Join-Path $outputRoot 'tiny-live-record.json'
    & dotnet run --no-build --project 'src/AbilityKit.Demo.Tiny.LiveRecord.Sample' -- `
        '127.0.0.1' $TcpPort $Prefix $recordPath $evidencePath 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $recordPath) -or
        -not (Test-Path -LiteralPath $evidencePath)) {
        throw "Tiny live Record/Replay failed. Log: $logPath"
    }
    $evidence = Get-Content -LiteralPath $evidencePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $checkpoints = @($evidence.checkpointFrames)
    $checkpointHashes = @($evidence.checkpointHashes)
    if (-not $evidence.passed -or $evidence.inputCount -ne 6 -or
        $evidence.snapshotCount -ne 3 -or $evidence.hashCount -lt 6 -or
        $checkpoints.Count -ne 2 -or $checkpointHashes.Count -ne 2 -or
        $evidence.baselineFrame -ge $checkpoints[0] -or
        $checkpoints[0] -ge $checkpoints[1] -or
        $checkpoints[1] -ge $evidence.finalFrame -or
        @($checkpointHashes | Where-Object { $_ -ne $evidence.baselineHash }).Count -gt 0 -or
        $evidence.ownerHp -ne 80 -or $evidence.guestHp -ne 80 -or
        @($evidence.actions).Count -ne 6) {
        throw "Tiny live Record/Replay evidence is incomplete: $evidencePath"
    }
}

function Invoke-TinyProcessSmoke([string]$Mode, [string]$Prefix) {
    $directory = Join-Path $outputRoot "process-$Mode"
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $clientDll = Join-Path $env:ArtifactsPath `
        'bin/AbilityKit.Demo.Tiny.Client/debug/AbilityKit.Demo.Tiny.Client.dll'
    if (-not (Test-Path -LiteralPath $clientDll)) {
        throw "Tiny process client is missing: $clientDll"
    }
    $children = @()
    try {
        foreach ($role in @('owner', 'guest')) {
            $start = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
            $start.WorkingDirectory = $repositoryRoot
            $start.UseShellExecute = $false
            $start.CreateNoWindow = $true
            $start.RedirectStandardOutput = $true
            $start.RedirectStandardError = $true
            $start.Arguments = (@($clientDll, '127.0.0.1', [string]$TcpPort,
                    $Prefix, "process-$role-$Mode", $directory) |
                ForEach-Object { '"' + $_ + '"' }) -join ' '
            $child = [System.Diagnostics.Process]::new()
            $child.StartInfo = $start
            if (-not $child.Start()) { throw "Tiny $Mode $role process did not start." }
            $children += [pscustomobject]@{
                Process = $child
                Role = $role
                Output = $child.StandardOutput.ReadToEndAsync()
                Error = $child.StandardError.ReadToEndAsync()
            }
        }
        foreach ($child in $children) {
            if (-not $child.Process.WaitForExit(90000)) {
                throw "Tiny $Mode $($child.Role) process timed out. Logs: $directory"
            }
            if ($child.Process.ExitCode -ne 0) {
                throw "Tiny $Mode $($child.Role) process exited $($child.Process.ExitCode). Logs: $directory"
            }
        }
        $owner = Get-Content -LiteralPath (Join-Path $directory 'owner.json') `
            -Raw -Encoding UTF8 | ConvertFrom-Json
        $guest = Get-Content -LiteralPath (Join-Path $directory 'guest.json') `
            -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($owner.Role -ne 'owner' -or $guest.Role -ne 'guest' -or
            $owner.Mode -ne $Mode -or $guest.Mode -ne $Mode -or
            $owner.ProcessId -eq $guest.ProcessId -or
            @($children | ForEach-Object { $_.Process.Id }) -notcontains $owner.ProcessId -or
            @($children | ForEach-Object { $_.Process.Id }) -notcontains $guest.ProcessId -or
            $owner.RoomId -ne $guest.RoomId -or $owner.BattleId -ne $guest.BattleId -or
            $owner.TargetHp -ne 90 -or $guest.TargetHp -ne 90 -or
            $owner.OwnerX -ne 0 -or $guest.OwnerX -ne 0 -or
            $owner.AuthoritativeFrame -le 0 -or $guest.AuthoritativeFrame -le 0 -or
            -not $guest.Recovered -or
            ($Mode -eq 'Hybrid') -ne ($owner.LocalPredictions -gt 0)) {
            throw "Tiny $Mode process evidence is incomplete: $directory"
        }
        Write-Host "Tiny $Mode independent processes passed: $($owner.ProcessId), $($guest.ProcessId)"
    }
    finally {
        foreach ($child in $children) {
            if (-not $child.Process.HasExited) {
                $child.Process.Kill()
                $child.Process.WaitForExit(5000) | Out-Null
            }
            $child.Output.GetAwaiter().GetResult() | Set-Content -LiteralPath `
                (Join-Path $directory "$($child.Role).out.log") -Encoding UTF8
            $child.Error.GetAwaiter().GetResult() | Set-Content -LiteralPath `
                (Join-Path $directory "$($child.Role).err.log") -Encoding UTF8
            $child.Process.Dispose()
        }
    }
}

function Invoke-TinyTurnSmoke([string]$Prefix) {
    $directory = Join-Path $outputRoot 'process-Turn'
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $clientDll = Join-Path $env:ArtifactsPath `
        'bin/AbilityKit.Demo.Tiny.Turn.StateSample/debug/AbilityKit.Demo.Tiny.Turn.StateSample.dll'
    if (-not (Test-Path -LiteralPath $clientDll)) {
        throw "Tiny Turn client is missing: $clientDll"
    }
    $children = @()
    try {
        foreach ($role in @('owner', 'guest')) {
            $start = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
            $start.WorkingDirectory = $repositoryRoot
            $start.UseShellExecute = $false
            $start.CreateNoWindow = $true
            $start.RedirectStandardOutput = $true
            $start.RedirectStandardError = $true
            $start.Arguments = (@($clientDll, '127.0.0.1', [string]$TcpPort,
                    $Prefix, $role, $directory) | ForEach-Object { '"' + $_ + '"' }) -join ' '
            $process = [System.Diagnostics.Process]::new()
            $process.StartInfo = $start
            if (-not $process.Start()) { throw "Tiny Turn $role process did not start." }
            $children += [pscustomobject]@{
                Process = $process
                Role = $role
                Output = $process.StandardOutput.ReadToEndAsync()
                Error = $process.StandardError.ReadToEndAsync()
            }
        }
        foreach ($child in $children) {
            if (-not $child.Process.WaitForExit(110000)) {
                throw "Tiny Turn $($child.Role) timed out: $directory"
            }
            if ($child.Process.ExitCode -ne 0) {
                throw "Tiny Turn $($child.Role) exited $($child.Process.ExitCode): $directory"
            }
        }
        $owner = Get-Content -LiteralPath (Join-Path $directory 'owner.json') -Raw | ConvertFrom-Json
        $guest = Get-Content -LiteralPath (Join-Path $directory 'guest.json') -Raw | ConvertFrom-Json
        if ($owner.ProcessId -eq $guest.ProcessId -or
            $owner.ProcessId -ne $children[0].Process.Id -or
            $guest.ProcessId -ne $children[1].Process.Id -or
            $owner.BattleId -ne $guest.BattleId -or $owner.RoomId -ne $guest.RoomId -or
            $owner.WinnerId -ne 1 -or $guest.WinnerId -ne 1 -or
            $owner.Turn -ne 3 -or $guest.Turn -ne 3 -or
            $owner.OwnerHp -ne 1 -or $guest.OwnerHp -ne 1 -or
            $owner.GuestHp -ne 0 -or $guest.GuestHp -ne 0 -or
            -not $owner.OutOfTurnRejected -or -not $guest.Recovered) {
            throw "Tiny Turn evidence is incomplete: $directory"
        }
        Write-Host "Tiny Turn independent processes passed: $($owner.ProcessId), $($guest.ProcessId)"
    }
    finally {
        foreach ($child in $children) {
            if (-not $child.Process.HasExited) {
                $child.Process.Kill()
                $child.Process.WaitForExit(5000) | Out-Null
            }
            $child.Output.GetAwaiter().GetResult() | Set-Content -LiteralPath `
                (Join-Path $directory "$($child.Role).out.log") -Encoding UTF8
            $child.Error.GetAwaiter().GetResult() | Set-Content -LiteralPath `
                (Join-Path $directory "$($child.Role).err.log") -Encoding UTF8
            $child.Process.Dispose()
        }
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

function Invoke-TinyCrossProcessUnity([string]$Mode) {
    $directory = Join-Path $outputRoot "unity-cross-$Mode"
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $roomFile = Join-Path $directory 'room-id.txt'
    $previousPort = $env:ABILITYKIT_TINY_UNITY_PORT
    $previousPrefix = $env:ABILITYKIT_TINY_UNITY_PREFIX
    $previousMode = $env:ABILITYKIT_TINY_CROSS_MODE
    $previousRoomFile = $env:ABILITYKIT_TINY_CROSS_ROOM_FILE
    $previousEvidenceDir = $env:ABILITYKIT_TINY_CROSS_EVIDENCE_DIR
    $children = @()
    try {
        foreach ($role in @('owner', 'guest')) {
            $crossProject = Join-Path $directory "$role-project"
            & (Join-Path $PSScriptRoot 'create-tiny-validation-project.ps1') `
                -OutputPath $crossProject -Standalone -IncludeTurn
            Assert-TinyPackageDistribution $crossProject
        }
        $env:ABILITYKIT_TINY_UNITY_PORT = [string]$TcpPort
        $env:ABILITYKIT_TINY_UNITY_PREFIX = "tiny-$runId-cross-$Mode"
        $env:ABILITYKIT_TINY_CROSS_MODE = $Mode
        $env:ABILITYKIT_TINY_CROSS_ROOM_FILE = $roomFile
        $env:ABILITYKIT_TINY_CROSS_EVIDENCE_DIR = $directory
        foreach ($role in @('owner', 'guest')) {
            $results = Join-Path $directory "$role.xml"
            $log = Join-Path $directory "$role-unity.log"
            $arguments = @(
                '-batchmode', '-nographics', '-projectPath', (Join-Path $directory "$role-project"),
                '-runTests', '-testPlatform', 'PlayMode',
                '-assemblyNames', 'TinyConsumer.CrossProcess.PlayMode.Tests',
                '-testFilter', "TinyConsumer.Tests.TinyConsumerCrossProcessPlayModeTests.Independent$($role.Substring(0, 1).ToUpper() + $role.Substring(1))UsesGateway",
                '-testResults', $results, '-logFile', $log)
            $child = Start-Process -FilePath $UnityExe -WindowStyle Hidden -PassThru `
                -ArgumentList $arguments
            $children += [pscustomobject]@{ Role = $role; Process = $child; Results = $results; Log = $log }
        }
        $deadline = [DateTime]::UtcNow.AddMinutes(5)
        while (@($children | Where-Object { -not $_.Process.HasExited }).Count -gt 0 -and
            [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Seconds 1
        }
        foreach ($child in $children) {
            if (-not $child.Process.HasExited) { throw "Tiny $Mode cross-process Unity timed out: $directory" }
            [xml]$xml = Get-Content -LiteralPath $child.Results -Raw -Encoding UTF8
            $run = $xml.'test-run'
            if ($child.Process.ExitCode -ne 0 -or $null -eq $run -or
                [int]$run.total -ne 1 -or [int]$run.passed -ne 1 -or [int]$run.failed -ne 0) {
                throw "Tiny $Mode $($child.Role) cross-process test failed: $($child.Log)"
            }
        }
        $owner = Get-Content -LiteralPath (Join-Path $directory 'owner.json') `
            -Raw -Encoding UTF8 | ConvertFrom-Json
        $guest = Get-Content -LiteralPath (Join-Path $directory 'guest.json') `
            -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($owner.mode -ne $Mode -or $guest.mode -ne $Mode -or
            $owner.role -ne 'owner' -or $guest.role -ne 'guest' -or
            $owner.processId -ne $children[0].Process.Id -or
            $guest.processId -ne $children[1].Process.Id -or
            $owner.processId -eq $guest.processId -or
            -not $owner.sceneRootReady -or -not $guest.sceneRootReady -or
            -not $owner.authoritativeResult -or -not $guest.authoritativeResult -or
            -not $owner.returnedToLobby -or -not $guest.returnedToLobby -or
            [string]::IsNullOrWhiteSpace($owner.roomId) -or
            [string]::IsNullOrWhiteSpace($owner.battleId) -or
            $owner.roomId -ne $guest.roomId -or $owner.battleId -ne $guest.battleId) {
            throw "Tiny $Mode cross-process evidence is incomplete: $directory"
        }
        Write-Host "Tiny $Mode Unity cross-process passed: $($owner.roomId)"
    }
    finally {
        foreach ($child in $children) {
            if (-not $child.Process.HasExited) {
                Stop-Process -Id $child.Process.Id -Force
                $child.Process.WaitForExit(5000) | Out-Null
            }
            $child.Process.Dispose()
        }
        $env:ABILITYKIT_TINY_UNITY_PORT = $previousPort
        $env:ABILITYKIT_TINY_UNITY_PREFIX = $previousPrefix
        $env:ABILITYKIT_TINY_CROSS_MODE = $previousMode
        $env:ABILITYKIT_TINY_CROSS_ROOM_FILE = $previousRoomFile
        $env:ABILITYKIT_TINY_CROSS_EVIDENCE_DIR = $previousEvidenceDir
    }
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

        Write-Phase 'dependencies' 'running'
        & (Join-Path $PSScriptRoot 'verify-tiny-dependencies.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'Tiny dependency tiers failed.' }
        Write-Phase 'dependencies' 'passed'

        Write-Phase 'dotnet' 'running'
        if ($FocusChapter) {
            switch ($FocusChapter) {
                '01' { Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.Logic.Sample', '--no-launch-profile', '-v:q') }
                '02' { Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.RoomSample/AbilityKit.Demo.Tiny.RoomSample.csproj', '--nologo', '-clp:ErrorsOnly') }
                '03' { Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.StateSample/AbilityKit.Demo.Tiny.StateSample.csproj', '--nologo', '-clp:ErrorsOnly') }
                '04' { Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.FrameSample/AbilityKit.Demo.Tiny.FrameSample.csproj', '--nologo', '-clp:ErrorsOnly') }
                '05' { Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.HybridSample/AbilityKit.Demo.Tiny.HybridSample.csproj', '--nologo', '-clp:ErrorsOnly') }
                '06' { Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.RecoverySample/AbilityKit.Demo.Tiny.RecoverySample.csproj', '--nologo', '-clp:ErrorsOnly') }
                '09' {
                    Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.Record.Sample',
                        '--no-launch-profile', '-v:q', '--', (Join-Path $outputRoot 'tiny-record.bin'))
                    Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.ProtocolEvolution.Sample',
                        '--no-launch-profile', '-v:q')
                    Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.LiveRecord.Sample/AbilityKit.Demo.Tiny.LiveRecord.Sample.csproj', '--nologo', '-clp:ErrorsOnly')
                }
                '10' {
                    Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.BattleStyles.Sample', '--no-launch-profile', '-v:q')
                    Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.Turn.StateSample/AbilityKit.Demo.Tiny.Turn.StateSample.csproj', '--nologo', '-clp:ErrorsOnly')
                }
            }
        }
        elseif ($FocusTurnRecord) {
            Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.BattleStyles.Sample',
                '--no-launch-profile', '-v:q')
            Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.Record.Sample',
                '--no-launch-profile', '-v:q', '--', (Join-Path $outputRoot 'tiny-record.bin'))
            foreach ($name in @('AbilityKit.Demo.Tiny.Turn.StateSample',
                    'AbilityKit.Demo.Tiny.Turn.ClientHarness',
                    'AbilityKit.Demo.Tiny.LiveRecord.Sample')) {
                Invoke-Dotnet @('build', "src/$name/$name.csproj", '--nologo', '-clp:ErrorsOnly')
            }
        }
        else {
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
        Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.ProtocolEvolution.Sample',
            '--no-launch-profile', '-v:q')
        Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.BattleStyles.Sample',
            '--no-launch-profile', '-v:q')
        Invoke-Dotnet @('run', '--project', 'src/AbilityKit.Demo.Tiny.Record.Sample',
            '--no-launch-profile', '-v:q', '--', (Join-Path $outputRoot 'tiny-record.bin'))
        Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.Client/AbilityKit.Demo.Tiny.Client.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.Turn.StateSample/AbilityKit.Demo.Tiny.Turn.StateSample.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.Turn.ClientHarness/AbilityKit.Demo.Tiny.Turn.ClientHarness.csproj',
            '--nologo', '-clp:ErrorsOnly')
        Invoke-Dotnet @('build', 'src/AbilityKit.Demo.Tiny.LiveRecord.Sample/AbilityKit.Demo.Tiny.LiveRecord.Sample.csproj',
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
        }
        Write-Phase 'dotnet' 'passed'

        if ($FocusChapter -eq '01') {
            Write-Phase 'tcp' 'skipped'
            Write-Host "Tiny chapter 01 passed. Evidence: $outputRoot"
            return
        }

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

        if ($FocusChapter) {
            switch ($FocusChapter) {
                '02' { Invoke-RoomSample "tiny-$runId-room" }
                '03' { Invoke-StateSample "tiny-$runId-state" }
                '04' { Invoke-SyncChapterSample 'Frame' "tiny-$runId-frame" }
                '05' { Invoke-SyncChapterSample 'Hybrid' "tiny-$runId-hybrid" }
                '06' { Invoke-SyncChapterSample 'Recovery' "tiny-$runId-recovery" }
                '09' {
                    Write-Phase 'tcp-record' 'running'
                    Invoke-TinyLiveRecord "tiny-$runId-live-record"
                    Write-Phase 'tcp-record' 'passed'
                }
                '10' {
                    Write-Phase 'tcp-turn' 'running'
                    Invoke-TinyTurnSmoke "tiny-$runId-turn"
                    Write-Phase 'tcp-turn' 'passed'
                }
            }
            Write-Phase 'tcp-process' 'skipped'
        }
        elseif (-not $FocusTurnRecord) {
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
        Write-Phase 'tcp-process' 'running'
        foreach ($mode in @('State', 'Frame', 'Hybrid')) {
            Invoke-TinyProcessSmoke $mode "tiny-$runId-process-$mode"
        }
        Write-Phase 'tcp-process' 'passed'
        } else {
            Write-Phase 'tcp-process' 'skipped'
        }
        if (-not $FocusChapter) {
            Write-Phase 'tcp-turn' 'running'
            Invoke-TinyTurnSmoke "tiny-$runId-turn"
            Write-Phase 'tcp-turn' 'passed'
            Write-Phase 'tcp-record' 'running'
            Invoke-TinyLiveRecord "tiny-$runId-live-record"
            Write-Phase 'tcp-record' 'passed'
        }
        Write-Phase 'tcp' 'passed'

        if (-not $SkipUnity -and -not $FocusChapter) {
            Write-Phase 'unity-network-playmode' 'running'
            if (-not (Test-Path -LiteralPath $UnityExe)) { throw "Unity editor not found: $UnityExe" }
            $project = Join-Path $outputRoot 'unity-project'
            & (Join-Path $PSScriptRoot 'create-tiny-validation-project.ps1') -OutputPath $project -IncludeTurn
            Assert-TinyPackageDistribution $project
            $previousUnityPort = $env:ABILITYKIT_TINY_UNITY_PORT
            $previousUnityPrefix = $env:ABILITYKIT_TINY_UNITY_PREFIX
            $previousUnityEvidence = $env:ABILITYKIT_TINY_UNITY_EVIDENCE
            if (-not $FocusTurnRecord) {
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
            } else {
                Write-Phase 'unity-network-playmode' 'skipped'
            }

            Write-Phase 'unity-turn-network-playmode' 'running'
            $turnResults = Join-Path $outputRoot 'tiny-turn-network-playmode.xml'
            $turnLog = Join-Path $outputRoot 'tiny-turn-network-unity.log'
            $turnEvidencePath = Join-Path $outputRoot 'tiny-turn-network.json'
            try {
                $env:ABILITYKIT_TINY_UNITY_PORT = [string]$TcpPort
                $env:ABILITYKIT_TINY_UNITY_PREFIX = "tiny-$runId-turn-unity"
                $env:ABILITYKIT_TINY_UNITY_EVIDENCE = $turnEvidencePath
                $turnArguments = @(
                    '-batchmode', '-nographics', '-projectPath', $project,
                    '-runTests', '-testPlatform', 'PlayMode',
                    '-assemblyNames', 'AbilityKit.Demo.Tiny.Turn.Network.PlayMode.Tests',
                    '-testResults', $turnResults,
                    '-logFile', $turnLog)
                for ($attempt = 1; $attempt -le 4; $attempt++) {
                    $turnExitCode = Invoke-UnityBatch $turnArguments
                    if ($turnExitCode -eq 0 -and (Test-Path -LiteralPath $turnResults)) { break }
                    $logText = if (Test-Path -LiteralPath $turnLog) {
                        Get-Content -LiteralPath $turnLog -Raw -Encoding UTF8
                    } else { '' }
                    if ($attempt -lt 4 -and $logText.Contains('MovedFromExtractor') -and
                        $logText.Contains('-1073741757')) {
                        Copy-Item -LiteralPath $turnLog -Destination `
                            (Join-Path $outputRoot "tiny-turn-import-attempt-$attempt.log")
                        Write-Warning "Tiny Turn import helper failed on attempt $attempt; retrying."
                        continue
                    }
                    throw "Tiny Turn Unity network PlayMode failed. Log: $turnLog"
                }
            }
            finally {
                $env:ABILITYKIT_TINY_UNITY_PORT = $previousUnityPort
                $env:ABILITYKIT_TINY_UNITY_PREFIX = $previousUnityPrefix
                $env:ABILITYKIT_TINY_UNITY_EVIDENCE = $previousUnityEvidence
            }
            [xml]$turnXml = Get-Content -LiteralPath $turnResults -Raw -Encoding UTF8
            $turnRun = $turnXml.'test-run'
            if ($null -eq $turnRun -or [int]$turnRun.total -ne 2 -or
                [int]$turnRun.passed -ne 2 -or [int]$turnRun.failed -ne 0 -or
                -not (Test-Path -LiteralPath $turnEvidencePath)) {
                throw "Tiny Turn Unity network evidence is incomplete: $turnResults"
            }
            $turnEvidence = Get-Content -LiteralPath $turnEvidencePath -Raw -Encoding UTF8 |
                ConvertFrom-Json
            if ($turnEvidence.turn -ne 3 -or $turnEvidence.winnerId -ne 1 -or
                -not $turnEvidence.guestRestored -or -not $turnEvidence.returnedToLobby -or
                [string]::IsNullOrWhiteSpace($turnEvidence.roomId)) {
                throw "Tiny Turn Unity network result is incomplete: $turnEvidencePath"
            }
            Write-Phase 'unity-turn-network-playmode' 'passed'

            if (-not $FocusTurnRecord) {
            Write-Phase 'unity-consumer-network-playmode' 'running'
            $consumerProject = Join-Path $outputRoot 'consumer-project'
            & (Join-Path $PSScriptRoot 'create-tiny-validation-project.ps1') `
                -OutputPath $consumerProject -Standalone
            Assert-TinyPackageDistribution $consumerProject
            $consumerNetworkResults = Join-Path $outputRoot 'tiny-consumer-network-playmode.xml'
            $consumerNetworkLog = Join-Path $outputRoot 'tiny-consumer-network-unity.log'
            $consumerNetworkEvidencePath = Join-Path $outputRoot 'tiny-consumer-network.json'
            try {
                $env:ABILITYKIT_TINY_UNITY_PORT = [string]$TcpPort
                $env:ABILITYKIT_TINY_UNITY_PREFIX = "tiny-$runId-consumer"
                $env:ABILITYKIT_TINY_UNITY_EVIDENCE = $consumerNetworkEvidencePath
                $consumerNetworkArguments = @(
                    '-batchmode', '-nographics', '-projectPath', $consumerProject,
                    '-runTests', '-testPlatform', 'PlayMode',
                    '-assemblyNames', 'TinyConsumer.Network.PlayMode.Tests',
                    '-testResults', $consumerNetworkResults,
                    '-logFile', $consumerNetworkLog)
                for ($attempt = 1; $attempt -le 4; $attempt++) {
                    $consumerNetworkExitCode = Invoke-UnityBatch $consumerNetworkArguments
                    if ($consumerNetworkExitCode -eq 0 -and
                        (Test-Path -LiteralPath $consumerNetworkResults)) { break }
                    $logText = if (Test-Path -LiteralPath $consumerNetworkLog) {
                        Get-Content -LiteralPath $consumerNetworkLog -Raw -Encoding UTF8
                    } else { '' }
                    if ($attempt -lt 4 -and $logText.Contains('MovedFromExtractor') -and
                        $logText.Contains('-1073741757')) {
                        Copy-Item -LiteralPath $consumerNetworkLog -Destination `
                            (Join-Path $outputRoot "tiny-consumer-network-import-attempt-$attempt.log")
                        Write-Warning "Tiny consumer network import helper failed on attempt $attempt; retrying."
                        continue
                    }
                    throw "Tiny consumer network PlayMode failed. Log: $consumerNetworkLog"
                }
            }
            finally {
                $env:ABILITYKIT_TINY_UNITY_PORT = $previousUnityPort
                $env:ABILITYKIT_TINY_UNITY_PREFIX = $previousUnityPrefix
                $env:ABILITYKIT_TINY_UNITY_EVIDENCE = $previousUnityEvidence
            }
            [xml]$consumerNetworkXml = Get-Content -LiteralPath $consumerNetworkResults -Raw -Encoding UTF8
            $consumerNetworkRun = $consumerNetworkXml.'test-run'
            if ($null -eq $consumerNetworkRun -or [int]$consumerNetworkRun.total -ne 1 -or
                [int]$consumerNetworkRun.passed -ne 1 -or [int]$consumerNetworkRun.failed -ne 0 -or
                -not (Test-Path -LiteralPath $consumerNetworkEvidencePath)) {
                throw "Tiny consumer network evidence is incomplete: $consumerNetworkResults"
            }
            $consumerNetworkEvidence = Get-Content -LiteralPath $consumerNetworkEvidencePath `
                -Raw -Encoding UTF8 | ConvertFrom-Json
            if (@($consumerNetworkEvidence.modes).Count -ne 3 -or
                @($consumerNetworkEvidence.modes | Where-Object {
                    -not $_.sceneRootReady -or -not $_.ownerProjected -or
                    -not $_.guestConverged -or -not $_.guestRestored -or
                    -not $_.returnedToLobby -or
                    ($_.mode -eq 'Hybrid') -ne $_.predictedBeforeConfirmation -or
                    [string]::IsNullOrWhiteSpace($_.roomId)
                }).Count -gt 0) {
                throw "Tiny consumer network mode evidence is incomplete: $consumerNetworkEvidencePath"
            }
            Write-Phase 'unity-consumer-network-playmode' 'passed'
            } else {
                Write-Phase 'unity-consumer-network-playmode' 'skipped'
            }

            Write-Phase 'unity-consumer-turn-network-playmode' 'running'
            $consumerTurnProject = Join-Path $outputRoot 'consumer-turn-project'
            & (Join-Path $PSScriptRoot 'create-tiny-validation-project.ps1') `
                -OutputPath $consumerTurnProject -Standalone -IncludeTurn
            Assert-TinyPackageDistribution $consumerTurnProject
            $consumerTurnResults = Join-Path $outputRoot 'tiny-consumer-turn-network-playmode.xml'
            $consumerTurnLog = Join-Path $outputRoot 'tiny-consumer-turn-network-unity.log'
            $consumerTurnEvidencePath = Join-Path $outputRoot 'tiny-consumer-turn-network.json'
            try {
                $env:ABILITYKIT_TINY_UNITY_PORT = [string]$TcpPort
                $env:ABILITYKIT_TINY_UNITY_PREFIX = "tiny-$runId-consumer-turn"
                $env:ABILITYKIT_TINY_UNITY_EVIDENCE = $consumerTurnEvidencePath
                $consumerTurnArguments = @(
                    '-batchmode', '-nographics', '-projectPath', $consumerTurnProject,
                    '-runTests', '-testPlatform', 'PlayMode',
                    '-assemblyNames', 'TinyConsumer.Turn.Network.PlayMode.Tests',
                    '-testResults', $consumerTurnResults,
                    '-logFile', $consumerTurnLog)
                for ($attempt = 1; $attempt -le 4; $attempt++) {
                    $consumerTurnExitCode = Invoke-UnityBatch $consumerTurnArguments
                    if ($consumerTurnExitCode -eq 0 -and
                        (Test-Path -LiteralPath $consumerTurnResults)) { break }
                    $logText = if (Test-Path -LiteralPath $consumerTurnLog) {
                        Get-Content -LiteralPath $consumerTurnLog -Raw -Encoding UTF8
                    } else { '' }
                    if ($attempt -lt 4 -and $logText.Contains('MovedFromExtractor') -and
                        $logText.Contains('-1073741757')) {
                        Copy-Item -LiteralPath $consumerTurnLog -Destination `
                            (Join-Path $outputRoot "tiny-consumer-turn-import-attempt-$attempt.log")
                        Write-Warning "Tiny consumer Turn import helper failed on attempt $attempt; retrying."
                        continue
                    }
                    throw "Tiny consumer Turn PlayMode failed. Log: $consumerTurnLog"
                }
            }
            finally {
                $env:ABILITYKIT_TINY_UNITY_PORT = $previousUnityPort
                $env:ABILITYKIT_TINY_UNITY_PREFIX = $previousUnityPrefix
                $env:ABILITYKIT_TINY_UNITY_EVIDENCE = $previousUnityEvidence
            }
            [xml]$consumerTurnXml = Get-Content -LiteralPath $consumerTurnResults -Raw -Encoding UTF8
            $consumerTurnRun = $consumerTurnXml.'test-run'
            if ($null -eq $consumerTurnRun -or [int]$consumerTurnRun.total -ne 1 -or
                [int]$consumerTurnRun.passed -ne 1 -or [int]$consumerTurnRun.failed -ne 0 -or
                -not (Test-Path -LiteralPath $consumerTurnEvidencePath)) {
                throw "Tiny consumer Turn result is incomplete: $consumerTurnResults"
            }
            $consumerTurnEvidence = Get-Content -LiteralPath $consumerTurnEvidencePath `
                -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($consumerTurnEvidence.turn -ne 3 -or $consumerTurnEvidence.winnerId -ne 1 -or
                -not $consumerTurnEvidence.sceneRootReady -or
                -not $consumerTurnEvidence.returnedToLobby -or
                [string]::IsNullOrWhiteSpace($consumerTurnEvidence.roomId)) {
                throw "Tiny consumer Turn evidence is incomplete: $consumerTurnEvidencePath"
            }
            Write-Phase 'unity-consumer-turn-network-playmode' 'passed'
            if (-not $FocusTurnRecord -or $VerifyCrossProcess) {
                Write-Phase 'unity-cross-process' 'running'
                foreach ($mode in @('State', 'Turn')) {
                    Invoke-TinyCrossProcessUnity $mode
                }
                Write-Phase 'unity-cross-process' 'passed'
            } else {
                Write-Phase 'unity-cross-process' 'skipped'
            }
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

if (-not $SkipUnity -and -not $FocusTurnRecord -and -not $FocusChapter) {
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
    if ($null -eq $consumerProject) { throw 'Tiny consumer project was not created.' }
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
    if ($SkipUnity) {
        Write-Phase 'unity-network-playmode' 'skipped'
        Write-Phase 'unity-turn-network-playmode' 'skipped'
        Write-Phase 'unity-consumer-network-playmode' 'skipped'
        Write-Phase 'unity-consumer-turn-network-playmode' 'skipped'
    }
    Write-Phase 'unity-editmode' 'skipped'
    Write-Phase 'unity-playmode' 'skipped'
    Write-Phase 'unity-consumer-playmode' 'skipped'
    Write-Phase 'unity-logic-editmode' 'skipped'
}

Write-Host "Tiny starter verification passed. Evidence: $outputRoot"
exit 0
