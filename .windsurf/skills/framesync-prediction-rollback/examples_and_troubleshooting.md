---
name: framesync-prediction-rollback
section: examples_and_troubleshooting
---

# Examples & troubleshooting

## 1) predicted 不前进

检查顺序：

- `CurrentPredictionWindow == 0`？
  - 如果为 0：
    - 看是否 `idealFrame` 把 window 压到了 0（ideal gate）。
    - 看 `MinPredictionWindow/MaxPredictionAheadFrames` 是否配置成 0。

- `ahead >= window`？
  - 如果是：
    - 看 stall 归因：Window 还是 IdealFrame。

## 2) rollback 从不触发

- 输入差异 rollback：
  - `AppliedInputs` 是否在 predicted tick 里记录？
  - authoritative 到来时是否对比到了同一帧？

- hash reconcile：
  - `buildComputeHash` 是否为 null？
  - `RecordPredictedHash` 是否在 PostTick 被调用？
  - authoritative hash 是否走到了 `IClientPredictionReconcileTarget.OnAuthoritativeStateHash`？

## 3) rollback 触发风暴/反复 replay

- 帧号对齐是否正确（authoritative hash/input 对应的 frame）
- hash 是否 deterministic
- replay 是否在缺 authoritative inputs 时持续使用 predicted inputs 导致再次发散

## 4) restore failed

- `rollbackHistoryFrames` 太小
- `rollbackCaptureEveryNFrames` 太大导致缺快照

## 5) 多 world 统计混在一起

- 统计接口必须按 `WorldId` 读取。
- DebugProvider/Editor 面板也需要按 worldId 选择显示。
