# com.abilitykit.network.room

> AbilityKit 多人联网 SDK 的**房间会话能力包**。在 `NetworkSdkClient` 之上，提供"房间网关"的线协议客户端、8 阶段会话编排与断线恢复，是**玩法无关**（gameplay-agnostic）的通用层 —— shooter 与 moba 两个示例共用同一套。

- **版本**：0.1.0（Beta）
- **程序集**：`AbilityKit.Network.Room`
- **依赖**：`network.runtime`、`network.sdk`、`protocol`、`protocol.room`
- **源文件**（4 个）：
  - `NetworkSdkRoomExtensions.cs` —— SDK→Room 桥（`CreateRoomClient()` 扩展方法）
  - `RoomGatewayWireSessionClient.cs` —— 线协议客户端 + op-code 表 + 传输契约（`IRoomGatewayRequestTransport` / `IRoomGatewayPushSource` / `RoomGatewayWireOpCodes`）
  - `RoomGatewaySessionFlow.cs` —— 8 阶段会话编排 + 全部 `*Request/*Result` DTO + 枚举
  - `RoomGatewayRestoreFirstConnectionPolicy.cs` —— "先恢复、失败回退创建"策略

## 与 SDK 的衔接：一个扩展方法

```csharp
public static class NetworkSdkRoomExtensions
{
    // 复用 NetworkSdkClient 的单一请求链，不新建 RequestClient
    public static RoomGatewayWireSessionClient CreateRoomClient(
        this NetworkSdkClient client,
        RoomGatewayWireOpCodes? opCodes = null);
}
```

`CreateRoomClient()` 返回的 `RoomGatewayWireSessionClient` 内部把 `NetworkSdkClient` 包成一个 `NetworkSdkRoomTransport`（同时实现 `IRoomGatewayRequestTransport` + `IRoomGatewayPushSource`）。**Dispose 这个 room client 不会关闭底层 SDK 连接**（有测试 `DisposedRoomClient_DoesNotOwnSdkAndRawRequestsStillWork` 守护）。

## 能力接口（按需组合）

房间客户端的能力被拆成多个细粒度接口，`RoomGatewayWireSessionClient` 实现了全部（即 `IRoomGatewaySessionClient`）。`RoomGatewaySessionFlow` 通过 `as` 探测可选能力，缺失时抛 `NotSupportedException` —— **非英雄/无分阶段加载的游戏可只实现 `IRoomGatewaySessionClientBase`**。

| 接口 | 方法 | 必需？ |
|------|------|--------|
| `IRoomGatewaySessionClientBase` | `CreateRoomAsync` / `JoinRoomAsync` / `LeaveRoomAsync` / `SetReadyAsync` / `RestoreRoomAsync` / `GetSnapshotAsync` | 必需 |
| `IRoomGatewayHeroPickCapability` | `PickHeroAsync` | 可选（MOBA 类选角；名字带 Hero 但语义是"配置出战"，非英雄游戏可不实现） |
| `IRoomGatewayStagedLoadingCapability` | `BeginLoadingAsync` / `ReportLoadingProgressAsync` / `ReportAssetsLoadedAsync` / `CancelLoadingAsync` | 可选（分阶段资源加载） |
| `IRoomGatewayDirectBattleStartCapability` | `StartBattleAsync` | 可选（房主直接开战） |
| `IRoomGatewayStateSyncSubscriptionCapability` | `SubscribeStateSyncAsync` | 可选（订阅状态同步推送） |
| `IRoomGatewaySnapshotFeed` | `Current` + `SnapshotChanged` 事件 | 推送驱动的房间快照视图 |

## RoomGatewaySessionFlow：8 阶段会话编排

把上面的 RPC 串成一条**每步独立可恢复**的房间流程。构造时传入一个 `IRoomGatewaySessionClientBase`（自动探测可选能力）：

