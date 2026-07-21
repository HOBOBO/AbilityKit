# ClientPredictionDriverModule

源文件：`Runtime/FrameSync/ClientPredictionDriverModule.cs`

> 完整排查工作流（窗口计算、stall 归因、rollback 风暴、reconcile mismatch）见 [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)。本页只讲 module 装配与配置接口。

## 类声明（5 接口）

```csharp
public sealed class ClientPredictionDriverModule :
    IHostRuntimeModule,
    IClientPredictionDriverStats,
    IClientPredictionTuningControl,
    IClientPredictionReconcileTarget,
    IClientPredictionReconcileControl   // ← 新接口
```

## 构造函数

```csharp
public ClientPredictionDriverModule(
    Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> resolveRemoteInputs,
    Func<WorldId, ILocalInputSource<LocalPlayerInputEvent[]>> resolveLocalInputs,
    Func<WorldId, int> resolveIdealFrameLimit = null,
    int inputDelayFrames = 0,
    int maxPredictionAheadFrames = 30,
    int minPredictionWindow = 1,
    float backlogEwmaAlpha = 0.20f,
    bool enableRollback = false,
    int rollbackHistoryFrames = 240,
    int rollbackCaptureEveryNFrames = 1,
    Func<IWorld, RollbackRegistry> buildRollbackRegistry = null,
    Func<IWorld, Func<FrameIndex, WorldStateHash>> buildComputeHash = null)
```

## 4 个配置/统计接口

### IClientPredictionDriverStats（只读统计，per-world）

- `TryGetFrames(WorldId, out FrameIndex confirmed, out FrameIndex predicted)`
- `TryGetPredictionWindowStats(WorldId, out int backlogRaw, out float backlogEwma, out int window, out bool stalled)` — 5 参数重载多 `out long stallsTotal`
- `TryGetIdealFrameStallStats(WorldId, out int idealFrameLimit, out bool stalled, out long stallsTotal)`
- `TryGetLocalDelayQueueDepth(WorldId, out int depth)`

全局属性：`MaxPredictionAheadFrames` / `MinPredictionWindow` / `BacklogEwmaAlpha`、`TotalRollbackCount` / `TotalRollbackRestoreFailed`、`IsPredictionStalledByWindow` / `IsPredictionStalledByIdealFrame`、`CurrentIdealFrameLimit`、`TotalAuthoritativeHashIgnoredNoReconciler` / `TotalAuthoritativeHashSkippedNoPredictedHash`

### IClientPredictionTuningControl（运行时调参）

显式接口实现（cast 才能调）：

- `SetMaxPredictionAheadFrames(int)` / `SetMinPredictionWindow(int)` / `SetBacklogEwmaAlpha(float)` / `ResetDefaults()`

### IClientPredictionReconcileTarget（权威 hash 入口）

```csharp
void OnAuthoritativeStateHash(WorldId worldId, FrameIndex frame, WorldStateHash hash);
```

内部分支：无 reconciler → `TotalAuthoritativeHashIgnoredNoReconciler`；缺 predicted hash → `TotalAuthoritativeHashSkippedNoPredictedHash`；否则 → `ctx.Reconciler.OnAuthoritativeHash`。

### IClientPredictionReconcileControl（**新接口**）

```csharp
void ResetReconcile(WorldId worldId);                   // 清 hash、退出回放模式
void SetReconcileEnabled(WorldId worldId, bool enabled);
bool TryGetReconcileEnabled(WorldId worldId, out bool enabled);
```

Reconcile 调试面板（`BattleDebugFrameSyncReconcilePanel`）有按钮调这些。

## 内部流程

- `Install`：注册 4 个 feature 接口；挂 PreTick/PostTick；监听 WorldCreated/WorldDestroyed
- `OnPreTick`：算 idealFrame → 算 backlog/EWMA/window → 入队本地输入 → 优先消费权威输入 → 输入差异 rollback → replay → 预测步
- `OnPostTick`：快照 capture（按 `captureEveryNFrames`）+ `ctx.Reconciler.RecordPredictedHash` + 补 compare
- `RequestReconcileRollback(WorldId, FrameIndex mismatchFrame)`：hash mismatch 触发的回滚（含风暴保护 `ShouldRequestReconcileRollback`）

## per-world WorldContext（私有嵌套类，line 61-105）

关键字段：`ConfirmedFrame` / `PredictedFrame` / `LocalDelayQueue` / `Rollback`(RollbackCoordinator) / `AppliedInputs`(InputHistoryRingBuffer) / `AuthoritativeInputs`(InputHistoryRingBuffer) / `ComputeHash` / `PredictedHashes` / `AuthoritativeHashes`(WorldStateHashRingBuffer) / `Reconciler`(ClientPredictionReconciler) / `ReconcileEnabled` / `Mode`(ReplayMode.Normal/Replaying) / `ReplayTo` / `LastRollbackFrame` / `IdealFrameLimit` / `PredictionWindow` / `IdealFrameCappedWindow` / `IdealFrameStalled`

## 核心常量

`ReplayWaitTimeoutTicks = 120`（replay 超时保护）
