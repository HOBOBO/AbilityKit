# 07 · 多人联网 SDK 新示例接入指南

> 权威清单：一个**新玩法示例**要接入 AbilityKit 多人联网，"白拿什么 / 必须自己写什么 / 按什么顺序接入"。基于 shooter 与 moba 两个已落地示例的源码核校（2026-08-09）。
>
> 相关：SDK 装配、所有权、线程和 transport 证据以 `com.abilitykit.network.sdk` README 为 package canonical；房间能力见 `com.abilitykit.network.room` README。

## 0. 一句话定位

AbilityKit 的多人联网底层已经是一套玩法无关的 SDK（`network.sdk` + `network.room`），shooter 与 moba 都有真实接入。新示例可复用连接、重连、房间流程和当前 TCP/Orleans 网关骨架；仍需交付游戏协议、战斗数据面、同步策略，并为非 TCP transport 单独完成服务端与部署验收。

## 1. 分层回顾（哪些是 SDK，哪些是示例自写）

| 层 | 包 | 新示例 | 状态 |
|---|---|---|---|
| L0 传输 | `network.runtime`（`ITransport`→`TcpTransport`） | 可注入自定义传输 | ✅ 通用，可替换 |
| L1 连接 | `network.runtime`（`IConnection`→`ConnectionManager`） | — | ✅ 通用 |
| L2 组装根 | `network.sdk`（`NetworkSdkBuilder`/`NetworkSdkClient`） | — | ✅ 通用 |
| L3 房间会话 | `network.room`（`RoomGatewaySessionFlow` 8 阶段） | 实现可选能力接口 | ✅ 通用 |
| L4 战斗数据面 | （不在 SDK） | **自己写** | ⬜ 示例自写 |
| 服务端网关 | `AbilityKit.Orleans.Gateway.*` | 注册 op-code handler | ✅ 骨架通用，handler 分游戏 |
| 登录 | `com.abilitykit.demo.common`（`DemoRoomGatewayAccountClient`） | — | ✅ 通用 |

> ⚠️ 历史文档曾称"经 coordinator（`SessionCoordinator`/`ISyncAdapter`）接入多人"。**源码中两个示例的多人路径都不经 coordinator**，coordinator 仅在本地/harness 场景有价值。详见 `05-SessionCoordination.md` 与 coordinator skill 的修正说明。

## 2. 白拿（SDK 免费给，新示例零成本继承）

- **连接生命周期**：建连 / 心跳 / 断线重连 / 快重连（`FastReconnectSession`）/ 时钟同步（`TimeSyncBridge`）
- **请求-响应 RPC + 服务端推送分发**（`NetworkSdkClient.SendRawRequestAsync` + `ServerPushReceived`）
- **房间完整流程**：建房/加入/离开/准备/配置出战/分阶段加载/报告加载完成/开战/订阅状态同步（`RoomGatewaySessionFlow` 8 阶段）
- **断线恢复**：区分重连/中途加入的分步恢复（`RestoreAsync` + `NextStep`）+ 先恢复后回退策略（`RoomGatewayRestoreFirstConnectionPolicy`）
- **房间状态推送**（`IRoomGatewaySnapshotFeed`）
- **服务端网关骨架**：`TcpTransportServer` + `GatewayRequestRouter` + `GatewayHandlerRegistry` + `GatewaySessionRegistry/Binder` + Orleans grains（`RoomGrain` / `BattleLogicHostGrain` / `StateSyncObserverGrain`）
- **登录**：`DemoRoomGatewayAccountClient.LoginTcpAsync`（返回 `SessionToken`，后续每个 room/battle RPC 携带）

## 🆕 最快接入路径：GatewayMultiplayerSession

`network.room` 新增的高层会话门面。新项目用 **~10 行代码**完成从零到"进战斗 + 订阅状态同步"：

```csharp
var session = await GatewayMultiplayerSession.CreateAsync(
    "127.0.0.1", 4000, "player-1",
    RoomGatewayLaunchSpec.CreateDefault("yourgame", "yourgame-world"),
    transportFactory: () => new TcpTransport(),  // 当前生产与 Smoke 默认路径
    configureRoomClient: rc => { /* 注册游戏推送 */ });

// session.SdkClient     — 用于战斗数据面（NetworkBattleConfig + NetworkTransport）
// session.Result        — roomId / battleId / playerId / RoomSnapshot
// session.RoomClient    — 游戏专属 RPC（PickHero 等）
session.Tick(deltaTime);
session.Dispose();
```

