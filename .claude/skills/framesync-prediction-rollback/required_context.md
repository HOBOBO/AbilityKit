# Required context (ask before changing code)

在开始改动前应明确：

## SyncMode 与路径

- **SyncMode**：Lockstep / SnapshotAuthority / HybridPredictReconcile（不同模式下 input/hash 的来源不同）
- **使用哪套 primitives？**
  - 标准 ClientPredictionDriverModule 路径 → `com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/`（15 个 .cs）
  - ConfirmedAuthority 路径 → `com.abilitykit.host.extension/Runtime/Client/FrameSync/`（generic）
  - 服务端 → `com.abilitykit.host.extension/Runtime/Rollback/ServerRollbackModule.cs`

## authoritative input 来源

- `IConsumableRemoteFrameSource<PlayerInputCommand[]>`（**位置**：`com.abilitykit.network.runtime/Runtime/Network/Abstractions/IConsumableRemoteFrameSource.cs`）
  - 来自基类 `IRemoteFrameSource`：`TargetFrame`、`TryGet(int frame, out TFrame frameData)`
  - 自身：`TryConsume(int frame, out TFrame frameData)`
- 注意：这些接口在 **network.runtime** 包，**不是** framesync 包内（旧 skill 暗示在 framesync 包内）

## local input 来源

- `ILocalInputSource<LocalPlayerInputEvent[]>`（**位置**：`com.abilitykit.network.runtime/Runtime/Network/Abstractions/ILocalInputSource.cs`）
- `TryDequeue(out TInput input)` 每 tick 出一批（允许为空）；另有 `int LocalFrame`

## ClientPredictionDriverModule 配置（构造函数参数，全部当前有效）

- `enableRollback`（默认 false）
- `rollbackHistoryFrames`（默认 240）
- `rollbackCaptureEveryNFrames`（默认 1）
- `buildComputeHash`（`Func<IWorld, Func<FrameIndex, WorldStateHash>>`）
- `maxPredictionAheadFrames`（默认 30）
- `minPredictionWindow`（默认 1）
- `backlogEwmaAlpha`（默认 0.20f）
- `inputDelayFrames`（默认 0）
- `resolveIdealFrameLimit` / `resolveRemoteInputs` / `resolveLocalInputs` / `buildRollbackRegistry`

## reconcile 是否启用

- `buildComputeHash` 是否提供 deterministic hash？
- 运行时可用 `IClientPredictionReconcileControl.SetReconcileEnabled(worldId, bool)` 动态开关
- 重置：`ResetReconcile(worldId)` 清 hash、退出回放模式

## idealFrame 来源

- 是否由正式 time sync + anchor 计算得到（并且是 per-world）？
- 若存在，确认 `effectiveWindow` 是否被压缩，stall 是否记到 `TotalIdealFrameStalls`（long）

如果这些信息不明确，优先通过日志/Stats/Editor 面板补齐观察点，再做改动。
