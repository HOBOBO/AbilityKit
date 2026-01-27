param(
    [int]$GatewayPort = 5001,
    [int]$SiloPort = 11111,
    [int]$SiloGatewayPort = 30000,
    [switch]$KillPortsFirst
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$src = Join-Path $root "src"

$hostProj = Join-Path $src "AbilityKit.Orleans.Host\AbilityKit.Orleans.Host.csproj"
$gatewayProj = Join-Path $src "AbilityKit.Orleans.Gateway\AbilityKit.Orleans.Gateway.csproj"

if ($KillPortsFirst) {
    $kill = Join-Path $PSScriptRoot "kill_tcp_ports.ps1"
    if (Test-Path $kill) {
        & $kill -Ports @($GatewayPort)
    }
}

if (!(Test-Path $hostProj)) { throw "Host csproj not found: $hostProj" }
if (!(Test-Path $gatewayProj)) { throw "Gateway csproj not found: $gatewayProj" }

Write-Host "Starting Orleans Silo Host..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
    '-NoExit',
    '-Command',
    "dotnet run --project `"$hostProj`""
) | Out-Null

Start-Sleep -Seconds 1

Write-Host "Starting Orleans Gateway..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
    '-NoExit',
    '-Command',
    "dotnet run --project `"$gatewayProj`""
) | Out-Null

Write-Host "" 
Write-Host "Gateway:" -ForegroundColor Green
Write-Host "  http://localhost:$GatewayPort/health" 
Write-Host "  http://localhost:$GatewayPort/debug/" 
Write-Host "" 
Write-Host "Note: start order is Host then Gateway. If Gateway fails to connect, wait Host ready and restart Gateway." 