内部封装：NetworkSdkBuilder → 连接 → GuestLogin → RoomGatewaySessionFlow（create/join → ready → waitForStart → subscribe）。替代各 demo 各自实现的 ~200 行组装代码。

## 可选传输 + 配置（SDK 扩展包）

| 包 | 客户端实现与局部测试 | 生产采用 / 服务端前提 |
|---|---|---|
| `network.runtime` / `TcpTransport` | 裸 TCP 实现；SDK 有契约测试 | Room、Battle、Moba、Shooter 和 Orleans Smoke 的默认或实际路径 |
| `network.transport.websocket` | `ClientWebSocket`；本机 `HttpListener` echo round-trip | 未发现生产消费者；不支持 Unity WebGL；Orleans 服务端类存在，但 canonical Gateway 尚未注册或托管 |
| `network.transport.litenet` | LiteNetLib `ReliableOrdered`；本机 UDP echo round-trip | 未发现生产消费者和 AbilityKit UDP/LiteNet 网关；没有真实弱网或 TCP 性能对比证据 |
| `network.transport.inmemory` | linked-pair 同步回环测试 | 仅适合进程内测试，不模拟 socket、时延、丢包、乱序或异步线程 |
| `network.battle.config` | `NetworkBattleConfig` 高层配置 builder | 封装标准 room-gateway 协议预设，不改变 transport 的服务端前提 |

客户端注入方式是 `NetworkSdkBuilder.UseTransportFactory(() => new YourTransport())`，因此 SDK 上层装配可以保持不变；这不表示对应服务端监听、目标平台、TLS/代理、弱网与恢复链路已经可用。详细所有权、线程和缓冲区契约以 SDK package README 为准。

`NetworkBattleConfig` builder：`new NetworkBattleConfig().WithGateway(...).WithTcpTransport().WithSession(...).UseRoomGatewayProtocol(...).WithInputSerializer(...).WithSnapshotDeserializer(...).Build()` → 自动设置 8 个标准 opcodes + auth 握手 + reliable-event ack + full-state-sync + command-sequence wrapping。游戏只需提供 input serialize/deserialize + snapshot deserialize。

## 3. 必须自己写（这是"少量适配"的真正边界，共 4 块）

### 3.1 游戏专属 wire protocol（参考 `protocol.shooter` / `protocol.moba`）

- op-code 表（参考 `ShooterOpCodes`：9 个，分 Input / Snapshot.{StartGame,State,Events,PackedState,...}）
- MemoryPack DTO（玩家指令、输入载荷、开战载荷、快照载荷）
- codec（快照编解码，如 `ShooterStateSnapshotCodec` / `ShooterPackedSnapshotCodec`）

体量：十来个文件。这是任何联网游戏都躲不掉的游戏内容。

### 3.2 房间适配器（把游戏 DTO 映射到通用 `RoomGateway*` 类型）

一个薄适配器，把游戏专属的 room RPC DTO 适配到 `network.room` 的通用能力接口。两个范本：

- shooter：`ShooterRoomGatewayFlow` 内部的 `ShooterRoomGatewaySessionClient`（实现 `IRoomGatewaySessionClientBase` + 各 capability，~150-200 行）
- moba：`GatewayRoomClient`（包装通用 `RoomGatewayWireSessionClient`，叠加 moba 专属操作）

非英雄游戏可不实现 `IRoomGatewayHeroPickCapability`；无分阶段加载可不实现 `IRoomGatewayStagedLoadingCapability`（缺失时 flow 抛 `NotSupportedException`）。

### 3.3 战斗数据面（输入上行 + 快照下行 + 客户端同步策略）

这是**唯一的工程变量**。注意：通用的"输入上行 + 快照/事件下行 + 可靠事件 + resync"引擎**已经存在** —— `NetworkTransport` / `NetworkTransportOptions`（`com.abilitykit.network.battle`，见其 README）。**MOBA 与 shooter 均已在用**（shooter 2026-08-09 迁移到两连接 NetworkTransport，multiprocess smoke 全绿，详见第 4 节）。下方两条 bullet 描述两个示例当前各自的数据面接线：

