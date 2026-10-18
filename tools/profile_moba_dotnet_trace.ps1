# L4 方法级性能采样：用 dotnet-trace 采样式 profiler 抓 MOBA console 战斗，
# 产出方法级 speedscope 火焰图（比插桩探针细到任意函数，且无观察者偏差）。
# 用法：powershell -ExecutionPolicy Bypass -File tools/profile_moba_dotnet_trace.ps1 [-Seconds 20]
param(
    [int]$Seconds = 20,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "local/Logs/moba-console-profile/dotnet-trace"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

# 1) 确保 dotnet-trace 全局工具可用
$trace = Get-Command dotnet-trace -ErrorAction SilentlyContinue
if (-not $trace) {
    Write-Host "[profile] installing dotnet-trace global tool..."
    dotnet tool install -g dotnet-trace
    if ($LASTEXITCODE -ne 0) { throw "dotnet-trace install failed" }
}

# 2) 构建 console 宿主
Write-Host "[profile] building console ($Configuration)..."
dotnet build (Join-Path $repoRoot "src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj") -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "console build failed" }

$consoleDll = Join-Path $repoRoot "src/AbilityKit.Demo.Moba.Console/bin/$Configuration/net10.0/AbilityKit.Demo.Moba.Console.dll"
if (-not (Test-Path $consoleDll)) { throw "console dll not found: $consoleDll" }

# 3) 采样：--skill 模式无头跑技能施放场景并自行退出；--duration 只是上限
# dotnet-trace 转换 speedscope 格式时会自行追加 .speedscope.json，这里不带扩展名
$output = Join-Path $artifactDir ("moba-method-level-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
Write-Host "[profile] collecting $Seconds s method-level trace -> $output"
dotnet-trace collect --profile dotnet-sampled-thread-time --format speedscope --duration ([TimeSpan]::FromSeconds($Seconds)) -o $output -- dotnet $consoleDll --skill
if ($LASTEXITCODE -ne 0) { throw "dotnet-trace collect failed" }

# dotnet-trace 可能自行追加 .speedscope.json 后缀，归一化一下
$produced = Get-ChildItem $artifactDir -Filter "*.speedscope.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
Write-Host "[profile] done: $($produced.FullName)"
Write-Host "[profile] 打开 https://www.speedscope.app 拖入该文件即可看方法级火焰图（左/右火焰 + 时间线）。"
