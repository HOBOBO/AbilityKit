param(
    [int]$SiloPort = 11111,
    [int]$SiloGatewayPort = 30000,
    [int]$PrimarySiloPort,
    [string]$ClusterId = 'abilitykit-dev',
    [string]$ServiceId = 'abilitykit-orleans',
    [string]$InstanceName = 'dev-shared',
    [string]$Role = 'Shared',
    [string[]]$LogicalGroups,
    [switch]$IsExclusive,
    [int]$MaxRooms = 0,
    [int]$MaxBattles = 0,
    [int]$MaxSessions = 0,
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [switch]$NoCleanup,
    [switch]$CleanAll,
    [switch]$HiddenWindows,
    [int]$SiloGatewayWaitSeconds = 120
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'abilitykit_process_utils.ps1')

function Get-RoleLogicalGroups([string]$role, [string[]]$overrideGroups) {
    if ($overrideGroups -and $overrideGroups.Count -gt 0) {
        return @($overrideGroups)
    }

    switch ($role.ToLowerInvariant()) {
        'session' { return @('session') }
        'account' { return @('session') }
        'room' { return @('room') }
        'battle' { return @('battle') }
        'combat' { return @('battle') }
        'gateway' { return @('gateway') }
        default { return @('session', 'room', 'battle') }
    }
}

function Add-ConfigArg([System.Collections.Generic.List[string]]$configArguments, [string]$key, [object]$value) {
    $configArguments.Add($key)
    $configArguments.Add([string]$value)
}