- **输入上行**：经通用 gateway 的 `SubmitBattleInput` op-code（`RoomGatewayOpCodes.SubmitBattleInput`）提交。shooter 的 `ShooterRoomGatewayClient.SubmitBattleInputAsync`、moba 的 `GatewayRoomClient.SubmitBattleInputAsync` 即此。
- **快照下行**：订阅 `ServerPushReceived`，按 op-code 分发到快照解码与应用。shooter 的 `ShooterRoomGatewayConnection.OnServerPushReceived`、moba 的 `BattleSessionFeature` 即此。
- **客户端同步策略**：二选一（或自定义）：
  - **状态同步（StateSync，服务端权威）** —— 参考 shooter：`ShooterClientSyncControllerFactory` 按 `NetworkSyncModel` 选 `PredictRollback` / `AuthoritativeInterpolation` / `HybridHeroPrediction`；自带 `ShooterClientPredictionRuntimeAdapter`。
  - **帧同步（FrameSync，客户端预测 + 服务端权威对账）** —— 参考 moba：复用 `com.abilitykit.host.extension` 的 `FrameSyncDriverModule`（服务端）+ `ClientPredictionDriverModule`（客户端，双世界：预测 + 权威 + 各自 jitter buffer）。

> 两种模型机制不同，**不要强行用同一套预测实现**。新示例按玩法选其一即可。

### 3.4 配置默认值

连接/房间默认值（host/port/region/serverId/roomType/worldType），各示例写在 ScriptableObject 或静态常量里（如 shooter 的 `ShooterGameplay` / `ShooterRemoteStateSyncDefaults`，moba 的 `BattleGatewayConfigSO`）。

## 4. 两种已验证参考实现对照

| 维度 | Shooter（StateSync） | MOBA（FrameSync） |
|------|---------------------|-------------------|
| 入口 | `ShooterClientNetworkLauncher` | `MultiplayerGatewayEntryModule` |
| SDK 组装 | `new NetworkSdkBuilder().UseConnectionFactory(...).Build()` | 同 |
| 房间适配 | `ShooterRoomGatewayConnection` + `ShooterRoomGatewayFlow` | `GatewayRoomClient`（包 `RoomGatewayWireSessionClient`） |
| 战斗 session | `ShooterClientSession` | `BattleSessionFeature` |
| 同步策略 | 自写 3 套 SyncController + `ShooterClientPredictionRuntimeAdapter` | `FrameSyncDriverModule` + `ClientPredictionDriverModule` |
| 协议 | `protocol.shooter`（9 opcodes） | `protocol.moba` |
| 经 coordinator？ | **否**（契约测试守护） | **否**（战斗走 host.extension framesync） |

## 5. 服务端侧（Orleans）

当前 TCP 主链的通用骨架已就绪，新示例只需注册游戏专属 handler。若选择 WebSocket 或 LiteNet，还必须先完成对应 listener 的启动注册、配置和端到端 smoke；仅客户端替换 factory 不会让服务端自动支持新协议。

- 通用（白拿）：`GuestLoginHandler` / `CreateRoomHandler` / `JoinRoomHandler` / `LeaveRoomHandler` / `RoomReadyHandler` / `BeginLoadingHandler` / `ReportAssetsLoadedHandler` / `StartRoomBattleHandler` / `SubscribeStateSyncHandler` / `RestoreRoomHandler` / `GetSnapshotHandler` / `TimeSyncHandler` + grains（`RoomGrain` / `BattleLogicHostGrain`）。
- 游戏专属（自写）：战斗数据 handler，如 `SubmitBattleInputHandler`（按 `roomType`/`worldType` 路由到该玩法的战斗逻辑）、快照构建。参考 shooter 的 `AddShooterSmokeGateway` 注册方式。

## 6. 逐步接入 checklist

1. **建包**：`com.abilitykit.demo.<game>.runtime` / `.view.runtime` / `.share` / `com.abilitykit.protocol.<game>`
2. **协议**（3.1）：定义 op-code + MemoryPack DTO + codec
3. **客户端组装**：`NetworkSdkBuilder` → `NetworkSdkClient` → `sdk.CreateRoomClient()`
4. **房间适配**（3.2）：实现 `IRoomGatewaySessionClientBase`（+ 按需 capability），把游戏 DTO 映射到 `RoomGateway*`
5. **战斗数据面**（3.3）：输入上行（`SubmitBattleInput`）+ 快照下行解码 + 选同步策略（StateSync / FrameSync）
6. **服务端**（5）：注册通用 handler + 游戏专属 battle handler/grain
7. **配置**（3.4）：默认 endpoint / roomType / worldType
8. **验证**：参考 shooter 的 `multiplayer_verification`（烟雾测试 + Unity 无头双实例）

