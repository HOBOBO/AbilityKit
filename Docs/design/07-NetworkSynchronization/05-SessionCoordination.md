# 7.5 会话协调

> 本文从源码角度说明 AbilityKit 如何把客户端会话、已有 world 接入、远端 transport、Gateway 入场流程、Room 生命周期、Battle Host 权威推进、帧包适配和状态订阅串成一次可恢复、可重连、可观测的联机会话。会话协调不是单一类的职责，而是 `SessionCoordinator`、`RemoteSyncAdapter`、`IRemoteBattleSyncTransport`、`RoomGatewaySessionFlow`、`RoomGrain`、`BattleLogicHostGrain` 和 `FramePacketNetAdapter` 的组合边界。

---

## 目录

1. [能力定位](#1-能力定位)
2. [源码入口](#2-源码入口)
3. [会话协调的分层边界](#3-会话协调的分层边界)
4. [客户端 SessionCoordinator](#4-客户端-sessioncoordinator)
5. [远端同步端口](#5-远端同步端口)
6. [Gateway 入场流程](#6-gateway-入场流程)
7. [Room 与 Battle 服务端协调](#7-room-与-battle-服务端协调)
8. [端侧帧包适配](#8-端侧帧包适配)
9. [完整会话时序](#9-完整会话时序)
10. [恢复、晚加入与已有 world 接入](#10-恢复晚加入与已有-world-接入)
11. [设计意图](#11-设计意图)
12. [风险与检查点](#12-风险与检查点)
13. [源码阅读路径](#13-源码阅读路径)

---

## 1. 能力定位

会话协调解决的是跨层问题：玩家从“我要加入一局游戏”到“本地 world 跟随服务器帧和快照稳定推进”之间，需要经过身份、房间、准备、开战、世界锚点、输入提交、快照订阅、重连恢复等步骤。

AbilityKit 把这些职责拆成几层：

| 层级 | 责任 | 关键类型 |
|------|------|----------|
| 客户端会话 | 创建或复用 world，选择 sync adapter，驱动 Tick，暴露 hooks | `SessionCoordinator`、`ISessionCoordinatorHost` |
| 远端同步 | 连接 endpoint，提交输入，接收服务器快照 | `RemoteSyncAdapter`、`IRemoteBattleSyncTransport` |
| Gateway Flow | 按 create/join/ready/start/subscribe/restore 编排入场流程 | `RoomGatewaySessionFlow`、`IRoomGatewaySessionClient` |
| 房间域 | 成员、准备、玩法房间状态、恢复、晚加入、开战入口 | `RoomGrain`、`RoomMemberTracker`、`IRoomGameplayAdapter` |
| 战斗域 | 权威 Tick、输入调度、运行时 session、快照推送 | `BattleLogicHostGrain`、`BattleInputBuffer`、`BattleTickDriver` |
| 帧同步广播 | 纯 frame push 场景下按帧广播输入 | `BattleFrameSyncGrain` |
| 端侧帧包 | 把网络帧包写入 remote-driven/confirmed 输入源并路由快照 | `FramePacketNetAdapter`、`RemoteFrameAggregator` |

设计目标：

- 业务层不直接依赖 Gateway handler、Orleans Grain 或 Socket 类型。
- Room 和 Battle 分层，避免大厅/成员生命周期污染权威 Tick。
- 客户端既能创建新 world，也能把已有 world 纳入 Coordinator。
- 输入、快照、确认、恢复都通过明确边界传递。
- 断线恢复和晚加入都能拿到 `WorldStartAnchor`、`BattleId`、`WorldId` 等会话锚点。

---

## 2. 源码入口

### 2.1 客户端/Unity Package

| 能力 | 源码 | 说明 |
|------|------|------|
| 会话协调器 | `Unity/Packages/com.abilitykit.coordinator/Runtime/Core/SessionCoordinator.cs` | 生命周期、world 创建、adapter attach、Tick |
| 已有 world host | `Unity/Packages/com.abilitykit.coordinator/Runtime/Core/ExistingWorldSessionCoordinatorHost.cs` | 把已有 `IWorld` 包装成 coordinator host |
| 同步 adapter 工厂 | `Unity/Packages/com.abilitykit.coordinator/Runtime/Adapters/SyncAdapterFactory.cs` | 按有效同步模式选择 Local、Remote 或 Hybrid adapter |
| 本地同步 adapter | `Unity/Packages/com.abilitykit.coordinator/Runtime/Adapters/LocalSyncAdapter.cs` | 本地输入缓冲、逻辑帧推进和 driver bridge |
| 远端同步 adapter | `Unity/Packages/com.abilitykit.coordinator/Runtime/Adapters/RemoteSyncAdapter.cs` | 连接 transport、提交输入、接收服务器快照 |
| 混合同步 adapter | `Unity/Packages/com.abilitykit.coordinator/Runtime/Adapters/HybridSyncAdapter.cs` | 已有 transport/bookkeeping；通用预测校正仍未闭环 |
| 远端 transport 端口 | `Unity/Packages/com.abilitykit.coordinator/Runtime/Transport/IRemoteBattleSyncTransport.cs` | 环境提供 Gateway/Socket/测试替身 |
| Gateway flow | `Unity/Packages/com.abilitykit.host.extension/Runtime/Session/RoomGatewaySessionFlow.cs` | create/join/ready/start/subscribe/restore 编排 |
| 帧包适配 | `Unity/Packages/com.abilitykit.host.extension/Runtime/Session/FramePacketNetAdapter.cs` | 输入双写和快照路由 |
| MOBA view adapter | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/BattleSessionNetAdapter.cs` | Demo View 包对通用帧包适配器的封装 |
| Shooter 客户端会话 | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client` | 已有完整 session 生命周期，直接组合 Gateway flow、预测管线与框架输入队列 |

### 2.2 Server/Orleans

| 能力 | 源码 | 说明 |
|------|------|------|
| 房间 Grain | `Server/Orleans/src/AbilityKit.Orleans.Grains/Rooms/RoomGrain.cs` | 加入、准备、恢复、晚加入、开战 |
| 战斗 Host Grain | `Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/BattleLogicHostGrain.cs` | 初始化 runtime、输入调度、Tick、状态推送 |
| 帧同步 Grain | `Server/Orleans/src/AbilityKit.Orleans.Grains/FrameSync/BattleFrameSyncGrain.cs` | 按帧广播输入事件 |
| 输入 Gateway handler | `Server/Orleans/src/AbilityKit.Orleans.Gateway/Gateway/Handlers/SubmitBattleInputHandler.cs` | 校验 request/session token 并转发输入 |
| Battle 契约 | `Server/Orleans/src/AbilityKit.Orleans.Contracts/Battle/IBattleLogicHostGrain.cs` | Battle Host Grain API |
| FrameSync 契约 | `Server/Orleans/src/AbilityKit.Orleans.Contracts/FrameSync/IBattleFrameSyncGrain.cs` | 帧同步 Grain API |

---

## 3. 会话协调的分层边界

```mermaid
flowchart TB
    subgraph Client[Client]
        App[Application]
        Coordinator[SessionCoordinator]
        WorldHost[IWorldHost]
        World[IWorld]
        SyncAdapter[RemoteSyncAdapter]
        Transport[IRemoteBattleSyncTransport]
        NetAdapter[FramePacketNetAdapter]
    end

    subgraph GatewayLayer[Gateway]
        Flow[RoomGatewaySessionFlow]
        GatewayClient[IRoomGatewaySessionClient]
        InputHandler[SubmitBattleInputHandler]
    end

    subgraph Orleans[Orleans]
        Room[RoomGrain]
        FrameSync[BattleFrameSyncGrain]
        Battle[BattleLogicHostGrain]
        Runtime[IBattleRuntimeSession]
    end

    App --> Coordinator
    Coordinator --> WorldHost
    WorldHost --> World
    Coordinator --> SyncAdapter
    SyncAdapter --> Transport
    Flow --> GatewayClient
    Transport --> InputHandler
    GatewayClient --> Room
    Room --> FrameSync
    Room --> Battle
    InputHandler --> Battle
    Battle --> Runtime
    Runtime --> Transport
    Transport --> NetAdapter
```

这张图强调两个边界：

1. `SessionCoordinator` 是客户端本地装配器，不应该直接知道 Orleans Grain。
2. `RoomGatewaySessionFlow` 是入场编排脚本，不是底层 transport；真正提交输入时仍应通过 `RemoteSyncAdapter` 和 `IRemoteBattleSyncTransport`。

---

## 4. 客户端 SessionCoordinator

### 4.1 初始化流程

`SessionCoordinator.Initialize(SessionConfig config, ISessionCoordinatorHost host)` 的源码顺序：

1. 状态必须是 `Idle`，否则抛异常。
2. 状态切到 `Initializing`。
3. 保存 config 和 host。
4. 如果 host 实现 `ISessionCoordinatorConfigPolicy`，调用 `ConfigureSession(ref config)`。
5. 解析 `SessionRuntimePolicy`。
6. `host.CreateWorldHost(config)`。
7. 创建 `WorldCreateOptions`，并调用 `host.ConfigureWorldCreateOptions`。
8. `worldHost.CreateWorld(options)`。
9. `world.Initialize()`。
10. 保存 `world.Services` 为 resolver。
11. `host.LoadConfig(world, config)`。
12. `host.RegisterServices(world, config)`。
13. 创建 `ViewTimeline`。
14. `SyncAdapterFactory.Create(world, config)`。
15. `syncAdapter.Attach(this)`。
16. 如果已经设置 driver，则 `syncAdapter.SetLogicWorldDriver(driver)`。
17. 触发 `SessionStarting` hook。
18. 状态回到 `Idle`，等待 `Start()`。

```mermaid
sequenceDiagram
    participant App as App
    participant Coord as SessionCoordinator
    participant Host as ISessionCoordinatorHost
    participant WorldHost as IWorldHost
    participant World as IWorld
    participant Adapter as ISyncAdapter

    App->>Coord: Initialize(config, host)
    Coord->>Host: ConfigureSession(ref config)
    Coord->>Host: CreateWorldHost(config)
    Host-->>Coord: IWorldHost
    Coord->>Host: ConfigureWorldCreateOptions
    Coord->>WorldHost: CreateWorld(options)
    WorldHost-->>Coord: IWorld
    Coord->>World: Initialize()
    Coord->>Host: LoadConfig(world, config)
    Coord->>Host: RegisterServices(world, config)
    Coord->>Coord: new ViewTimeline
    Coord->>Coord: SyncAdapterFactory.Create(world, config)
    Coord->>Adapter: Attach(this)
```

### 4.2 Start 和 Tick

`Start()` 的源码行为：

- 状态必须是 `Idle`。
- 状态切到 `Running`。
- 如果 `UseCoordinatorSpawnService` 为 true，调用 host 创建 spawn data 并尝试通过 `ISpawnService` 生成玩家。
- 重新 attach sync adapter。
- 触发 `SessionStarted` 和 `FirstFrameReceived` hooks。

`Tick(float deltaTime)` 的顺序：

1. 非 Running 状态直接返回。
2. `PreTick` hook。
3. 所有 `ISessionPreTickSubFeature.OnPreTick`。
4. `_syncAdapter.Tick(deltaTime)`。
5. 如果 `CanDriveLogicWorld(deltaTime)`，调用 `_worldHost.Tick(deltaTime)`。
6. 所有 `ISessionPostTickSubFeature.OnPostTick`。
7. `PostTick` hook。

```mermaid
flowchart TD
    A[Tick] --> B{State == Running}
    B -- no --> X[return]
    B -- yes --> C[InvokePreTick]
    C --> D[SubFeature OnPreTick]
    D --> E[SyncAdapter.Tick]
    E --> F{CanDriveLogicWorld}
    F -- yes --> G[WorldHost.Tick]
    F -- no --> H[skip world]
    G --> I[SubFeature OnPostTick]
    H --> I
    I --> J[InvokePostTick]
```

`CanDriveLogicWorld` 会优先查询 `ILogicWorldDriveGate`。如果 runtime policy 要求 gate，但 resolver 中没有 gate 或 gate 返回 false，本地 world 就不会被 Tick。该边界服务于远端权威和状态同步客户端的驱动隔离。

---

## 5. 远端同步端口

### 5.1 `IRemoteBattleSyncTransport`

这个接口是 Coordinator 和具体网络环境之间的端口：

| 成员 | 语义 |
|------|------|
| `IsConnected` | 当前是否连接 |
| `OnConnectionChanged` | 连接状态变化 |
| `OnServerSnapshot` | 收到服务器快照 |
| `OnServerConfirmation` | 收到服务器确认 |
| `Connect(endpoint, roomId, playerId, syncMode)` | 连接远端战斗同步服务 |
| `Disconnect()` | 断开 |
| `Tick(deltaTime)` | 推进网络 transport |
| `SubmitInput(PlayerInput input)` | 提交本地输入 |

源码注释明确说明：Coordinator 拥有同步编排，具体环境拥有 grain calls、gateway requests、sockets 或 test doubles。

### 5.2 `RemoteSyncAdapter`

`RemoteSyncAdapter` 构造时从 `world.Services` 解析 `IRemoteBattleSyncTransport`。如果没有解析到，使用 `NullRemoteBattleSyncTransport.Instance`，其 `IsConnected` 永远 false，`SubmitInput` 返回 false。

```mermaid
flowchart TD
    A[RemoteSyncAdapter ctor] --> B[Resolve IRemoteBattleSyncTransport]
    B --> C{found}
    C -- yes --> D[Bind transport]
    C -- no --> E[NullRemoteBattleSyncTransport]
    D --> F[Subscribe connection and snapshot events]
    E --> F
    F --> G[Connect endpoint]
    G --> H[SubmitInput only when connected]
    H --> I[FeedServerSnapshot]
    I --> J[OnServerSnapshot + OnFrameSync]
```

关键行为：

| 方法 | 源码行为 |
|------|----------|
| `Connect` | 保存 endpoint、roomId、playerId，调用 transport.Connect |
| `Tick` | 累加 render time，调用 transport.Tick |
| `SubmitInput` | transport 未连接时直接返回 |
| `FeedServerSnapshot` | 清空并更新 `_lastSnapshot`，触发快照和帧同步事件 |
| `GetAllEntityStates` | 有 driver 时读 driver，否则返回 `_lastSnapshot` |
| `Dispose` | Disconnect、解绑事件、清空状态 |

### 5.3 为什么不能让业务直接调 Gateway

业务直接调 Gateway 会带来三个问题：

1. 本地/远端/回放/测试模式无法复用同一入口。
2. 表现层会依赖传输协议，难以替换 TCP、HTTP、Orleans 或本地模拟器。
3. 输入确认、服务器快照、连接状态无法统一接入 `ISyncAdapter` 生命周期。

标准路径应该是：

```mermaid
flowchart LR
    UI[Input System or UI] --> Coord[SessionCoordinator.SubmitLocalInput]
    Coord --> Adapter[RemoteSyncAdapter.SubmitInput]
    Adapter --> Transport[IRemoteBattleSyncTransport.SubmitInput]
    Transport --> Gateway[Gateway or Socket]
    Gateway --> Battle[BattleLogicHostGrain.SubmitInputAsync]
```

### 5.4 Local、Remote 与 Hybrid 成熟度矩阵

`DefaultSyncAdapterFactory` 当前把 `Lockstep` 映射到 `LocalSyncAdapter`，把 `SnapshotAuthority` 和 `StateSync` 映射到 `RemoteSyncAdapter`，把 `Hybrid` 映射到 `HybridSyncAdapter`。这个映射证明实例化入口存在，不等同于每种同步模式的全部语义均已闭环。

| Adapter | 当前已实现能力 | 尚未覆盖或限制 | 当前成熟度判断 |
|---------|----------------|----------------|----------------|
| `LocalSyncAdapter` | 本地输入缓冲；按 tick rate 推进；通过 `ILogicWorldDriverBridge` 提交输入、启动和推进 world；支持 drive gate；输出帧事件 | 自身不提供跨端输入交换、确定性校验或远端确认；无 driver 时只推进内部帧号和时间，实体状态为空 | 本地/离线装配为 Pilot；不能仅凭类型名声明通用多人锁步已闭环 |
| `RemoteSyncAdapter` | 解析并绑定 transport；连接/断开；已连接时提交输入；接收权威快照；转发连接、快照和帧事件；无 driver 时保留最后快照 | 不承担客户端预测、回滚或插值；缺少 transport 时退化为永不连接的 Null transport | 远端权威 adapter 契约已实现；生产成熟度仍取决于具体 transport、场景测试和恢复门禁 |
| `HybridSyncAdapter` | transport 绑定；输入缓冲与服务端提交；预测开关和预测帧 bookkeeping；接收确认/快照；保存 confirmed snapshot；触发 reconciliation 状态和事件 | 未把输入应用到预测状态；未执行通用本地预测模拟；未比较预测与权威状态；未实施校正和 replay；无 driver 时不返回预测快照 | transport/bookkeeping 可作为 Pilot 子能力；通用预测、对账、校正与重演仍是 Experimental |

Hybrid 当前流程的真实边界如下：

```mermaid
flowchart LR
    Input[SubmitInput] --> Queue[输入入队]
    Queue --> Counter[预测帧计数加一]
    Queue --> Transport[发送到服务端]
    Transport --> Confirm[收到确认或快照]
    Confirm --> Store[保存 confirmed snapshot]
    Store --> Mark[标记需要 reconciliation]
    Mark --> Stub[Reconcile bookkeeping]
    Stub -. 未实现 .-> Sim[本地预测模拟]
    Stub -. 未实现 .-> Compare[预测与权威比较]
    Stub -. 未实现 .-> Replay[校正并重演未确认输入]
```

因此文档、PPT 和选型材料应使用以下措辞：

- 可以说“Hybrid adapter 已有 transport 接入、输入缓冲、确认状态和 reconciliation 骨架”。
- 不应说“框架已提供完整通用客户端预测和自动回滚校正”。
- 项目若已经通过 Shooter 或 MOBA 专用控制器实现预测，只能声明该项目路径已验证，不能反推通用 `HybridSyncAdapter` 已完成。
- Hybrid 晋升 Pilot/Supported 前，需要补预测状态接口、确定性 replay、误差阈值、输入确认清理、恢复测试和断线重连场景。

成熟度采用公司治理定义，详见[公司级采用与模块治理规范](../10-EngineeringQuality/04-CompanyAdoptionAndModuleGovernance.md)。

### 5.5 模式选型速查

| 场景 | 建议起点 | 采用前必须确认 |
|------|----------|----------------|
| 单机、离线验证、同进程逻辑 | Local | driver 是否由 adapter 或 coordinator 驱动；是否会发生双 Tick |
| 客户端只消费服务器权威状态 | Remote | transport 注入、订阅恢复、world drive gate、快照基线和断线策略 |
| 项目已有专用预测控制器 | Remote + 项目预测层 | 预测状态不归通用 adapter；确认源、校正和 replay 由项目契约覆盖 |
| 希望直接使用通用 Hybrid 预测 | 暂不作为生产默认选型 | 5.4 列出的预测模拟、比较、校正、重演和恢复缺口必须先闭合 |
| 多人确定性锁步 | 不能仅凭 Local 选定 | 还需跨端输入交换、帧确认、确定性 hash、迟到输入和重连协议 |

选型评审应先确认权威源、谁驱动 world、输入如何确认、状态如何恢复，再选择 adapter。`SyncAdapterFactory` 的枚举映射只是装配结果，不是同步方案设计的替代品。

---

## 6. Gateway 入场流程

`RoomGatewaySessionFlow` 是依赖 `IRoomGatewaySessionClient` 的阶段化会话工具。框架公开 create/join/ready/loading/wait/subscribe/restore 原子阶段；具体示例在自己的会话层组合阶段。未稳定的 create/join/restore 聚合入口已经删除，不再维护两套流程。

### 6.1 创建房间并开战

Shooter 创建房间的业务用例按以下阶段组合：

1. 校验 `sessionToken` 和 `playerId`。
2. `CreateRoomAsync`。
3. `JoinRoomAsync`。
4. `SetReadyAsync`。
5. `BeginLoadingAsync`。
6. `ReportAssetsLoadedAsync`。
7. `WaitForBattleStartAsync`。
8. `SubscribeStateSyncAsync`。
9. 示例会话构造包含 room、battle、world、player、anchor、server ticks、entry kind 的最终结果。

```mermaid
sequenceDiagram
    participant Flow as RoomGatewaySessionFlow
    participant Client as IRoomGatewaySessionClient

    Flow->>Client: CreateRoomAsync
    Client-->>Flow: RoomId
    Flow->>Client: JoinRoomAsync
    Client-->>Flow: JoinResult
    Flow->>Client: SetReadyAsync
    Client-->>Flow: ReadyResult
    Flow->>Client: BeginLoadingAsync
    Flow->>Client: ReportAssetsLoadedAsync
    Flow->>Client: WaitForBattleStartAsync
    Client-->>Flow: BattleId + WorldId + WorldStartAnchor
    Flow->>Client: SubscribeStateSyncAsync
    Client-->>Flow: SubscriptionResult
    Flow-->>Flow: Build example launch result
```

### 6.2 加入房间或运行中战斗

示例会话先调用 `JoinRoomAsync`。如果 join 结果不是 `TeamLobby`，且带 `BattleId`，说明已经是 reconnect 或 late join 到运行中战斗：

- 直接 `SubscribeStateSyncAsync`。
- 返回 `started: true`。
- 使用 join 结果中的 `WorldStartAnchor`、`WorldId`、`JoinKind`。

否则才进入 ready/loading/report/wait/subscribe。

```mermaid
flowchart TD
    A[JoinRoomAsync] --> B{JoinKind != TeamLobby and BattleId exists}
    B -- yes --> C[SubscribeStateSyncAsync]
    C --> D[return running battle result]
    B -- no --> E[SetReadyAsync]
    E --> F[BeginLoading + ReportAssetsLoaded]
    F --> G[WaitForBattleStart + SubscribeStateSync]
    G --> H[return lobby-start result]
```

### 6.3 从任意房间阶段恢复

`RestoreAsync` 的源码步骤：

1. 校验 session token、region、serverId、playerId。
2. 调用底层 `RestoreRoomAsync(sessionToken, region, serverId)` 恢复成员关系。
3. active room 存在时读取一次 `GetSnapshotAsync`；请求失败时用 restore 响应合成最小快照。
4. 根据快照阶段返回 `RoomGatewayStagedRestoreNextStep`。
5. 结果同时保留 snapshot、entry kind、can start、restore status/error code 和服务器时间。

示例会话根据 `NextStep` 从正确位置继续：Lobby 从 ready/loading 开始，Loading 只补资源上报，Starting 只等待开战，InBattle 直接订阅。最终订阅携带 `eventEpoch` 和 `lastEventAck`，不会因阶段恢复丢失可靠事件游标。

---

## 7. Room 与 Battle 服务端协调

### 7.1 `RoomGrain` 的职责

`RoomGrain` 持有：

- `RoomSummary`
- `IRoomGameplayAdapter`
- 玩法房间状态 `_gameplayState`
- `RoomMemberTracker`
- `_closed`
- `_battleId`
- `_worldId`
- `_worldStartAnchor`

关键 API：

| API | 行为 |
|-----|------|
| `JoinMemberAsync` | 加入房间；如果已在战斗中，区分 reconnect 和 late join |
| `RestoreAsync` | 根据账号恢复 active room/battle 状态 |
| `SetReadyAsync` | 设置准备状态 |
| `SubmitGameplayCommandAsync` | 提交房间玩法命令 |
| `StartBattleAsync` | 构造 battle init params，初始化 FrameSync/Battle Host |
| `GetSnapshotAsync` | 返回 RoomSnapshot |
| `CloseAsync` | 关闭房间并清理映射 |

### 7.2 开战时 Room 做什么

`RoomGrain.StartBattleAsync` 的源码顺序：

1. 校验 request。
2. 读取 summary、gameplay、gameplayState。
3. 确认操作者是房主。
4. 如果 `_battleId` 已存在，直接返回已有 battle 信息。
5. 确认房间 open。
6. `gameplay.CanStart(gameplayState)`。
7. `_battleId = summary.RoomId`。
8. `gameplay.BuildBattleInitParams`。
9. `RoomBattleSyncOptionsMapper.Resolve`。
10. 保存 `_worldId`。
11. `RoomFrameSyncRoute.ResolveStartRoute`。
12. 如果有 frame sync options，初始化 `IBattleFrameSyncGrain`。
13. 如果需要 battle runtime，初始化 `IBattleLogicHostGrain` 并读取 `WorldStartAnchor`。
14. 否则本地创建 `WorldStartAnchor`。
15. `_closed = true`。
16. 通知 room directory。
17. 返回 `StartRoomBattleResponse`。

```mermaid
flowchart TD
    A[StartBattleAsync] --> B[Ensure owner]
    B --> C{battle exists}
    C -- yes --> D[return existing response]
    C -- no --> E[EnsureOpen]
    E --> F{gameplay.CanStart}
    F -- no --> X[throw not ready]
    F -- yes --> G[BuildBattleInitParams]
    G --> H[Resolve sync options]
    H --> I[Resolve start route]
    I --> J{FrameSyncOptions}
    J -- yes --> K[IBattleFrameSyncGrain.InitializeAsync]
    J -- no --> L[skip]
    K --> M{RequiresBattleRuntime}
    L --> M
    M -- yes --> N[IBattleLogicHostGrain.InitializeBattleAsync]
    N --> O[GetWorldStartAnchorAsync]
    M -- no --> P[CreateWorldStartAnchor]
    O --> Q[Close room and notify]
    P --> Q
    Q --> R[StartRoomBattleResponse]
```

### 7.3 `BattleLogicHostGrain` 的职责

`BattleLogicHostGrain` 是权威战斗主机。它持有：

- `ServerGameplayModuleCatalog`
- `BattleRuntimeRegistry`
- `BattleHostState`
- `BattleInputBuffer<BattleInputItem>`
- `BattleTickDriver<BattleInputItem>`
- `BattleObserverRegistry<IStateSyncObserverGrain>`
- `BattleSnapshotPublisher`
- `IBattleRuntimeSession`
- `WorldStartAnchor`
- sync profile/template

`InitializeBattleAsync` 的源码顺序：

1. 校验 initParams。
2. 如果已初始化，忽略重复初始化。
3. 解析 room type 对应 gameplay module。
4. 解析 sync profile/template。
5. 归一化 `BattleSyncStartOptions`。
6. 保存 worldId、tickRate、inputDelayFrames。
7. 创建 `WorldStartAnchor`。
8. 初始化 `BattleHostState`。
9. 通过 runtime registry 创建 `IBattleRuntimeSession`。
10. 调用 runtime session `Start(initParams)`。
11. 成功后置 `_initialized = true`。
12. 发布初始快照。
13. 启动 Orleans timer。

### 7.4 服务端输入调度

`SubmitInputAsync` 不直接把输入塞给 runtime，而是经过调度：

```mermaid
sequenceDiagram
    participant Gateway as Gateway
    participant Battle as BattleLogicHostGrain
    participant Scheduler as BattleInputFrameScheduler
    participant Buffer as BattleInputBuffer

    Gateway->>Battle: SubmitInputAsync(worldId, frame, input)
    Battle->>Battle: check initialized/world/input
    Battle->>Scheduler: Schedule(frame,currentFrame,inputDelay)
    alt accepted or remapped
        Scheduler-->>Battle: AcceptedFrame + Status
        Battle->>Buffer: Enqueue(acceptedFrame,input)
        Battle-->>Gateway: Accepted result
    else rejected
        Scheduler-->>Battle: rejected status
        Battle-->>Gateway: Rejected result
    end
```

拒绝/重映射原因包括：

- battle 未初始化。
- world mismatch。
- null input。
- invalid frame。
- too far future。
- late input remapped。
- too early input remapped。
- input buffer 拒绝。

### 7.5 Battle Tick 和快照推送

`OnTickAsync` 的源码逻辑：

1. `_tickDriver.Tick(_battleHostState, _inputBuffer)`。
2. tick driver drain 输入、提交给 runtime、tick runtime。
3. 根据 `BattleSnapshotSyncPolicy.ShouldPublish` 判断是否推送。
4. 如果 runtime 支持 observer-aware，按 observer 构造状态推送。
5. 否则构造普通 `StateSyncPush`。
6. 通过 `IStateSyncObserverGrain.OnSnapshotPushedAsync` 推送。

```mermaid
sequenceDiagram
    participant Timer as Orleans Timer
    participant Battle as BattleLogicHostGrain
    participant Driver as BattleTickDriver
    participant Runtime as IBattleRuntimeSession
    participant Publisher as BattleSnapshotPublisher
    participant Observer as IStateSyncObserverGrain

    Timer->>Battle: OnTickAsync
    Battle->>Driver: Tick(hostState,inputBuffer)
    Driver->>Runtime: SubmitInputs(frame, inputs)
    Driver->>Runtime: Tick(frame,tickRate,deltaTime)
    Driver-->>Battle: tickResult
    alt ShouldPublish
        Battle->>Publisher: Publish observers
        Publisher->>Runtime: CreateStateSyncPush
        Publisher->>Observer: OnSnapshotPushedAsync(push)
    end
```

### 7.6 `BattleFrameSyncGrain`

`BattleFrameSyncGrain` 适合纯帧同步广播路线。它维护：

- `_inputsByFrame`：frame -> inputs。
- `_frame`：当前服务器帧。
- `_observers`：订阅者。
- `_tickInterval` 与 catch-up 控制。

每个 timer tick 会：

1. 计算当前是否到下一帧。
2. 最多 catch up `MaxCatchUpFramesPerTimer`。
3. 取出当前帧输入，没有则空列表。
4. 构造 `FramePushedEvent`。
5. 调用所有 observer 的 `OnFramePushed`。
6. `_frame++`。

---

## 8. 端侧帧包适配

### 8.1 `FramePacketNetAdapter`

端侧收到 `FramePacket` 或聚合后的 `RemoteInputFrame`/`RemoteSnapshotFrame` 后，通过 `FramePacketNetAdapter` 进入本地输入源和快照 dispatcher。

```mermaid
sequenceDiagram
    participant Network as Network
    participant Aggregator as RemoteFrameAggregator
    participant Adapter as FramePacketNetAdapter
    participant Remote as RemoteDrivenSink
    participant Confirmed as ConfirmedSink
    participant Snapshot as FrameSnapshotDispatcher

    Network->>Aggregator: AddPacket(packet)
    Aggregator-->>Network: BuildInputFrame / BuildSnapshotFrame
    Network->>Adapter: ProcessAndFeed(worldId,inputFrame,snapshotFrame)
    Adapter->>Remote: Add(frame, inputs)
    Adapter->>Confirmed: Add(frame, inputs)
    Adapter->>Snapshot: Feed(envelope)
```

`ProcessInput` 内部会懒创建两个 `FrameJitterBuffer`：

| Buffer | delay | 用途 |
|--------|-------|------|
| `RemoteDriven` | `InputDelayFrames` | 网络驱动、插值、预测前平滑消费 |
| `Confirmed` | 0 | 服务器确认输入、对账、回滚修正 |

### 8.2 MOBA View 的封装

`BattleSessionNetAdapter` 在 MOBA View 包中包装通用 `FramePacketNetAdapter`。它的 `AdapterContext` 把 Demo 自己的 `IBattleSessionNetAdapterContext` 映射到通用 `IFramePacketNetAdapterContext`。

额外行为是更新 jitter buffer 调试统计：

- delay frames
- missing mode
- target frame
- max received frame
- last consumed frame
- buffered count
- duplicate/late/consumed/default-filled count

这说明通用 adapter 不负责 UI 诊断，Demo view 可以在外层读取 buffer 统计并展示。

---

## 9. 完整会话时序

```mermaid
sequenceDiagram
    participant App as Client App
    participant Flow as RoomGatewaySessionFlow
    participant Gateway as Gateway Client
    participant Room as RoomGrain
    participant Battle as BattleLogicHostGrain
    participant Coord as SessionCoordinator
    participant Sync as RemoteSyncAdapter
    participant Transport as IRemoteBattleSyncTransport
    participant NetAdapter as FramePacketNetAdapter
    participant View as View/Snapshot Dispatcher

    App->>Flow: CreateReadyStartAndSubscribe or JoinReadyStartAndSubscribe
    Flow->>Gateway: create/join/ready/start/subscribe
    Gateway->>Room: JoinMemberAsync / SetReadyAsync / StartBattleAsync
    Room->>Battle: InitializeBattleAsync
    Battle-->>Room: WorldStartAnchor
    Room-->>Gateway: BattleId + WorldId + Anchor
    Gateway-->>Flow: RoomGatewaySessionFlowResult

    App->>Coord: Initialize(config, host)
    Coord->>Coord: Create or reuse world
    Coord->>Sync: Attach coordinator
    App->>Sync: Connect(endpoint, roomId, playerId)
    Sync->>Transport: Connect

    loop input frames
        App->>Coord: SubmitLocalInput
        Coord->>Sync: SubmitInput
        Sync->>Transport: SubmitInput
        Transport->>Battle: SubmitInputAsync via Gateway
        Battle->>Battle: schedule and buffer input
        Battle->>Battle: Tick runtime
        Battle-->>Transport: StateSyncPush or snapshot
        Transport->>NetAdapter: ProcessAndFeed
        NetAdapter->>View: Feed snapshots
    end
```

这个时序把“入场”和“持续同步”分开：

- 入场阶段需要 room/battle/world/anchor 信息。
- 持续同步阶段只应该围绕 coordinator、sync adapter、transport、frame packet 和 snapshot dispatcher。

---

## 10. 恢复、晚加入与已有 world 接入

### 10.1 Reconnect 和 LateJoin

`RoomGrain.JoinMemberAsync` 在房间已经处于 InBattle 时：

- 如果成员已存在，返回 `RoomJoinKind.Reconnect`。
- 如果不是成员但房间未满，会加入成员、调用 gameplay join、执行 `JoinRunningBattleAsync`，成功后返回 `RoomJoinKind.LateJoin`。
- `JoinRunningBattleAsync` 会通过 gameplay 构造 late join player，再调用 `BattleLogicHostGrain.JoinPlayerAsync`。
- 如果 battle 拒绝 late join，会回滚 Room 成员和玩法状态。

```mermaid
flowchart TD
    A[JoinMemberAsync while InBattle] --> B{already member}
    B -- yes --> C[Touch online]
    C --> D[Reconnect response]
    B -- no --> E{room full}
    E -- yes --> X[throw room full]
    E -- no --> F[Add member and gameplay.Join]
    F --> G[JoinRunningBattleAsync]
    G --> H{Battle accepted}
    H -- yes --> I[LateJoin response]
    H -- no --> J[Remove member and gameplay.Leave]
    J --> K[throw rejected]
```

### 10.2 Restore

`RoomGrain.RestoreAsync` 先确认账号是成员，再 touch online，返回 `RestoreRoomResponse`：

- 如果没有 `_battleId`，join kind 是 `TeamLobby`。
- 如果已有 `_battleId`，join kind 是 `Reconnect`，`IsInBattle` 为 true。

`RoomGatewaySessionFlow.RestoreAsync` 会再读取房间快照并返回阶段化 `NextStep`。因此客户端既能恢复运行中战斗，也能从 Lobby、Loading、Starting 继续，不需要回退到整条聚合启动流程。

MOBA 的 `MultiplayerRoomFlowController.RestoreAsync` 只根据 `NextStep` 定位业务状态：Lobby 对应 `InLobby`，Loading 对应 `LoadingAssets`，Starting 对应 `WaitingForBattle`，InBattle 对应 `InBattle`。恢复本身不会重放 ready、begin-loading、assets-loaded 或 wait 请求；后续动作仍由各状态的显式入口触发。状态同步订阅继续由 `BattleSessionFeature` 持有，房间恢复层不会建立第二套订阅生命周期。

恢复失败使用结构化诊断而不是统一抛出 `InvalidOperationException`。`NoActiveRoom`、`NotMember`、`RoomClosed`、`RoomExpired`、`InvalidSession`、`Timeout` 和 `Failed/InternalError` 会保留到示例结果；`Timeout` 与内部错误标记为可重试，调用者取消仍保持取消语义并直接抛出。

### 10.3 已有 world 接入 Coordinator

有些示例或客户端运行时已经创建了 `IWorld`，不能让 `SessionCoordinator` 再创建第二套 world。源码提供 `ExistingWorldSessionCoordinatorHost`：

- 构造时传入已有 `IWorld`。
- 可传入 `serviceOverrides`，例如远端 transport。
- `CreateWorldHost` 返回包装后的 `ExistingWorldHost`。
- `CreateWorld` 总是返回已有 world。
- `DestroyWorld` 返回 false，不销毁外部 world。
- `DisposeAll` 不销毁外部 world。
- `ExistingWorldOverlayResolver` 会先查 service overrides，再查原 world services。

`ExistingWorldSessionCoordinatorHost` 只适合 Coordinator 确实拥有完整会话编排的场景。Shooter 当前已经由 `ShooterClientSession`、`ShooterClientNetworkLauncher` 和 `RoomGatewaySessionFlow` 持有连接、房间、快照订阅、预测与重连生命周期，因此没有再接入 Coordinator：

```mermaid
flowchart TD
    A[ShooterRemoteStateSyncPlayModeHost] --> B[ShooterClientSession]
    A --> C[ShooterClientNetworkLauncher]
    C --> D[RoomGatewaySessionFlow]
    B --> E[Local prediction and reconciliation]
    A --> F[RemoteClientInputSubmitQueue]
    F --> G[ShooterClientBattleHandle]
    G --> H[Gateway battle input]
```

该边界避免为了转发一次已接受输入而创建 `SessionCoordinator`、existing-world overlay、remote adapter 和额外 Tick。Shooter 仍复用框架 `RemoteClientInputSubmitQueue` 的背压、最新值替换和诊断能力；只有协议相关提交留在 `ShooterClientBattleHandle`。

---

## 11. 设计意图

### 11.1 Coordinator 管本地装配，Transport 管外部通信

`SessionCoordinator` 只认识 `ISyncAdapter` 和 world 服务。远端通信藏在 `IRemoteBattleSyncTransport` 后面，这让同一套会话生命周期可以跑在 Unity、Console、Shooter View、测试替身或 Orleans Gateway 上。

### 11.2 Gateway Flow 管入场脚本，不管每帧输入

`RoomGatewaySessionFlow` 适合 create/join/ready/start/subscribe/restore 这种阶段性操作。每帧输入的出口由当前会话所有者决定：Coordinator 拥有完整会话时走 `RemoteSyncAdapter.SubmitInput`；Shooter 已有完整客户端 session 时走框架提交队列到 `ShooterClientBattleHandle`。两种方式都只能有一个网络生命周期所有者。

### 11.3 Room 管成员和恢复，Battle 管权威模拟

Room 如果直接 Tick 战斗，会混入成员清理、目录通知、玩法房间状态等非战斗职责。Battle 如果直接管理成员映射和大厅恢复，会污染权威 Tick。拆成两个 Grain 能让生命周期更清晰。

### 11.4 FramePacket 让端侧消费协议中立

客户端最终需要的是某一帧的输入和快照，而不是某个 Gateway DTO。`FramePacketNetAdapter` 消费 `FramePacket`/`RemoteInputFrame`/`RemoteSnapshotFrame`，保持输入源、快照 dispatcher 和传输协议分离。

### 11.5 ExistingWorldHost 解决示例/业务已有 world 的接入问题

真实项目经常先有自己的 world 或 runtime bootstrap。`ExistingWorldSessionCoordinatorHost` 允许接入 Coordinator 而不重建 world，是框架可渐进迁移的关键扩展点。

---

## 12. 风险与检查点

| 风险 | 表现 | 检查点 |
|------|------|--------|
| 把 adapter 类型存在当作模式完备 | Hybrid 被宣传为已完成预测、校正和重演 | 对照 5.4 成熟度矩阵与源码 TODO，按子能力声明 |
| 重复创建会话协调层 | 只为输入转发又启动一套 Coordinator、transport 和 Tick | 先确认现有 session 是否已经拥有连接、预测、快照与重连生命周期 |
| transport 未注入 | Remote 模式启动但永远 disconnected | world services 是否能解析 `IRemoteBattleSyncTransport` |
| 已有 world 被重复创建 | 客户端出现两个逻辑世界或双 Tick | 完整接入 Coordinator 时使用 `ExistingWorldSessionCoordinatorHost`；已有完整 session 时不要再叠加 Coordinator |
| Room/Battle 边界混乱 | 晚加入、恢复、权威 Tick 互相影响 | Room 只管成员/生命周期，Battle 只管 runtime/Tick |
| 输入帧落点不一致 | 客户端请求帧与服务端接受帧不同步 | 检查 `BattleInputSubmitResult.AcceptedFrame` 和 `Status` |
| 快照订阅缺失 | 战斗开始后客户端没有状态推送 | Gateway flow 是否调用 `SubscribeStateSyncAsync` |
| RemoteDriven/Confirmed 混用 | 预测和确认状态互相污染 | 消费者明确读取对应输入源 |
| 聚合器内存增长 | 长连接后 `_inputsByFrame` 和 `_envelopesByFrame` 变大 | 定期 `TrimBefore` |

---

## 13. 源码阅读路径

1. `Unity/Packages/com.abilitykit.coordinator/Runtime/Core/SessionCoordinator.cs`：客户端会话生命周期。
2. `Unity/Packages/com.abilitykit.coordinator/Runtime/Adapters/SyncAdapterFactory.cs`：同步模式到 adapter 的实例化映射。
3. `Unity/Packages/com.abilitykit.coordinator/Runtime/Adapters/LocalSyncAdapter.cs`：本地输入和逻辑帧驱动。
4. `Unity/Packages/com.abilitykit.coordinator/Runtime/Adapters/RemoteSyncAdapter.cs`：远端输入和权威快照端口。
5. `Unity/Packages/com.abilitykit.coordinator/Runtime/Adapters/HybridSyncAdapter.cs`：混合模式已实现边界与预测 TODO。
6. `Unity/Packages/com.abilitykit.host.extension/Runtime/Session/RoomGatewaySessionFlow.cs`：create/join/ready/start/restore 编排。
7. `Server/Orleans/src/AbilityKit.Orleans.Grains/Rooms/RoomGrain.cs`：房间和恢复语义。
8. `Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/BattleLogicHostGrain.cs`：权威 Tick 和输入调度。
9. `Unity/Packages/com.abilitykit.host.extension/Runtime/Session/FramePacketNetAdapter.cs`：端侧输入和快照如何落到 world。
10. `Docs/design/07-NetworkSynchronization/00-SynchronizationCapabilityMap.md`：全局同步架构中的模块关系。

---

*文档版本：v2.1 | 最后更新：2026-07-14*
