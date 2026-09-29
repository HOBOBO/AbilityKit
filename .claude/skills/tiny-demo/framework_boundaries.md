# Tiny 与框架的边界（用什么 / 不用什么 / 为什么）

基于源码核校（2026-09-28）。**用途**：判断"照 Tiny 做"能拿到框架的多少能力、以及哪里会撞到天花板。要动手扩展 Tiny 或照它接一个真实项目前，先读本文。

## 覆盖面的数字

- 框架包（非 `thirdparty.*` / 非 `demo.*`）约 **75 个**。
- Tiny 在 **代码级**（`using`）只用了 **6 个**：`world.framesync`、`network.room`、`network.sdk`、`network.runtime`、`protocol.room`、`game.view.runtime`。
- **包级闭包 10 个**（额外 `protocol`、`core`、`deterministic`、`world.di` 由上述包传递带入，Tiny 代码不直接引用）。

所以 Tiny 是 **Room + State/Frame/Hybrid + 恢复** 这条链路的忠实展示，**不是**整个工具集的展示。

## 两条传输拓扑（**最重要的集成结论**）

框架里存在两种客户端传输拓扑，Tiny 走的是**较少人走的那条**：

| | MOBA / Shooter | Tiny / Turn |
|---|---|---|
| 拓扑 | **双连接**：房间控制面 + 战斗数据面（`NetworkTransport`） | **单连接**：帧同步直接走 Room 连接 |
| 框架门面 | `com.abilitykit.network.client` 的 `GatewayBattleClientHost`（259 行，单文件单包） | **无对应门面**，只有原语 |
| 同步载体 | 独立战斗传输 + 快照/事件 | `RoomClient.FrameSyncFrameReceived` + `RoomGatewayFrameInbox`（Room 帧同步 opcode `121`/`122`/`9006`） |
| Tiny 的依赖 | — | **不依赖** `network.client`，也不依赖任何 `network.transport.*` |

`com.abilitykit.network.client/README.md` 自述其定位是"把'房间控制面连接 + 战斗数据面连接'的组装、凭证传递和推送绑定纪律收进一个组件，**替代各 demo 各自手拼的 300-700 行接入外壳**"。Tiny 的 `TinyBattleSession`(627) + `TinyGatewayClient`(147) = **774 行**，正落在这个区间——而且 `TinyBattleSessionTests` 里一大批用例（`OldBattleSubscriptionCannotReplaceNewBattle`、`OlderPollCannotReplaceNewBattleInSameRoom`、`LateJoinCannotReplaceRestoredRoom`…）正是在手工守护 README 说的"推送绑定纪律"（observerKey 单槽 last-writer-wins）**要靠约定而非结构**的那类竞态。

**判读**：Tiny 绕过 `GatewayBattleClientHost` **在拓扑上是有理由的**——那个宿主是双连接拓扑的产物，而 Tiny 的同步不走战斗数据面。但这意味着：**选择单连接拓扑，就没有结构化的接入宿主可复用**，会话纪律得自己写、自己测。这是"复杂项目该选哪条拓扑"的核心权衡点。

## Tiny 用到 vs 不用（同一问题的两面）

**用了**（且用得对）：Room 生命周期与连接恢复、同步能力协商（7 轴 `NetworkSyncProfile` + `RequiredProfile` 校验）、全量快照游标、有界帧收件箱、命令 ID 台账、Loading 阶段、启动清单哈希、`world.framesync` 的回滚环与 `IRollbackStateProvider`、view 模块宿主（`ModuleHost`/`IGameModule`/`ClientLoadingPipeline`）、Room wire 协议。

**不用**（且这些在"最小双人联机"射程内）：