## 会话装配配方（statesync / framesync 两条）

coordinator 的 session 引擎已移除（它 moba/local 形状、不适配 statesync、无人使用）。会话装配现在由各 demo 用「端口契约 + 可复用底层零件」自己拼。下面两条配方分别给出 statesync（shooter 范式）与 framesync（moba 范式）的完整装配链，新 demo 照着拼即可。**共享的是零件 + 契约，不是 monolithic session 引擎。**

### 通用零件（两配方都用）
- **端口契约**（`com.abilitykit.coordinator`，现已收缩为契约包）：`ILogicWorldDriverBridge`（逻辑世界驱动）、`ILogicWorldDriveGate`（玩法层闸门）、`ISessionCoordinatorHost`/`ISessionCoordinatorConfigPolicy`（宿主适配）、`ISpawnService`（出生）；`SessionConfig`/`SyncMode`/`HostMode`/`SessionId`/`PlayerInput`/`EntityState`/`SnapshotEntityState`/`FrameSnapshotData`/`PlayerSpawnData`。
- **连接/房间**：`network.sdk`（`NetworkSdkBuilder`/`NetworkSdkClient`）+ `network.room`（`RoomGatewaySessionFlow` 8 阶段）。
- **战斗数据面**：`network.battle`（`NetworkTransport`，契约中立 —— typed 与 raw 两种形态，见下）。

### 配方 A：statesync（服务端权威，shooter 范式）
适用：服务端权威快照 + 客户端预测/插值。
```
房间流:   RoomGatewaySessionFlow(房间连接) → create/join/ready/loading/start/subscribe
战斗面:   NetworkTransport(独立战斗连接，契约中立形态)
   输入上行: await SendInputAsync(req) → NetworkSubmitInputResponse（per-submit 真实结果，满足需校验 ServerTicks/AcceptedFrame 的客户端）
   下行:     订阅 RawServerPushReceived(opCode, payload) → 喂既有 raw apply 管线（如 shooter 的 ApplyGatewayPush）
同步策略: 自写（shooter: PredictRollback / AuthoritativeInterpolation / HybridHeroPrediction，选一）
驱动桥:   实现 ILogicWorldDriverBridge(SubmitInputs/AdvanceFrame/GetAllEntityStates) + ILogicWorldDriveGate
生命周期: 宿主每帧 tick（房间 NetworkSdkClient + 战斗 NetworkTransport）；销毁 dispose 二者
```
范本：`com.abilitykit.demo.shooter.view.runtime/Runtime/Client/`（`ShooterClientNetworkLauncher`/`GatewayLauncher`/`Session` + `ShooterBattleDriverHost`）。

### 配方 B：framesync（客户端预测 + 权威对账，moba 范式）
适用：帧同步，客户端预测世界 + 服务端权威世界双世界并行。
```
房间流:   RoomGatewaySessionFlow(房间连接，entry module 持有) → create/join/ready/loading/start/subscribe
战斗面:   NetworkTransport(独立战斗连接，typed / fire-and-forget 形态)
   输入上行: void SendInput(req)（fire-and-forget）
   下行:     订阅 FramePushed(FramePacket) + StateSyncSnapshotPushed/ReliableEventsPushed(typed)
双世界:   FramePacketNetAdapter(FramePacket → RemoteDriven jitter-buffer + Confirmed jitter-buffer)
同步策略: host.extension 的 FrameSyncDriverModule(服务端权威帧驱动) + ClientPredictionDriverModule(客户端预测+回滚+对账)
驱动桥:   实现 ILogicWorldDriverBridge + ILogicWorldDriveGate
生命周期: BattleSessionFeature tick + dispose(镜像 moba DisposeRemoteInterpolation：退订事件、清游标)
```
范本：`com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/`（`MultiplayerGatewayEntryModule`/`BattleSessionFeature`）+ `host.extension` framesync 模块。

