# Changelog — com.abilitykit.world.di

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。
AbilityKit 整体仍处于开发期，0.x 版本不承诺向后兼容；重大变更会在对应版本条目里写明迁移要点。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。World DI（服务容器 / 世界作用域 / 模块装配）是 AbilityKit 的运行时关键基础设施，
被 host / coordinator / ability / triggering / combat 等多个下游包使用，并已通过两个生产级 demo
端到端验证 + 直接契约测试（`src/AbilityKit.World.DI.Tests`，31 用例）。

### API 边界（本包承诺稳定的部分）
- 服务容器：注册 / 解析 / 作用域（World 生命周期）。
- 模块装配：模块安装、自动注册、DI 注入。
- 世界抽象：世界创建选项、世界 ID。

### 构建门槛
- 在 `src/AbilityKit.World.DI` 上启用 `AbilityKitStable=true`：`TreatWarningsAsErrors` 已开启，
  **本包自身代码非可空/非文档类警告为零**（依赖包仍按各自设置编译）。

### 已知限制 / 不在 0.1.0 承诺范围
- 可空性(CS8xxx)暂为咨询级警告，不计入硬门槛（见根 `Directory.Build.props` 的 `WarningsNotAsErrors`）。
- 性能基线尚未纳入门禁；不承诺跨 major 版本二进制兼容。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）；依赖 `com.abilitykit.core` 同步升至 `0.1.0`。
- 启用 `AbilityKitStable`：本包以“局部属性”方式开启（不向 ProjectReference 传递），故无需先把整条依赖链清到零警告。
- 建立 CHANGELOG 与发版基线。
