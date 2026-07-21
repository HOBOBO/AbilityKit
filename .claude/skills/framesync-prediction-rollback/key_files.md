# Key files

## Driver / Integration（路径仍然有效）

- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule.cs` — **核心**
  - per-world `WorldContext`（私有嵌套类 line 61-105）
  - confirmed/predicted 推进（`OnPreTick`）
  - prediction window（EWMA backlog）
  - idealFrame window cap + stall 归因
  - rollback/replay timeout + reconcile stats（`OnPostTick`）
  - 实现 5 接口：`IHostRuntimeModule / IClientPredictionDriverStats / IClientPredictionTuningControl / IClientPredictionReconcileTarget / IClientPredictionReconcileControl`
- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/IClientPredictionDriverStats.cs`
- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/IClientPredictionTuningControl.cs`
- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/IClientPredictionReconcileTarget.cs` — **同文件还定义新接口 `IClientPredictionReconcileControl`**（旧 skill 未覆盖）

## Rollback primitives（两套并行 — 重要）

### 主集合：`com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/`（15 个 .cs，被 `ClientPredictionDriverModule` 使用）

**旧 skill 已覆盖的 7 个**（路径全部有效）：

- `RollbackCoordinator.cs` — `Capture / TryCaptureAndStore / TryRestore / Restore / ClearHistory` + `OperationCompleted` 事件
- `RollbackSnapshotRingBuffer.cs` — `Store / TryGet / Clear`
- `RollbackRegistry.cs` — `Seal / Register / TryGet / Clear`
- `IRollbackStateProvider.cs` — `Key / Export(FrameIndex) / Import(FrameIndex, byte[])`
- `InputHistoryRingBuffer.cs` — `Store / TryGet / Clear`
- `WorldStateHashRingBuffer.cs` — `Store / TryGet / Clear`
- `ClientPredictionReconciler.cs` — `RecordPredictedHash / OnAuthoritativeHash / Clear` + `OnRollbackRequested`（**`RecordPredictedHash` 在这里**，不是 Driver 上）

**旧 skill 未覆盖的 8 个新增**：

- `ClientPredictionRunner.cs` — 单世界独立预测/回放器（`TickPredicted / OnAuthoritativeStateHash / Reset`，含内部 `HandleRollbackRequested` 重放循环）
- `IWorldStateHashProvider.cs`
- `IRollbackCommand.cs` / `CommandRollbackLog.cs` / `CommandRollbackStateProvider.cs` — 命令式回滚
- `RollbackEntriesArrayPool.cs` — 池化
- `WorldRollbackSnapshot.cs` — 快照结构体 + Codec 版本
- `RollbackOperationResult.cs` — `Kind / Status / Frame / ...`
- `FrameTimeRollbackStateProvider.cs`

### Generic 集合：`com.abilitykit.host.extension/Runtime/Client/FrameSync/`（4 个 .cs，被 ConfirmedAuthority 路径使用）

**旧 skill 完全未覆盖**：

- `ClientPredictionInputHistory.cs` — `ClientPredictionInputHistory<TInput>`（`Record / TrimBefore / SubmitFrame / ReplayTo`）
- `ClientPredictionReconciliationCoordinator.cs` — `ClientPredictionReconciliationCoordinator<TInput>`（`ReconcileAfterAuthoritativeSnapshot`）+ `ClientPredictionReconciliationResult`
- `WorldStartFrameCatchUpCalculator.cs` — 静态计算器（`Calculate / CalculateFromSnapshotFrame`）
- `RemoteTimeAnchorProjector.cs` — 静态投影器（`Project`，投影 `SyncTimeAnchor`）

**不要与主集合混用**：两套是不同集成层级的客户端预测实现。

## 服务端模块