| 包 | Tiny 自己手写了什么 | 判断 |
|---|---|---|
| `com.abilitykit.timer`（CLAUDE.md 定位为**统一调度器**） | `TinyGameplayRoot`/`TinyTurnGameplayRoot` 里 **8 个 `_nextXxx` float deadline 字段** + `Time.unscaledTime >= _nextX` 比较 + `TinyInputModule` 的手写限流 | **该用**。`DefaultScheduler` + `SchedulePeriodic/RequestCancel` 约 5 行即可替换；代价是要注意 `TimerSchedulerModule` 的既有告诫——用 PostTick 墙钟 dt 驱动**不是**确定性路径，应挂在帧对账路径上 |
| `world.framesync` 的 `ClientPredictionReconciler` | `TinyFrameSyncSession` 自己维护 `_hashes` 环 + 哈希比对分支 | **该用**（同一个已在依赖里的包）。`ClientPredictionReconciler(WorldStateHashRingBuffer)` 的 `RecordPredictedHash`/`OnAuthoritativeHash` 就是 Tiny 手写的那套，约 30 行可替换。注意 `ClientPredictionRunner` 需要 `IWorld`/`IWorldInputSink`，Tiny 没有，**不能**照搬 |
| `com.abilitykit.world.snapshot` | `TinyGatewayClient` 的 `_latest` 门 + `TinyBattleSession.Tick` 里的手工 drain 与 `PayloadOpCode` 判断 | 中等价值；该包语义假设 `WorldId`/`WorldStateSnapshot`，Tiny 没有 |
| `scenario` / `battlescenario` / `environment` | 全部验收都是手写测试 + 1042 行脚本 | **射程内但代价大**：Runner 由项目提供，且该体系当前是 Beta（作者层还在演进）。Tiny 的四种恢复故障本可写成 `network disconnect/reconnect` 场景数据 |
| `world.statesync` | State 模式**不做插值**：`TinyActorViewModule` 直接 `view.transform.position = new Vector3(actor.X, height * 0.5f, actor.Z)` 硬贴合 | 可解释（整数格 30Hz 慢速移动看不出），但**宣布的是 `AuthoritativeInterpolation` 档案**——见下"声明 vs 实现" |
| `network.battle` / `.config` | `TinyBattleSession` 手写 3 次重试与 `FrameAlreadyProcessed` 处理 | 该包是战斗数据面引擎；Tiny 走 Room 帧道，语义不同 |
| `diagnostics` / `record` | HUD 自己数计数器；录制靠独立 `.NET` 工程 | 低优先级，但 `ProfilerHub.Current.Sample("battle.tick")` 是几行的事 |
| `game.battle.runtime` | 自声明 17 成员 `ITinyBattleGateway` | 该包是逻辑传输抽象；Tiny 不需要 |

**刻意不用（正确判断）**：技能栈全部（`ability`/`triggering`/`modifiers`/`attributes`/`combat.*`/`continuous`/`behavior`）、ECS 三件套、`gameplaytags`、`deterministic`（Tiny 用整数而非 `Fixed64`）、`ai.*`、编辑器链（`actionschema`/`actioneditor.impl`/`excel-sync`/`base.editor`/`analyzer`）。

## 声明 vs 实现：能力档案是"声明"，不强制实现

`NetworkSyncProfile` 是**通用**内置档案（12 个工厂），框架校验的是 ① 档案内部一致性、② 两端 bitfield 协商一致——**不校验**本玩法的 adapter 是否真的具备所声明能力。两个已核实的漂移：

1. **可靠事件**：Tiny 的 State 模式宣布 `NetworkSyncProfiles.AuthoritativeInterpolation`，该档案带 5 项 `ReliableEventPolicy`，且这些位会经 `RoomNetworkSyncCapabilityResolver` → `NetworkSyncCapabilities.ReliableEvent` **上线**。而 Tiny 的 adapter **没有实现** `IReliableBattleEventProducer`（该接口 2026-09-28 起已 public，所以这是"没做"而非"做不到"；Tiny 目前只实现了 `IBattleRuntimeInputDiagnostics`）。`com.abilitykit.network.sdk` 的 `ReliableEventSessionBuilder` 会真的读这些位。
2. **插值**：State 模式宣布 `AuthoritativeInterpolation`，但视图是硬贴合，无插值。

两者在当前 Tiny 客户端里都不会炸（两端用的是**同一个**内置档案，协商自然通过），但说明**档案名 ≠ 实现能力**。给复杂项目选档案时不要只看名字。

## 会话层：组合层已下沉到框架（2026-09-28 落地）

