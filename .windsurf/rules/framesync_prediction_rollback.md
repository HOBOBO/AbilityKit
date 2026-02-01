---
trigger: manual
---

# FrameSync Prediction / Rollback / Reconcile 规则

## 0. 目标

本文定义客户端帧同步体系中“预测/回滚/重演/对账（reconcile）/时间门控（idealFrame）”的工程级约束。

适用代码主要分布：

- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule.cs`
- `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/*`
-（时间门控/idealFrame 来源）`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Flow/Battle/BattleSessionFeature.cs`

---

## 1. 不允许吞异常（hard rule）

- 预测/回滚/输入消费属于高风险路径。
- **任何 catch 住的异常必须通过 `Log.Exception(...)` 或 `Log.Error(...)` 输出**，不得空 catch。
- 不要在 runtime 代码里直接用 `UnityEngine.Debug.*`。

---

## 2. Determinism 与 Reconcile 的前置条件

- `computeHash(frame)` 必须是 deterministic：
  - 相同输入序列 => 相同 hash。
  - 随机数、浮点不确定性、遍历顺序等必须被规避或纳入回滚状态。

- authoritative hash 与 predicted hash 的 frame 必须对齐。
  - 若 hash 不对齐，mismatch 统计会误导，rollback 会形成“风暴”。

---

## 3. Rollback provider 约束

- 可回滚状态由 `IRollbackStateProvider` 负责。
- **provider 必须做到 Export/Import 的双向一致**。
- `RollbackRegistry` 在 runtime 使用前必须 `Seal()`，禁止运行中动态注册 provider。

---

## 4. RingBuffer / Pooling 约束（performance + correctness）

- 所有历史结构（inputs/hash/snapshot）都应使用固定容量 ring buffer。
- `RollbackSnapshotRingBuffer` 覆盖槽位时需要释放旧 entries（数组池）。
- 高频路径避免：
  - LINQ、闭包、临时 `List/Dictionary` 分配
  - 每帧分配大 `byte[]`（provider payload 尽量可复用/可池化）

---

## 5. 多 World（multi-world）约束

- `ClientPredictionDriverModule` 的所有 state 必须按 `WorldId` 分离（字典/WorldContext）。
- 统计接口必须能按 world 读取（例如 `TryGetIdealFrameStallStats`）。
- 时间门控（idealFrame）可能按 world anchor 不同，需要在调用边界上显式传入 `WorldId`。

---

## 6. idealFrame gate 的策略约束

- idealFrame gate 不能作为“临时 hack”。应由正式 time sync + anchor 计算得到。
- 推荐策略：
  - **优先 window cap**：`effectiveWindow = min(rawWindow, max(0, idealLimit-confirmed))`
  - stall 统计需要能清晰区分：
    - 预测窗口 stall（window）
    - 时间门控 stall（idealFrame）

---

## 7. 可观测性（debuggability）

- 每个 world 至少应暴露：
  - `confirmed/predicted`
  - backlog（raw + EWMA）
  - prediction window（raw + effective）
  - idealFrame limit + 是否被 cap/是否 stalled
  - rollback 次数、restore failed、replay timeout、mismatch frame

- Editor 面板应能展示：
  - `FrameSync/Prediction`：window/backlog/stalls/rollback/mismatch（per-world）
  - `FrameSync/Time`：time sync/anchor/idealFrame raw-magin-limit（per-world）

---

## 8. 文档要求

对预测/回滚/对账相关改动，应同步更新以下文档（避免知识遗失）：

- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/README.md`
- `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/README.md`
- `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/Design.md`
