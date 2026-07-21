# IHostRuntimeModule 体系

源文件：`Runtime/FrameSync/*Module.cs` + `Runtime/Rollback/ServerRollbackModule.cs` + `Runtime/Time/ServerFrameTimeModule.cs` + `Runtime/WorldStart/WorldAutoStartModule.cs`

## 接口定义（在 com.abilitykit.host 包）

```csharp
public interface IHostRuntimeModule {
    void Install(HostRuntime runtime, HostRuntimeOptions options);
    void Uninstall(HostRuntime runtime, HostRuntimeOptions options);
}
```

## 5 个 Module

### FrameSyncDriverModule（服务端帧同步权威驱动）

`Runtime/FrameSync/FrameSyncDriverModule.cs:14`，实现 `IHostRuntimeModule, IFrameSyncInputHub, IFrameSyncDriverEvents`。

- **职责**：输入汇聚 + 逐帧广播 `FrameMessage`
- **OnPreTick**：把 pending inputs flush 给 `IWorldInputSink.Submit`
- **OnPostTick**：从 `IWorldStateSnapshotProvider` 收快照并 `_runtime.Broadcast(new FrameMessage(...))`
- **关键方法**：`RegisterSession/UnregisterSession(WorldId)`、`SubmitInput(ServerClientId, WorldId, PlayerInputCommand) -> bool`、`AddInputsFlushed/RemoveInputsFlushed`、`AddPostStep/RemovePostStep`
- **属性**：`Frame`（FrameIndex）
- **同文件内联**：`static WorldCatchUpDriver.CatchUpAndFeedSnapshots`、`static FrameSyncInputHubFactory.CreateJitterBufferHub<TFrame>`、`sealed FrameJitterBufferHub<TFrame>`（同时实现 `IConsumableRemoteFrameSource` + `IRemoteFrameSink`）

### ClientPredictionDriverModule

详见 [client_prediction.md](client_prediction.md)。

### ServerFrameTimeModule

`Runtime/Time/ServerFrameTimeModule.cs:12`。

- **构造**：`(float fixedDeltaSeconds, bool advanceOnHostTickFallback = true)` 或无参
- **职责**：注册 `IFrameTime`（`FrameTime` 实例）到每个 world 的 ServiceBuilder
- **依赖**：**弱依赖** `IFrameSyncDriverEvents`（缺失则降级到 `options.PostTick`）
- **方法**：`TryGet(WorldId, out IFrameTime)`

### WorldAutoStartModule

`Runtime/WorldStart/WorldAutoStartModule.cs:10`。

- **职责**：OnPostTick 遍历所有 world，从 `world.Services` 解析 `IWorldAutoStartHandler` 调 `TryAutoStart`，成功后加入 `_completed` 不再重试
- **IWorldAutoStartHandler**（`IService`，world-scoped）：`bool TryAutoStart(IWorld world, float deltaTime)`
- **典型实现**：moba 的 `MobaWorldAutoStartHandler`（委托 `IMobaGameStartOrchestrator`）

### ServerRollbackModule

`Runtime/Rollback/ServerRollbackModule.cs:11`。

- **构造**：`(int historyFrames, int captureEveryNFrames, Func<IWorld, RollbackRegistry> buildRegistry)`
- **依赖**：**强依赖** `IFrameSyncDriverEvents`（Install 时取不到会抛 `InvalidOperationException`，**必须先装 FrameSyncDriverModule**）
- **职责**：per-world 持有 `RollbackCoordinator + InputHistoryRingBuffer`，挂在 InputsFlushed（存输入历史）+ PostStep（按 N 帧捕获 snapshot）
- **关键方法**：`TryRollbackAndReplay(WorldId, FrameIndex rollbackFrame, FrameIndex replayToFrame, float deltaTimePerFrame) -> bool`

## 安装顺序约束

`MobaHostRuntimeBuilder.CreateModules` 实现以下顺序：

```
1. EnableFrameSync       → modules.Add(new FrameSyncDriverModule())
2. EnableServerFrameTime → modules.Add(new ServerFrameTimeModule(normalized.FixedDeltaSeconds))
3. EnableWorldAutoStart  → modules.Add(new WorldAutoStartModule())
4. EnableRollback        → modules.Add(new ServerRollbackModule(history, captureEvery, buildRollbackRegistry))
```

顺序保证 `ServerRollbackModule.Install` 时 `IFrameSyncDriverEvents` 已注册。

## 不是 IHostRuntimeModule 的类（澄清）

- `BattleHostLifecycleRunner`（普通编排器，应用层调 Start/Stop）
- `MobaGameStartOrchestrator` / `MobaServerGameLifecycle`（world-scoped `[WorldService]`，DI 容器管理）
- `MobaWorldAutoStartHandler`（实现 `IWorldAutoStartHandler`，由 `WorldAutoStartModule` 调用）
- `FixedStepTickRunner`（工具类）
