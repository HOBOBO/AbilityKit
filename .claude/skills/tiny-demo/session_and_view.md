# Tiny 会话与表现

## TinyBattleSession（344 行，`Runtime/View/`）

Tiny 项目的 Room + 战斗会话编排。**2026-09-28 起继承 `RoomGatewayBattleSessionBase`**（`com.abilitykit.network.room`，见 [framework_boundaries.md](framework_boundaries.md)）：房间全生命周期、恢复/重连、命令 ID 台账、Loading 阶段、快照游标、generation/revision 守卫与基线等待都在基类；本类只剩三模式同步策略（`OnTick` 里的快照/帧应用、`SubmitInputAsync` 的提交重试、`SubscribeBattleChannelsAsync` 的帧流订阅）与表现读取（`TryGetNewSnapshot`/`Telemetry`）。`TinyTurnSession`（290 行）同理，只剩回合所有权。

关键设计点：

- **可测**：经 `ITinyBattleGateway` 与传输隔离，所以能脱离场景测试。
- **并发守卫**：`IsCurrent` / `IsCurrentCommand` / `IsCurrentBattle` + `_bindingRevision` / `ConnectionGeneration`。**过期异步响应不得改写重新绑定的会话**——这条被大量用例覆盖（`OldConnectionPollCannotReplaceCurrentRoomSnapshot`、`LateCreateDoesNotJoinAfterRestore`、`LateJoinCannotReplaceRestoredRoom`、`OldBattleSubscriptionCannotReplaceNewBattle`、`OlderPollCannotReplaceNewBattleInSameRoom` 等）。
- **单飞**：`TinyGameplayRoot` 侧有 Startup/Command/Poll/Recover/Snapshot/Exit 单飞状态机（`Run`/`ExecuteAsync`/`CancelActiveRequest` 三件套用 `_activeRequest` 引用比对，保证**被取消的操作永远不会清掉替换它的新操作**）。
- **超时**：所有 Room 请求使用启动请求的 `Timeout`；快照请求、帧订阅、战斗输入各有本地期限。**超时表示客户端未收到结果，不表示服务端没执行**——重连时以恢复得到的 Room 快照为准。
- **建房命令 ID**：由调用方持有，同一次建房重试**复用 ID 与同步模式**，新的建房意图用新 ID；恢复期间**不自动重发**建房；恢复为空房间时保留待确认的建命令 ID 与同步模式供用户重试；已关闭房间不因旧命令重放而重新绑定账号。
- **基线等待**：订阅成功 / 全量快照请求被接受**都不等于**已有可用基线。State 要等当前 World 的完整角色快照；Frame/Hybrid 要等完整 Tiny 状态载荷成功应用。等待期间三种模式都暂停输入、界面显示 `Waiting for baseline`；请求被拒或推送未到则场景侧每 2 秒重试；连接恢复与快照请求**不并行**。错误 World、非全量、不可用快照都不解除等待。
- **恢复入口**：`RecoverConnectionAsync`（断线）、`TryGetFrameOverflow` + `RequestFullSnapshotAsync`（帧溢出，取覆盖缺口的快照）。
- **`Dispose()` 只释放 gateway，不取消在途工作**：`DisposedSessionIgnoresDelayedPoll` **显式断言**了迟到的 poll 仍会写 `Status`/`CanStart`（但不抛）。这是**记录在案的决定**，不是 bug——改它要先改测试。

### Hybrid 拒绝语义（容易记反）

| 模式 | 收到 `FrameAlreadyProcessed` | 收到 `FrameTooFarAhead` | 收到 `RateLimited` |
|---|---|---|---|
| Frame | 重试 1 次，帧号 ≥ 服务端帧 + `SubmissionLeadFrames`(8) | 请求全量快照 | 抛错，**不**恢复 |
| Hybrid | **不重试**，请求全量基线，输入阻塞 | 请求全量快照 | 抛错，**不**恢复 |

Frame 连续 3 次 `FrameAlreadyProcessed` 后**停止提交并抛错**，且 `NeedsFullSnapshot == false`（普通 Frame 不因它请求恢复）。

## TinyGameplayRoot（306 行）

`[DisallowMultipleComponent]` `MonoBehaviour`。`Start()` 从 `TinyProjectLaunch` 消费启动意图并连接 + 恢复；`Update()` 用 `Time.unscaledDeltaTime` tick 会话，优先级为**连接恢复(2s) → 全量快照请求(2s) → 房间轮询(0.5s)**；`OnDestroy()` 取消生命周期、取消在途请求、释放会话。

## 表现层（`Runtime/View/`）

