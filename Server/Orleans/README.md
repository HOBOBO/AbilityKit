# AbilityKit Orleans 服务器

此目录包含用于本地开发和集成验证的 Orleans 服务器、TCP 网关、房间与战斗 Grain，以及 Shooter 自动化冒烟工具。

## 项目

- `AbilityKit.Orleans.Contracts`: Grain 接口、DTO 和共享结果状态码。
- `AbilityKit.Orleans.Grains`: 房间、战斗和玩法 Grain 实现。
- `AbilityKit.Orleans.Gateway`: HTTP/TCP 网关和房间操作错误映射。
- `AbilityKit.Orleans.Host`: 独立的 silo 宿主。
- `AbilityKit.Orleans.ShooterSmoke`: 自托管 Shooter TCP 网关端到端冒烟运行器。
- `AbilityKit.Orleans.Grains.Tests`: Grain 与运行时适配器测试。
- `AbilityKit.Orleans.Gateway.Tests`: 网关错误映射测试。

## 运行

使用一键启动器构建并启动 Orleans Host 和 HTTP/TCP Gateway，供本地开发使用：

```powershell
.\tools\start_abilitykit.ps1
```

默认 `dev` 配置的管理控制台地址为 `http://localhost:5001/admin`，健康检查端点为 `http://localhost:5001/health`。

列出已配置的启动环境：

```powershell
.\tools\start_abilitykit.ps1 -ListProfiles
```

按名称运行环境配置：

```powershell
.\tools\start_abilitykit.ps1 -Profile ops-a
```

使用相互隔离的端口和集群 ID，在一台机器启动多个本地环境：

```powershell
.\tools\start_abilitykit.ps1 -Profile ops-a,ops-b -NoBuild
```

需要临时调整环境端口时，可直接覆盖：

```powershell
.\tools\start_abilitykit.ps1 -Profile dev -GatewayPort 5051 -SiloPort 11511 -SiloGatewayPort 30500 -TcpPort 4050
```

环境配置定义于 `tools\abilitykit_launch_profiles.json`。每份配置独立指定 `instanceName`、`clusterId`、`serviceId`、HTTP 网关端口、Orleans silo 端口、Orleans 网关端口和 TCP 网关端口。日志写入 `logs\<instanceName>\`。

`tools\restart_all.ps1` 保留为兼容包装入口，当前委托给环境配置启动器。

## Shooter 冒烟验证

Shooter 冒烟运行器自行托管 Orleans silo 和 TCP 网关，然后验证访客登录、创建房间与准备、战斗启动、快照推送、本地输入提交、过期快照拒绝、中途加入投影、重连投影和战斗清理。

```powershell
.\tools\run_shooter_smoke.ps1 -Configuration Debug -TcpPort 41001
```

```cmd
tools\run_shooter_smoke.bat -Configuration Debug -TcpPort 41001
```

冒烟项目已构建时可使用 `-NoBuild`。并行运行时可通过 `-TcpPort` 避免本地端口冲突。

## 验证

```cmd
dotnet test src\AbilityKit.Orleans.Grains.Tests\AbilityKit.Orleans.Grains.Tests.csproj --filter ShooterBattleRuntimeAdapterTests
```

```cmd
dotnet test src\AbilityKit.Orleans.Gateway.Tests\AbilityKit.Orleans.Gateway.Tests.csproj
```
