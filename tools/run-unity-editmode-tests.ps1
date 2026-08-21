# Runs Unity EditMode tests for a test assembly in the AbilityKit Unity project.
# 前置：Unity 工程不能被其它 Unity 实例占用（Temp/UnityLockfile 存在时会直接报错）。
# 用法：powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1
#       powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Ability.Editor.Tests
# 产物按仓库约定落 local/Logs/（已被 .gitignore 覆盖）。
# 退出码：0 = 全部通过；2 = 存在失败用例；其它 = Unity 启动/编译错误（看 log）。
param(
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe",
    [string]$TestAssembly = "AbilityKit.Ability.Editor.Tests",
    [string]$ResultsPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "Unity"
if ([string]::IsNullOrEmpty($ResultsPath)) {
    $ResultsPath = Join-Path $repoRoot "local\Logs\unity-editmode-$TestAssembly.xml"
}
$logPath = Join-Path $repoRoot "local\Logs\unity-editmode-run.log"
New-Item -ItemType Directory -Force (Split-Path -Parent $ResultsPath) | Out-Null

if (Test-Path (Join-Path $projectPath "Temp\UnityLockfile")) {
    Write-Error "Unity project is locked by another editor instance. Close it first: $projectPath"
}

if (-not (Test-Path $UnityExe)) {
    Write-Error "Unity editor not found at '$UnityExe'. Pass -UnityExe with the full path."
}

Write-Host "Running EditMode tests for '$TestAssembly' ..."
Write-Host "Project: $projectPath"
Write-Host "Results: $ResultsPath"

& $UnityExe -batchmode -nographics -projectPath $projectPath `
    -runTests -testPlatform EditMode -assemblyNames $TestAssembly `
    -testResults $ResultsPath -logFile $logPath
$exitCode = $LASTEXITCODE

Write-Host "Unity exit code: $exitCode"
Write-Host "Results: $ResultsPath"
Write-Host "Log:     $logPath"
exit $exitCode
