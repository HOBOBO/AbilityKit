param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

function Stop-DotNetByProject([string]$projectName)
{
    $procs = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like "*AbilityKit.Orleans.$projectName*" }

    foreach ($p in $procs)
    {
        Write-Host "Stopping dotnet PID=$($p.ProcessId) ($projectName)" -ForegroundColor Yellow
        Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

Stop-DotNetByProject 'Host'
Stop-DotNetByProject 'Gateway'

if (-not $NoBuild)
{
    Write-Host 'Building solution...' -ForegroundColor Cyan
    dotnet build .\AbilityKit.Orleans.sln -c Debug
}

Write-Host 'Starting Host...' -ForegroundColor Green
Start-Process -FilePath 'dotnet' -ArgumentList 'run --project .\src\AbilityKit.Orleans.Host\AbilityKit.Orleans.Host.csproj' -WorkingDirectory $PSScriptRoot | Out-Null

Write-Host 'Starting Gateway...' -ForegroundColor Green
Start-Process -FilePath 'dotnet' -ArgumentList 'run --project .\src\AbilityKit.Orleans.Gateway\AbilityKit.Orleans.Gateway.csproj' -WorkingDirectory $PSScriptRoot | Out-Null

Write-Host 'Done.' -ForegroundColor Green
