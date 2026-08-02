# Changelog — com.abilitykit.world.snapshot

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。
AbilityKit 整体仍处于开发期，0.x 版本不承诺向后兼容；重大变更会在对应版本条目里写明迁移要点。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。快照路由/解码/管线是状态同步(state-sync)与帧同步快照侧的基础设施，
已被两个生产级 demo 端到端验证，并随本版本首次具备**脱离 demo 的直接契约测试**
（`src/AbilityKit.World.Snapshot.Tests`，覆盖 `SnapshotRegistryCatalog` 的成功/失败/边界）。

### API 边界（本包承诺稳定的部分）
- 快照路由：`SnapshotRegistryCatalog`（按 id 注册/查找）、`IIdentifiedSnapshotRegistry`。
- 快照分发：`FrameSnapshotDispatcher` / `SnapshotPipeline`。
- 路由构建：`SnapshotRoutingBuilder`。

### 构建门槛
- 在 `src/AbilityKit.World.Snapshot` 上启用 `AbilityKitStable=true`：`TreatWarningsAsErrors` 已开启，
  **本包自身代码非可空/非文档类警告为零**（依赖包仍按各自设置编译）。

### 已知限制 / 不在 0.1.0 承诺范围
- 可空性(CS8xxx)暂为咨询级警告，不计入硬门槛。
- 直接测试目前覆盖路由注册层；解码器/管线执行路径仍以 demo 集成覆盖为主（后续补强）。
- 依赖 `world.networkfragments` 尚未升 0.1.0（待同步推进）。
- 性能基线尚未纳入门禁；不承诺跨 major 版本二进制兼容。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）；依赖 `com.abilitykit.core` 同步升至 `0.1.0`。
- 新增 `src/AbilityKit.World.Snapshot.Tests`（首批脱离 demo 的直接单测）。
- 建立 CHANGELOG 与发版基线。
