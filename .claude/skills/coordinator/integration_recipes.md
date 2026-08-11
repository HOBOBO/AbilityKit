# 接入 coordinator 的两种模式

> ⚠️ **源码核校 2026-08-06（重大修正）**：本文档原描述的"两个 demo 经 coordinator 接入多人联网"路径**与当前源码不符**。
>
> - **Shooter 的 coordinator 接入路径已删除**。`ShooterCoordinatorSessionHost` / shooter 用的 `ExistingWorldSessionCoordinatorHost` / `ShooterGatewayCoordinatorInputTransport` / `ShooterCoordinatorInputBridge` 在源码中**均不存在**——`src/AbilityKit.Demo.Shooter.Runtime.Tests/Client/ShooterRemoteCoordinatorInputContractTests.cs` 显式断言其缺席，设计文档 `Docs/design/07-NetworkSynchronization/05-SessionCoordination.md:748` 记录了有意移除；shooter `view.runtime` asmdef 不引用 `AbilityKit.Coordinator`。
> - **MOBA 战斗路径不驱动 `SessionCoordinator`**。`MobaSessionCoordinatorHost` 仍实现 `ISessionCoordinatorHost` / `ISessionCoordinatorConfigPolicy`（见下文模式 A 的 host 实现，这部分真实存在），但 moba 战斗走 `com.abilitykit.host.extension` 的 `FrameSyncDriverModule` + `ClientPredictionDriverModule`，**不实例化 `SessionCoordinator`、不使用任何 `ISyncAdapter`**。
> - 因此**两个 demo 的多人联网实际都不经过 coordinator**。真正的多人接入路径是 `com.abilitykit.network.sdk`（`NetworkSdkBuilder` → `NetworkSdkClient`）+ `com.abilitykit.network.room`（`sdk.CreateRoomClient()` → `RoomGatewaySessionFlow`）。权威接入清单见各包 README 与 `Docs/design/07-NetworkSynchronization/`。
> - 下文模式 A 的 host 实现仍可作为"实现 coordinator host 端口"的参考；模式 B 已标注为已删除的历史路径。`CoordinatorInputSubmitBridge` 类存在于 coordinator 包，但**当前无任何 demo 使用**。

## 模式 A：直接实现 ISessionCoordinatorHost（moba demo）

文件：`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Session/`

### MobaSessionCoordinatorHost（应用层实现）

```csharp
public sealed class MobaSessionCoordinatorHost
    : ISessionCoordinatorHost, ISessionCoordinatorConfigPolicy, ILogicWorldSessionHost
{
    public void ConfigureSession(ref SessionConfig config) {
        config.RequireLogicWorldDriveGate = true;
        config.UseCoordinatorSpawnService = false;   // moba 自己管出生
    }

    public IWorldHost CreateWorldHost(SessionConfig config) {
        var typeRegistry = new WorldTypeRegistry();
        MobaWorldBlueprintsRegistration.RegisterAll(...);   // 注册 battle/lobby
        var worldManager = new WorldManager(new RegistryWorldFactory(typeRegistry));
        return new HostRuntime(worldManager, new HostRuntimeOptions());
    }

    public void ConfigureWorldCreateOptions(in SessionConfig config, WorldCreateOptions options) {
        options.WorldId = config.WorldId;
        options.WorldType = config.WorldType;
        options.ServiceBuilder = ...;   // 注册 ITextAssetLoader/IMobaConfigTableRegistry/ICollisionService
        RegisterCreateWorldInitData(...);   // 构造 MobaCreateWorldSpec → WorldInitData
    }

    public PlayerSpawnData[] CreatePlayerSpawnData(SessionConfig config) => Array.Empty<PlayerSpawnData>();
}
```

### MobaBattleDriverHost（实现 ILogicWorldDriverBridge）

```csharp
public sealed class MobaBattleDriverHost : ILogicWorldDriverBridge {
    public void BindLogicWorld(IWorld world, HostRuntime hostRuntime) {
        _runtimePort = world.Services.Resolve<IMobaBattleRuntimePort>();
        _gate = world.Services.Resolve<ILogicWorldDriveGate>();
    }

    public void SubmitInputs(PlayerInput[] inputs) {
        var commands = MobaPlayerInputCommandConverter.Convert(inputs);
        _runtimePort.Submit(_currentFrame, commands);
    }

    public void AdvanceFrame(float deltaTime) {
        if (!CanDriveLogicWorld(deltaTime)) return;
        _currentFrame++;
        _hostRuntime.Tick(deltaTime);
        TryGetTransformSnapshot(...);
    }

    public SnapshotEntityState[] GetAllEntityStates() {
        // 经 MobaCoordinatorStateAdapter 把 LogicWorldEntityState[] 转成 EntityState 再 ToSnapshotEntityState()
    }
}
```

### MobaLogicWorldDriveGate（world-scoped service）