**改造前**：框架只有原语（`RoomGatewaySessionFlow` 1496 行 + wire client 1035 行 + ledger/cursor/inbox/loading-stage），"订阅到快照/帧流"之上的全部东西（恢复顺序、generation 守卫、基线闸门、命令台账使用）都要各 demo 自己写——不存在任何 `IGameSession`/`BattleSessionBase`。最强证据曾是 Tiny 自己：`TinyBattleSession`(627) 与 `TinyTurnSession`(520) 有 12/13 个脚手架方法同名同序，同一家族内重复 ~85-90%；MOBA/Shooter 则各有一套 13k/19k 行的独立客户端栈。

**现状**：`com.abilitykit.network.room/Runtime/RoomGatewayBattleSessionBase.cs`（584 行）补上了组合层，三个公开类型：

- `IRoomGatewayBattleConnection` —— 会话依赖的连接抽象（Rooms/ConnectionState/ConnectionGeneration/Tick/CompleteRestore）。`ITinyBattleGateway`、`ITinyTurnGateway` 均继承它，实现零改动。
- `RoomGatewaySessionIdentity` —— token/region/server/account + 请求期限（≤0 回落 10s）。
- `RoomGatewayBattleSessionBase`（abstract）—— 拥有房间全生命周期（创建/加入/准备/加载/轮询/退出）、恢复与重连、命令 id 台账、Loading 阶段、快照游标、战斗绑定、`IsCurrent*` 守卫与基线等待；并把**不变式写进类型**：`BindRoom`/`InvalidatePendingCommands`/`Dispose` 递增 binding revision，过期异步响应在结构上无法改写重绑后的会话。玩法只实现钩子：`OnTick`（规则推进）、`CreateRoomRequest`、`ValidateLaunchManifest`、`ResolveSubscriptionModelName/Profile`、`SubscribeBattleChannelsAsync`（帧流等）、`OnBattleBound`/`OnRoomUnbound`/`OnDisposing`、`SendFullSnapshotRequestAsync`，外加消息文案与状态文案的虚属性。

**迁移结果**：`TinyBattleSession` 627→**344** 行、`TinyTurnSession` 520→**290** 行（各 −45%）；会话只剩玩法策略（三模式调度 / 回合所有权）。验证：新增 14 个基类契约测试（`src/AbilityKit.Network.Room.Tests/RoomGatewayBattleSessionBaseTests.cs`）+ 用户在途的 10 个 `TinyTurnSessionTests` 全绿 + 真实 Gateway TCP 三模式验收通过（State `baseline=full,targetHp=90`；Frame `predictions=0,双端回滚各1`；Hybrid `predictions=1`）。

**注意**：Unity 侧 `TinyBattleSessionTests`/`TinyViewProjectionPlayModeTests` 用反射打会话私有字段，字段上移基类后 `GetField` 看不到——两个测试文件的 `SetPrivate` 助手已改为**沿继承链查找**并显式 `Assert.Fail`（找不到时报错而非 NRE）；`SnapshotCursor` 因此提为 public，四处反射改为直接调用。**Unity 侧测试尚未实际运行**，需要在 Unity 里跑一次 EditMode/PlayMode 收红。

MOBA/Shooter 尚未迁移到该基类（各自的 13k/19k 行客户端栈独立存在）；它们的迁移是后续可选项，不阻塞 Tiny。

## 预测：三条并行技术栈（框架自己标注为待收敛）

`ShooterClientPredictionRuntimeAdapter.cs:14-18` 有一段 `TODO(v1.0)`，原文承认：

> Currently three independent prediction stacks coexist:
> `world.framesync/Rollback` | `host.extension/Client/FrameSync` | `shooter (this adapter)`

即：**没有单一权威的客户端预测路径**，项目得在三者里挑一个。Tiny 选的是最底层的 `world.framesync/Rollback`，并自己手写 `TinyFrameReplication`(184) 充当适配层。另有一档 `host.extension` 的 `ClientPredictionDriverModule`（1376 行，MOBA 用）需要客户端有 `HostRuntime` + `IWorld`，Tiny 没有，所以**用不了**。

## Tiny 跳过的其它门面

