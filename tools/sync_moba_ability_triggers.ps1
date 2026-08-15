<#
.SYNOPSIS
  Reconcile console ability-trigger configs with the Unity-authoritative copies.

.DESCRIPTION
  The console demo loads skill triggers from its own snapshot
  (src/AbilityKit.Demo.Moba.Console/Configs/ability/triggers/<category>/),
  keyed by category in trigger_source_manifest.json. That snapshot can drift from the
  authoritative runtime config shipped in the Unity package
  (Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability/triggers/<category>/).

  This tool diffs Unity -> console per file (byte-for-byte SHA256) and reports:
    SAME       both sides identical
    DIFFERENT  console drifted; Unity is authoritative
    MISSING    present in Unity, absent in console
    EXTRA      present in console, absent in Unity (reported only, never deleted)

  Direction is ALWAYS Unity -> console. Default is a dry-run report; pass -Apply to copy
  DIFFERENT + MISSING files verbatim. Pass -FailOnDrift to exit 1 when any drift remains
  (for a CI drift gate).

.PARAMETER UnityRoot
  Authoritative triggers root (Unity package Resources).

.PARAMETER ConsoleRoot
  Console snapshot triggers root.

.PARAMETER Categories
  Subdirectories to reconcile. Default 'skills'. Use 'all' to reconcile every category dir
  under UnityRoot.

.PARAMETER Apply
  Copy DIFFERENT + MISSING files from Unity to console (verbatim). Default is report only.

.PARAMETER FailOnDrift
  Exit 1 if any DIFFERENT or MISSING entries remain after the run.

.PARAMETER ReportPath
  Where to write the JSON drift report. Default artifacts/moba-trigger-sync/report.json.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\sync_moba_ability_triggers.ps1
  powershell -ExecutionPolicy Bypass -File tools\sync_moba_ability_triggers.ps1 -Categories all -Apply
#>
[CmdletBinding()]
param(
    [string]$UnityRoot = 'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability/triggers',
    [string]$ConsoleRoot = 'src/AbilityKit.Demo.Moba.Console/Configs/ability/triggers',
    [string[]]$Categories = @('skills'),
    [switch]$Apply,
    [switch]$FailOnDrift,
    [string]$ReportPath = 'artifacts/moba-trigger-sync/report.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$unityRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $UnityRoot))
$consoleRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ConsoleRoot))
$reportFullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ReportPath))

if (-not (Test-Path -LiteralPath $unityRoot -PathType Container)) { throw "UnityRoot not found: $unityRoot" }
if (-not (Test-Path -LiteralPath $consoleRoot -PathType Container)) { throw "ConsoleRoot not found: $consoleRoot" }

# 'all' = every category directory present under UnityRoot.
if ($Categories.Count -eq 1 -and $Categories[0] -eq 'all') {
    $Categories = @(Get-ChildItem -LiteralPath $unityRoot -Directory | ForEach-Object { $_.Name })
}

function Get-FileMap {
    param([string]$Dir)
    if (-not (Test-Path -LiteralPath $Dir -PathType Container)) { return @{} }
    $map = @{}
    Get-ChildItem -LiteralPath $Dir -Filter '*.json' -File | ForEach-Object {
        $map[$_.Name] = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
    }
    return $map
}

$entries = @()
foreach ($category in $Categories) {
    $uDir = Join-Path $unityRoot $category
    $cDir = Join-Path $consoleRoot $category
    $unity = Get-FileMap $uDir
    $console = Get-FileMap $cDir

    foreach ($name in ($unity.Keys | Sort-Object)) {
        if (-not $console.ContainsKey($name)) {
            $entries += [pscustomobject]@{ category = $category; file = $name; status = 'MISSING'; source = (Join-Path $uDir $name); target = (Join-Path $cDir $name) }
        }
        elseif ($unity[$name] -ne $console[$name]) {
            $entries += [pscustomobject]@{ category = $category; file = $name; status = 'DIFFERENT'; source = (Join-Path $uDir $name); target = (Join-Path $cDir $name) }
        }
        else {
            $entries += [pscustomobject]@{ category = $category; file = $name; status = 'SAME'; source = (Join-Path $uDir $name); target = (Join-Path $cDir $name) }
        }
    }
    foreach ($name in ($console.Keys | Where-Object { -not $unity.ContainsKey($_) } | Sort-Object)) {
        $entries += [pscustomobject]@{ category = $category; file = $name; status = 'EXTRA'; source = $null; target = (Join-Path $cDir $name) }
    }
}

$copied = 0
foreach ($e in $entries) {
    if (($e.status -eq 'DIFFERENT' -or $e.status -eq 'MISSING') -and $Apply) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $e.target) | Out-Null
        Copy-Item -LiteralPath $e.source -Destination $e.target -Force
        $copied++
    }
}

$drift = @($entries | Where-Object { $_.status -eq 'DIFFERENT' -or $_.status -eq 'MISSING' })
$summary = [ordered]@{
    generatedAt = (Get-Date).ToString('o')
    mode        = if ($Apply) { 'apply' } else { 'dry-run' }
    categories  = $Categories
    total       = $entries.Count
    same        = @($entries | Where-Object { $_.status -eq 'SAME' }).Count
    different   = @($entries | Where-Object { $_.status -eq 'DIFFERENT' }).Count
    missing     = @($entries | Where-Object { $_.status -eq 'MISSING' }).Count
    extra       = @($entries | Where-Object { $_.status -eq 'EXTRA' }).Count
    copied      = $copied
    driftRemaining = $drift.Count
    entries     = $entries | Sort-Object category, file
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $reportFullPath) | Out-Null
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportFullPath -Encoding UTF8

Write-Host ""
Write-Host ("==> trigger sync [{0}]" -f $summary.mode)
Write-Host ("   categories: {0}" -f ($Categories -join ', '))
Write-Host ("   total={0}  same={1}  DIFFERENT={2}  MISSING={3}  EXTRA={4}" -f $summary.total, $summary.same, $summary.different, $summary.missing, $summary.extra)
foreach ($e in ($entries | Where-Object { $_.status -ne 'SAME' } | Sort-Object category, file)) {
    Write-Host ("   [{0}] {1}/{2}" -f $e.status, $e.category, $e.file)
}
if ($Apply) { Write-Host ("   copied {0} file(s) Unity -> console." -f $copied) }
Write-Host ("   report -> {0}" -f $reportFullPath)
Write-Host ("   (direction is always Unity -> console; EXTRA entries are reported, never deleted)" -f $copied)

if ($FailOnDrift -and $drift.Count -gt 0) { exit 1 }
exit 0