- `Unity/Packages/com.abilitykit.host.extension/Runtime/Rollback/ServerRollbackModule.cs` — `IHostRuntimeModule`，per-world 持有 `RollbackCoordinator + InputHistoryRingBuffer`，依赖 `IFrameSyncDriverEvents`（需先装 `FrameSyncDriverModule`）；暴露 `TryRollbackAndReplay(worldId, rollbackFrame, replayToFrame, dt)`
- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/FrameSyncDriverModule.cs` — `IHostRuntimeModule, IFrameSyncInputHub, IFrameSyncDriverEvents`，输入汇聚 + 逐帧广播 `FrameMessage`；同文件内联 `WorldCatchUpDriver`（静态）、`FrameSyncInputHubFactory`、`FrameJitterBufferHub<TFrame>`（同时实现 `IConsumableRemoteFrameSource` 和 `IRemoteFrameSink`）
- `Unity/Packages/com.abilitykit.host.extension/Runtime/Server/BattleHost/` — `BattleHostLifecycleRunner.cs` / `BattleHostLifecycleContext.cs` / `BattleHostState.cs`

## CatchUp 子系统（旧 skill 未覆盖）

- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/CatchUp/Shared/FrameSyncCatchUpMessages.cs`
- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/CatchUp/Shared/FrameSyncCatchUpPolicy.cs`
- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/CatchUp/Shared/FrameSyncCatchUpTypes.cs` — `FrameSyncCatchUpRequest` / `FrameSyncCatchUpPayload`
- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/CatchUp/Shared/IFrameSyncInputHistory.cs`
- `Unity/Packages/com.abilitykit.host.extension/Runtime/Client/FrameSync/CatchUp/IFrameSyncCatchUpSink.cs`

## 输入/网络接口（位置不在 framesync 包内）

- `Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Abstractions/IRemoteFrameSource.cs` — 基类（`TargetFrame` / `TryGet`）
- `Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Abstractions/IConsumableRemoteFrameSource.cs` — `TryConsume`
- `Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Abstractions/ILocalInputSource.cs` — `TryDequeue` / `LocalFrame`

## Time sync / Battle（**路径已变** — 旧 skill 路径失效）

- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/Core/BattleSessionFeature.cs` — **不再是单文件**，是 40+ partial class 分布在 `Core/` `Gateway/` `Net/` `Sim/` `Snapshot/` `Editor/` 子目录；关键 partial：
  - `Core/BattleSessionFeature.Lifecycle.cs` — `OnAttach / OnDetach / Tick`
  - `Sim/BattleSessionFeature.SimTick.RemoteDriven.cs` — `TickRemoteDrivenLocalSim(float dt)`
  - `Sim/BattleSessionFeature.SimTick.Confirmed.cs` — `TickConfirmedAuthorityWorldSim(float dt)`
  - `Gateway/BattleSessionFeature.GatewayTimeSync.cs` / `GatewayFrameTiming.cs` / `GatewayTimeSyncStats.cs`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Debug/BattleFlowDebugProvider.cs` — **路径变了**（旧在 `Runtime/Game/Flow/Battle/`，现移到 `Runtime/Game/Battle/Debug/`）；仍是 static class，字段扩展：`Current` / `CurrentHud` / `CurrentView` / `CurrentConfirmedView` / `JitterBufferStats` / `TimeSyncStats` / `TimeSyncStatsByWorld` / `ConfirmedAuthorityWorldStats`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Shared/Context/BattleContext.Debug.cs` — 暴露预测句柄：`PredictionStats`(IClientPredictionDriverStats) / `PredictionReconcileTarget` / `PredictionReconcileControl` / `PredictionTuningControl`

## Editor 调试面板（6 个，**旧 skill 只有 2 个**）

全部在 `Unity/Packages/com.abilitykit.demo.moba.editor/Editor/BattleDebug/Panels/`：

- `BattleDebugFrameSyncPanel.cs` — **新**，总览聚合
- `BattleDebugFrameSyncPredictionPanel.cs`（Order 51）— 旧 skill 已列
- `BattleDebugFrameSyncRollbackPanel.cs`（Order 52）— **新**，`IsReplaying / ReplayToFrame / LastRollbackFrame / TotalRollbackCount / TotalRollbackRestoreFailed`
- `BattleDebugFrameSyncReconcilePanel.cs`（Order 53）— **新**，含"恢复/关闭对账/开启对账"按钮，调 `IClientPredictionReconcileControl`
- `BattleDebugFrameSyncTimePanel.cs`（Order 54）— 旧 skill 已列
- `BattleDebugFrameSyncNetworkPanel.cs` — **新**，jitter buffer

## 设计文档

- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/Design.md` — 可作重写参考
