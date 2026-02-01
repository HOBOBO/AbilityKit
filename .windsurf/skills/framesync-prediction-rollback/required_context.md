---
name: framesync-prediction-rollback
section: required_context
---

# Required context (ask before changing code)

在开始改动前应明确：

- **SyncMode**：Lockstep / SnapshotAuthority / HybridPredictReconcile（不同模式下 input/hash 的来源不同）。
- **authoritative input 来源**：`IConsumableRemoteFrameSource<PlayerInputCommand[]>` 的语义（TargetFrame、TryGet、TryConsume 是否跳帧）。
- **local input 来源**：`ILocalInputSource<LocalPlayerInputEvent[]>` 是否每 tick 只出一批。
- **rollback 是否启用**：`enableRollback`、`rollbackHistoryFrames`、`rollbackCaptureEveryNFrames`。
- **reconcile 是否启用**：`buildComputeHash` 是否提供 deterministic hash。
- **idealFrame 来源**：是否由正式 time sync + anchor 计算得到（并且是 per-world）。

如果这些信息不明确，优先通过日志/Stats/Editor 面板补齐观察点，再做改动。