function Add-ConfigArrayArgs([System.Collections.Generic.List[string]]$configArguments, [string]$key, [string[]]$values) {
    for ($i = 0; $i -lt $values.Count; $i++) {
        Add-ConfigArg -configArguments $configArguments -key "$key`:$i" -value $values[$i]
    }
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

if (-not $PSBoundParameters.ContainsKey('PrimarySiloPort') -or $PrimarySiloPort -le 0) {
    $PrimarySiloPort = $SiloPort
}

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$src = Join-Path $root 'src'
$hostProj = Join-Path $src 'AbilityKit.Orleans.Host\AbilityKit.Orleans.Host.csproj'
$hostWorkingDirectory = Split-Path -Parent $hostProj
$instanceLogs = Join-Path $root (Join-Path 'logs' $InstanceName)
$logicalGroupsValue = Get-RoleLogicalGroups $Role $LogicalGroups
$preferredAffinity = if ($Role -eq 'Shared') { @('Session', 'Room', 'Battle') } else { @($Role) }

if (!(Test-Path $hostProj)) { throw "Host csproj not found: $hostProj" }
New-Item -ItemType Directory -Force -Path $instanceLogs | Out-Null

$managedPorts = @($SiloPort, $SiloGatewayPort)
$managedProjects = if ($CleanAll) { @('AbilityKit.Orleans.Host.csproj') } else { @() }
if (-not $NoCleanup) {
    Stop-AbilityKitServices -Ports $managedPorts -CommandPatterns $managedProjects -GraceSeconds 2
}

$commonArgs = @(
    '-p:UseSharedCompilation=false',
    '-p:nodeReuse=false'
)

if (-not $NoBuild) {
    Write-Host "Building Orleans Host for role '$Role'..." -ForegroundColor Cyan
    dotnet build $hostProj -c $Configuration @commonArgs
}
else {
    Write-Host "Skipping host build for role '$Role' because -NoBuild was specified." -ForegroundColor Yellow
}

$configArgs = [System.Collections.Generic.List[string]]::new()
Add-ConfigArg $configArgs '--AbilityKit:Orleans:ClusterId' $ClusterId
Add-ConfigArg $configArgs '--AbilityKit:Orleans:ServiceId' $ServiceId
Add-ConfigArg $configArgs '--AbilityKit:Orleans:SiloPort' $SiloPort
Add-ConfigArg $configArgs '--AbilityKit:Orleans:GatewayPort' $SiloGatewayPort
Add-ConfigArg $configArgs '--AbilityKit:Orleans:PrimarySiloPort' $PrimarySiloPort
Add-ConfigArg $configArgs '--AbilityKit:Deployment:Role' $Role
Add-ConfigArrayArgs $configArgs '--AbilityKit:Deployment:Groups' $logicalGroupsValue
Add-ConfigArrayArgs $configArgs '--AbilityKit:Deployment:Affinity' $preferredAffinity
Add-ConfigArg $configArgs '--AbilityKit:Deployment:TargetSiloCount' 1
Add-ConfigArg $configArgs '--AbilityKit:Deployment:MaxRoomsPerSilo' $MaxRooms
Add-ConfigArg $configArgs '--AbilityKit:Deployment:MaxBattlesPerSilo' $MaxBattles
Add-ConfigArg $configArgs '--AbilityKit:Deployment:MaxSessionsPerGateway' $MaxSessions
Add-ConfigArg $configArgs '--AbilityKit:Deployment:Mode:Mode' $Role
Add-ConfigArrayArgs $configArgs '--AbilityKit:Deployment:Mode:EnabledRoles' @('Shared', 'Session', 'Room', 'Battle')
Add-ConfigArg $configArgs '--AbilityKit:Deployment:SiloRole:Role' $Role
Add-ConfigArg $configArgs '--AbilityKit:Deployment:SiloRole:IsGateway' 'false'
Add-ConfigArg $configArgs '--AbilityKit:Deployment:SiloRole:IsExclusive' ([string]$IsExclusive.IsPresent).ToLowerInvariant()
Add-ConfigArrayArgs $configArgs '--AbilityKit:Deployment:SiloRole:LogicalGroups' $logicalGroupsValue
Add-ConfigArg $configArgs '--AbilityKit:Deployment:RuntimeProfile:Role' $Role
Add-ConfigArrayArgs $configArgs '--AbilityKit:Deployment:RuntimeProfile:LogicalGroups' $logicalGroupsValue
Add-ConfigArrayArgs $configArgs '--AbilityKit:Deployment:RuntimeProfile:PreferredAffinity' $preferredAffinity
Add-ConfigArg $configArgs '--AbilityKit:Deployment:RuntimeProfile:IsExclusive' ([string]$IsExclusive.IsPresent).ToLowerInvariant()
Add-ConfigArg $configArgs '--AbilityKit:Deployment:RuntimeProfile:IsGateway' 'false'
Add-ConfigArg $configArgs '--AbilityKit:Deployment:RuntimeProfile:MaxRooms' $MaxRooms
Add-ConfigArg $configArgs '--AbilityKit:Deployment:RuntimeProfile:MaxBattles' $MaxBattles
Add-ConfigArg $configArgs '--AbilityKit:Deployment:RuntimeProfile:MaxSessions' $MaxSessions
Add-ConfigArg $configArgs '--AbilityKit:Deployment:RuntimeProfile:Notes' "Local expandable $Role silo."

$configArgArray = $configArgs.ToArray()
$configLine = ConvertTo-CommandLine -commandArguments $configArgArray
$noBuildArg = '--no-build'
$hostLog = Join-Path $instanceLogs "host-$Role-$SiloPort.log"
$hostCommand = "`$Host.UI.RawUI.WindowTitle = 'AbilityKit $InstanceName $Role Silo'; dotnet run --project `"$hostProj`" -c $Configuration $noBuildArg -- $configLine 2>&1 | Tee-Object -FilePath `"$hostLog`" -Append"
$hostArgs = @('-NoExit', '-NoProfile', '-Command', $hostCommand)

Write-Host "Starting Orleans $Role silo for instance '$InstanceName'..." -ForegroundColor Green
Write-Host "  ClusterId:        $ClusterId" -ForegroundColor Gray
Write-Host "  ServiceId:        $ServiceId" -ForegroundColor Gray
Write-Host "  Role:             $Role" -ForegroundColor Gray
Write-Host "  LogicalGroups:    $($logicalGroupsValue -join ', ')" -ForegroundColor Gray
Write-Host "  Primary Silo:     127.0.0.1:$PrimarySiloPort" -ForegroundColor Gray
Write-Host "  Silo:             127.0.0.1:$SiloPort" -ForegroundColor Gray
Write-Host "  Orleans Gateway:  127.0.0.1:$SiloGatewayPort" -ForegroundColor Gray
Write-Host "  Log:              $hostLog" -ForegroundColor Gray

$hostWindowStyle = if ($HiddenWindows) { 'Hidden' } else { 'Normal' }
$hostWindow = Start-Process powershell -ArgumentList $hostArgs -WorkingDirectory $hostWorkingDirectory -PassThru -WindowStyle $hostWindowStyle
Write-Host "  Host window PID: $($hostWindow.Id)" -ForegroundColor Gray

Write-Host "Waiting for Orleans Silo Gateway TCP endpoint 127.0.0.1:$SiloGatewayPort ..." -ForegroundColor Cyan
$waitStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$ready = $false
$deadline = (Get-Date).AddSeconds($SiloGatewayWaitSeconds)
while ((Get-Date) -lt $deadline) {
    if (Test-AbilityKitTcpPort -HostName '127.0.0.1' -Port $SiloGatewayPort -TimeoutMilliseconds 1000) {
        $ready = $true
        break
    }

    Start-Sleep -Seconds 1
}
$waitStopwatch.Stop()

if ($ready) {
    Write-Host ("Orleans $Role silo gateway: OK after {0:n1}s (127.0.0.1:$SiloGatewayPort)" -f $waitStopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
}
else {
    Write-Host ("Orleans $Role silo gateway: not listening after {0:n1}s (127.0.0.1:$SiloGatewayPort)" -f $waitStopwatch.Elapsed.TotalSeconds) -ForegroundColor Yellow
    Write-Host "  Please inspect host log: $hostLog" -ForegroundColor Yellow
}
