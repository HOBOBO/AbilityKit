# Client Prediction Driver（Host Extension / FrameSync）

本目录的核心是 `ClientPredictionDriverModule`：它把 **远端权威输入流**、**本地输入队列**、**预测窗口**、**回滚/重演**、**对账（hash reconcile）**、以及 **idealFrame 时间门控** 集成到 HostRuntime 的 tick 流程中。

本文档从“如何使用/如何调试”的角度说明该模块。

---

## 1. 你会用到的核心类型

- **`ClientPredictionDriverModule`**
  - 实现：
    - `IHostRuntimeModule`
    - `IClientPredictionDriverStats`
    - `IClientPredictionTuningControl`
    - `IClientPredictionReconcileTarget` / `IClientPredictionReconcileControl`
  - 在 `HostRuntimeOptions.PreTick` 中做输入消费与预测推进。
  - 在 `HostRuntimeOptions.PostTick` 中做 rollback snapshot capture 与 predicted hash 记录。

- **输入源/输入汇**
  - `IConsumableRemoteFrameSource<PlayerInputCommand[]>`：远端权威输入（可 peek / consume）。
  - `ILocalInputSource<LocalPlayerInputEvent[]>`：本地输入事件（每 tick 一批）。
  - `IWorldInputSink`：将某帧 inputs 提交给世界。

- **回滚与对账基础设施**（来自 `com.abilitykit.world.framesync/Runtime/FrameSync/Rollback`）
  - `RollbackCoordinator` / `RollbackSnapshotRingBuffer`
  - `InputHistoryRingBuffer`
  - `WorldStateHashRingBuffer`
  - `ClientPredictionReconciler`

---

## 2. 时序概览（每个 world，每个 tick）

模块内部大体分为三个阶段：

### Step 0：采样本地输入并推进 delay queue

- 从 `local.TryDequeue` 取一批 `LocalPlayerInputEvent[]`（可能为空）。
- 入队到 `LocalDelayQueue`，用于 `inputDelayFrames` 的固定延迟。

### Step 1：优先应用 authoritative input（Confirmed 前进）

- 若 `remote.TargetFrame` 已经覆盖 `confirmed+1`：
  - `TryConsume` 取出 authoritative inputs
  - `InputSink.Submit(frame, inputs)`
  - `ConfirmedFrame++`
  - 记录到 `AuthoritativeInputs` / `AppliedInputs`

> 这一步是“收敛优先”的关键：只要权威输入到了，就尽量先走权威路径。

### Step 2：预测推进 Predicted（受窗口限制）

- 当权威输入不够推进 confirmed 时，尝试预测推进 `predicted+1`。
- 预测使用 `LocalDelayQueue` 中对应延迟后的 inputs（或为空）。

---

## 3. 预测窗口（Prediction Window）

### 3.1 raw window

窗口的原始估计来自：

- `rawBacklog = remote.TargetFrame - confirmed`
- `BacklogEwma = EWMA(rawBacklog)`
- `window = round(BacklogEwma) + inputDelayFrames`
- clamp：
  - `MinPredictionWindow`
  - `MaxPredictionAheadFrames`

### 3.2 idealFrame gate（时间门控）对 window 的影响

当前实现为：

- `idealLimit` 来源于 time sync（由上层传入 `Func<WorldId,int> resolveIdealFrameLimit`）。
- `maxAheadByIdeal = idealLimit - confirmed`
- `effectiveWindow = min(rawWindow, max(0, maxAheadByIdeal))`

意义：

- 不再用“超过 idealLimit 就硬停”的方式，而是把它变成对预测窗口的**动态上限**。
- stall 统计会归因为 `IdealFrameStalls`（而不是 window stall），便于定位是时间门控导致的保守收敛。

---

## 4. 回滚/重演与对账（Reconcile）

### 4.1 两类触发来源

- **输入差异触发 rollback**（authoritative input 与 applied input 不一致）
  - 当 `confirmed+1` 到来且该帧之前已经 predicted 过，会对比 `AppliedInputs[frame]` 与 `authInputs`。
  - 不一致则 restore 到 `frame-1` 并进入 replay。

- **hash mismatch 触发 rollback**（由外部调用 `OnAuthoritativeStateHash` 进入）
  - 需要 `buildComputeHash` 提供 deterministic hash。
  - mismatch -> `RequestReconcileRollback(worldId, mismatchFrame)`。

### 4.2 replay 过程

- `ctx.Mode = Replaying`
- `ReplayTo = 原 PredictedFrame`
- 从 `rollbackFrame+1` 开始，尽量用 authoritative inputs 推进直到 `ReplayTo`
- 若缺权威输入，会等待（计数 `ReplayWaitTicks`），超时会自动 disable reconcile 并退出 replay（防卡死）。

---

## 5. 运行时调参与观测

### 5.1 调参接口

- `IClientPredictionTuningControl`
  - `SetMaxPredictionAheadFrames(int)`
  - `SetMinPredictionWindow(int)`
  - `SetBacklogEwmaAlpha(float)`
  - `ResetDefaults()`

### 5.2 统计接口

- `IClientPredictionDriverStats`
  - backlog/window：`TryGetPredictionWindowStats(worldId, ...)`
  - ideal gate：`TryGetIdealFrameStallStats(worldId, ...)`
  - replay/rollback/reconcile：多个计数与 last frame 字段

### 5.3 Editor 面板

- `FrameSync/Prediction`：
  - window/backlog、stalls、rollback/replay/mismatch、per-world ideal gate 等。
- `FrameSync/Time`：
  - time sync、anchor、idealFrame raw/margin/limit 等（由 BattleFlowDebugProvider 提供）。

---

## 6. 使用清单（Checklist）

- **输入链路正确**：
  - remote `TargetFrame` 单调递增
  - `TryConsume` 不会跳帧

- **hash deterministic**（如果启用 reconcile）：
  - 相同输入序列必须产生相同 hash。
  - 随机数/浮点不确定性/迭代顺序都要被规避或纳入回滚状态。

- **history 足够**（如果启用 rollback）：
  - `rollbackHistoryFrames` >= 最大可能回滚跨度
  - `rollbackCaptureEveryNFrames` 不要过大

---

## 7. 关联文档

- `Runtime/FrameSync/Design.md`
- `com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/README.md`
- `com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/Design.md`
