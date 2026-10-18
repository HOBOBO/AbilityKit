#Requires -Version 5.1
<#
.SYNOPSIS
    Entitas 组件代码生成 gate 入口：对 tools/ 下全部 entitas-codegen 项目配置做生成基线校验。

.DESCRIPTION
    由 tools/test-gates.json 的 entitas-codegen gate（powershell-script step）调用。
    退出码非零 = 生成行为漂移（模板被无意改动 / 组件定义与基线不一致）。
    有意的模板演进：tools/generate-entitas.ps1 -Project <name> -UpdateBaseline 固化后提交。

    显式排除 pin（索引锁定）与 baseline（基线清单）文件——它们是生成器的输入而非项目配置。
#>
param(
    [switch]$CI
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$fail = 0

$configs = Get-ChildItem (Join-Path $repoRoot 'tools') -Filter 'entitas-codegen.*.json' |
    Where-Object { $_.Name -notmatch '\.(pin|baseline)\.json$' } |
    Sort-Object Name

if (-not $configs) {
    Write-Error "tools/ 下没有 entitas-codegen.*.json 项目配置。"
    exit 2
}

foreach ($config in $configs) {
    $project = $config.Name -replace '^entitas-codegen\.', '' -replace '\.json$', ''
    Write-Host "=== entitas-codegen project: $project ==="
    & (Join-Path $repoRoot 'tools/generate-entitas.ps1') -Project $project
    if ($LASTEXITCODE -ne 0) { $fail = 1 }
}

exit $fail
