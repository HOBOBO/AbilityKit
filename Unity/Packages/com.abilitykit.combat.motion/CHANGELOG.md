# Changelog — com.abilitykit.combat.motion

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。

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
