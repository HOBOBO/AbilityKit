---
name: coordinator
description: AbilityKit 会话协调器（com.abilitykit.coordinator）——位于 Host/World（下层）与 View/Network（上层）之间的会话层编排器。覆盖 SessionCoordinator 生命周期状态机、ISessionCoordinatorHost/ILogicWorldDriverBridge 应用端口、三种 SyncAdapter（Lockstep/SnapshotAuthority+StateSync/Hybrid）、IRemoteBattleSyncTransport、IViewEventSink、ViewTimeline、ISessionSubFeature 扩展点、CoordinatorInputSubmitBridge 异步输入桥。触发场景：会话初始化/启动/停止/销毁、SyncMode 选择、ExistingWorld 接管、moba/shooter 接入 coordinator、PlayerInput 提交、视图事件下沉、子功能挂接。
---

> v0.1.0 Beta -- AbilityKitStable=true, has direct src tests (3), HybridSyncAdapter [Obsolete(error:true), 计划 v0.2.0 移除]。
# coordinator skill

基于源码核校（2026-07-20）。`com.abilitykit.coordinator` 包根：`Unity/Packages/com.abilitykit.coordinator/`。

## 核心定位

Coordinator 是"会话层编排器"，位于 Host/World（下层）与 View/Network（上层）之间。**不实现具体网络协议、不实现具体 World 逻辑、不创建 Unity 视图对象**。

协调 4 类对象：
1. **逻辑世界**（IWorld/IWorldHost/HostRuntime）—— 通过 `ISessionCoordinatorHost.CreateWorldHost`
2. **同步策略**（ISyncAdapter）—— 由 `SyncAdapterFactory` 按 `SessionConfig.SyncMode` 创建
3. **表现层**（IViewEventSink + Timeline.IViewTimeline）—— 通过 `SessionCoordinator.Notify*`
4. **扩展点**（ISessionSubFeature + SessionHooks）—— 按 Priority 挂接

## 包结构

```
com.abilitykit.coordinator/Runtime/
├── Core/           ISessionCoordinator/SessionCoordinator/ISessionCoordinatorHost/ExistingWorldSessionCoordinatorHost
│                   ILogicWorldDriverBridge/ILogicWorldDriveGate/ISpawnService/IViewEventSink
│                   SessionConfig/SessionId/SessionHooks/SessionEnums
├── Adapters/       ISyncAdapter + LocalSyncAdapter/RemoteSyncAdapter/HybridSyncAdapter + SyncAdapterFactory
├── SubFeatures/    ISessionSubFeature 接口族 + ISessionHost + 3 个内置 SubFeature
├── Timeline/       IViewTimeline + ViewTimeline + ScalarSampleBuffer/VectorSampleBuffer
├── Transport/      IRemoteBattleSyncTransport + NullRemoteBattleSyncTransport + CoordinatorInputSubmitBridge
└── Data/           PlayerInput/EntityState/FrameSnapshotData/PlayerSpawnData/NetworkEndpoint/CoordinatorPayloadCodec
```

空目录：`PlayMode/`、`Events/`（仅 `.meta` 占位）。无 `Tests/`、无 `Editor/`。

## 最重要的注意（README 与源码偏差）

包内 `README.md` 与 `docs/ET-Integration-Guide.md` **已严重过时**。skill 内容全部基于源码，不以 README 为准。主要偏差：

- README 写的 `IBattleDriverHost` → 实际是 `ILogicWorldDriverBridge`（多了 `AdvanceFrame` / `Start/Stop`，返回 `SnapshotEntityState[]` 不是 `EntityState[]`）
- README 的 `ISessionCoordinatorHost` 4 个方法 → 实际 5 个（多了 `ConfigureWorldCreateOptions`），另有 `ISessionCoordinatorConfigPolicy`
- README 的 `SyncMode` 3 个值 → 实际 4 个（`Lockstep/SnapshotAuthority/StateSync/Hybrid`）
- README 把 `SessionConfig` 写成 sealed class → 实际是 `struct`
- README 的 `PlayerInput` 用 `InputType` 枚举 + 字典 → 实际是 `int OpCode + byte[] Payload`（MemoryPack）

详见 [source_vs_readme.md](source_vs_readme.md)。

## Sections

- [when_to_use.md](when_to_use.md) — 何时启用本 skill
- [lifecycle.md](lifecycle.md) — SessionCoordinator 状态机 + SessionConfig 工厂方法 + SessionRuntimePolicy
- [host_and_driver_ports.md](host_and_driver_ports.md) — ISessionCoordinatorHost + ExistingWorldSessionCoordinatorHost + ILogicWorldDriverBridge + ILogicWorldDriveGate + ISpawnService
- [sync_adapters.md](sync_adapters.md) — 三种 adapter + HybridSyncAdapter 未完成 TODO
- [transport_and_codec.md](transport_and_codec.md) — IRemoteBattleSyncTransport + CoordinatorInputSubmitBridge + CoordinatorPayloadCodec
- [view_and_timeline.md](view_and_timeline.md) — IViewEventSink + ViewTimeline 采样插值
- [subfeatures_and_hooks.md](subfeatures_and_hooks.md) — ISessionSubFeature 接口族 + 内置 3 个 + SessionHooks 全字段
- [integration_recipes.md](integration_recipes.md) — moba 与 shooter 两种接入模式（含真实调用链）
- [source_vs_readme.md](source_vs_readme.md) — README 9 处偏差对照

## 相关 skill

- 完整技能/触发/BUFF 速查见 [ability-kit](../ability-kit/SKILL.md)
- host.extension 的 BattleHost/FrameSync 见 [host-extension](../host-extension/SKILL.md)
- moba demo 接入见 [moba-demo](../moba-demo/SKILL.md)
- shooter demo 接入见 [shooter-demo](../shooter-demo/SKILL.md)