### 选哪条 + 何时再抽模板
- 玩法需客户端预测+回滚（快节奏、低延迟手感）→ framesync（B）。
- 服务端权威即可（实现简单、防作弊）→ statesync（A）。
- 两者共用同一套 连接/房间/战斗数据面，差别只在 `NetworkTransport` 契约形态（typed vs raw）+ 同步策略 + 是否双世界。
- **现在只有 2 个 demo**。等第三个 demo 落地、会话胶水重复明显时，再把这两条配方提炼成可继承的「会话模板」（`statesync-session-template` / `framesync-session-template`）—— 从真实用法提取，而非凭空设计（coordinator 当初凭空设计才被废弃）。

## 逻辑层 sync-agnostic 的边界（换 sync 模型时，逻辑层改什么 / 不改什么）

coordinator 当初的设想是"逻辑层不关心帧同步/状态同步，宿主组合模块决定"。**这个设想在接口层成立，但有实现层边界。** 现在的落地方式：

### 接口层：逻辑层确实 sync-agnostic
逻辑世界实现 **`ILogicWorldDriverBridge`**（`SubmitInputs(PlayerInput[])` / `AdvanceFrame` / `GetAllEntityStates()`→`SnapshotEntityState[]`）—— 只暴露"按输入推进一帧 + 报告状态"。这些输入是来自 jitter-buffer（帧同步）还是服务端快照对账（状态同步），逻辑层不感知。**shooter 与 moba 的逻辑世界都实现这同一个端口**，被各自的同步机器驱动。这是 vision 的可达成核心。

### 实现层硬约束：帧同步要求逻辑确定性
"逻辑层完全不关心 sync 模型"在**接口层**对，但**实现层**有一个绕不开的约束：
- **帧同步**（配方 B）要求逻辑世界**确定性**：定点运算、无堆分配、确定性迭代顺序（moba 的定点 A* 就是为此）—— 否则客户端预测/回滚会与服务端权威发散。
- **状态同步**（配方 A）不要求：服务端权威，客户端可非确定预测。

所以"宿主组合模块切 sync"对 **driving/network 侧**成立；**逻辑仿真自身**若非确定，注定只能走状态同步。确定性是逻辑层的**性质**，同步层抹不平 —— 这是 vision 的真实边界。

### 换 sync 模型时，改什么 / 不改什么
| | 帧同步 ↔ 状态同步 切换时 |
|---|---|
| **不改** | `ILogicWorldDriverBridge` 实现（同一端口：推进 + 报告） |
| **改（逻辑层）** | 逻辑世界的**确定性**：上帧同步必须确定（定点 / 无堆分配 / 确定迭代序）；状态同步可松 |
| **改（宿主组合）** | 同步机器：帧同步配方（`host.extension` 的 `FrameSyncDriverModule`/`ClientPredictionDriverModule` + `FramePacketNetAdapter` 双世界）↔ 状态同步配方（自写 / 复用 `ShooterClientSyncController` 类的预测/插值） |
| **改（数据面）** | `NetworkTransport` 契约形态：帧同步用 typed / fire-and-forget（`SendInput` + `FramePushed`）；状态同步用 raw / awaitable（`SendInputAsync` + `RawServerPushReceived`）—— **同一引擎，两种形态** |

### 结论
- **逻辑层 sync-agnostic**：成立 —— 通过 `ILogicWorldDriverBridge` 端口（两 demo 共用）。
- **宿主组合决定 sync 模型**：成立 —— 选 framesync 配方或 statesync 配方 + 对应 `NetworkTransport` 契约形态。
- **唯一硬边界**：帧同步要求逻辑确定性。若逻辑天生非确定（浮点物理、非确定迭代等），只能走状态同步。
- **不再有"一个 adapter 接口切 N 模式"的 monolithic session 引擎**（coordinator 的 `ISyncAdapter` 体系已删 —— 它过度统一、不适配 statesync）。共享的是**端口 + 配方 + 契约中立数据面**，不是 session engine。

## 7. 后续收敛（路线图）

源码核校（2026-08-09）：通用战斗数据面引擎已迁至中立包 `com.abilitykit.network.battle`（命名空间 `AbilityKit.Network.Battle`，见其 README）；**moba 与 shooter 均在用**（shooter 已迁移，multiprocess smoke 含 recoverable-retry 故障注入全绿）。`Moba/` 遗留子树已随 `game.battle.transport.runtime` 包整体删除（Console demo 已迁移到统一 SDK）。SDK package canonical 已补齐 Builder/Client 所有权、Tick/Dispose、dispatcher、缓冲区和 transport 证据边界。

