# Changelog — com.abilitykit.combat.collision.abstractions

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。碰撞几何原语（Aabb / Obb / Sphere / Capsule / ColliderShape）是
collision / motion / navigation / projectile 的基础，已被 MOBA demo 端到端验证并随本版本
首次具备脱离 demo 的直接契约测试（`src/AbilityKit.Combat.Collision.Abstractions.Tests`，7 用例）。

### API 边界（本包承诺稳定的部分）
- 碰撞形状：`Aabb` / `Obb` / `Sphere` / `Capsule` / `ColliderShape`（读写 + 交叠 + 包含）。
- 层系统：`CollisionLayers` / `ColliderShapeType`。
- 射线检测原语：`CollisionQueries` / `OrientedBoxSweepQueries` / `SphereSweepQueries`。

### 构建门槛
- 在 `src/AbilityKit.Combat.Collision.Abstractions` 上启用 `AbilityKitStable=true`：零错误。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）。
- 新增 `src/AbilityKit.Combat.Collision.Abstractions.Tests`。
- 建立 CHANGELOG 与发版基线。
