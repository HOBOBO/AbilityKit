# When to use

启用本 skill 的典型场景：

- 你要为一个新玩法实现**会话端口契约**：`ILogicWorldDriverBridge`（SubmitInputs/AdvanceFrame/GetAllEntityStates→SnapshotEntityState[]）、`ILogicWorldDriveGate`（玩法层闸门）、`ISessionCoordinatorHost`/`ISessionCoordinatorConfigPolicy`（宿主适配）、`ISpawnService`（出生服务）
- 你要用 coordinator 的**纯数据结构**：`PlayerInput`（raw 4 参 ctor）、`EntityState`/`SnapshotEntityState`、`FrameSnapshotData`、`SessionConfig`（含 `SyncMode`/`HostMode` 选择）、`SessionId`、`PlayerSpawnData`、`NetworkEndpoint`
- 你要理解 `SyncMode`（Lockstep/SnapshotAuthority/StateSync/Hybrid）和 `HostMode`（Local/Host/Client）的语义（coordinator 提供枚举，但不驱动会话）

## 不要在本 skill 找的内容

- ~~SessionCoordinator 状态机~~ → **已删除**（session engine 已清理，各 demo 自己做会话编排）
- ~~ISyncAdapter / SyncAdapterFactory~~ → **已删除**（framesync/statesync 各自实现同步机器）
- ~~IRemoteBattleSyncTransport / CoordinatorInputSubmitBridge~~ → **已删除**
- ~~IViewEventSink / ViewTimeline / ISessionSubFeature / SessionHooks~~ → **已删除**
- 技能/BUFF/触发器业务 → [ability-kit](../ability-kit/SKILL.md)
- 战斗数据面引擎（NetworkTransport，契约中立）→ `com.abilitykit.network.battle`
- 多人联网接入指南（会话装配配方）→ `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
- BattleHost 服务端编排 / ClientPredictionDriverModule 配置 → [host-extension](../host-extension/SKILL.md)
- 具体玩法实现 → [moba-demo](../moba-demo/SKILL.md) 或 [shooter-demo](../shooter-demo/SKILL.md)
