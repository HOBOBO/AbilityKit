# Shooter 多人同步验证（v0.1.0 新增）

## 烟雾测试（纯 C#，已通过 ✅）

使用 `AbilityKit.Orleans.ShooterSmoke` 项目（net10.0），无需 Unity：

```powershell
# 启动 Shooter state-sync 服务器
.\Server\Orleans\tools\restart_shooter_state_sync.ps1
# 或:
dotnet run --project Server\Orleans\src\AbilityKit.Orleans.ShooterSmoke -c Release -- --server --tcp-port 41001

# Owner 创建房间
dotnet run --project Server\Orleans\src\AbilityKit.Orleans.ShooterSmoke -c Release -- `
  --client --client-mode create --tcp-port 41001 --client-id owner --player-id 1 `
  --inputs 5 --seed 12345 --timeout-seconds 120 --wait-for-match-end `
  --state-sync-payload-mode packed

# Member 加入房间
dotnet run --project Server\Orleans\src\AbilityKit.Orleans.ShooterSmoke -c Release -- `
  --client --client-mode join --tcp-port 41001 --client-id member --player-id 2 `
  --room-id <room-id> --inputs 5 --seed 12345 --timeout-seconds 120 `
  --wait-for-match-end --state-sync-payload-mode packed
```

**验证结果**：Owner 和 Member 在 frame 600 达到完全一致的 `stateHash=0xB39E53ED`，snapshotHashMatched=True，reconcileAuthoritativeHashMatched=True，lagCompAccepted=True。

## Unity 无头双实例

脚本：`tools/run_shooter_unity_headless_multiplayer.ps1`

使用 `GameFrameworkGatewayConnectionFactory` 创建真实 TCP 连接，通过共享文件协调 roomId：

```powershell
.\tools\run_shooter_unity_headless_multiplayer.ps1 `
    -GatewayHost 127.0.0.1 -GatewayPort 4000
```

入口：`ShooterHeadlessClient.cs`，挂在 `ShooterMultiplayerScene` 的 GameObject 上。

## 暂停/恢复 GUI

`ShooterFormalMultiplayerController.DrawBattleStatus()` 新增 Pause/Resume 按钮：
- **Pause** → `ShooterRemoteStateSyncPlayModeHost.PauseForReconnectValidation()` — 关闭连接模拟断线
- **Resume** → `ShooterRemoteStateSyncPlayModeHost.ResumeFromPauseAsync()` — 重连恢复战斗

## 统一网关抽象（v0.1.0 新增）

`com.abilitykit.host.extension/Runtime/Gateway/`:
- `IGatewayConnection` — 统一请求/响应 + 推送注册接口
- `GatewayConnection` — 默认实现，包装 `IConnection` + seq 匹配 + push dispatch