```csharp
[WorldService(typeof(ILogicWorldDriveGate), WorldLifetime.Scoped)]
public sealed class MobaLogicWorldDriveGate : ILogicWorldDriveGate {
    public bool CanDriveLogicWorld(float deltaTime) {
        return IsFinite(deltaTime)
            && _phase != null && _phase.InGame
            && _runtime != null && _runtime.Status.IsReadyForBattleLoop
            && !LastValidationBlocks();
    }
}
```

### 装配调用链

```
外部 → new MobaSessionCoordinatorHost(loader)
外部 → new SessionCoordinator()
外部 → coordinator.Initialize(config, host)
       └─ host.CreateWorldHost → HostRuntime
       └─ host.ConfigureWorldCreateOptions → 注入 ServiceBuilder / WorldInitData
       └─ worldHost.CreateWorld → IWorld + Initialize
       └─ host.LoadConfig / RegisterServices
       └─ SyncAdapterFactory.Create → LocalSyncAdapter(Lockstep) + Attach
外部 → coordinator.SetLogicWorldDriver(mobaBattleDriverHost)
外部 → coordinator.Start()
       └─ UseCoordinatorSpawnService=false → 跳过 coordinator 出生的创建
       └─ syncAdapter.Attach(coordinator, driverHost)
外部每帧 → coordinator.Tick(dt)
       └─ LocalSyncAdapter.Tick → 累计时间到 frameInterval 时 ProcessLogicFrame
              └─ driverHost.SubmitInputs / Start / AdvanceFrame
              └─ driverHost.AdvanceFrame → _hostRuntime.Tick（推进 moba 战斗）
```

## 模式 B（⚠️ 历史路径，源码中已删除）

> 以下 `ShooterCoordinatorSessionHost` / `ShooterGatewayCoordinatorInputTransport` / `ShooterCoordinatorInputBridge.Create` 在源码中**均已不存在**（见本文档顶部修正说明）。shooter 真实的网络接入路径见本节末尾"shooter 真实路径"。保留下文仅作历史参考，**不要照此实现**。
>
> 原描述文件路径 `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Hosting/` 已不存在。

### ShooterCoordinatorSessionHost（包装 ExistingWorldSessionCoordinatorHost）

```csharp
public sealed class ShooterCoordinatorSessionHost
    : ISessionCoordinatorHost, ISessionCoordinatorConfigPolicy
{
    private readonly ExistingWorldSessionCoordinatorHost _host;

    public ShooterCoordinatorSessionHost(IWorld existingWorld) {
        var transport = new ShooterGatewayCoordinatorInputTransport(...);
        _host = new ExistingWorldSessionCoordinatorHost(
            existingWorld,
            serviceOverrides: new object[] { transport });   // 覆盖 IRemoteoteSyncTransport 解析
    }

    public void ConfigureSession(ref SessionConfig config) {
        config.SyncMode = SyncMode.StateSync;
        config.HostMode = HostMode.Client;
        config.RequireLogicWorldDriveGate = true;
    }

    // 所有 host 方法直接委托给 _host
    public IWorldHost CreateWorldHost(SessionConfig c) => _host.CreateWorldHost(c);
    // ...
}
```

### ShooterGatewayCoordinatorInputTransport（实现 IRemoteBattleSyncTransport）

```csharp
public sealed class ShooterGatewayCoordinatorInputTransport : IRemoteBattleSyncTransport {
    private readonly CoordinatorInputSubmitBridge<ShooterClientInputSubmitResult,
                                                   ShooterClientGatewayInputSubmitResult> _submitBridge;

    public void SubmitInput(PlayerInput input) {
        if (!_submitBridge.TrySubmit(input))
            Log.Warning(...);
    }

    public void Connect(NetworkEndpoint endpoint, string roomId, int playerId) {
        _connected = true;   // 不真连 socket，输入走 gateway 异步路径
    }
}
```

### ShooterCoordinatorInputBridge.Create 装配

```csharp
public static class ShooterCoordinatorInputBridge {
    public static (SessionCoordinator, IRemoteBattleSyncTransport) Create(...) {
        var transport = new ShooterGatewayCoordinatorInputTransport(...);
        var host = new ShooterCoordinatorSessionHost(existingWorld, transport);
        var coordinator = new SessionCoordinator();
        coordinator.Initialize(config, host);
        coordinator.Start();
        if (coordinator.SyncAdapter is IRemoteSyncAdapter remote)
            remote.Connect(endpoint, roomId, playerId);
        else
            transport.Connect(endpoint, roomId, playerId);
        return (coordinator, transport);
    }
}
```

## shooter 真实路径（不经 coordinator）

源码核校 2026-08-06。文件均在 `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/`：

