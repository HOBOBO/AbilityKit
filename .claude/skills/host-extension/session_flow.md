# RoomGatewaySessionFlow（8 阶段会话流）

> ⚠️ 源码核校 2026-08-06：`RoomGatewaySessionFlow` / `RoomGatewayRestoreFirstConnectionPolicy` 实际位于 **`com.abilitykit.network.room`** 包（`Runtime/RoomGatewaySessionFlow.cs`），**不在** `com.abilitykit.host.extension`。host.extension 的 `Runtime/Session/` 下只有 `FramePacketNetAdapter`（见文末）。本节保留在 host-extension skill 仅因历史编排；房间会话流程的权威文档应以 `com.abilitykit.network.room` 包为准。

源文件（network.room）：`com.abilitykit.network.room/Runtime/RoomGatewaySessionFlow.cs` + `RoomGatewayRestoreFirstConnectionPolicy.cs`

## 核心接口 IRoomGatewaySessionClient

```csharp
public interface IRoomGatewaySessionClient {
    Task<...> CreateRoomAsync(...);
    Task<...> JoinRoomAsync(...);
    Task<...> SetReadyAsync(...);
    Task<...> StartBattleAsync(...);
    Task<...> SubscribeStateSyncAsync(...);
    Task<...> RestoreRoomAsync(...);
    Task<...> PickHeroAsync(...);
    Task<...> BeginLoadingAsync(...);
    Task<...> ReportAssetsLoadedAsync(...);
    Task<...> GetSnapshotAsync(...);
}
```

## 8 阶段流程（新 API）

| 阶段 | 方法 | 说明 |
|------|------|------|
| 1 | `CreateRoomAsync` | 创建房间 |
| 2 | `JoinRoomAsync` | 加入房间 |
| 3 | `ConfigureLoadoutAsync`（PickHero） | 选英雄/配置 loadout |
| 4 | `SetReadyAsync` | 准备就绪 |
| 5 | `BeginLoadingAsync` | 开始资源加载 |
| 6 | `ReportAssetsLoadedAsync` | 报告资源加载完成 |
| 7 | `WaitForBattleStartAsync` | 轮询等待开战 |
| 8 | `SubscribeStateSyncAsync` | 订阅状态同步 |

## 阶段枚举

```csharp
public enum RoomGatewaySessionPhase {
    Lobby, Loading, Starting, InBattle, Closing, Closed, Expired
}

public enum RoomGatewaySessionEntryKind {
    TeamLobby, Reconnect, LateJoin
}

public enum RoomGatewayStagedRestoreNextStep { ... }
public enum RoomGatewaySessionRestoreStatus { ... }
public enum RoomGatewaySessionErrorCode { ... }
```

## 阶段化恢复（Reconnect）

```csharp
public Task<RoomGatewayStagedRestoreResult> RestoreAsync(...);
```

返回 `RoomGatewayStagedRestoreResult`（含 `NextStep` 建议），分步恢复连接。

## 旧 API（已废弃）

以下方法标 `[Obsolete]`，**不要在新代码用**：

- `CreateReadyStartAndSubscribeAsync`
- `JoinReadyStartAndSubscribeAsync`

## RoomGatewayRestoreFirstConnectionPolicy（静态助手）

```csharp
public static class RoomGatewayRestoreFirstConnectionPolicy {
    public static Task<RoomGatewayRestoreFirstConnectionResult<TResult>> ConnectAsync<TResult>(
        Func<Task<TResult>> restoreAsync,
        Func<Task<TResult>> fallbackCreateAsync,
        bool allowFallbackCreate);
}
```

"先尝试恢复，失败则回退创建"的策略。

## 关键 DTO

- `RoomGatewayLaunchSpec` — 启动参数
- `RoomGatewayWorldStartAnchor` — 世界起始锚（含 idealFrame / time anchor）
- `RoomGatewaySnapshot`（class）— 房间快照
- 大量 `*Request/*Result` struct

## FramePacketNetAdapter

详见 [client_helpers.md](client_helpers.md)。
