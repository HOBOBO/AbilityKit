# Examples & troubleshooting

## 1) predicted 不前进

检查顺序：

- `CurrentPredictionWindow == 0`？
  - 如果为 0：
    - 看是否 `idealFrame` 把 window 压到了 0（`IsPredictionStalledByIdealFrame`）。
    - 看 `MinPredictionWindow / MaxPredictionAheadFrames` 是否配置成 0。
- `ahead >= window`？
  - 如果是：看 stall 归因 — `IsPredictionStalledByWindow` vs `IsPredictionStalledByIdealFrame`。

## 2) rollback 从不触发

- **输入差异 rollback**：
  - `AppliedInputs`（WorldContext 字段，类型 `InputHistoryRingBuffer`）是否在 predicted tick 里记录？
  - authoritative 到来时是否对比到了同一帧？
- **hash reconcile**：
  - `buildComputeHash` 是否为 null？
  - `RecordPredictedHash`（在 **`ClientPredictionReconciler`** 类，Driver 在 PostTick 调）是否被调用？
  - authoritative hash 是否走到了 `IClientPredictionReconcileTarget.OnAuthoritativeStateHash`？
  - reconcile 是否被关闭？用 `IClientPredictionReconcileControl.TryGetReconcileEnabled(worldId, out bool)` 检查。

## 3) rollback 触发风暴/反复 replay

- 帧号对齐是否正确（authoritative hash/input 对应的 frame）。
- hash 是否 deterministic。
- replay 是否在缺 authoritative inputs 时持续使用 predicted inputs 导致再次发散。
- 看 `TotalRollbackCount` 与 `LastRollbackFrame`（Rollback 面板）。
- Driver 内置风暴保护：`ShouldRequestReconcileRollback`。

## 4) restore failed

- `rollbackHistoryFrames`（默认 240）太小。
- `rollbackCaptureEveryNFrames`（默认 1）太大导致缺快照。
- 看 `TotalRollbackRestoreFailed`（Rollback 面板）。

## 5) 多 world 统计混在一起

- 统计接口必须按 `WorldId` 读取（`TryGetFrames` / `TryGetPredictionWindowStats` / `TryGetIdealFrameStallStats` 都是 per-world）。
- DebugProvider/Editor 面板也按 worldId 选择显示：`BattleFlowDebugProvider.TimeSyncStatsByWorld` 是 per-world 字典。

## 6) 误用了 generic primitives 集合（旧 skill 未覆盖的陷阱）

- 如果项目用 `ClientPredictionDriverModule` 标准路径 → 必须用 `com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/` 下的 `RollbackCoordinator` / `ClientPredictionReconciler` 等。
- 如果项目用 `ConfirmedAuthorityWorldRuntimeFactory` → 必须用 `com.abilitykit.host.extension/Runtime/Client/FrameSync/` 下的 `ClientPredictionInputHistory<TInput>` / `ClientPredictionReconciliationCoordinator<TInput>`。
- 两套 API 不互通，跨用会导致状态丢失/帧错位。

## 7) 服务端回滚

- `ServerRollbackModule` 依赖 `IFrameSyncDriverEvents`，必须先装 `FrameSyncDriverModule`。
- 调用入口：`TryRollbackAndReplay(worldId, rollbackFrame, replayToFrame, dt)`。

## 8) 字段名对照（旧 skill 写错 → 实际）

| 旧 skill | 实际 |
|---------|------|
| `IdealFrameStalls` | `TotalIdealFrameStalls`（long） |
| 隐含 stall 是单字段 | 实际有 `IsPredictionStalledByIdealFrame` + `IsPredictionStalledByWindow` 两个 bool + `TotalIdealFrameStalls` 计数 |
| `RecordPredictedHash` 在 Driver | 在 `ClientPredictionReconciler`（Driver 通过 `ctx.Reconciler.RecordPredictedHash` 调） |
| 2 个 Editor 面板 | 实际 6 个（含 Rollback / Reconcile / Network / 总览） |
| Rollback primitives 7 个 | 主集合实际 15 个，另有 generic 集合 4 个 |
