# When to use

使用本 skill 的典型场景：

## 客户端预测

- 修改/调试 `ClientPredictionDriverModule`（预测推进、窗口、replay、rollback、hash reconcile、新 `IClientPredictionReconcileControl`）
- 修改/调试 `com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/` 下的 primitives（`RollbackCoordinator`、`RollbackSnapshotRingBuffer`、`RollbackRegistry`、`IRollbackStateProvider`、`InputHistoryRingBuffer`、`WorldStateHashRingBuffer`、`ClientPredictionReconciler`、新增的 `ClientPredictionRunner` / `CommandRollbackStateProvider` 等）
- 修改/调试 time sync / idealFrame，并需要确认其对预测窗口的影响与 stall 归因
- 把统计做成 per-world，或在 Editor 面板中展示关键指标

## ConfirmedAuthority 路径（generic primitives）

- 修改/调试 `com.abilitykit.host.extension/Runtime/Client/FrameSync/`（`ClientPredictionInputHistory<TInput>` / `ClientPredictionReconciliationCoordinator<TInput>` / `WorldStartFrameCatchUpCalculator` / `RemoteTimeAnchorProjector`）
- 这套与 `world.framesync` 主集合是**并行实现**，被 `ConfirmedAuthorityWorldRuntimeFactory` 使用

## 服务端

- 修改/调试 `ServerRollbackModule`（`com.abilitykit.host.extension/Runtime/Rollback/ServerRollbackModule.cs`）— per-world 持有 `RollbackCoordinator + InputHistoryRingBuffer`，`TryRollbackAndReplay(worldId, rollbackFrame, replayToFrame, dt)`
- 修改/调试 `FrameSyncDriverModule`（`com.abilitykit.host.extension/Runtime/FrameSync/FrameSyncDriverModule.cs`）— 服务端输入汇聚 + 逐帧广播 `FrameMessage`，内联 `WorldCatchUpDriver` + `FrameSyncInputHubFactory` + `FrameJitterBufferHub<TFrame>`
- 修改/调试 `BattleHost` 子系统（`com.abilitykit.host.extension/Runtime/Server/BattleHost/`：`BattleHostLifecycleRunner` / `BattleHostLifecycleContext` / `BattleHostState`）

## CatchUp 子系统

- 修改/调试 `com.abilitykit.host.extension/Runtime/FrameSync/CatchUp/Shared/`（`FrameSyncCatchUpMessages` / `FrameSyncCatchUpPolicy` / `FrameSyncCatchUpTypes` / `IFrameSyncInputHistory`）
- 修改/调试 `com.abilitykit.host.extension/Runtime/Client/FrameSync/CatchUp/IFrameSyncCatchUpSink.cs`

## Editor 面板

- 6 个面板在 `com.abilitykit.demo.moba.editor/Editor/BattleDebug/Panels/`：
  - `BattleDebugFrameSyncPanel.cs`（总览）
  - `BattleDebugFrameSyncPredictionPanel.cs`（Order 51）
  - `BattleDebugFrameSyncRollbackPanel.cs`（Order 52，**新**）
  - `BattleDebugFrameSyncReconcilePanel.cs`（Order 53，**新**，含"恢复/关闭/开启对账"按钮调 `IClientPredictionReconcileControl`）
  - `BattleDebugFrameSyncTimePanel.cs`（Order 54）
  - `BattleDebugFrameSyncNetworkPanel.cs`（**新**，jitter buffer）
