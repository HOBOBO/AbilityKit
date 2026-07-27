param(
    [int]$TcpPort = 44201,
    [int]$HostTimeoutSeconds = 60,
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$NoCleanup
)

# MOBA multiprocess smoke 编排脚本。
#
# 当前拓扑：1 个 host-only Orleans silo 进程 + 1 个 client-only 场景进程。
# 场景进程内部创建 owner/member 两条独立 TCP 连接，使两名玩家进入同一房间，
# 验证双方权威实体快照和移动结果收敛，并验证显式全量状态恢复推送及
# 可靠事件 epoch/watermark ACK。
#
# 该拓扑验证 silo 与客户端场景的进程隔离，但 owner/member 尚未拆分为两个操作系统进程。
# 若要覆盖客户端进程级崩溃和重启，需要增加外部 room id、owner/member role、
# 房间创建就绪协调及跨进程结果汇总协议。

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $scriptDir '..\..\..')
$artifactRoot = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

. (Join-Path $scriptDir 'abilitykit_process_utils.ps1')

if (-not $NoCleanup) {
    Stop-AbilityKitServices `
        -Ports @($TcpPort, ($TcpPort + 100), 12211, 31101) `
        -CommandPatterns @('AbilityKit.Orleans.MobaSmoke.csproj') `
        -GraceSeconds 2
}

$project = Join-Path $repoRoot 'Server\Orleans\src\AbilityKit.Orleans.MobaSmoke\AbilityKit.Orleans.MobaSmoke.csproj'
$projectDirectory = Split-Path -Parent $project

if (-not $NoBuild) {
    Write-Host 'Building MOBA smoke project...' -ForegroundColor Cyan
    & dotnet build $project -c $Configuration -m:1 -p:UseSharedCompilation=false -p:nodeReuse=false
    if ($LASTEXITCODE -ne 0) {
        throw "MOBA smoke build failed with exit code $LASTEXITCODE."
    }
}

# Phase 1: 启动独立的 host-only silo 进程。
Write-Host "Starting host-only MOBA silo on port $TcpPort..." -ForegroundColor Cyan
$siloProc = Start-Process -FilePath 'dotnet' -ArgumentList @(
    'run', '--project', $project, '-c', $Configuration, '--no-build',
    '--', '--host-only', '--tcp-port', $TcpPort, '--host-timeout-seconds', $HostTimeoutSeconds
) -WorkingDirectory $projectDirectory -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $artifactRoot 'moba-silo-out.log') -RedirectStandardError (Join-Path $artifactRoot 'moba-silo-err.log')

try {
    # 等待 silo ready
    $ready = $false
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        if ($siloProc.HasExited) {
            $siloProc.Refresh()
            throw "Host-only silo exited prematurely with code $($siloProc.ExitCode). See artifacts\moba-silo-err.log."
        }
        Start-Sleep -Seconds 1
        # 检查输出日志是否含 MOBA_SMOKE_HOST_READY
        $siloOutputPath = Join-Path $artifactRoot 'moba-silo-out.log'
        if (Test-Path $siloOutputPath) {
            $logContent = Get-Content $siloOutputPath -Raw -ErrorAction SilentlyContinue
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

    # Phase 2: 启动 client-only 场景进程连接外部 silo。
    # RunScenarioAsync 在该进程内创建 owner/member 两条 TCP 连接并加入同一房间。
    Write-Host "Running client-only MOBA smoke connecting to port $TcpPort..." -ForegroundColor Cyan
    Push-Location $projectDirectory
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