- `TinyActorViewModule`：`OnAttach` 程序化生成 `Tiny Arena` + 正交相机 + 方向光（**共 3 个子物体**）；每个 Actor 一个立方体，按快照设位置与 `scale.y`（HP 100 → 1.0，90 → 0.9）。`OnDetach` 清空到 0 子物体——重复 Attach/Detach **不泄漏**（`AttachDetachAndReattachOwnSceneObjects` 断言 0→3→0→3→0）。
- **同帧复用对象**：位置/HP 更新是**复用同一 GameObject**，不是销毁重建；快照里缺席的 Actor 才销毁其视图。战斗绑定切换（清 `battleId`/`worldId`）时销毁旧 Actor 视图但**保留**程序化 `Tiny Arena`，新基线到达前不显示陈旧 Actor。
- `TinyHudPresenter`：`OnGUI` IMGUI 面板——建房/加入/准备/开战/返回按钮 + 遥测（同步模式、连接状态、权威帧与预测帧、预测/回滚/快照校正/恢复请求/帧队列溢出计数）。计数由复制器与通用 Room 帧收件箱提供。**刻意只用最基础 IMGUI**，不复刻正式 UI。
- 场地由代码生成，**无外部资源**。所以 `TinyAssetPreparation` 的 Loading 是**有意的空实现**（1 步、立即报 1.0），它真正做的事是**校验启动清单**。别把它当成加载系统；有资源的项目替换 `ClientLoadingPipeline` 的资源准备步骤。

## TinyFrameReplication（184 行，`Runtime/View/Sync/`）

权威 Room 帧/快照 → `TinyFrameSyncSession` 的适配：输入帧预留（`ReserveInputFrame`）、本地预测、回滚记账（`SnapshotCorrectionCount`、`LocalPredictions`、`NeedsFullSnapshot`）、表现快照合成。`src/AbilityKit.Demo.Tiny.Replication.Tests` **复用这份源码**做 .NET 单测（6 例）。

## FrameSync 层（`Runtime/FrameSync/`，192 行）

- `TinyFrameSyncSession`：驱动 `RollbackCoordinator` + `RollbackSnapshotRingBuffer` + `InputHistoryRingBuffer` + `WorldStateHashRingBuffer`。`ApplyAuthoritative` 返回 `TinyReconcileResult` ∈ `{Matched, Replayed, NeedsFullSnapshot}`。
- `TinyBattleRollbackProvider`：`IRollbackStateProvider` + `IRollbackStatePreflightProvider`，经 `TinyBattleStateCodec` 存取 `TinyBattle`。

**真正的回滚机器不在 Tiny 里**（在 `com.abilitykit.world.framesync`）。Tiny 只提供 provider 与适配。

## 私有反射陷阱（**改字段名会炸，但已改为响亮失败**）

`Tests/Editor/TinyBattleSessionTests.cs`（**26 个测试方法**）与 `Tests/PlayMode/TinyViewProjectionPlayModeTests.cs` 用 `SetPrivate` / `SetRootPrivate` / `TickRoot` 直接写 `TinyBattleSession` 与 `TinyGameplayRoot` 的**私有字段**并调私有 `Update`。2026-09-28 会话继承 `RoomGatewayBattleSessionBase` 后，房间侧字段（`_roomId`/`_battleId`/`_worldId`/`_playerId`/`_awaitingBaseline`）上移基类，两个测试文件的 `SetPrivate` 已改为**沿继承链查找并在找不到时 `Assert.Fail`**——所以现在重命名/删除被反射的字段会**响亮失败**而不是静默 NRE。`_frameReplication`/`_syncMode` 仍是会话自有字段。改这两个类的字段名前仍要先 grep 测试文件；`SnapshotCursor` 已是 public 属性，直接用 `session.SnapshotCursor`。

`TinyViewProjectionPlayModeTests.SetPrivate` 会断言成员非 null，是唯一带编译期感的护栏。

## TinyProjectLaunch（脱离 Starter 的入口）

```csharp
var launch = new DemoMultiplayerLaunchRequest(
    gatewayHost, gatewayPort, region, serverId, accountId, sessionToken, TimeSpan.FromSeconds(10));
TinyProjectLaunch.Open(launch, "YourLobbyScene");
```

- `Prepare` 发布 `DemoLaunchIntent` + `DemoMultiplayerLaunchIntent`；`TryConsume` **消费即清空**（一次性）；第二次 `TryConsume` 返回 false。
- 未认证（空 session token）→ `ArgumentException`，且**两个意图都不发布**。
- 仍需 DemoCommon 的启动请求类型与场景路由——这是模板依赖边界（Tiny 不能完全脱离 `demo.common`）。

回合制对应物是 `TinyTurnProjectLaunch.Open` / `TinyTurnGameplayScene` / `TinyTurnSession`（520 行，用 `ITinyTurnGateway` 隔离），API 形状与实时版对齐。回合制会话把**已去重 ACK 当作重试信号**（`CommandSequenceResumeAttempts = 3`），重建会话后若有界续号失败则继续，直到新行动拿到有效 ACK。
