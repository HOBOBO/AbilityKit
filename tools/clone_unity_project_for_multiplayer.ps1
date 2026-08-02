<#
.SYNOPSIS
    Creates a junction-linked Unity project instance for multi-instance multiplayer testing.

.DESCRIPTION
    Creates a new directory and links Assets/Packages/ProjectSettings from the current
    Unity project via Windows directory junctions (mklink /J). Library/Logs/Temp/obj are
    created fresh inside the new instance so two Unity instances can open the same
    project with independent Library caches, enabling multiplayer testing on one machine.

.PARAMETER TargetDir
    New instance directory to create.

.PARAMETER SourceDir
    Unity project root. Defaults to repo root (parent of tools/).
.PARAMETER Force
    Overwrite if the target already exists and is non-empty.
#>
[CmdletBinding()]
param(
    [string]$TargetDir = "",
    [string]$SourceDir = "",
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SourceDir)) {
    # Default: <repo>/Unity (the Unity project root inside the repo)
    $SourceDir = Join-Path (Split-Path -Parent $PSScriptRoot) "Unity"
}
$SourceDir = (Resolve-Path $SourceDir).Path

if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    # Default: sibling of repo root (outside the git repo), "<UnityProjectName>-Instance2"
    $projectName = Split-Path -Leaf $SourceDir
    $repoRoot = Split-Path -Parent $SourceDir
    $TargetDir = Join-Path (Split-Path -Parent $repoRoot) "${projectName}-Instance2"
}

$required = @("Assets", "Packages", "ProjectSettings")
foreach ($d in $required) {
    if (-not (Test-Path (Join-Path $SourceDir $d) -PathType Container)) {
        Write-Error "Source directory missing required folder '$d': $SourceDir"
        exit 1
    }
}

$TargetDir = [System.IO.Path]::GetFullPath($TargetDir)

if (Test-Path $TargetDir -PathType Container) {
    $existing = Get-ChildItem $TargetDir -Force -ErrorAction SilentlyContinue
    if ($existing.Count -gt 0) {
        if ($Force) {
            Write-Host "Clearing existing target: $TargetDir" -ForegroundColor Yellow
            Remove-Item $TargetDir -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            Write-Error "Target exists and is non-empty: $TargetDir. Use -Force to overwrite."
            exit 1
        }
    }
}

New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

Write-Host "Source: $SourceDir"
Write-Host "Target: $TargetDir"

function New-Junction([string]$Link, [string]$Target) {
    if (Test-Path $Link) { Remove-Item $Link -Recurse -Force -ErrorAction SilentlyContinue }
    $out = & cmd.exe /c "mklink /J `"$Link`" `"$Target`"" 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Error "junction failed: $Link -> $Target`n$out"; exit 1 }
    Write-Host "  junction: $Link -> $Target" -ForegroundColor Green
}

foreach ($d in $required) {
    New-Junction -Link (Join-Path $TargetDir $d) -Target (Join-Path $SourceDir $d)
}

foreach ($d in @("Library", "Logs", "Temp", "obj")) {
    $p = Join-Path $TargetDir $d
    if (-not (Test-Path $p)) { New-Item -ItemType Directory -Path $p -Force | Out-Null }
}

$gi = "Library/`nLogs/`nTemp/`nobj/`n"
$gi | Out-File -FilePath (Join-Path $TargetDir ".gitignore") -Encoding UTF8 -NoNewline

Write-Host ""
Write-Host "Done. Open '$TargetDir' in a second Unity instance via Unity Hub." -ForegroundColor Green
Write-Host "Both instances share Assets/Packages/ProjectSettings but have independent Library." -ForegroundColor Yellow
