param(
    [int]$TcpPort = 44201,
    [int]$HostTimeoutSeconds = 60,
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$NoCleanup
)

# MOBA multiprocess smoke 编排脚本（占位）。
#
# 目标（完整版）：启动 1 个 Orleans silo（独立进程）+ 2 个独立客户端进程，
# 每个客户端连同一个 silo、进同一房间、验证双方互见对方英雄的实体快照。
# 对标 shooter 的 run_shooter_multiprocess_smoke.ps1。
#
# 当前限制（2026-07-20）：
#   MobaSmoke 的非 host-only 模式会自己启动 silo + 跑双客户端（单进程内）。
#   它不支持"只跑客户端、连接外部 silo"模式。要实现真正的 multiprocess：
#   1. 给 MobaSmoke Program.cs 加 --client-only --connect-port 参数
#   2. --client-only 模式跳过本地 silo 启动，直接用 RequestClient 连指定端口
#   3. 本脚本编排：启动 1 个 silo（--host-only）+ 2 个 client（--client-only --connect-port）
#
# 当前行为（占位）：
#   启动一个 host-only silo 验证它能独立运行并接受连接，然后跑 run_moba_smoke.ps1
#   （后者在另一端口启动自己的 silo + 双客户端）。这验证了端口隔离和 silo 生命周期管理，
#   但还不是"两个客户端连同一个 silo"。
#
# 后续扩展步骤：
#   1. 在 MobaSmoke Program.cs 加 --client-only 逻辑（约 30 行）
#   2. 本脚本改为真正的 1 silo + 2 client 编排
#   3. 加端口/超时/清理的健壮性处理（参考 shooter multiprocess 脚本）

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir '..\..\..')

. (Join-Path $scriptDir 'abilitykit_process_utils.ps1')

if (-not $NoCleanup) {
    Stop-AbilityKitServices `
        -Ports @($TcpPort, ($TcpPort + 100), 12211, 31101) `
        -CommandPatterns @('AbilityKit.Orleans.MobaSmoke.csproj') `
        -GraceSeconds 2
}

$project = Join-Path $repoRoot 'Server\Orleans\src\AbilityKit.Orleans.MobaSmoke\AbilityKit.Orleans.MobaSmoke.csproj'

if (-not $NoBuild) {
    Write-Host 'Building MOBA smoke project...' -ForegroundColor Cyan
    & dotnet build $project -c $Configuration -p:UseSharedCompilation=false -p:nodeReuse=false
    if ($LASTEXITCODE -ne 0) {
        throw "MOBA smoke build failed with exit code $LASTEXITCODE."
    }
}

# Phase 1: 启动 host-only silo 验证独立进程能运行
Write-Host "Starting host-only MOBA silo on port $TcpPort..." -ForegroundColor Cyan
$siloProc = Start-Process -FilePath 'dotnet' -ArgumentList @(
    'run', '--project', $project, '-c', $Configuration, '--no-build',
    '--', '--host-only', '--tcp-port', $TcpPort, '--host-timeout-seconds', $HostTimeoutSeconds
) -PassThru -NoNewWindow -RedirectStandardOutput "$repoRoot\artifacts\moba-silo-out.log" -RedirectStandardError "$repoRoot\artifacts\moba-silo-err.log"

try {
    # 等待 silo ready
    $ready = $false
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        if ($siloProc.HasExited) {
            throw "Host-only silo exited prematurely with code $($siloProc.ExitCode). See artifacts\moba-silo-err.log."
        }
        Start-Sleep -Seconds 1
        # 检查输出日志是否含 MOBA_SMOKE_HOST_READY
        if (Test-Path "$repoRoot\artifacts\moba-silo-out.log") {
            $logContent = Get-Content "$repoRoot\artifacts\moba-silo-out.log" -Raw -ErrorAction SilentlyContinue
            if ($logContent -match 'MOBA_SMOKE_HOST_READY') {
                $ready = $true
                Write-Host "MOBA silo is ready." -ForegroundColor Green
                break
            }
        }
    }

    if (-not $ready) {
        throw "Host-only silo did not signal ready within 30 seconds."
    }

    # Phase 2: 跑 client-only smoke 连接外部 silo（真正的进程隔离）
    # MobaSmoke --client-only 模式跳过本地 silo 启动，直接连接 connect-port 指定的外部 silo。
    # RunScenarioAsync 内部仍创建两个 TCP 客户端（owner + member），进同一房间。
    # 这验证了"独立 silo 进程 + 独立 client 进程"的进程隔离。
    Write-Host "Running client-only MOBA smoke connecting to port $TcpPort..." -ForegroundColor Cyan
    Push-Location (Split-Path -Parent $project)
    try {
        & dotnet run --project $project -c $Configuration --no-build -- --client-only --connect-port $TcpPort
        if ($LASTEXITCODE -ne 0) {
            throw "Client-only MOBA smoke failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    Write-Host "MOBA multiprocess smoke PASSED." -ForegroundColor Green
}
finally {
    if ($siloProc -and -not $siloProc.HasExited) {
        Write-Host "Stopping host-only silo..." -ForegroundColor DarkGray
        Stop-Process -Id $siloProc.Id -Force -ErrorAction SilentlyContinue
        $siloProc.WaitForExit(5000) | Out-Null
    }
}
