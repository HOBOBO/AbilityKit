# 接入 coordinator 的两种模式

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

## 模式 B：用 ExistingWorldSessionCoordinatorHost 组合（shooter demo）

文件：`Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Hosting/`

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

## CoordinatorInputSubmitBridge（shooter 异步路径的关键）

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
