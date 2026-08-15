<#
.SYNOPSIS
  Captures real MOBA acceptance traces into the dotnet acceptance verdict baseline dir.

.DESCRIPTION
  Runs the whole expectation directory in Unity batchmode
  (MobaAcceptanceWebCommand.RunDirectoryFromCommandLine), collects each case's
  <caseId>_trace.jsonl, and copies them into the dotnet verdict baseline dir (Traces/).
  CompositeTraceSource then consumes these (real Traces first, synthetic Fixtures fallback),
  upgrading the moba-acceptance-dotnet gate from needs-trace to real regression verdicts.

  Exit code: 0 if at least one trace was captured, else 1. Independent of Unity allPassed:
  traces of completed cases are collected regardless of per-case verdict (failing cases are
  reported so they can be fixed).

.PARAMETER UnityEditorPath
  Path to the Unity editor exe. Default is the 2022.3.62f1 Hub path.

.PARAMETER ExpectationDir
  Expectation directory (*.expected.json). If omitted, the Unity entrypoint uses its default.

.PARAMETER OutputDirectory
  Unity run artifact dir (must be under artifacts/). Default artifacts/moba-acceptance-capture.

.PARAMETER TracesTarget
  Baseline dir the dotnet gate reads. Default src/AbilityKit.Demo.Moba.Acceptance.Tests/Traces.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\capture_moba_acceptance_traces.ps1
#>
[CmdletBinding()]
param(
    [string]$UnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe',

    [string]$ExpectationDir,

    [string]$OutputDirectory = 'artifacts/moba-acceptance-capture',

    [string]$TracesTarget = 'src/AbilityKit.Demo.Moba.Acceptance.Tests/Traces'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$unityProject = Join-Path $repoRoot 'Unity'
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$tracesTargetPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $TracesTarget))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))

if (-not $outputPath.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be under artifacts/: $OutputDirectory"
}
if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) {
    throw "Unity editor not found: $UnityEditorPath (pass -UnityEditorPath)"
}
if (-not (Test-Path -LiteralPath $unityProject -PathType Container)) {
    throw "Unity project not found: $unityProject"
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
New-Item -ItemType Directory -Force -Path $tracesTargetPath | Out-Null
$logPath = Join-Path $outputPath 'unity.log'

$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $unityProject,
    '-executeMethod', 'AbilityKit.Game.Test.UnitTest.MobaAcceptanceWebCommand.RunDirectoryFromCommandLine',
    '-mobaAcceptanceOutput', $outputPath
)
if ($ExpectationDir) {
    $resolvedExpectationDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ExpectationDir))
    $arguments += @('-mobaAcceptanceExpectationDir', $resolvedExpectationDir)
}
$arguments += @('-logFile', $logPath)

Write-Host "==> Unity batchmode capture -> $outputPath"
$startedAt = [DateTime]::UtcNow
$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -Wait -PassThru
$endedAt = [DateTime]::UtcNow
$unityExitCode = $process.ExitCode

# Collect every emitted trace (independent of verdict: failing cases that reached Export emit too).
$traceFiles = @(Get-ChildItem -LiteralPath $outputPath -Filter '*_trace.jsonl' -File -ErrorAction SilentlyContinue)
$capturedCaseIds = @()
foreach ($f in $traceFiles) {
    Copy-Item -LiteralPath $f.FullName -Destination $tracesTargetPath -Force
    $capturedCaseIds += ([System.IO.Path]::GetFileName($f.Name) -replace '_trace\.jsonl$', '')
}

$batchPath = Join-Path $outputPath 'batch_summary.json'
$batch = $null
if (Test-Path -LiteralPath $batchPath) {
    $batch = Get-Content -LiteralPath $batchPath -Raw | ConvertFrom-Json
}

Write-Host ""
Write-Host "==> Capture result"
Write-Host "   Unity exitCode = $unityExitCode (0 when allPassed; capture judged by trace count, not exitCode)"
if ($batch) {
    Write-Host ("   batch total={0} passed={1} failed={2} allPassed={3}" -f $batch.total, $batch.passed, $batch.failed, $batch.allPassed)
    foreach ($r in $batch.results) {
        $mark = if ($r.passed) { 'PASS' } elseif ($r.errorType) { 'ERR(' + $r.errorType + ')' } else { 'FAIL' }
        Write-Host ("   [{0}] {1}" -f $mark, $r.caseId)
    }
}
Write-Host ("   captured {0} trace(s) -> {1}" -f $traceFiles.Count, $tracesTargetPath)
if ($capturedCaseIds.Count -gt 0) {
    Write-Host ("   caseIds: {0}" -f ($capturedCaseIds -join ', '))
}

$manifest = [ordered]@{
    capturedAtUtc      = $startedAt.ToString('O')
    durationMs         = [int]($endedAt - $startedAt).TotalMilliseconds
    unityExitCode      = $unityExitCode
    expectationDir     = if ($ExpectationDir) { $ExpectationDir } else { '<default>' }
    outputDirectory    = $outputPath
    tracesTarget       = $tracesTargetPath
    capturedTraceCount = $traceFiles.Count
    capturedCaseIds    = $capturedCaseIds
    batchTotal         = if ($batch) { $batch.total } else { $null }
    batchPassed        = if ($batch) { $batch.passed } else { $null }
    batchFailed        = if ($batch) { $batch.failed } else { $null }
}
$manifestPath = Join-Path $tracesTargetPath 'capture-manifest.json'
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host ""
Write-Host "   manifest -> $manifestPath"
Write-Host "   next: review Traces/, git-commit them; moba-acceptance-dotnet gate then judges them as the regression baseline."
Write-Host "   (re-run this script after intentional behavior changes; a red gate = real regression signal.)"

if ($traceFiles.Count -eq 0) {
    Write-Warning "No traces captured. Check $logPath ."
    exit 1
}
exit 0