| 门面 | 位置 | Tiny 的替代物 | 后果 |
|---|---|---|---|
| `NetworkSdkClientHub`（267 行）/ `NetworkSdkDiagnosticsAggregator`（238） | `network.sdk` | `RoomGatewayConnectionSession.ConnectAsync` 自建裸 `NetworkSdkBuilder` | 拿不到流量监控与诊断聚合（MOBA/Shooter 都走 hub） |
| `NetworkSyncSessionBuilder<TController,TContext>`（1766 行） | `network.sdk` | 只调 `RoomGatewayNetworkSyncSessionBinding.Negotiate` | **校验了能力却从不绑定同步控制器** |
| `ReliableEventSessionBuilder<T>`（2355 行） | `network.sdk` | 无 | 无可靠事件路径 |
| `FramePacketNetAdapter`（183 行） | `host.extension` | `TinyGatewayClient`(147) | 连接边界自己写 |
| `ClientPredictionReconciliationCoordinator`（134 行） | `host.extension` | `TinyFrameReplication.ApplyFullSnapshot` 里的临时哈希比对 | 见上 |
| `PhaseFeatureHost`/`PhaseStateMachineValidator` 等阶段机 | `game.view.runtime` | 只用同包的 `ModuleHost`/`ClientGameModule`/`ClientLoadingPipeline` | 项目侧阶段机未用 |
| `com.abilitykit.coordinator` 整体 | — | — | 该包只剩 port/POCO（13 文件 / 1026 行，无编排引擎），且 **README 记录的 `SessionCoordinator`/`ISyncAdapter`/`ISessionSubFeature`/`SessionHooks`/`ViewTimeline` 五个类型在源码里已不存在**；不在任何 demo 的客户端路径上 |
| `com.abilitykit.game.battle.runtime` 整体 | — | 自声明 17 成员 `ITinyBattleGateway` | MOBA/Shooter 都引用，Tiny 完全不引用 |

## `IWorld` 契约税

`TinyBattleRuntimeAdapter.Tick` 的实体：

```csharp
if (_world is null) return false;
_world.Tick(deltaTime);   // 框架要求的完整 IWorld
_battle.Tick();           // 真正的权威（整数帧）
```

`_world = _worldManager.CreateBattleWorld(...)` 在 `Start` 里创建，对应 blueprint 是 `DelegateWorldBlueprint(TinyGameplay.WorldType, options => options.WorldType = TinyGameplay.WorldType)`——**不注册任何 system/module**，对状态零贡献。合约层面 `ServerGameplayModule` 强制要求至少一个 blueprint 的 `WorldType == Descriptor.DefaultWorldType`，所以这个空世界是**必付的**。

顺带一个确定性口径问题：`Tick(int frame, int tickRate, float deltaTime)` 的契约是**浮点时间**形状，而 Tiny 的权威是整数帧——`deltaTime` 只被喂给那个空世界，Tiny 的规则用不上它。

## 两种接入形态（复杂项目走的是另一种）

- **Tiny 形态**：规则是一个普通对象（`TinyBattle`），adapter 直接持有并 tick 它；blueprint 空。
- **MOBA 形态**：blueprint + 世界系统（`MobaWorldBlueprint` + systems + Entitas ECS），战斗能力栈（技能/Buff/触发器/碰撞/寻路/属性）都假设这种形态存在。

Tiny **没有演示**"规则对象如何用上战斗能力栈"。一个需要技能/Buff/命中判定的复杂项目，必须走 MOBA 那种世界系统形态——那是**另一套装配故事**，Tiny 只教到"规则怎么联机"，没教"玩法怎么装配进世界"。

## 规则共源带来的迁移面

服务端经 `src/AbilityKit.Demo.Tiny.Core` / `Turn.Core` 的 `<Compile Include>` 直接编译 Unity 包源码，所以**服务端构建依赖 Unity 目录树存在**，改 `Unity/Packages/**` 下的规则会静默改变服务端行为。复杂项目若沿用这个共源方式，要把这条耦合当成架构决策接受（好处是客户端/服务端规则字节一致，坏处是构建/发布边界跨越了 Unity 目录）。
