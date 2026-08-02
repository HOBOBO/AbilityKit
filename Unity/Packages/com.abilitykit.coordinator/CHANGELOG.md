# Changelog — com.abilitykit.coordinator

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。
AbilityKit 整体仍处于开发期，0.x 版本不承诺向后兼容。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。Session 协调框架（LocalSyncAdapter / RemoteSyncAdapter / SessionCoordinator）
已被 MOBA / Shooter 两个 demo 端到端验证，并随本版本首次具备**脱离 demo 的直接契约测试**
（`src/AbilityKit.Coordinator.Tests`，覆盖 `PlayerInput` 的构造/创建/操作码）。

### 范围收口（决策 D2）
- **`HybridSyncAdapter`（客户端预测模式）已标记 `[Obsolete]`**：类内 4 处 TODO 桩（SubmitInput/Tick/Reconcile/GetAllEntityStates）尚未实现，且无 demo 消费者。
  两款 demo 的预测通路均不经 coordinator 的 ISyncAdapter 体系（MOBA 用 `host.extension/ClientPredictionDriverModule`，Shooter 用 `ShooterClientPredictionRuntimeAdapter`）。
  coordinator 0.1.0 承诺的同步模式为 **Local / Remote only**（不含 Hybrid）。

### API 边界（本包承诺稳定的部分）
- 会话协调器：`SessionCoordinator` / `ISessionCoordinator` / `SessionConfig`。
- 同步适配器：`LocalSyncAdapter` / `RemoteSyncAdapter` / `ISyncAdapter` / `SyncAdapterFactory`。
- 玩家输入：`PlayerInput`（构造 / Create* 工厂 / TryGet* 解析）。
- 输入编解码：`CoordinatorPayloadCodec`（MemoryPack payload 编解码）。

### 构建门槛
- 在 `src/AbilityKit.Coordinator` 上启用 `AbilityKitStable=true`：`TreatWarningsAsErrors` 已开启，零错误。

### 已知限制
- 可空性(CS8xxx)暂为咨询级。
- `HybridSyncAdapter` 不在承诺范围（已弃用）。
- 性能基线尚未纳入门禁。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）。
- 按决策 D2 将 `HybridSyncAdapter` 标记 `[Obsolete]`。
- 新增 `src/AbilityKit.Coordinator.Tests`（首批直接单测，3 用例）。
- 建立 CHANGELOG 与发版基线。
