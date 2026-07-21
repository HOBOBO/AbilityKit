# When to use

启用本 skill 的典型场景：

- 你需要为一个新玩法（moba/shooter/其他）接入"会话生命周期 + 同步策略 + 输入提交"——实现 `ISessionCoordinatorHost` + `ILogicWorldDriverBridge`
- 排查 `SessionCoordinator.Initialize/Start/Stop/Destroy/Tick` 状态机问题（`Idle/Initializing/Running/Paused/Stopping/Stopped/Error`）
- 选择 `SyncMode`（Lockstep / SnapshotAuthority / StateSync / Hybrid）和 `HostMode`（Local / Host / Client）
- 用 `ExistingWorldSessionCoordinatorHost` 接管一个外部已创建的 world（shooter 模式）
- 实现 `IRemoteBattleSyncTransport` 走自定义网络路径（含 gateway 异步路由）
- 通过 `CoordinatorInputSubmitBridge` 把"coordinator 提交本地输入"与"transport 复用输入路由"串起来
- 实现 `IViewEventSink` 接收快照/伤害/生命周期事件
- 用 `ViewTimeline` 做位置/旋转采样插值
- 挂接自定义 `ISessionSubFeature` 或订阅 `SessionHooks`
- 修正旧 API 误用（`IBattleDriverHost` / `SetDriverHost` / `SessionConfig.CreateForMode` 等已不存在）

## 不要在本 skill 找的内容

- 技能/BUFF/触发器业务 → [ability-kit](../ability-kit/SKILL.md)
- BattleHost 服务端编排 / ClientPredictionDriverModule 配置 → [host-extension](../host-extension/SKILL.md)
- 具体网络协议（OpCode 定义）→ 各 demo 的 protocol 包（`com.abilitykit.protocol.moba` / `com.abilitykit.protocol.shooter`）
- 具体玩法实现 → [moba-demo](../moba-demo/SKILL.md) 或 [shooter-demo](../shooter-demo/SKILL.md)