```
ShooterClientConnectionFactory          选 transport（Tcp/FromTransportFactory/FromGameFrameworkNetwork/...）→ IConnection
  ↓
ShooterClientNetworkLauncher            new NetworkSdkBuilder().UseConnectionFactory(...).Build() → NetworkSdkClient
  ↓
ShooterRoomGatewayConnection            实现 IRoomGatewayRequestTransport + IRoomGatewayPushSource，send 经 sdkClient.SendRawRequestAsync
  ↓
ShooterRoomGatewayFlow                  包装通用 RoomGatewaySessionFlow（8 阶段），内含 ShooterRoomGatewaySessionClient 适配 shooter DTO → 通用能力接口
  ↓
ShooterClientSession                    tick 本地预测 + 应用下推快照

【P2.2 战斗数据面 —— 两连接拓扑（2026-08-09，房间控制面之外的独立 battle 连接）】
ShooterClientNetworkLauncher            房间连接之外另建 battle NetworkTransport（独立 TcpTransport，dispatcher 驱动 + Tick 泵 heartbeat）
  ↓
ShooterBattleTransportGatewayClient     输入上行经 NetworkTransport.SendInputAsync（per-submit 结果：AcceptedFrame/ServerTicks/ShouldResync，喂 lag-compensation）
ShooterBattleDataPlane                  下推经 RawServerPushReceived（类型化解码前的原始 opCode,payload）→ 喂既有 ShooterClientSession.ApplyGatewayPush；push 在主线程 Drain（不与 session.Tick 竞争）；承载 reliable-event ack + 10s 全量重同步旁路
ShooterRoomGatewayConnection            收缩为房间控制面 + battle-state facade（由 NotifyBattlePushDispatched 灌注，SnapshotPushDispatched/CurrentSession 等消费者零改动）
```

- shooter 不引用 `AbilityKit.Coordinator`（view.runtime asmdef），不实现 `IRemoteBattleSyncTransport`，不创建 `SessionCoordinator`。
- `ShooterBattleDriverHost : ILogicWorldDriverBridge`（runtime 包）存在，但**独立使用**（验收/AI 路径），未接到任何 coordinator。
- 三种客户端同步策略由 `ShooterClientSyncControllerFactory` 按 `NetworkSyncModel` 选择：`PredictRollback` / `AuthoritativeInterpolation` / `HybridHeroPrediction`（注意这是 `com.abilitykit.network.runtime` 的枚举，**不是** coordinator 的 `SyncMode`）。

moba 真实路径结构相同（`MultiplayerGatewayEntryModule` → `NetworkSdkBuilder` → `GatewayRoomClient`），战斗数据面改走 `host.extension` 的 framesync 模块。

## CoordinatorInputSubmitBridge（⚠️ 存在于 coordinator 包，当前无 demo 使用）

`Runtime/Transport/CoordinatorInputSubmitBridge.cs` 是泛型异步输入桥：

```csharp
public sealed class CoordinatorInputSubmitBridge<TLocalSubmitResult, TRemoteSubmitResult> {
    public CoordinatorInputSubmitBridge(
        Func<TLocalSubmitResult, TimeSpan, Task<TRemoteSubmitResult>> submitAsync,
        ...);

    public bool TrySubmit(PlayerInput input);
    public Task<TRemoteSubmitResult> SubmitViaCoordinatorAsync(
        SessionCoordinator coordinator,
        TLocalSubmitResult local,
        ...);
}
```

流程：
1. 应用层调 `SubmitViaCoordinatorAsync(coordinator, local, ...)` → 内部 `_createInput(local)` 生成 `PlayerInput` → `coordinator.SubmitLocalInput(input)`
2. coordinator → `syncAdapter.SubmitInput` → `transport.SubmitInput` → `_submitBridge.TrySubmit(input)` 匹配 pending 的 local → `_submitAsync(local, ...)` 返回远程结果 Task

## 接入清单（我要接入 coordinator，需要实现什么）

### 必实现（2 个）

- `ISessionCoordinatorHost`（或复用 `ExistingWorldSessionCoordinatorHost`）
- `ILogicWorldDriverBridge`

### 可选（按需）

- `IViewEventSink` — 接收快照/伤害/生命周期事件
- `ISpawnService` — 由 coordinator 创建出生
- `ILogicWorldDriveGate` — 玩法层闸门（moba 强烈建议）
- `IRemoteBattleSyncTransport` — 远端同步（Lockstep 不需要）

### 典型装配序列

```
1. new 你的 SessionCoordinatorHost（或 new ExistingWorldSessionCoordinatorHost(existingWorld, serviceOverrides)）
2. new SessionCoordinator()
3. coordinator.Initialize(config, host)   // 内部建 world、adapter、timeline
4. coordinator.SetLogicWorldDriver(yourDriverHost)
5. coordinator.SetViewEventSink(yourViewSink)   // 可选
6. coordinator.Start()
7. 每帧 coordinator.Tick(dt)
8. （可选）coordinator.SubmitLocalInput(playerInput)
9. coordinator.Stop() / coordinator.Destroy()
```
