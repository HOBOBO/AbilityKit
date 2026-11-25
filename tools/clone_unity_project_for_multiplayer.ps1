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
    # 默认：<repo>/Unity，即仓库内的 Unity 项目根目录。
    $SourceDir = Join-Path (Split-Path -Parent $PSScriptRoot) "Unity"
}
$SourceDir = (Resolve-Path $SourceDir).Path

if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    # 默认：仓库根的同级目录（Git 仓库外），名称为 "<UnityProjectName>-Instance2"。
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
            Write-Host "正在清理已有目标： $TargetDir" -ForegroundColor Yellow
            Remove-Item $TargetDir -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            Write-Error "Target exists and is non-empty: $TargetDir. Use -Force to overwrite."
            exit 1
        }
    }
}

New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

Write-Host "源目录：$SourceDir"
Write-Host "目标目录：$TargetDir"

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
Write-Host "完成。请通过 Unity Hub 在第二个 Unity 实例中打开 '$TargetDir'。" -ForegroundColor Green
Write-Host "两个实例共享 Assets、Packages 和 ProjectSettings，各自拥有独立的 Library。" -ForegroundColor Yellow
