# 无 Unity 实例也能做"Unity 侧编译验证"：按 asmdef 边界镜像编译 behaviortree 包 + demo BTree 源码，
# 直接引用 Unity 2022.3 托管 DLL。dotnet 的 net10/latest + ImplicitUsings 会掩盖 Unity 侧错误
# （init 访问器、缺 using、跨 asmdef 引用缺失），此校验能提前抓。
# 用法：powershell -ExecutionPolicy Bypass -File tools/run-unity-compile-check.ps1
# 退出码：0=编译通过；1=存在编译错误。产物落 local/Logs/。
param(
    [string]$UnityVersion = "2022.3.62f1"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "tools\unity-compile-check\UnityCompileCheck.csproj"
$unityManaged = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Data\Managed"

if (-not (Test-Path $unityManaged)) {
    Write-Warning "Unity managed DLL 目录不存在：$unityManaged（编译检查需要 Unity 安装；跳过）"
    exit 0
}

Write-Host "Unity compile check (asmdef-boundary mirror) ..."
Write-Host "Project: $project"

# 覆盖 csproj 内硬编码路径：用 -p 传全局属性（需 csproj 里对 UnityManaged/RepoRoot 用 Condition 兜底）。
# 当前 csproj 为硬编码本机路径；如需 CI 参数化，把两处 PropertyGroup 改为 Condition="'$(X)'==''" 形式。
& dotnet build $project -v quiet
$exitCode = $LASTEXITCODE
exit $exitCode
