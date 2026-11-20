# Changelog — com.abilitykit.combat.motion

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。

## [Unreleased]

- 新增 `MotionPipelinePolicyState` 及 `MotionPipelinePolicy.CaptureState / ValidateState / RestoreState`，按完整纯值规则排序捕获、验证并原子替换策略；运行配置 API 和组数组 getter 语义保持不变。
- .NET 验证：Motion 核心项目 72/72；本批不涉及 MOBA Provider、帧载荷或 Solver/Events/导航恢复接线。
- 新增 `PathFollowerState` 及 `PathFollowerMotionSource.CaptureState / ValidateState / RestoreState`，完整保存路径、冻结参数、仲裁参数和推进游标；捕获与恢复复制数组，恢复验证失败不改变来源。
- PathFollower 推进迁入包内无状态算法，保留池化、逐航点步进、旧局部快照规则与正常通知时序；普通 Tick 不新增路径或状态对象分配。
- 完整状态 API 不管理管线成员、顺序、来源身份或导航依赖，不自动提供完整世界回滚；详细用法及限制见 [使用文档](Document/Usage.md#71-pathfollower-完整领域状态)。
- .NET 验证：共享 Motion 59/59、受影响 MOBA 22/22；本批未运行 Unity/Player 验证。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。MotionPipeline / ConfigurableMotionSolver / LocomotionMotionSource 等
运动调度与碰撞求解管线是 MOBA demo 角色移动的核心，已被端到端验证（含防卡墙/滑行/旋转碰撞），
并随本版本首次具备脱离 demo 的直接契约测试（`src/AbilityKit.Combat.Motion.Tests`，2 用例）。

### API 边界（本包承诺稳定的部分）
- 运动管线：`MotionPipeline`（多源优先+覆盖+碰撞求解）。
- 碰撞求解器：`ConfigurableMotionSolver`（sweep + 墙滑 + leash）。
- 运动源：`LocomotionMotionSource` / `FixedDeltaMotionSource` / `PathFollowerMotionSource`。
- 运动约束：`MotionCollisionConstraints` / `MotionLeashConstraints`。

### 构建门槛
- 在 `src/AbilityKit.Combat.Motion` 上启用 `AbilityKitStable=true`：零错误。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）。
- 新增 `src/AbilityKit.Combat.Motion.Tests`。
- 建立 CHANGELOG 与发版基线。
