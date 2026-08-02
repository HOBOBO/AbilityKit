param(
    [int]$GatewayPort = 5001,
    [int]$SiloPort = 11111,
    [int]$SiloGatewayPort = 30000,
    [int]$TcpPort = 4000,
    [int]$PrimarySiloPort,
    [object[]]$Silos,
    [string]$ClusterId = 'abilitykit-dev',
    [string]$ServiceId = 'abilitykit-orleans',
    [string]$InstanceName = 'dev',
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [switch]$NoCleanup,
    [switch]$CleanAll,
    [switch]$ForceStartGateway,
    [int]$SiloGatewayWaitSeconds = 120
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'abilitykit_process_utils.ps1')

function Get-ObjectValue($object, [string]$name, $fallback) {
    if ($null -ne $object -and $object.PSObject.Properties.Name -contains $name -and $null -ne $object.$name) {
        return $object.$name
    }

    return $fallback
}

function ConvertTo-StringArray($value) {
    if ($null -eq $value) {
        return @()
    }

    return @($value | ForEach-Object { [string]$_ })
}

function Add-ConfigArg([System.Collections.Generic.List[string]]$configArguments, [string]$key, [object]$value) {
    $configArguments.Add($key)
    $configArguments.Add([string]$value)
}

function ConvertTo-CommandLine([string[]]$commandArguments) {
    ($commandArguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    }) -join ' '
}

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$src = Join-Path $root 'src'

$hostProj = Join-Path $src 'AbilityKit.Orleans.Host\AbilityKit.Orleans.Host.csproj'
$gatewayProj = Join-Path $src 'AbilityKit.Orleans.Gateway\AbilityKit.Orleans.Gateway.csproj'
$gatewayWorkingDirectory = Split-Path -Parent $gatewayProj
$siloScript = Join-Path $PSScriptRoot 'start_orleans_silo.ps1'
$healthUri = "http://localhost:$GatewayPort/health/ready"
$healthLiveUri = "http://localhost:$GatewayPort/health/live"
$adminUri = "http://localhost:$GatewayPort/admin"
$instanceLogs = Join-Path $root (Join-Path 'logs' $InstanceName)

if (!(Test-Path $hostProj)) { throw "Host csproj not found: $hostProj" }
if (!(Test-Path $gatewayProj)) { throw "Gateway csproj not found: $gatewayProj" }
if (!(Test-Path $siloScript)) { throw "Silo start script not found: $siloScript" }

if (-not $PSBoundParameters.ContainsKey('PrimarySiloPort') -or $PrimarySiloPort -le 0) {
    $PrimarySiloPort = $SiloPort
}

if (-not $Silos -or $Silos.Count -eq 0) {
    $Silos = @(
        [pscustomobject]@{
            role = 'Shared'
            siloPort = $SiloPort
            siloGatewayPort = $SiloGatewayPort
            primarySiloPort = $PrimarySiloPort
            logicalGroups = @('session', 'room', 'battle')
            maxRooms = 500
            maxBattles = 200
            maxSessions = 5000
        }
    )
}

$siloSpecs = @($Silos | ForEach-Object {
    [pscustomobject]@{
        Role = [string](Get-ObjectValue $_ 'role' 'Shared')
        SiloPort = [int](Get-ObjectValue $_ 'siloPort' $SiloPort)
        SiloGatewayPort = [int](Get-ObjectValue $_ 'siloGatewayPort' $SiloGatewayPort)
        PrimarySiloPort = [int](Get-ObjectValue $_ 'primarySiloPort' $PrimarySiloPort)
        LogicalGroups = ConvertTo-StringArray (Get-ObjectValue $_ 'logicalGroups' @())
        IsExclusive = [bool](Get-ObjectValue $_ 'isExclusive' $false)
        MaxRooms = [int](Get-ObjectValue $_ 'maxRooms' 0)
        MaxBattles = [int](Get-ObjectValue $_ 'maxBattles' 0)
        MaxSessions = [int](Get-ObjectValue $_ 'maxSessions' 0)
    }
})

New-Item -ItemType Directory -Force -Path $instanceLogs | Out-Null

$managedPorts = @($GatewayPort, $TcpPort) + @($siloSpecs | ForEach-Object { @($_.SiloPort, $_.SiloGatewayPort) })
$managedPorts = @($managedPorts | Sort-Object -Unique)
$managedProjects = if ($CleanAll) {
    @(
        'AbilityKit.Orleans.Host.csproj',
        'AbilityKit.Orleans.Gateway.csproj'
    )
}
else {
    @()
}

if (-not $NoCleanup) {
    Stop-AbilityKitServices -Ports $managedPorts -CommandPatterns $managedProjects -GraceSeconds 2
}

$commonArgs = @(
    '-p:UseSharedCompilation=false',
    '-p:nodeReuse=false'
)

$startupStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$noBuildArg = '--no-build'

if (-not $NoBuild) {
    Write-Host 'Building Orleans Host and Gateway...' -ForegroundColor Cyan
    $buildStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet build $hostProj -c $Configuration @commonArgs
    dotnet build $gatewayProj -c $Configuration @commonArgs
    $buildStopwatch.Stop()
    Write-Host ("Build completed in {0:n1}s. Runtime windows will use dotnet run --no-build to avoid rebuilding." -f $buildStopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
}
else {
    Write-Host 'Skipping build because -NoBuild was specified. Runtime windows will use dotnet run --no-build.' -ForegroundColor Yellow
}

Write-Host "Starting AbilityKit instance '$InstanceName'..." -ForegroundColor Green
Write-Host "  ClusterId:        $ClusterId" -ForegroundColor Gray
Write-Host "  ServiceId:        $ServiceId" -ForegroundColor Gray
Write-Host "  Primary Silo:     127.0.0.1:$PrimarySiloPort" -ForegroundColor Gray
Write-Host "  HTTP Gateway:     $GatewayPort" -ForegroundColor Gray
Write-Host "  TCP Gateway:      $TcpPort" -ForegroundColor Gray
Write-Host "  Logs:             $instanceLogs" -ForegroundColor Gray
Write-Host '  Silo roles:' -ForegroundColor Gray
foreach ($silo in $siloSpecs) {
    Write-Host "    $($silo.Role): Silo $($silo.SiloPort), Orleans Gateway $($silo.SiloGatewayPort), Groups $($silo.LogicalGroups -join ', ')" -ForegroundColor Gray
}
Write-Host ''

foreach ($silo in $siloSpecs) {
    $siloParams = @{
        ClusterId = $ClusterId
        ServiceId = $ServiceId
        InstanceName = $InstanceName
        Role = $silo.Role
        SiloPort = $silo.SiloPort
        SiloGatewayPort = $silo.SiloGatewayPort
        PrimarySiloPort = $silo.PrimarySiloPort
        Configuration = $Configuration
        NoBuild = $true
        NoCleanup = $true
        SiloGatewayWaitSeconds = $SiloGatewayWaitSeconds
        MaxRooms = $silo.MaxRooms
        MaxBattles = $silo.MaxBattles
        MaxSessions = $silo.MaxSessions
    }

    if ($silo.LogicalGroups -and $silo.LogicalGroups.Count -gt 0) {
        $siloParams.LogicalGroups = $silo.LogicalGroups
    }

    if ($silo.IsExclusive) {
        $siloParams.IsExclusive = $true
    }

    & $siloScript @siloParams
}

$clientGatewayPort = ($siloSpecs | Select-Object -First 1).SiloGatewayPort
Write-Host "Waiting for Orleans client gateway endpoint 127.0.0.1:$clientGatewayPort ..." -ForegroundColor Cyan
$siloWaitStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$siloGatewayReady = $false
$siloGatewayDeadline = (Get-Date).AddSeconds($SiloGatewayWaitSeconds)
while ((Get-Date) -lt $siloGatewayDeadline) {
    if (Test-AbilityKitTcpPort -HostName '127.0.0.1' -Port $clientGatewayPort -TimeoutMilliseconds 1000) {
        $siloGatewayReady = $true
        break
    }

    Start-Sleep -Seconds 1
}

$siloWaitStopwatch.Stop()

if (-not $siloGatewayReady) {
    Write-Host ("Orleans client gateway: not listening after {0:n1}s (127.0.0.1:$clientGatewayPort)" -f $siloWaitStopwatch.Elapsed.TotalSeconds) -ForegroundColor Yellow
    Write-Host "  Host may still be building/starting, may have exited, or may be blocked by another process/port." -ForegroundColor Yellow
    if (-not $ForceStartGateway) {
        Write-Host "  Gateway startup skipped to avoid an immediate Orleans client ConnectionRefused failure." -ForegroundColor Yellow
        Write-Host "  Re-run with -ForceStartGateway only if you intentionally want to start the HTTP Gateway before the Silo Gateway port is ready." -ForegroundColor Yellow
        exit 1
    }

    Write-Host "  -ForceStartGateway specified; Gateway will still be started, but Orleans client startup may fail until the silo binds this port." -ForegroundColor Yellow
}
else {
    Write-Host ("Orleans client gateway: OK after {0:n1}s (127.0.0.1:$clientGatewayPort)" -f $siloWaitStopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
}

$gatewayConfigArgs = [System.Collections.Generic.List[string]]::new()
Add-ConfigArg $gatewayConfigArgs '--AbilityKit:Orleans:ClusterId' $ClusterId
Add-ConfigArg $gatewayConfigArgs '--AbilityKit:Orleans:ServiceId' $ServiceId
Add-ConfigArg $gatewayConfigArgs '--AbilityKit:Orleans:GatewayPort' $clientGatewayPort
Add-ConfigArg $gatewayConfigArgs '--AbilityKit:Gateway:Http:Port' $GatewayPort
Add-ConfigArg $gatewayConfigArgs '--AbilityKit:Gateway:Tcp:Port' $TcpPort
Add-ConfigArg $gatewayConfigArgs '--TcpGateway:Port' $TcpPort
Add-ConfigArg $gatewayConfigArgs '--AbilityKit:Deployment:Role' 'Gateway'
Add-ConfigArg $gatewayConfigArgs '--AbilityKit:Deployment:SiloRole:Role' 'Gateway'
Add-ConfigArg $gatewayConfigArgs '--AbilityKit:Deployment:SiloRole:IsGateway' 'true'
Add-ConfigArg $gatewayConfigArgs '--AbilityKit:Deployment:RuntimeProfile:Role' 'Gateway'
Add-ConfigArg $gatewayConfigArgs '--AbilityKit:Deployment:RuntimeProfile:IsGateway' 'true'
$gatewayConfigArgArray = $gatewayConfigArgs.ToArray()
$gatewayConfigLine = ConvertTo-CommandLine -commandArguments $gatewayConfigArgArray

Write-Host 'Starting Orleans Gateway...' -ForegroundColor Cyan
$gatewayLog = Join-Path $instanceLogs 'gateway.log'
$gatewayCommand = "`$Host.UI.RawUI.WindowTitle = 'AbilityKit $InstanceName Gateway'; dotnet run --project `"$gatewayProj`" -c $Configuration $noBuildArg -- $gatewayConfigLine 2>&1 | Tee-Object -FilePath `"$gatewayLog`" -Append"
$gatewayArgs = @('-NoExit', '-NoProfile', '-Command', $gatewayCommand)
$gatewayWindow = Start-Process powershell -ArgumentList $gatewayArgs -WorkingDirectory $gatewayWorkingDirectory -PassThru -WindowStyle Normal
Write-Host "  Gateway window PID: $($gatewayWindow.Id)" -ForegroundColor Gray

$gatewayHealthStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$httpReady = Wait-AbilityKitHttpEndpoint -Uri $healthUri -TimeoutSeconds 30
if (-not $httpReady) {
    $httpReady = Wait-AbilityKitHttpEndpoint -Uri $healthLiveUri -TimeoutSeconds 5
}

$tcpReady = $false
$tcpDeadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $tcpDeadline) {
    if (Test-AbilityKitTcpPort -HostName '127.0.0.1' -Port $TcpPort -TimeoutMilliseconds 1000) {
        $tcpReady = $true
        break
    }

    Start-Sleep -Milliseconds 500
}

$gatewayHealthStopwatch.Stop()
if ($httpReady -and $tcpReady) {
    Write-Host ("Gateway HTTP and TCP: OK after {0:n1}s ($healthUri; 127.0.0.1:$TcpPort)" -f $gatewayHealthStopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
}
else {
    Write-Host ("Gateway startup incomplete after {0:n1}s." -f $gatewayHealthStopwatch.Elapsed.TotalSeconds) -ForegroundColor Yellow
    if (-not $httpReady) {
        Write-Host "  HTTP health endpoint is not reachable: $healthUri" -ForegroundColor Yellow
    }
    if (-not $tcpReady) {
        Write-Host "  Game TCP gateway is not reachable: 127.0.0.1:$TcpPort" -ForegroundColor Yellow
    }
    if (-not (Test-AbilityKitTcpPort -HostName '127.0.0.1' -Port $clientGatewayPort -TimeoutMilliseconds 1000)) {
        Write-Host "  Orleans client gateway is not reachable: 127.0.0.1:$clientGatewayPort" -ForegroundColor Yellow
    }
    Write-Host "  Please inspect logs under: $instanceLogs" -ForegroundColor Yellow
    exit 1
}

$startupStopwatch.Stop()
Write-Host ("Startup script orchestration completed in {0:n1}s." -f $startupStopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
Write-Host ''
Write-Host 'Gateway:' -ForegroundColor Green
Write-Host "  Admin: $adminUri"
Write-Host "  HTTP:  $healthUri"
Write-Host "  TCP:   127.0.0.1:$TcpPort"
Write-Host ''
Write-Host 'Add another local silo with start_orleans_silo.ps1 while the primary silo remains running.' -ForegroundColor Gray
Write-Host "Stop this instance with: powershell -ExecutionPolicy Bypass -File `"$PSScriptRoot\stop_abilitykit.ps1`" -Profile $InstanceName" -ForegroundColor Gray
Write-Host 'Close the spawned PowerShell windows to stop services manually, or run the stop script for a clean full exit.' -ForegroundColor Gray
