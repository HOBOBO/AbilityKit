# Drives the demo.moba config sync chain headlessly (no Unity window interaction).
# 面向 AI/CI：AI 改逐条目 JSON（Resources/moba/{表}/）后 push-json 落盘 Excel 真相源并刷新运行时 JSON。
# 前置：Unity 工程不能被其它 Unity 实例占用（Temp/UnityLockfile 存在时会直接报错）。
# 用法：
#   powershell -ExecutionPolicy Bypass -File tools/run-moba-config-sync.ps1 -Mode status
#   powershell -ExecutionPolicy Bypass -File tools/run-moba-config-sync.ps1 -Mode bootstrap -Table Buff
#   powershell -ExecutionPolicy Bypass -File tools/run-moba-config-sync.ps1 -Mode push-json -Table Buff
#   powershell -ExecutionPolicy Bypass -File tools/run-moba-config-sync.ps1 -Mode pull-excel
# 退出码：0=成功；1=处理失败；2=合并冲突；3=参数错误；其它=Unity 启动/编译错误（看 log）。
# 产物按仓库约定落 local/Logs/（已被 .gitignore 覆盖）。
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("push-json", "pull-excel", "bootstrap", "export-typed", "seed-from-json", "status")]
    [string]$Mode,
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe",
    [string]$Table = "",
    [string]$ExcelFolder = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "Unity"
$logPath = Join-Path $repoRoot "local\Logs\moba-config-sync.log"
New-Item -ItemType Directory -Force (Split-Path -Parent $logPath) | Out-Null

if (Test-Path (Join-Path $projectPath "Temp\UnityLockfile")) {
    Write-Error "Unity project is locked by another editor instance. Close it first: $projectPath"
}

if (-not (Test-Path $UnityExe)) {
    Write-Error "Unity editor not found at '$UnityExe'. Pass -UnityExe with the full path."
}

$unityArgs = @(
    "-batchmode", "-nographics", "-projectPath", $projectPath,
    "-executeMethod", "AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.MobaConfigHeadlessSync.Run",
    "-mode", $Mode
)
if ($Table) { $unityArgs += @("-table", $Table) }
if ($ExcelFolder) { $unityArgs += @("-excelFolder", $ExcelFolder) }
$unityArgs += @("-logFile", $logPath)

Write-Host "Running moba config sync mode='$Mode' table='$Table' ..."
& $UnityExe @unityArgs
$exitCode = $LASTEXITCODE

Write-Host "Unity exit code: $exitCode"
Write-Host "Log: $logPath"
exit $exitCode
