# Changelog — com.abilitykit.core

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。
AbilityKit 整体仍处于开发期，0.x 版本不承诺向后兼容；重大变更会在对应版本条目里写明迁移要点。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。`core` 是 AbilityKit 的 #1 依赖枢纽（35+ 下游包传递依赖），
其数学原语/事件/池化/配置/日志/标记系统已被两个生产级 demo（MOBA、Shooter）端到端验证，
并随本版本首次具备**脱离 demo 的直接契约测试**（`src/AbilityKit.Core.Tests`）。

### API 边界（本包承诺稳定的部分）
- 数学原语：`Vec2` / `Vec3` / `Quat` / `Transform3` / `MathUtil`。
- 事件：`EventDispatcher` / `EventKey<TArgs>`（强类型事件总线）。
- 池化：`ObjectPool` / `PoolManager` / `PoolRegistry` / `Pools`（通用对象池）。
- 配置：分层 JSON 配置加载（`LayeredJsonSettingsStore` 等）。
- 日志：`Log` / `ILogSink`。
- 标记：`MarkerSystem` / `MarkerScanner`（反射式标记注册）。

### 构建门槛
- 在 `src/AbilityKit.Core` 上启用 `AbilityKitStable=true`：`TreatWarningsAsErrors` 已开启，
  **非可空/非文档类警告为零**。
- ⚠ **可空性(CS8xxx)暂为咨询级警告（~280 条），不计入硬门槛**：属于独立的“可空启用”专项，
  完成后可从 `Directory.Build.props` 的 `WarningsNotAsErrors` 列表移除相应代码升级为硬错误。

### 已知限制 / 不在 0.1.0 承诺范围
- `core` 不含碰撞几何原语（`Aabb`/`Obb`/`ColliderShape` 属于 `com.abilitykit.combat.collision.abstractions`）。
- 性能基线尚未纳入门禁；不承诺跨 major 版本二进制兼容。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）。
- 新增 `src/AbilityKit.Core.Tests`（首批脱离 demo 的直接单测）。
- 启用 `AbilityKitStable`：修复 `CS0419`（歧义 cref）、对副作用静态字段 `_registered` 显式抑制 `CS0414`。
- 建立 CHANGELOG 与发版基线。
