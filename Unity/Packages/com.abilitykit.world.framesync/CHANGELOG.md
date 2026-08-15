# Changelog — com.abilitykit.world.framesync

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。
AbilityKit 整体仍处于开发期，0.x 版本不承诺向后兼容；重大变更会在对应版本条目里写明迁移要点。

## [Unreleased]

### 变更
- **`FrameTime` 累计时刻定点化**：内部时间以 Q32.32（raw long 整数累加，无精度漂移）维护，
  `Frame` / `DeltaTime` / `Time` 公开语义不变（`Time` 为边界单次换算视图）；
  `FrameToTime` / `TimeToFrame` 改为定点运算。
- **`FrameTimeRollbackStatePayload` 升至 v2**：`Time` / `FixedDelta` 以 raw long 存储
  （`TimeRaw` / `FixedDeltaRaw`），版本不匹配严格拒绝——**与 v1 载荷不兼容**（回滚缓冲为会话内存态，无持久化迁移需求）。
- 新增依赖 `com.abilitykit.deterministic`（0.1.0）。
- 新增契约测试：定点累加精确性（N 步 raw 恒等于 N × 单步 raw）、回滚 payload raw 往返无损。
- 新增《定点帧同步接入指南》（`Document/定点帧同步接入指南.md`）。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。锁步(frame-sync)与回滚(rollback)工具是确定性模拟的基础，
已被两个生产级 demo 端到端验证，并随本版本首次具备**脱离 demo 的直接契约测试**
（`src/AbilityKit.World.FrameSync.Tests`，覆盖 `FrameTime` 的帧推进/重置/换算）。

### API 边界（本包承诺稳定的部分）
- 帧时间：`FrameTime`（Frame / DeltaTime / Time、StepTo / Reset / FrameToTime / TimeToFrame）。
- 帧索引：`FrameIndex`。
- 通用回滚原语：`RollbackCoordinator` / `IRollbackStateProvider` / `CommandRollbackLog` /
  `RollbackSnapshotRingBuffer` / `WorldStateHashRingBuffer` 等（供预测/回滚栈复用）。

### ⚠ 范围收窄（架构决策 D1）
- **客户端预测栈：本包自带的 `ClientPredictionRunner` / `ClientPredictionReconciler` 已标记 `[Obsolete]`**。
  规范客户端预测栈为 `com.abilitykit.host.extension/ClientPredictionDriverModule`（MOBA/Shooter 两个 demo 均用它）。
  本包这两个类**无 demo 消费者**，仅保留作过渡，后续版本将移除。
- 0.1.0 **不承诺**任何一套客户端预测栈为本包的规范能力；规范预测栈归属 `host.extension`。

### 构建门槛
- 在 `src/AbilityKit.World.FrameSync` 上启用 `AbilityKitStable=true`：`TreatWarningsAsErrors` 已开启，
  **本包自身代码非可空/非文档类警告为零**（依赖包仍按各自设置编译）。

### 已知限制 / 不在 0.1.0 承诺范围
- 可空性(CS8xxx)暂为咨询级警告，不计入硬门槛。
- 直接测试目前覆盖 FrameTime 核心；回滚协调器(RollbackCoordinator)全链路仍以 demo 集成覆盖为主（后续补强）。
- 性能基线尚未纳入门禁；不承诺跨 major 版本二进制兼容。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）；依赖 core/world.di 同步升至 `0.1.0`。
- 按决策 D1 将 `ClientPredictionRunner` / `ClientPredictionReconciler` 标记 `[Obsolete]`（规范栈改用 `host.extension`）。
- 新增 `src/AbilityKit.World.FrameSync.Tests`（首批脱离 demo 的直接单测）。
- 建立 CHANGELOG 与发版基线。
