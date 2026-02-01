# Rollback & Reconcile 设计文档（Design.md）

本文档补充 `Runtime/FrameSync/Rollback` 目录内代码的设计约束、算法细节、边界条件与扩展点。

---

## 设计目标

- **确定性恢复**：同一帧快照 restore 后，世界应回到与当时一致的状态。
- **可扩展/解耦**：通过 `IRollbackStateProvider` 解耦具体系统（移动/技能/冷却/随机数等）。
- **可控的资源消耗**：固定容量 ring buffer；快照 payload 以 `byte[]` 为边界，允许调用侧自行选择零拷贝/压缩策略。
- **可观测**：驱动层（例如 `ClientPredictionDriverModule`）应记录 rollback 次数、restore failed、replay timeout、mismatch frame 等统计。

---

## 数据结构与内存行为

### 1) 快照结构

- `WorldRollbackSnapshot`
  - `Version`：编码版本，用于兼容检查。
  - `Frame`：帧号。
  - `Entries[]`：每个 provider 一条 entry（`Key + Payload`）。

- `RollbackSnapshotRingBuffer`
  - `Store(snapshot)`：frame 取模放入槽位。
  - 若槽位已有旧 snapshot，则释放旧 `Entries`（数组池）。
  - `Clear()`：释放所有占用槽位的 entries，清空 `_has`。

### 2) `IRollbackStateProvider` 的 payload 约束

- payload 是 provider 的**责任边界**：
  - provider 必须保证 `Export(frame)` 与 `Import(frame,payload)` 的双向一致性。
  - payload 内容建议包含必要的版本/校验字段（由 provider 自己决定）。

- provider 不应：
  - 在 `Export/Import` 中吞异常。
  - 依赖外部时钟/随机源而不做固定化（除非你把随机数种子/状态也纳入回滚状态）。

---

## Reconcile（对账）逻辑细节

当前 reconcile 的最小实现为：

- predicted side：`RecordPredictedHash(frame, hash)`
- authoritative side：`OnAuthoritativeHash(frame, authoritative)`

触发条件：

- predicted ring buffer 中存在该帧 predicted hash
- 且 predicted != authoritative

触发动作：

- 触发 `OnRollbackRequested(frame)`

注意：

- 如果 authoritative 先到，而 predicted hash 尚未记录，`OnAuthoritativeHash` 会返回 `false`。
- 因此驱动层需要：
  - 要么缓存 authoritative hash（`AuthoritativeHashes` ring buffer）
  - 要么在 predicted hash 记录时再次尝试 compare

`ClientPredictionDriverModule` 当前采用的是：

- authoritative hash 到来时：若 predicted hash 缺失 -> 计数并返回（稍后重试）
- predicted hash 记录时：如果 authoritative ring buffer 已有该帧 hash -> 立即 compare

---

## Rollback + Replay（回滚与重演）策略

### 1) 回滚帧选择

常见做法：

- mismatchFrame：出现 hash 不一致的帧
- rollbackFrame = mismatchFrame - 1

原因：

- mismatchFrame 的状态已经“错误”，回滚到上一帧更容易保证 restore 的一致性，再通过 replay 推进到最新。

### 2) replay 的输入来源

为了收敛，replay 应尽量使用 authoritative inputs。

- 若 authoritative inputs 对某帧缺失：
  - 可以暂缓 replay（等待权威到达），否则会出现“预测再次发散”的 tug-of-war。
  - `ClientPredictionDriverModule` 目前用 `ReplayWaitTimeoutTicks` 保护不被卡死，并可自动 disable reconcile。

---

## 多 World 支持

Rollback 目录内的基础设施本身是“单 world 实例化”的：

- 每个 `IWorld` 通常对应一套：
  - `RollbackCoordinator`
  - `RollbackSnapshotRingBuffer`
  - `InputHistoryRingBuffer`
  - `WorldStateHashRingBuffer`
  - `ClientPredictionReconciler`

多 world 场景下建议由更上层的 driver（如 `ClientPredictionDriverModule`）在 `WorldCreated/WorldDestroyed` 时维护各自上下文。

---

## 扩展点

- **更丰富的 reconcile 判据**：
  - hash 之外可加入关键 entity 的 deterministic checksum/序列化快照对比。

- **增量快照**：
  - 当前 provider payload 为全量，后续可以扩展为 delta + 基线帧（需要更复杂的 restore 机制）。

- **零拷贝与版本化**：
  - provider 可以将 payload 设计成 length-prefixed binary，并配合 pooling 减少 GC。
  - `WorldRollbackSnapshotCodec` 的版本升级需要考虑兼容策略。

---

## 边界条件与调参建议

- **historyFrames 太小**：
  - 现象：`TryRestore=false`，rollback 失败。
  - 处理：增大 `rollbackHistoryFrames`。

- **captureEveryNFrames 太大**：
  - 现象：回滚只能落在稀疏帧；replay 距离变长；失败概率上升。
  - 处理：降低 `rollbackCaptureEveryNFrames`。

- **频繁 mismatch**：
  - 先确认：hash 是否真正 deterministic。
  - 再确认：authoritative hash 与 predicted hash 的帧号对齐是否一致。
  - 若 mismatch 主要在网络差时出现：结合 prediction window/idealFrame gate 做更保守的预测上限。
