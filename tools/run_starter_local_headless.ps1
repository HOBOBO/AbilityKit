[CmdletBinding()]
param(
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe',
    [string]$ProjectPath = '',
    [string]$ArtifactsRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $PSScriptRoot '..\Unity'
}
if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    $ArtifactsRoot = Join-Path $PSScriptRoot '..\artifacts\starter-local-headless'
}
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
$ArtifactsRoot = [IO.Path]::GetFullPath($ArtifactsRoot)
if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
    throw "Unity editor was not found: $UnityExe"
}
if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    throw "Unity project was not found: $ProjectPath"
}

$runDirectory = Join-Path $ArtifactsRoot ([DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$results = @()
foreach ($gameplay in @('Moba', 'Shooter')) {
    $resultPath = Join-Path $runDirectory ($gameplay.ToLowerInvariant() + '-result.json')
    $logPath = Join-Path $runDirectory ($gameplay.ToLowerInvariant() + '-unity.log')
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $ProjectPath,
        '-executeMethod', 'AbilityKit.Game.Editor.Automation.StarterLocalLaunchHeadlessCommand.Run',
        '-starterGameplay', $gameplay,
        '-starterResult', $resultPath,
        '-logFile', $logPath
    )

    $process = Start-Process `
        -FilePath $UnityExe `
        -ArgumentList $arguments `
        -WorkingDirectory $ProjectPath `
        -Wait `
        -PassThru
    $exitCode = $process.ExitCode
    if ($exitCode -ne 0) {
        throw "$gameplay Starter local launch failed with Unity exit code $exitCode. Log: $logPath"
    }
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "$gameplay Starter local launch produced no result file: $resultPath"
    }

    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if (-not [bool]$result.success) {
        throw "$gameplay Starter local launch reported failure: $($result.message)"
    }
    if ([string]$result.gameplay -ne $gameplay -or [string]$result.mode -ne 'Local') {
        throw "$gameplay result has an unexpected launch identity: $($result.gameplay)/$($result.mode)"
    }

    $results += $result
    Write-Host "PASS ${gameplay}: scene=$($result.scenePath), profile=$($result.profileId)"
}

$summaryPath = Join-Path $runDirectory 'summary.json'
@{
    success = $true
    generatedAt = [DateTime]::UtcNow.ToString('O')
    projectPath = $ProjectPath
    results = $results
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host "Starter local launch headless flow passed for MOBA and Shooter. Artifacts: $runDirectory"