| 阶段 | 方法 | 说明 |
|------|------|------|
| 1 | `CreateRoomAsync` | 创建房间，返回 `roomId` |
| 2 | `JoinRoomAsync` | 加入房间，返回 join 结果（含 snapshot / battleId） |
| 3 | `ConfigureLoadoutAsync`（PickHero） | 配置出战（可选能力） |
| 4 | `SetReadyAsync` | 准备就绪 |
| 5 | `BeginLoadingAsync` | 开始资源加载（可选能力） |
| 6 | `ReportAssetsLoadedAsync` | 报告资源加载完成（可选能力） |
| 7 | `WaitForBattleStartAsync` | 轮询 `GetSnapshot` 直到 `Phase == InBattle` |
| 8 | `SubscribeStateSyncAsync` | 订阅状态同步（可选能力） |

阶段枚举：`RoomGatewaySessionPhase` = `Lobby / Loading / Starting / InBattle / Closing / Closed / Expired`。
入口类型：`RoomGatewaySessionEntryKind` = `TeamLobby / Reconnect / LateJoin`。

### 断线恢复（Reconnect / LateJoin）

```csharp
public Task<RoomGatewayStagedRestoreResult> RestoreAsync(...);
```

返回 `RoomGatewayStagedRestoreResult`（含 `NextStep` 建议），**分步**恢复连接 —— 区分"断线重连"与"中途加入"，可从任意阶段续上，而不必从头走 8 步。

辅助：`RoomGatewayRestoreFirstConnectionPolicy.ConnectAsync<TResult>(restoreAsync, fallbackCreateAsync, allowFallbackCreate)` —— "先尝试恢复，失败则回退新建"。

## 关键 DTO

- `RoomGatewayLaunchSpec` —— 启动参数（region/serverId/roomType/roomTitle/maxPlayers/tags）
- `RoomGatewayWorldStartAnchor` —— 世界起始锚（idealFrame / time anchor）
- `RoomGatewaySnapshot`（class）—— 房间快照（含 `RoomGatewayPlayerSnapshot` 列表）
- 大量 `RoomGateway*Request` / `RoomGateway*Result` struct

## 不在本包的内容（各示例自写）

- **战斗数据面**（输入上行 / 快照下行解码 / 预测回滚）—— shooter 与 moba 各自实现，走通用 gateway 的 `SubmitBattleInput` op-code 与各自协议（`protocol.shooter` / `protocol.moba`）。这是后续 `network.battle` 能力包计划收敛的部分。
- 游戏专属 room DTO（如 shooter 的 `ShooterGatewayCreateRoomRequest`、moba 的英雄/loadout 字段）—— 由各示例在自己的"房间适配器"里映射到本包的通用 `RoomGateway*` 类型。

## 用法骨架

```csharp
var sdk = new NetworkSdkBuilder().UseTransportFactory(() => new TcpTransport()).Build();
sdk.Open(host, port);

// 挂房间能力
var roomClient = sdk.CreateRoomClient();
var flow = new RoomGatewaySessionFlow(roomClient);

string roomId = await flow.CreateRoomAsync(sessionToken, launchSpec);
await flow.SetReadyAsync(...);
await flow.BeginLoadingAsync(...);
await flow.ReportAssetsLoadedAsync(...);
await flow.WaitForBattleStartAsync(...);
var sub = await flow.SubscribeStateSyncAsync(...);

// 房间快照推送
((IRoomGatewaySnapshotFeed)roomClient).SnapshotChanged += snap => { /* 更新大厅 UI */ };

// 断线后
var restore = await flow.RestoreAsync(...);   // 按 NextStep 续上
```

## 🆕 GatewayMultiplayerSession（新项目快速接入）

新项目用 **~10 行代码**完成"连接 → 登录 → 建房/加入 → 准备 → 开战 → 订阅状态同步"：

```csharp
var session = await GatewayMultiplayerSession.CreateAsync(
    "127.0.0.1", 4000, "player-1",
    RoomGatewayLaunchSpec.CreateDefault("yourgame", "yourgame-world"));
// session.SdkClient — 战斗数据面；session.Result — roomId/battleId/playerId
session.Tick(deltaTime);
session.Dispose();
```

替代各 demo 各自实现的 ~200 行组装代码。可选：`transportFactory`（WebSocket/LiteNetLib）、`joinRoomId`（加入已有房间）、`configureRoomClient`（游戏专属推送）。

## 相关

- 组装根/生命周期 → `com.abilitykit.network.sdk`
- 传输/连接原语 → `com.abilitykit.network.runtime`
- 完整新示例接入清单 → `Docs/design/07-NetworkSynchronization/`
