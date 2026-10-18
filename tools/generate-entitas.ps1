#Requires -Version 5.1
<#
.SYNOPSIS
    Entitas 组件代码生成入口（设计时 CLI，取代已失管的 Jenny 链路）。

.PARAMETER Project
    项目名：按约定解析 <repoRoot>/tools/entitas-codegen.<project>.json（与 -Config 二选一）。

.PARAMETER Config
    显式配置路径（与 -Project 二选一）。

.PARAMETER Check
    只对比不写盘（gate 模式；默认行为也是只对比，此处显式命名）。

.PARAMETER Write
    生成到草稿目录 local/Logs/entitas-codegen/out（绝不写示例包）。

.PARAMETER UpdatePin
    把新发现的组件追加进 pin 文件（新增组件后跑一次）。

.PARAMETER Validate
    只校验配置/源/pin，不生成不对比（doctor 角色）。

.PARAMETER ExportConfig
    导出有效配置（默认值填满、路径仓库相对）到指定路径。

.PARAMETER UpdateBaseline
    模板/模型有意的变更后，固化新的生成基线清单（path→内容哈希）。

.PARAMETER DiffGeneratedDir
    报告与示例包生成目录的落地差异（接入替换前消化，不影响退出码）。

.EXAMPLE
    ./tools/generate-entitas.ps1 -Project moba                # 基线校验（exit code 非零 = 生成行为漂移）
    ./tools/generate-entitas.ps1 -Project moba -Write         # 生成到草稿目录检查产物
    ./tools/generate-entitas.ps1 -Project moba -Validate      # doctor
    ./tools/generate-entitas.ps1 -Project moba -DiffGeneratedDir   # 看与示例包目录的预期增量
#>
param(
    [string]$Project,
    [string]$Config,
    [switch]$Check,
    [switch]$Write,
    [switch]$UpdatePin,
    [switch]$Validate,
    [string]$ExportConfig,
    [switch]$UpdateBaseline,
    [switch]$DiffGeneratedDir,
    [switch]$VerboseOutput
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$toolProject = Join-Path $repoRoot 'tools/AbilityKit.Entitas.CodeGen/AbilityKit.Entitas.CodeGen.csproj'

$extra = @()
if ($Project) { $extra += @('--project', $Project) }
if ($Config) { $extra += @('--config', $Config) }
if ($Write) { $extra += '--write' }
if ($UpdatePin) { $extra += '--update-pin' }
if ($Validate) { $extra += '--validate' }
if ($ExportConfig) { $extra += @('--export-config', $ExportConfig) }
if ($UpdateBaseline) { $extra += '--update-baseline' }
if ($DiffGeneratedDir) { $extra += '--diff-generated-dir' }
if ($VerboseOutput) { $extra += '--verbose' }

& dotnet run --project $toolProject -- @extra
exit $LASTEXITCODE
