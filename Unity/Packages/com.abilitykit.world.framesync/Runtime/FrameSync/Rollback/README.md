# Client Prediction / Rollback 模块（Runtime/FrameSync/Rollback）

本目录提供**客户端预测（Client Prediction）**相关的**回滚（Rollback）**与**对账/纠错（Reconcile）**基础设施。其目标是：

- 在网络输入延迟/抖动下依然能保持客户端流畅（先预测）。
- 当发现预测与权威状态不一致时，能够回滚到历史帧并重演（rollback + replay）。
- 通过结构化的接口，把“需要回滚的数据”从世界逻辑中解耦出来（provider registry）。

> 说明：真正的“驱动逻辑”（如何消费 authoritative input、如何推进 predicted frame、如何触发 replay/timeout、如何统计调试数据）当前主要在 `com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule.cs`。

## 文档

- 两种网络同步方案概览：`Document/NetworkSyncModels.md`

---

## 核心类型与职责

- **`RollbackRegistry`**
  - 维护一组 `IRollbackStateProvider`，并在构造 `RollbackCoordinator` 时 `Seal()` 固化。
  - 通过 `Key(int)` 唯一标识每个 provider，避免重复注册。

- **`IRollbackStateProvider`**
  - `int Key { get; }`
  - `byte[] Export(FrameIndex frame)`：导出该模块在指定帧的可回滚状态。
  - `void Import(FrameIndex frame, byte[] payload)`：恢复该模块在指定帧的状态。

- **`RollbackCoordinator`**
  - 将 registry 中各 provider 的导出结果聚合为一个 `WorldRollbackSnapshot`。
  - 通过 `RollbackSnapshotRingBuffer` 管理历史快照。
  - 关键方法：
    - `CaptureAndStore(frame)`：抓取并存入 ring buffer。
    - `TryRestore(frame)`：从 ring buffer 取出快照并对所有 provider 执行 `Import`。

- **`RollbackSnapshotRingBuffer`**
  - 一个按帧索引取模的 ring buffer（固定容量）。
  - 存储 `WorldRollbackSnapshot`（包含多个 `WorldRollbackSnapshotEntry`）。
  - 覆盖旧槽位时会释放旧 entries 的数组池资源（`RollbackEntriesArrayPool.Release`）。

- **`InputHistoryRingBuffer`**
  - 保存每一帧提交给世界的 inputs（用于 rollback 后重演）。
  - 用法上通常会区分：
    - `AppliedInputs`：实际对世界生效过的输入
    - `AuthoritativeInputs`：权威输入（来自远端），用于 replay 收敛

- **`WorldStateHashRingBuffer`**
  - 保存某帧的 `WorldStateHash`（通常来自 `computeHash(frame)`）。
  - 用于 reconcile 比对（predicted vs authoritative）。

- **`ClientPredictionReconciler`**
  - 记录 predicted hash：`RecordPredictedHash(frame, hash)`
  - 接收 authoritative hash：`OnAuthoritativeHash(frame, authoritative)`
  - 若不一致则触发 `OnRollbackRequested(frame)`（外部可接回滚逻辑）。

- **`ClientPredictionRunner`**
  - 一个相对“纯粹”的 runner：
    - `TickPredicted(nextFrame, fixedDelta, inputs, computeHash)`：提交 inputs、Tick 世界、Capture snapshot、Record predicted hash。
    - 内部订阅 reconciler 的 rollback 请求，执行 `TryRestore` + 从 rollbackFrame+1 重演到 `PredictedFrame`。
  - 当前工程的主驱动逻辑并不完全依赖该 runner，而更多在 `ClientPredictionDriverModule` 做集成（更贴近实际网络输入/窗口/超时策略）。

---

## 典型工作流（概念时序）

### 1) 正常预测（每帧）

- 将下一帧 inputs `Submit(frame, inputs)` 到 `IWorldInputSink`
- `world.Tick(fixedDelta)`
- `rollback.CaptureAndStore(frame)`
- `hash = computeHash(frame)`
- `reconciler.RecordPredictedHash(frame, hash)`

### 2) 收到权威 hash（异步）

- `reconciler.OnAuthoritativeHash(frame, authoritativeHash)`
- 若该帧存在 predicted hash 且不一致：触发 `OnRollbackRequested(frame)`

### 3) 执行回滚与重演

- `TryRestore(rollbackFrame)`（通常是 `mismatchFrame - 1`）
- 从 `rollbackFrame+1` 重演至当前 predictedFrame：
  - 每帧从 `InputHistoryRingBuffer` 取 inputs
  - `Submit` -> `Tick` -> `CaptureAndStore` -> `RecordPredictedHash`

---

## 集成指南（最小集成点）

你需要准备：

- **历史长度**：`rollbackHistoryFrames`
- **抓取频率**：`rollbackCaptureEveryNFrames`（越小越稳，越大越省 CPU/内存）
- **回滚状态 providers**：实现多个 `IRollbackStateProvider`，将所有需要“可回滚”的数据注册进 registry
- **可比对 hash**：提供 `Func<FrameIndex, WorldStateHash> computeHash`

然后在驱动侧（例如 `ClientPredictionDriverModule`）：

- 创建 `RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(historyFrames))`
- 创建 `InputHistoryRingBuffer(historyFrames)`
- 创建 `WorldStateHashRingBuffer(historyFrames)` + `ClientPredictionReconciler(predictedHashes)`
- 在 tick 流程中：
  - 每次 predicted tick 后 capture snapshot & record predicted hash
  - 当收到 authoritative hash 时喂给 reconciler

---

## 常见问题（FAQ）

- **为什么 `RollbackRegistry` 要 `Seal()`？**
  - 防止 runtime 中途动态增删 provider 导致快照结构不稳定（会破坏 restore 的一致性）。

- **回滚失败（TryRestore=false）意味着什么？**
  - ring buffer 没有该帧的快照（历史不足/抓取频率太低/容量太小）。
  - 通常需要提高 `rollbackHistoryFrames` 或提高 `rollbackCaptureEveryNFrames` 的捕获频率。

- **hash mismatch 不触发 rollback**
  - 可能原因：该帧 predicted hash 还没记录（`WorldStateHashRingBuffer` 没有该帧）。
  - 驱动层应在 predicted hash 记录后再次检查是否存在“先到的 authoritative hash”。

---

## 相关目录

- `com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule.cs`
  - 真实项目里预测/回滚/重演/窗口/统计的集成与驱动。

- `com.abilitykit.demo.moba.editor/Editor/BattleDebug/Panels/`
  - 调试面板（Prediction/Time 等）用于观察 stalls、rollback、idealFrame 等。
