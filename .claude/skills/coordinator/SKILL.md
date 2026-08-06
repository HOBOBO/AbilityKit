---
name: coordinator
description: AbilityKit 会话契约包（com.abilitykit.coordinator）—— moba/shooter 实现的会话端口接口（ILogicWorldDriverBridge/ILogicWorldDriveGate/ISessionCoordinatorHost/ISessionCoordinatorConfigPolicy/ISpawnService）+ 它们使用的纯数据结构（PlayerInput/EntityState/SnapshotEntityState/FrameSnapshotData/SessionConfig/SessionId/PlayerSpawnData/NetworkEndpoint）。⚠ session 编排引擎（SessionCoordinator + SyncAdapter/SubFeature/Timeline/Transport 体系）已于 2026-08-06 移除（死代码，无 demo 使用）。触发场景：实现 ILogicWorldDriverBridge/ILogicWorldDriveGate、构造 PlayerInput/EntityState/SnapshotEntityState/FrameSnapshotData、SessionConfig/SyncMode/HostMode 配置、PlayerSpawnData/NetworkEndpoint。
---

# coordinator skill

基于源码核校（2026-08-06，清理后）。`com.abilitykit.coordinator` 包根：`Unity/Packages/com.abilitykit.coordinator/`。

## ⚠ 重大变更：session 引擎已移除（2026-08-06）

原 coordinator 定位是"会话层编排器"（SessionCoordinator 状态机 + 三种 SyncAdapter + SubFeature/Timeline/Transport 体系）。**但没有任何 demo/server 实例化过 `SessionCoordinator`** —— moba/shooter 都绕过它，各自实现端口接口 + 直接驱动自己的 runtime。该引擎是 moba/local 形状、不适配 statesync，属死代码，已整体移除。

**移除内容**（连同 `HybridSyncAdapter`[Obsolete]）：
- `SessionCoordinator` + `ExistingWorldSessionCoordinatorHost` + `SessionConfigConfigurator`
- 整个 `Adapters/`（ISyncAdapter + Local/Remote/HybridSyncAdapter + SyncAdapterFactory）
- 整个 `SubFeatures/`（ISessionSubFeature 接口族 + ISessionHost + 3 内置 SubFeature）
- 整个 `Timeline/`（IViewTimeline + ViewTimeline + SampleBuffer）
- 整个 `Transport/`（IRemoteBattleSyncTransport + NullRemoteBattleSyncTransport + CoordinatorInputSubmitBridge）
- `IViewEventSink` + `SessionHooks`（仅被已删的 ISessionCoordinator 成员引用）
- `PlayerInput` 的内置 payload-codec 辅助（CreateMove/CreateSkill/CreateStop/TryGet* + MoveInputPayload/SkillInputPayload/InputOpCodes）

**保留内容**（demo 实际实现/使用）：见下"当前包结构"。

> 现在只有 2 个 demo。等第三个 demo 落地、会话胶水重复明显时，再从真实用法提炼"会话模板"（见 `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md` 的"会话装配配方"）—— 从真实提取，而非凭空设计（coordinator 当初凭空设计才被废弃）。

## 当前定位：会话契约包

coordinator 现在只承载**端口契约 + 纯数据结构** —— 不实现网络协议、不实现 World 逻辑、不做会话编排。各 demo 自己实现这些端口 + 用这些 struct，组装自己的 session（用 `network.sdk/room/battle` + `host.extension` 等可复用零件）。

## 当前包结构（14 个 .cs）

```
com.abilitykit.coordinator/Runtime/
├── Core/   端口接口 + 配置
│   ├── ILogicWorldDriverBridge   逻辑世界驱动（shooter/moba 实现）
│   ├── ILogicWorldDriveGate      玩法层闸门（moba 实现）
│   ├── ISessionCoordinatorHost   宿主适配（moba 实现）
│   ├── ISessionCoordinatorConfigPolicy  会话配置策略（moba 实现）
│   ├── ISpawnService             出生服务（moba 实现）
│   ├── ISessionCoordinator       ⚠ 仅 vestigial（无实现者；唯一外部引用是 MobaBattleDriverHost.Bind 的未用参数，待删）
│   ├── SessionConfig / SessionId / SessionEnums(SyncMode/HostMode/SessionState)
├── Data/   纯数据结构
│   ├── PlayerInput / EntityState / SnapshotEntityState / FrameSnapshotData
│   ├── PlayerSpawnData / NetworkEndpoint
│   └── CoordinatorPayloadCodec   ⚠ 仅 EntityState.ToSnapshotEntityState 用（内部，alive）
```

## 端口契约速查（demo 如何接入）

- **实现 `ILogicWorldDriverBridge`**（SubmitInputs / AdvanceFrame / GetAllEntityStates → SnapshotEntityState[]）—— shooter `ShooterBattleDriverHost`、moba `MobaBattleDriverHost`。
- **实现 `ILogicWorldDriveGate`**（CanDriveLogicWorld）—— moba `MobaLogicWorldDriveGate`（world-scoped service）。
- **实现 `ISessionCoordinatorHost` + `ISessionCoordinatorConfigPolicy`** —— moba `MobaSessionCoordinatorHost`（注：moba 实现了这些 host 端口但**不**实例化 SessionCoordinator —— host 端口是契约，session 编排由 moba 自己做）。
- **使用 struct**：`PlayerInput`(raw 4 参 ctor) / `EntityState` / `SnapshotEntityState` / `FrameSnapshotData` / `PlayerSpawnData` / `SessionConfig`(SyncMode/HostMode) / `NetworkEndpoint`。

## 真实接入路径（不经 coordinator 的 session 引擎）

会话装配由各 demo 自己拼，见 `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md` 的"**会话装配配方（statesync / framesync）**"。coordinator 只提供上面的端口 + struct。

## Sections（注意：部分描述已删除的代码，仅作历史参考）

- [when_to_use.md](when_to_use.md) — 何时启用本 skill
- [host_and_driver_ports.md](host_and_driver_ports.md) — **保留**：ISessionCoordinatorHost + ILogicWorldDriverBridge + ILogicWorldDriveGate + ISpawnService（当前有效）
- [integration_recipes.md](integration_recipes.md) — moba/shooter 接入（顶部有修正横幅：两 demo 都不经 coordinator session 引擎）
- ⚠ 以下描述**已删除**的 session 引擎，仅历史参考：
  - [lifecycle.md](lifecycle.md) — ~~SessionCoordinator 状态机~~（已删）
  - [sync_adapters.md](sync_adapters.md) — ~~ISyncAdapter 体系~~（已删）
  - [transport_and_codec.md](transport_and_codec.md) — ~~IRemoteBattleSyncTransport/CoordinatorInputSubmitBridge~~（已删；CoordinatorPayloadCodec 保留）
  - [view_and_timeline.md](view_and_timeline.md) — ~~IViewEventSink/ViewTimeline~~（已删）
  - [subfeatures_and_hooks.md](subfeatures_and_hooks.md) — ~~ISessionSubFeature/SessionHooks~~（已删）
- [source_vs_readme.md](source_vs_readme.md) — 历史：README 与源码偏差（README 本就过时）

## 相关 skill

- 完整技能/触发/BUFF 速查见 [ability-kit](../ability-kit/SKILL.md)
- host.extension 的 BattleHost/FrameSync 见 [host-extension](../host-extension/SKILL.md)
- 战斗数据面引擎 → `com.abilitykit.network.battle`（contract-neutral：SendInputAsync + RawServerPushReceived）
- moba/shooter demo 接入 → [moba-demo](../moba-demo/SKILL.md) / [shooter-demo](../shooter-demo/SKILL.md)
