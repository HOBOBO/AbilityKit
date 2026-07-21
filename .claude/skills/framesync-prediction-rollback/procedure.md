# Procedure (how to work on a prediction/rollback/reconcile task)

1. **确认帧推进来源**
   - `remote.TargetFrame` 是否推进？是否能拿到 `confirmed+1` 的 authoritative input？
   - `local.TryDequeue` 是否每 tick 有输入批次（允许为空）。
   - 接口位置：`com.abilitykit.network.runtime/Runtime/Network/Abstractions/`

2. **确认 confirmed/predicted 基线**
   - per-world `confirmed` 是否单调递增（`TryGetFrames(worldId, out confirmed, out predicted)`）。
   - `predicted >= confirmed` 是否被保持。

3. **确认窗口计算与归因**
   - 用 `TryGetPredictionWindowStats(worldId, out backlogRaw, out backlogEwma, out window, out stalled)` 一次性取窗口与 stall 状态。
   - 或用 5 参数重载取 `out long stallsTotal`。
   - backlog raw 与 EWMA 是否符合预期（`backlogEwmaAlpha` 默认 0.20）。
   - `window` 是否被 `MaxPredictionAheadFrames / MinPredictionWindow` clamp。
   - 若存在 idealFrame：用 `TryGetIdealFrameStallStats(worldId, out idealFrameLimit, out stalled, out stallsTotal)`，stall 字段是 **`TotalIdealFrameStalls`**（long，**不是旧 skill 写的 `IdealFrameStalls`**）。`stalled` 对应 `IsPredictionStalledByIdealFrame`，与 `IsPredictionStalledByWindow` 区分。

4. **确认回滚快照是否足够**（若启用 rollback，`enableRollback=true`）
   - `rollbackHistoryFrames`（默认 240）是否覆盖最坏回滚跨度。
   - `rollbackCaptureEveryNFrames`（默认 1）是否过大导致 restore 失败。
   - 失败计数：`TotalRollbackRestoreFailed`（在 Rollback 面板）。

5. **确认 reconcile 的 compare 是否发生**（若启用 hash，`buildComputeHash != null`）
   - predicted hash 是否记录：Driver 在 `OnPostTick` 中通过 `ctx.Reconciler.RecordPredictedHash(...)` 调用（`ClientPredictionReconciler.RecordPredictedHash(FrameIndex, WorldStateHash)`，**不是 Driver 自己的方法**）。
   - authoritative hash 是否到达并喂给 `IClientPredictionReconcileTarget.OnAuthoritativeStateHash(WorldId, FrameIndex, WorldStateHash)`。
   - 缺 predicted hash 时：计入 `TotalAuthoritativeHashSkippedNoPredictedHash`。
   - 无 reconciler 时：计入 `TotalAuthoritativeHashIgnoredNoReconciler`。
   - 若 authoritative 先到，是否会在 predicted hash 记录后补一次 compare。
   - **强制重置**：可调 `IClientPredictionReconcileControl.ResetReconcile(worldId)` 清账。

6. **复现与日志**
   - 优先用 Stats/Editor 面板定位（per-world）：
     - `BattleDebugFrameSyncPredictionPanel`（Order 51）
     - `BattleDebugFrameSyncRollbackPanel`（Order 52，新）
     - `BattleDebugFrameSyncReconcilePanel`（Order 53，新）
     - `BattleDebugFrameSyncTimePanel`（Order 54）
     - `BattleDebugFrameSyncNetworkPanel`（新，jitter buffer）
     - `BattleDebugFrameSyncPanel`（总览）
   - 面板通过 `BattleFlowDebugProvider.Current.*` 与 `BattleContext.Debug.cs` 暴露的 `PredictionStats / PredictionReconcileTarget / PredictionReconcileControl / PredictionTuningControl` 读取。
   - 必要时补 `Log.Info/Warning`（不要空 catch）。
