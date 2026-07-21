# SessionCoordinator 生命周期

源文件：`Runtime/Core/SessionCoordinator.cs` + `ISessionCoordinator.cs` + `SessionConfig.cs` + `SessionEnums.cs` + `SessionId.cs`

## SessionState 状态机

```
Idle ─Initialize()→ Initializing ─ok→ Idle（等待 Start）
                                      │
                              Start() │
                                      ▼
                                   Running ─Stop()─→ Stopping ─→ Stopped
                                      │                              │
                                  异常/Destroy()                    │
                                      ▼                              │
                                    Error ←──────────────────────────┘
                                                Destroy() 把状态重置回 Idle
```

枚举（`SessionEnums.cs`）：
```csharp
public enum SessionState { Idle, Initializing, Running, Paused, Stopping, Stopped, Error }
public enum SyncMode { Lockstep=0, SnapshotAuthority=1, StateSync=2, Hybrid=3 }
public enum HostMode { Local=0, Host=1, Client=2 }
```

注意：`SyncMode` 实际**有 4 个**值（README 只列 3 个）。

## SessionCoordinator 关键方法（源码核校）

```csharp
public sealed class SessionCoordinator : ISessionCoordinator
{
    public void Initialize(SessionConfig config, ISessionCoordinatorHost host);
    public void Start();
    public void Stop();
    public void Destroy();
    public void Tick(float deltaTime);
    public void SubmitLocalInput(PlayerInput input);

    public SessionId SessionId { get; }
    public SessionConfig Config { get; }
    public SessionState State { get; }
    public IWorldHost WorldHost { get; }
    public IWorld World { get; }
    public IWorldResolver WorldResolver { get; }
    public ISyncAdapter SyncAdapter { get; }
    public Timeline.IViewTimeline ViewTimeline { get; }
    public SessionHooks Hooks { get; }

    public void SetLogicWorldDriver(ILogicWorldDriverBridge driverHost);
    public ILogicWorldDriverBridge? LogicWorldDriver { get; }
    public void SetViewEventSink(IViewEventSink sink);
    public IViewEventSink? ViewSink { get; }

    public T Resolve<T>() where T : class;
    public bool TryResolve<T>(out T service) where T : class;
}
```

## Initialize 内部流程

`Idle → Initializing`，然后：

1. 若 host 实现 `ISessionCoordinatorConfigPolicy`，调 `ConfigureSession(ref _config)`
2. `ResolveRuntimePolicy()` 派生 `SessionRuntimePolicy`
3. `host.CreateWorldHost(config)` 拿到 `IWorldHost`
4. `host.ConfigureWorldCreateOptions(in config, options)` 修正 options
5. `_worldHost.CreateWorld(options)` → `_world.Initialize()`
6. `host.LoadConfig(world, config)` + `host.RegisterServices(world, config)`
7. 建 `ViewTimeline`
8. `SyncAdapterFactory.Create(_config.SyncMode)` + `adapter.Attach(this)`
9. 若已设 driver，挂接
10. `InvokeSessionStarting` → 回到 `Idle`

任何异常 → `Error` 状态 + `InvokeSessionFailed`。

## Start / Stop / Destroy / Tick 行为

- **Start**：`Idle → Running`；若 `runtimePolicy.UseCoordinatorSpawnService` 则调 `host.CreatePlayerSpawnData` + 内部 `CreatePlayerSpawns`（优先走 `ISpawnService`，找不到打 warning）；`syncAdapter.Attach(this, driverHost)`；触发 `OnSessionStarted` + `OnFirstFrameReceived`
- **Stop**：`Running → Stopping → Stopped`；`OnDetach` 全部 SubFeature 并清空；触发相应 hooks
- **Destroy**：`Stop()` → 释放 adapter / timeline / world（`_worldHost.DestroyWorld` + `_world.Dispose()`）→ `_hooks.Clear()` → 回到 `Idle`
- **Tick**：仅 `Running` 时执行。顺序：`InvokePreTick` → SubFeature `OnPreTick` → `_syncAdapter.Tick` → `CanDriveLogicWorld` 为真时 `_worldHost.Tick` → SubFeature `OnPostTick` → `InvokePostTick`
- **SubmitLocalInput**：转发给 `_syncAdapter.SubmitInput`

## CanDriveLogicWorld 闸门

从 `_worldResolver` 解析 `ILogicWorldDriveGate`：
- 有 gate → 用 gate 决定
- 无 gate → 按 `_runtimePolicy.RequireLogicWorldDriveGate` 决定（要求却缺失则不驱动）

moba demo 正是这样做（`MobaLogicWorldDriveGate` 实现）。

## SessionConfig（struct，**不是 sealed class**）

```csharp
public struct SessionConfig {
    public SessionId SessionId;
    public int MapId;
    public WorldId WorldId;
    public string WorldType;
    public PlayerId LocalPlayerId;
    public int ClientId;
    public SyncMode SyncMode;
    public HostMode HostMode;
    public int TickRate;
    public bool RequireLogicWorldDriveGate;
    public bool UseCoordinatorSpawnService;
    public bool EnableReplayRecording;
    public bool EnableReplayPlayback;
    public bool EnableClientPrediction;
    public int MaxPredictionAheadFrames;
    public NetworkEndpoint ServerEndpoint;
    public string RoomId;
    public SubFeatureConfigItem[] SubFeatures;

    public static SessionConfig Default;
    public static SessionConfig CreateLocal(...);
    public static SessionConfig CreateStateSyncClient(...);
    public static SessionConfig CreateHybrid(...);
    public static SessionConfig CreateHost(...);
    public SessionRuntimePolicy ResolveRuntimePolicy();
}
```

注意：工厂方法是 `CreateLocal/CreateStateSyncClient/CreateHybrid/CreateHost`，**不是** README 写的 `CreateServer/CreateClient/CreateForMode`。

## SessionRuntimePolicy 派生规则

```csharp
public readonly struct SessionRuntimePolicy {
    public SyncMode RequestedSyncMode;
    public SyncMode EffectiveSyncMode;       // HostMode.Local 强制 Lockstep
    public HostMode HostMode;
    public bool RequiresNetwork;
    public bool SupportsPrediction;          // 只有 Hybrid 才 true
    public bool EnableClientPrediction;
    public int MaxPredictionAheadFrames;
    public bool RequireLogicWorldDriveGate;
    public bool UseCoordinatorSpawnService;
}
```

规则：`HostMode.Local` 强制 `EffectiveSyncMode = Lockstep`；只有 `Hybrid` 才 `SupportsPrediction = true`。

## SessionId

```csharp
public readonly struct SessionId : IEquatable<SessionId> {
    public readonly long Value;
    public static SessionId New() => new SessionId(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
```
