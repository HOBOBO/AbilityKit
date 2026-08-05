param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

# restart-dev 是 start_orleans_dev 的便捷入口：直接委托给规范启动脚本。
# 规范脚本负责：停止旧进程 → 构建 Host/Gateway → 先起 Silo 并等待 Orleans 客户端网关(30000)就绪
# → 再起 HTTP/TCP Gateway → 健康检查。直接在这里 `dotnet run` 会因为缺 --AbilityKit:Orleans:*
# 配置段导致 Host 启动即崩、Gateway 连不上 Silo，故统一走 tools/start_orleans_dev.ps1。
& (Join-Path $PSScriptRoot 'tools\start_orleans_dev.ps1') -NoBuild:$NoBuild
