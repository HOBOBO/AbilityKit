[CmdletBinding()]
param(
    [string]$OwnerProject,
    [string]$MemberProject,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe',
    [string]$GatewayHost = '127.0.0.1',
    [int]$GatewayPort = 4000,
    [string]$GatewayRegion = 'dev',
    [string]$GatewayServerId = 'local',
    [string]$SyncTemplateId = 'mass-battle-lod-aoi',
    [int]$SyncModel = 5,
    [int[]]$EnemyBudgets = @(512, 2000),
    [ValidateSet('gameobject', 'gpu')]
    [string]$ViewBackend = 'gameobject',
    [ValidateSet('ideal', 'lan', 'mobile4g', 'crossregion', 'poorwifi', 'limitedbw')]
    [string[]]$NetworkEnvironments = @('ideal', 'mobile4g', 'poorwifi', 'limitedbw'),
    [int]$TimeoutSeconds = 300,
    [string]$OutputRoot,
    [switch]$SkipCompileWarmup
)

$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot 'run_shooter_unity_headless_multiplayer.ps1'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $PSScriptRoot '..\artifacts\shooter-sync-performance-matrix'
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

$results = [System.Collections.Generic.List[object]]::new()
$caseIndex = 0
foreach ($enemyBudget in $EnemyBudgets) {
    foreach ($networkEnvironment in $NetworkEnvironments) {
        $caseIndex++
        $caseRoot = Join-Path $OutputRoot ("enemies-{0}-{1}" -f $enemyBudget, $networkEnvironment)
        New-Item -ItemType Directory -Path $caseRoot -Force | Out-Null
        $startedAt = [DateTime]::UtcNow
        $succeeded = $false
        $errorMessage = ''
        try {
            $arguments = @{
                UnityExe = $UnityExe
                GatewayHost = $GatewayHost
                GatewayPort = $GatewayPort
                GatewayRegion = $GatewayRegion
                GatewayServerId = $GatewayServerId
                SyncTemplateId = $SyncTemplateId
                SyncModel = $SyncModel
                NetworkEnvironmentId = $networkEnvironment
                EnemyBudget = $enemyBudget
                ViewBackend = $ViewBackend
                TimeoutSeconds = $TimeoutSeconds
                OutputRoot = $caseRoot
                SkipCompileWarmup = $SkipCompileWarmup.IsPresent -or $caseIndex -gt 1
            }
            if (-not [string]::IsNullOrWhiteSpace($OwnerProject)) { $arguments.OwnerProject = $OwnerProject }
            if (-not [string]::IsNullOrWhiteSpace($MemberProject)) { $arguments.MemberProject = $MemberProject }
            & $runner @arguments
            $succeeded = $true
        }
        catch {
            $errorMessage = $_.Exception.Message
        }

        $runDirectory = Get-ChildItem -LiteralPath $caseRoot -Directory |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        $ownerResult = $null
        $memberResult = $null
        if ($runDirectory) {
            $ownerPath = Join-Path $runDirectory.FullName 'owner-result.json'
            $memberPath = Join-Path $runDirectory.FullName 'member-result.json'
            if (Test-Path $ownerPath -PathType Leaf) { $ownerResult = Get-Content $ownerPath -Raw | ConvertFrom-Json }
            if (Test-Path $memberPath -PathType Leaf) { $memberResult = Get-Content $memberPath -Raw | ConvertFrom-Json }
        }

        $results.Add([pscustomobject]@{
            enemyBudget = $enemyBudget
            networkEnvironment = $networkEnvironment
            success = $succeeded
            error = $errorMessage
            durationSeconds = [Math]::Round(([DateTime]::UtcNow - $startedAt).TotalSeconds, 3)
            artifactDirectory = if ($runDirectory) { $runDirectory.FullName } else { '' }
            owner = if ($ownerResult) { $ownerResult.state } else { $null }
            member = if ($memberResult) { $memberResult.state } else { $null }
        })
    }
}

$summaryPath = Join-Path $OutputRoot 'matrix-summary.json'
$summary = [pscustomobject]@{
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    syncTemplateId = $SyncTemplateId
    syncModel = $SyncModel
    viewBackend = $ViewBackend
    caseCount = $results.Count
    passedCount = @($results | Where-Object success).Count
    failedCount = @($results | Where-Object { -not $_.success }).Count
    cases = $results
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Host "Shooter sync performance matrix: $($summary.passedCount)/$($summary.caseCount) passed."
Write-Host "Summary: $summaryPath"
if ($summary.failedCount -gt 0) {
    throw "Shooter sync performance matrix has $($summary.failedCount) failing case(s)."
}