- **P2（已完成）**：把现有引擎文档化为 SDK 战斗层。
- **P2.1（已完成）**：把通用引擎（`NetworkTransport`/`NetworkTransportOptions`/`Projection`）从 `game.battle.transport.runtime` 搬到中立包 `com.abilitykit.network.battle`，命名空间改为 `AbilityKit.Network.Battle`。
- **P2.1b（已完成）**：Console demo 迁移到统一 SDK（`NetworkSdkClient` + `WireRoomGatewayBinary` + `RoomGatewayOpCodes`），`game.battle.transport.runtime` 包**整体删除**。
- **P2.2（已完成 2026-08-09）**：shooter 客户端数据面迁移到统一引擎 —— 两连接拓扑（房间控制面 + 独立 battle NetworkTransport 连接）；`ShooterBattleTransportGatewayClient` 适配 `NetworkTransport.SendInputAsync`（per-submit 结果），`ShooterBattleDataPlane` 经 `RawServerPushReceived` 喂既有 `ApplyGatewayPush`（push 在主线程 drain，不与 `session.Tick` 竞争）；`ShooterRoomGatewayConnection` 收缩为房间控制面 + battle-state facade（`NotifyBattlePushDispatched`）。退役了 `ShooterRoomGatewayClient`/`ShooterRoomGatewayConnection` 的手写战斗胶水。multiprocess smoke（含 recoverable-retry 3 次注入故障 + 重连）全绿、`diffStatus=Identical`。
### 进行中（WIP，未提交；2026-08-09 记录）

- **Room-flow staged-restore 重构（用户进行中）**：`network.room/Runtime/RoomGatewaySessionFlow.cs`（工作树 `M`）正从单步 `CreateRoomAsync`/`RestoreRoomAsync` 高层接口重构为分阶段 restore（`RoomGatewayStagedRestoreResult` / `NextStep` / `RestoreStatus` 等）。重构**尚未完成**：已知粗糙点 —— create 路径 `EntryKind` 因 server `JoinRoom` 的 alreadyMember 副产物被标成 `Reconnect`（shooter flow 已在 `ShooterRoomGatewayFlow.JoinAndLaunchAsync` 用 `createdRoomOwner ? TeamLobby : join.JoinKind` 修复；SDK flow 同类问题待随重构收尾确认）。
- **`GatewayMultiplayerSession`（高层 ~10 行门面，`network.room/Runtime/GatewayMultiplayerSession.cs`）**：封装 connect → GuestLogin → create/join → ready → 可选 `WaitForBattleStart` → SubscribeStateSync；已有首个真实消费者（Console demo，见下）。
- **GatewayMultiplayerSession 接入（高层门面 dogfood）—— ✅ Console 已接入（2026-08-10）**：① Console demo `StateSyncAdapter` 的房间组装已切到门面编排 seam `RunRoomFlowAsync`（`waitForBattleStart: false` 适配单 client SnapshotAuthority 无 formal battle start），保留自有连接/重连外壳与 join→create 回退；对本地 Orleans 网关端到端验证通过（login → join 409 回退 create → ready → subscribe），`moba-console-smoke` 门禁 4/4。② MOBA **评估后决定不接入门面**：其房间栈（`GatewayMultiplayerRoomSession`+`MultiplayerRoomFlowController`）已全面跑在 SDK `RoomGatewaySessionFlow` 分阶段 API 上，状态机承载 hero-pick 交互/loading 进度/断线恢复，线性门面表达不了事件驱动阶段，强切是降级 —— MOBA 的统一性证据记在 `RoomGatewaySessionFlow` 层。门面定位：线性/最小流程的快速入口（Console 背书）。

- **P3（后续）**：提供参考同步实现（如 shooter 的 `AuthoritativeInterpolation`）作为开箱默认。`GatewayMultiplayerSession` 已接入 Console 示例（见上）；剩余硬化项是 in-process gateway fixture 的完整 happy-path 测试；详见 `08-NetworkOptimizationPlan.md` P-A。
- **P4（后续）**：决策 coordinator 去留（收缩为本地/harness 专用，删远端死 adapter）。
- **P5（可选）**：收敛预测算法（帧同步 vs 状态同步机制不同，不强行统一；可能永远不做）。

预测/回滚算法本身保持示例自有。
