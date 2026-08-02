# Changelog — com.abilitykit.host

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。
AbilityKit 整体仍处于开发期，0.x 版本不承诺向后兼容；重大变更会在对应版本条目里写明迁移要点。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。Host 提供 WorldHost 装配的服务端世界模拟抽象（WorldHostBuilder / HostRuntime /
IWorldHost / IHostRuntimeModule），是 host.extension / coordinator / 两个 demo 的服务端装配基础，
已被两个生产级 demo 端到端验证，并随本版本首次具备**脱离 demo 的直接契约测试**
（`src/AbilityKit.Host.Tests`，覆盖 `WorldHostBuilder` 的成功/失败/边界）。

### API 边界（本包承诺稳定的部分）
- WorldHost 装配：`WorldHostBuilder`（fluent: Create / Set* / AddModule / Build / BuildWithOptions）。
- 运行时核心：`HostRuntime` / `IWorldHost` / `HostRuntimeOptions`。
- 模块/驱动/连接器接口：`IHostRuntimeModule` / `ITimeDriver` / `IInputDriver` / `IConnectionManager` / `IWorldFactory`。

### 构建门槛
- 在 `src/AbilityKit.Host` 上启用 `AbilityKitStable=true`：`TreatWarningsAsErrors` 已开启，
  **本包自身代码非可空/非文档类警告为零**（依赖包仍按各自设置编译）。

### 已知限制 / 不在 0.1.0 承诺范围
- 可空性(CS8xxx)暂为咨询级警告，不计入硬门槛。
- 直接测试目前覆盖 WorldHostBuilder 契约；HostRuntime 全链路（含真实 IWorldFactory/驱动装配）仍以 demo 集成覆盖为主（后续补强）。
- 依赖 `world.framesync` 尚未升 0.1.0（涉及 D1 决策，随后推进）；其余依赖已 0.1.0。
- 性能基线尚未纳入门禁；不承诺跨 major 版本二进制兼容。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）；依赖 core/world.di/network.runtime 同步升至 `0.1.0`。
- 新增 `src/AbilityKit.Host.Tests`（首批脱离 demo 的直接单测）。
- 建立 CHANGELOG 与发版基线。
