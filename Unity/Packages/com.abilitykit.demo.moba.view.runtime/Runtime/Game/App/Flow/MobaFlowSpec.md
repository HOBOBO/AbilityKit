# MOBA 流程规范

> 状态：当前实现与目标态对照
> 目标：描述 MOBA 示例的流程规划，并明确哪些已经实现，哪些仍是正式项目目标。

---

## 1. 当前实现

当前 Unity MOBA view runtime 已实现的是一个最小可运行流程：

RootFlow:

- Boot
- Lobby
- Battle

BattleFlow:

- Prepare
- Connect
- CreateOrJoinWorld
- LoadAssets
- InMatch
- End

当前实现特点：

- Root/Battle 状态拓扑由 `MobaFlowConfiguration` 维护。
- 状态机执行由 HFSM 完成。
- transition 条件仍由 `GameFlowDomain` adapter 绑定，例如 battle requested。
- 状态进入后的 feature 装配由 `PhaseStateFeatureSpec`、`PhaseFeaturePlan`、`PhaseStateFeatureBinding` 和 `PhaseStateFeatureRegistry` 完成。
- feature 生命周期由 `PhaseFeatureHost` 管理。

当前 Root 转移：

| Trigger | From | To | Condition |
|---|---|---|---|
| BootCompleted | Boot | Lobby | none |
| EnterBattle | Lobby | Battle | battle_entry_ready |
| EnterBattle | Boot | Battle | battle_entry_ready |
| ReturnLobby | Battle | Lobby | none |
| ReturnLobby | Boot | Lobby | none |

当前 Battle 转移：

| Trigger | From | To |
|---|---|---|
| PrepareDone | Prepare | Connect |
| Connected | Connect | CreateOrJoinWorld |
| JoinedWorld | CreateOrJoinWorld | LoadAssets |
| LoadingDone | LoadAssets | InMatch |
| Ended | InMatch | End |

当前 feature 组合：

| State | Features | Clear Before Enter |
|---|---|---|
| Boot | none | yes |
| Lobby | none | yes |
| Battle.Prepare | context, entity, session | yes |
| Battle.Connect | debug_ongui | no |
| Battle.CreateOrJoinWorld | debug_ongui | no |
| Battle.LoadAssets | debug_ongui | no |
| Battle.InMatch | sync, input, view, hud, debug_ongui | no |
| Battle.End | debug_ongui | yes |

---

## 2. 当前实现仍不够正式的地方

### 2.1 flow state/event 已独立，但仍是最小集合

当前 Root/Battle 的 state/event 已抽到 `MobaFlowTypes`，`MobaFlowConfiguration` 和 `GameFlowDomain` 都依赖同一份 flow definition，不再由配置反向依赖 Domain 的嵌套枚举。

不过这仍只是当前最小可运行集合。正式项目目标态里的 Auth、Matchmaking、Room、PostBattle、Connectivity 等状态域尚未落地。

### 2.2 condition resolver 已独立，正式流程条件上下文已预留
 
`battle_requested`、`authenticated`、`room_ready`、`connectivity_ready`、`assets_ready` 和组合条件 `battle_entry_ready` 已经由 `MobaFlowConditionIds` 维护，并通过 `MobaFlowConditionResolver` 从 condition id 解析到项目侧判断逻辑。`GameFlowDomain` 只负责把当前运行态组装成 `MobaFlowConditionContext`，再交给 resolver。
 
当前 Root 进入 Battle 的配置已改为 `battle_entry_ready`。由于 Auth、Room、Connectivity、AssetLoading 等正式系统还没有在示例流程里展开，`GameFlowDomain` 现在先用默认 ready 状态保持最小示例可运行；后续接入真实系统时，只需要替换 `BuildFlowConditionContext` 的上下文来源。

condition pipeline（`MobaFlowConditionContext`、`MobaFlowConditionResolver`、`MobaFlowConditionIds`）是纯 C#，已镜像进桌面测试工程并有 xUnit 覆盖：原子条件 id 到对应 gate 的映射、空/未知 id 的兜底语义、`BattleEntryReady` 组合条件在任一 gate 缺失时为 false。这样后续把 `GameFlowDomain` 的 gate 从默认 ready 替换成真实系统来源时，组合判断逻辑本身已有回归保护。

### 2.3 enter/exit action refs 已进入通用 spec 和生命周期
 
当前 `_battleSessionStarted`、`_battleFirstFrameReceived` 重置，以及 Battle.End 后回 Lobby，已经抽到 `MobaFlowActionIds` 和 `MobaFlowActionExecutor`。`PhaseStateFeatureSpec` 可以声明 enter before、enter after、exit action refs；`PhaseStateFeatureBindingFactory` 会把 enter before/after refs 接到 enter 生命周期，把 exit refs 接到 exit 生命周期，再由 `GameFlowDomain` 的项目侧 executor 执行。
 
当前已经落地：
 
- Battle.Prepare 通过 `MobaFlowConfiguration` 声明 enter before action ref。
- Battle.End 通过 `MobaFlowConfiguration` 声明 enter after action ref。
- `PhaseStateFeatureBinding` 提供 exit 生命周期。
- `PhaseStateFeatureRegistry` 提供按 state key 退出的 registry 入口。
- MOBA 的 HFSM state onExit 已接到对应 registry exit，未来配置里增加 exit action refs 不需要再改状态机回调代码。
 
当前校验能力：
 
- `PhaseActionCatalog` 可登记合法 action id。
- `PhaseStateFeatureValidator` 可校验 enter before、enter after、exit action refs 的重复和未知 id。
 
### 2.4 switch flow refs 已进入通用 spec 和生命周期

状态切换时的异步推进节点（session started / first frame received 后推进状态机）原本硬编码在 `GameFlowDomain` 的 Connect、CreateOrJoinWorld、LoadAssets onEnter lambda 里，现已抽到配置驱动的 switch flow refs，与 action refs 镜像。

当前已经落地：

- `PhaseStateFeatureSpec.AddSwitchFlow` 声明 switch flow ref ids，`SwitchFlowIds` 暴露只读列表。
- `PhaseSwitchFlowCatalog` 登记合法 switch flow id。
- `PhaseStateFeatureValidator` 校验 switch flow refs 的空、重复和未知 id。
- `PhaseStateFeatureBindingFactory.BuildSwitchFlow` 把 switch flow refs 接到 enter 完成后（`_afterEnter` 之后）的生命周期，带上 installedCount。
- MOBA 侧 `MobaFlowSwitchIds` 声明 `battle.advance_on_connect_enter`、`battle.advance_on_create_or_join_world_enter`、`battle.advance_on_load_assets_enter`，`MobaFlowSwitchExecutor` 分发到 `GameFlowDomain` 的 `TryAdvanceOnConnectEnter`/`TryAdvanceOnCreateOrJoinWorldEnter`/`TryAdvanceOnLoadAssetsEnter`。
- Battle.Connect、Battle.CreateOrJoinWorld、Battle.LoadAssets 通过 `MobaFlowConfiguration` 声明对应 switch flow ref，状态机推进逻辑不再硬编码在 onEnter 回调里。

### 2.4 当前只实现了最小 RootFlow

Auth、Matchmaking、Room、PostBattle、Connectivity 并行域仍是目标态，不是当前实现。

---

## 3. 正式项目目标态

目标 RootFlow:

- Boot
- Auth
- Lobby
- Matchmaking
- Room
- Battle
- PostBattle

目标 Root 事件：

- Sys.Started
- Auth.LoginRequested
- Auth.LoginSucceeded
- Auth.LoginFailed
- Auth.LogoutRequested
- Match.StartQueue
- Match.CancelQueue
- Match.Found
- Room.Joined
- Room.ReadyChanged
- Room.BpStarted
- Room.BpFinished
- Battle.EnterRequested
- Battle.Ended
- Conn.Disconnected
- Conn.ReconnectSucceeded
- Conn.Kicked

目标 Root 转移表：

| From | Event | To |
|---|---|---|
| Boot | Sys.Started | Auth |
| Auth | Auth.LoginSucceeded | Lobby |
| Lobby | Match.StartQueue | Matchmaking |
| Matchmaking | Match.Found | Room |
| Room | Room.BpFinished | Battle |
| Battle | Battle.Ended | PostBattle |
| PostBattle | ReturnLobby | Lobby |
| Any | Auth.LogoutRequested | Auth |
| Any | Conn.Kicked | Auth |

---

## 4. RoomFlow 目标态

RoomFlowState:

- Assemble
- Ready
- BP
- Confirm
- ExitToBattle

目标转移：

| From | Event | To |
|---|---|---|
| Assemble | Room.Joined | Ready |
| Ready | Room.BpStarted | BP |
| BP | Room.BpFinished | Confirm |
| Confirm | Battle.EnterRequested | ExitToBattle |

RoomFlow 应产出 BattlePlan，供 BattleFlow 使用。

---

## 5. BattleFlow 目标态

BattleFlowState:

- Prepare
- LoadAssets
- Connect
- CreateOrJoinWorld
- InMatch
- End
- Return

目标转移：

| From | Event | To |
|---|---|---|
| Prepare | PrepareDone | LoadAssets |
| LoadAssets | Battle.LoadingDone | Connect |
| Connect | Battle.Connected | CreateOrJoinWorld |
| CreateOrJoinWorld | Battle.JoinedWorld | InMatch |
| InMatch | Battle.Ended | End |
| End | ReturnRequested | Return |

注意：当前实现中 Connect 在 LoadAssets 前，目标态可能更适合先准备资源再连接，或根据真实项目需求保留当前顺序。这个需要结合资源加载、网关连接、首帧等待策略再定。

---

## 6. Connectivity 并行域目标态

ConnectivityState:

- Online
- Reconnecting
- Kicked
- Offline

规则：

- Conn.Kicked 必须打断任何主流程，清理会话并回到 Auth。
- Conn.Disconnected 可进入 Reconnecting，并暂停或降级当前 feature。
- Conn.ReconnectSucceeded 根据上下文恢复 Room 或 Battle。

当前框架尚未直接建模并行域。建议仍由 HFSM 或外层协调器处理，Client Flow 只负责 feature 清理、action/flow 执行和诊断。

---

## 7. 下一步落地顺序
 
1. 扩展 `MobaFlowConfiguration`，让配置更接近正式 RootFlow。
2. 将 Auth、Room、Matchmaking、Connectivity 逐步接入 `BuildFlowConditionContext` 的真实运行态来源。
3. 最后再处理 Auth、Room、Matchmaking、Connectivity 这些完整目标态。

---

## 8. Flow ↔ World Scope（用作用域世界承载大流程阶段）

### 8.1 定位

Flow 与 World 是正交的两件事：

- **Flow（`GameFlowDomain` / HFSM）**：时间轴上的控制流，管"当前处于哪个阶段、能否转场"。
- **World（`IWorld` + `IWorldScope`）**：依赖空间上的作用域，管"某段运行态内有哪些服务实例、活多久"。

目标是把"大流程阶段（如一局战斗）"映射成一个 per-battle 的 `WorldScope`：阶段服务全部注册成 `Scoped` + 构造注入，进入阶段建 scope、退出阶段 `scope.Dispose()` 自动清理，替换掉当前 `GameFlowDomain` 里靠字段 + flag 手工管理的 battle 生命周期，减少全局单例。

### 8.2 关键约束

- **FSM 本身不进 scope**：root flow 是常驻控制流，活在 singleton 容器；只有"阶段实例化出来的服务集合"进 scope。flow 持有当前 scope 句柄并控制其建/拆，而不是 scope 包 flow。
- **singleton 不得在构造时 Resolve scoped**：`WorldContainer` 会显式抛错拦截这种生命周期穿透；常驻服务不能捕获 battle-scoped 实例。
- **跨阶段数据走显式入参**（`WorldCreateOptions` / `BattleStartPlan`），不依赖上一个 scope 残留的单例。当前 `_pendingBootstrapper` / `_pendingGatewayConnectionFactory` 这类 pending 字段，迁移时应变成建 scope 时的 options。
- **scope 不是每帧创建**：每个 world/对局一次，整阶段复用。

### 8.3 三步迁移（渐进，不一步到位）

1. **Step 1（已完成）**：新建 `BattleWorldModule : IWorldModule` 骨架，把 battle 阶段服务声明为 `Scoped` 注册，**不接 flow**，先用桌面测试验证 scope 边界闭环。
   - 实现：`BattleWorldModule.cs`（纯 C#，无 Unity 依赖，已镜像进桌面测试工程）。
   - 覆盖（`BattleWorldModuleTests`，6 个用例）：compose 注册成立、同 scope 共享单例、构造注入共享 scoped 依赖、跨 scope 隔离、`scope.Dispose()` 释放 scoped 实例、一个 scope 释放不波及另一存活 scope（多 world 并存）。
2. **Step 2**（进行中，已落两刀）：把 per-battle 的 scope 生命周期抽成纯 C# 的 `BattleWorldScopeHost`（持有 `WorldContainer` + 当前 `WorldScope` 句柄），`GameFlowDomain` 持有它并在 `EnterBattle` 调 `BeginBattle()` 建 scope、在 `ReturnToBoot` / `ReturnLobbyAfterBattleEnd` 调 `EndBattle()` 释放。`BeginBattle` 可重入（旧 scope 先释放）、`EndBattle`/`Dispose` 幂等安全。
   - 覆盖（`BattleWorldScopeHostTests`，10 个用例）：进入建 scope、无 scope 解析抛错、同 scope 共享单例、退出释放 scoped 实例、未开始即退出安全、重入换新 scope 并隔离实例、活跃中重入释放旧 scope、`Dispose` 释放活跃 scope、`Dispose` 幂等、释放后 `BeginBattle` 抛 `ObjectDisposedException`。
   - **第一刀（scope 句柄 + 生命周期对齐）**：保持 `_battle*` 字段零行为变化，只引入 scope 并对齐 enter/exit 生命周期。
   - **第二刀（首个真实运行态字段迁移，已完成）**：把 `GameFlowDomain._battleSessionStarted` / `_battleFirstFrameReceived` 两个纯 bool 运行态标志迁成 scoped 服务 `IBattleRuntimeState`（在 `BattleWorldModule` 注册成 `WorldLifetime.Scoped`）。`OnBattleSessionStarted` / `OnBattleFirstFrameReceived` / `TryAdvanceOn*` 改为经 `_battleWorldScope.Resolve<IBattleRuntimeState>()` 读写；`ResetBattleSessionRuntimeState()` 加 `HasActiveScope` 守卫——scope 活跃时显式清零，scope 已释放（如 `ReturnLobbyAfterBattleEnd` 在 `EndBattle` 之后调用）则 no-op，因下一局 fresh scope 实例本就为 false，可观察行为与迁移前一致。「每局重置」从手工清零变为由 scope 生命周期天然保证。
     - 覆盖（`BattleRuntimeStateTests`，4 个用例）：scope 内默认 false、同 scope 共享单例、`Reset` 清零、退出战斗后重入得到 fresh 实例。dotnet test 82 通过（+4），demo console 0 错误。
     - 此刀**不涉及跨阶段输入**（两标志默认 false 起步），故 options 播种机制留给后续 `BattleSessionFeature` 迁移时再引入。
   - **第三刀（DI 引擎补 per-scope 播种能力，已完成）**：原 DI 引擎里 scoped 实例只能由注册时的固定工厂 `Func<IWorldResolver,T>` 创建，无法把"建 scope 时才存在的 per-battle 输入"（如 pending bootstrapper / gateway）注入，是迁移带跨阶段输入服务的前置缺口。已最小增量补齐**播种机制**（engine 层，纯增量、零行为变化）：
     - `WorldScope.SeedInstance(Type, object)`：把外部构造的实例写进**独立的 `_seeded` 字典**（与 scope 自建的 `_scoped` 物理隔离，使「拥有/不拥有」由字典归属强制区分而非靠 `_disposeOrder` 注释约定）；`Resolve` 与 `TryResolve` 均优先命中 `_seeded`（先于委托 root），故"播种但未在容器注册"的类型也可解析、且覆盖容器内同类型 scoped 工厂。
     - **生命周期语义（关键）**：播种=注入外部输入，**生命周期归调用方（flow）**；播种实例**不**进 `_disposeOrder`，`scope.Dispose()` 不接管其释放，避免误 Dispose 仍被别处持有的对象。
     - `IWorldScopeSeeder`（`Seed(Type,obj)` / `Seed<T>(T)`，fluent）+ `WorldContainer.CreateScope(Action<IWorldScopeSeeder>)` 重载 + `BattleWorldScopeHost.BeginBattle(Action<IWorldScopeSeeder>)` 重载，构成"建 scope 时播种"的闭环。既有 `CreateScope()` / `BeginBattle()` 无参路径不受影响。
     - 覆盖（`WorldScopeSeedingTests`，5 个用例）：播种命中、覆盖注册工厂、scope 释放不连带释放播种实例、未注册类型可解析、类型不匹配抛 `ArgumentException`。dotnet test：World.DI 15 通过、view 82 通过；demo console 0 错误。
   - **第四刀（`BattleSessionFeature` 的 `IBattleBootstrapper` 迁移到 scope 播种，已完成）**：把原 `_pendingBootstrapper` 裸字段的跨阶段传递改为 scope 播种，与已迁的 `IBattleRuntimeState` 统一承载。
     - `EnterBattle`：`bootstrapper != null` 时走 `BeginBattle(s => s.Seed<IBattleBootstrapper>(bootstrapper))` 播种；`null` 局（部分进入路径不带 bootstrapper）走无参 `BeginBattle()` 不播种。删除 `_pendingBootstrapper` 字段及 `ReturnToBoot` 中对它的清理。
     - `CreateBattleSessionFeature`：改用 `_battleWorldScope.TryResolve<IBattleBootstrapper>(out var bootstrapper)` 取回（取不到传 `null`，与迁移前「字段为 null」行为等价）。
     - **引擎缺口补全**：播种特性此前只补了 `Resolve` 优先命中，漏了 `TryResolve`——其 `!IsRegistered` 短路会让"已播种却未注册"的类型错误返回 false。本刀让 `TryResolve` 在该短路前先查 `_seeded`（与 `Resolve` 对齐），并新增 `BattleWorldScopeHost.TryResolve<T>(out T)` 容错接缝（无活跃 scope 返回 false 不抛）。
     - **时序说明**：`gateway 工厂`（`_pendingGatewayConnectionFactory`）因仅在 `AttachBattleFeatures` 期间瞬态存在（建 scope 时尚无值），本质是"随调用进出"的输入，不适合在 `BeginBattle` 时播种，故**本刀不迁**，保留原瞬态字段。
     - 覆盖：`WorldScopeSeedingTests` +1（播种但未注册类型 `TryResolve` 命中、未播种类型返回 false）。dotnet test：World.DI 16 通过、view 82 通过；demo console 0 错误。
   - 后续刀：评估 gateway 工厂是否值得迁（需先解决其瞬态时序），其余带跨阶段输入的服务逐个迁移——经 `BeginBattle(s => s.Seed<T>(...))` 把数据播种进 scope，再把服务改成从 scope 解析。一次迁一个，每步跑测试。
3. **Step 3（四个准入 gate 迁移到 scope 服务，已完成）**：把原 `GameFlowDomain` 上 `IsAuthenticatedForFlow` / `IsRoomReadyForFlow` / `IsConnectivityReadyForFlow` / `IsAssetsReadyForFlow` 四个硬编码 `return true` 的私有方法，换成 scope 内注册的 `IFlowGateProvider` 服务来源。
   - 新增 `FlowGateProvider.cs`（纯 C#）：`IFlowGateProvider` 暴露四个 gate；`DefaultFlowGateProvider` 四项全 true（等价迁移前硬编码，零行为变化）。`BattleWorldModule` 把它注册为 `Scoped`（每局一份，随 scope 释放）。
   - `BuildFlowConditionContext()`：经 `_battleWorldScope.TryResolve<IFlowGateProvider>(out var provider)` 从 per-battle scope 取 gate 来源；scope 尚未建立时（Boot/Lobby 阶段轮询求值）落空，用静态 `DefaultGates` 兜底——仍是四项全 true，与迁移前一致。删除四个硬编码 gate 方法。
   - 语义：gate 是「能否进入战斗」的实时准入判定（每次转移求值时读取当前值），故用 `Scoped` 服务而非 `Seed` 播种快照。准入合成口径不变：`BattleEntryReady = BattleRequested && Authenticated && RoomReady && ConnectivityReady && AssetsReady`。后续接真实鉴权/房间/连通/资源判定时，只需换 scope 内实现，flow 读取侧无感。
   - 覆盖：新增 `FlowGateProviderTests`（默认四 gate 全 true、模块注册为 Scoped 单例、任一 gate false 则 `BattleEntryReady` 为 false 的参数化用例、自定义实现可翻转单个 gate）。dotnet test：view 91 通过；demo console 0 错误。
3.5. **绞杀刀：battle 阶段推进决策表剥离为纯逻辑（已完成）**：把 `GameFlowDomain` 里散落在 6 个方法（`OnBattleSessionStarted` / `OnBattleFirstFrameReceived` / `OnBattleSessionFailed` / `TryAdvanceOnConnectEnter` / `TryAdvanceOnCreateOrJoinWorldEnter` / `TryAdvanceOnLoadAssetsEnter`）里的「当前 battle 态 + 信号/runtime 标志 → 应触发哪个 `MobaBattleEvent`」硬编码 if-else，抽成纯 C# 决策表。
   - 新增 `MobaBattleAdvanceDecider.cs`（纯 C#，无 UnityHFSM/scope/Unity 依赖）：暴露 `OnSessionStarted` / `OnFirstFrameReceived` / `OnSessionFailed` / `OnStateEntered` 四个查询，返回可空 `MobaBattleEvent?`（null = 不推进）。三个 `TryAdvanceOn*Enter` 合并为私有 `TryAdvanceOnStateEnter(state)` 调 `OnStateEntered`。
   - 边界：副作用（写 `IBattleRuntimeState`、日志、`_battleFsm.Trigger`）全部留在 domain；decider 只算「该触发什么」。`OnStateEntered` 语义逐一对齐——Connect 看 `SessionStarted‖FirstFrameReceived`，CreateOrJoinWorld / LoadAssets 仅看 `FirstFrameReceived`。零行为变化。
   - 覆盖：新增 `MobaBattleAdvanceDeciderTests`（四个查询的推进/不推进全枚举参数化，含 Connect 双标志 OR、CreateOrJoinWorld 不看 SessionStarted 等边界）。注：枚举为 `internal`，public `[Theory]` 签名不能暴露之，故 `InlineData` 用底层 `int`、方法体内转回枚举。dotnet test：view 122 通过；demo console 0 错误。
 3.6. **绞杀刀：`MobaFlowConfiguration.CreateDefault()` 流程拓扑特征化测试（已完成）**：`GameFlowDomain` 已榨干易剥离的纯逻辑（FlowGateProvider、MobaBattleAdvanceDecider 已出，剩余全是 UnityHFSM/Unity 耦合）。本刀转向**补特征化测试**而非继续剥离——`MobaFlowConfiguration.CreateDefault()` 是流程拓扑的唯一真相源（根/战斗状态机 + 状态/迁移/条件 + 每态 feature/action/switch 列表），纯 C#、零 Unity 依赖，但此前零测试覆盖。
   - **前置小重构**：`MobaFlowActionIds` / `MobaFlowSwitchIds` 原与依赖 sealed `GameFlowDomain` 的 `MobaFlowActionExecutor` / `MobaFlowSwitchExecutor` 同处 `MobaFlowActions.cs`，整文件无法独立镜像编译。把这两个纯 const 类拆到新文件 `MobaFlowActionIds.cs`（零行为变化，纯数据/派发分离），使其可独立镜像。
   - 覆盖：新增 `MobaFlowConfigurationTests`（28 个用例）——Root 状态机（3 态、StartState=Boot、5 条迁移逐条断言含 2 条 `BattleEntryReady` 门控）、Battle 状态机（6 态线性链、StartState=Prepare、5 条迁移逐条断言）、10 个 `PhaseStateFeatureSpec`（每态的 `ClearBeforeEnter` / `FeatureIds` 顺序与内容 / `EnterBefore/AfterActionIds` / `SwitchFlowIds` 全锁死）。镜像编译新增 `MobaFlowActionIds.cs` + `MobaFlowConfiguration.cs`。dotnet test：view 150 通过（+28）；demo console 0 错误。
 4. **Step 4.0（收口前置盘点，已完成）**：在真正切 asmdef 前，先把当前 `Flow` 目录拆成「可进入纯核心」与「必须留在表现层」两类，避免为追求收口而把 Unity/HFSM/表现层副作用误塞进核心包。
    - **第一版可进入纯核心的候选**：`MobaFlowTypes.cs`（枚举）、`MobaFlowConditions.cs`（条件 id + 条件上下文 + resolver）、`FlowGateProvider.cs`（准入 gate 服务契约/默认实现）、`BattleRuntimeState.cs`（每局运行态契约/默认实现）、`BattleWorldModule.cs`（scope 注册模块，依赖 World.DI 但不依赖 Unity）、`BattleWorldScopeHost.cs`（scope 生命周期 host，依赖 World.DI 但不依赖 Unity）、`MobaBattleAdvanceDecider.cs`（推进决策表）、`MobaFlowActionIds.cs`（action/switch const）、`MobaFlowConfiguration.cs`（流程拓扑配置）。这些文件已经由桌面 xUnit 镜像编译覆盖，是 Step 4.1 的最小迁移集合。
    - **暂不进入纯核心的边界**：`GameFlowDomain.cs` 仍依赖 `UnityHFSM` / `UnityEngine` / 日志 / feature 安装等表现层副作用；`MobaFlowActions.cs` 的 executor 部分仍通过 sealed `GameFlowDomain` 执行动作/切换流；`GamePhaseContracts.cs` 绑定 `GameEntry` / `IEntity` / view phase 契约；`Boot/BootMenuOnGUIFeature.cs` 明确依赖 `UnityEngine` 与 OnGUI。它们应留在 `AbilityKit.Demo.Moba.View.Runtime`，由表现层引用纯核心，而不是反向污染核心。
    - **当前脚手架债务**：`AbilityKit.Game.View.Runtime.Tests.csproj` 已逐文件镜像 11 个源文件；随着 `MobaFlowConfigurationTests` 加入，镜像清单继续增长，已经到达可执行 asmdef/csproj 收口的临界点。下一刀不再建议继续扩镜像，而是先切最小 `AbilityKit.Game.Flow.Core` 边界。
 4.1. **Step 4.1（最小 Flow.Core 边界收口，已完成）**：把 Step 4.0 标出的最小纯核心集合归拢到独立 `AbilityKit.Game.Flow.Core`，Unity 侧新增 `Flow/Core/AbilityKit.Game.Flow.Core.asmdef`（`noEngineReferences: true`），桌面侧新增 `src/AbilityKit.Game.Flow.Core/AbilityKit.Game.Flow.Core.csproj`，两边消费同一批核心源码。
    - **迁入核心的文件**：`MobaFlowTypes.cs`、`MobaFlowConditions.cs`、`FlowGateProvider.cs`、`BattleRuntimeState.cs`、`BattleWorldModule.cs`、`BattleWorldScopeHost.cs`、`MobaBattleAdvanceDecider.cs`、`MobaFlowActionIds.cs`、`MobaFlowConfiguration.cs` 已移动到 `Flow/Core/`。`GameFlowDomain.cs`、`MobaFlowActions.cs` executor、`GamePhaseContracts.cs`、`Boot/BootMenuOnGUIFeature.cs` 继续留在表现层，只引用/消费核心类型。
    - **引用关系**：`com.abilitykit.demo.moba.view.runtime.asmdef` 新增对 `AbilityKit.Game.Flow.Core` 的引用；`AbilityKit.Game.View.Runtime.Tests.csproj` 从 9 个 Flow 纯文件逐文件镜像改为 `ProjectReference` 到 `AbilityKit.Game.Flow.Core.csproj`，仅保留 `IGameModule.cs` / `ModuleHost.cs` 两个模块兼容测试镜像。
    - **internal 边界**：核心程序集新增 `FlowCoreAssemblyInfo.cs`，通过 `InternalsVisibleTo` 保持 `AbilityKit.Demo.Moba.View.Runtime`、`AbilityKit.Game.View.Runtime.Tests`、`AbilityKit.Game.UnitTests` 可访问既有 internal 枚举/类型，避免为了拆程序集扩大 public API。
    - **验证**：`dotnet test src/AbilityKit.Game.View.Runtime.Tests/AbilityKit.Game.View.Runtime.Tests.csproj --nologo -v q` 通过 150/150；`dotnet build src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj --nologo -v q` 通过（0 错误，保留既有 NuGet/Entitas 兼容警告）。

 4.2. **Step 4.2（executor 接口解耦 + 迁入核心，已完成）**：把 `MobaFlowActionContext` / `MobaFlowActionExecutor` / `MobaFlowSwitchExecutor` 从 `MobaFlowActions.cs` 拆出，通过 `IMobaFlowActionTarget` 接口解耦对 sealed `GameFlowDomain` 的直接依赖，使 dispatch 逻辑可独立测试并归入纯核心。
    - **新增接口**：`Core/IMobaFlowActionTarget.cs`——暴露 5 个方法（`ResetBattleSessionRuntimeState`、`ReturnLobbyAfterBattleEnd`、`TryAdvanceOnConnectEnter`、`TryAdvanceOnCreateOrJoinWorldEnter`、`TryAdvanceOnLoadAssetsEnter`），把 executor 对 domain 的耦合从具体类型降为接口。
    - **迁入核心**：`Core/MobaFlowActionDispatch.cs`——包含 `MobaFlowActionContext`（持有 `IMobaFlowActionTarget` 而非 `GameFlowDomain`）、`MobaFlowActionExecutor`（action id → target 方法派发）、`MobaFlowSwitchExecutor`（switch id → target 方法派发）。三个类型均为 `internal`，通过 `FlowCoreAssemblyInfo.cs` 的 `InternalsVisibleTo` 对测试工程可见。
    - **Domain 侧零行为变化**：`GameFlowDomain : IMobaFlowActionTarget`——只需在类声明加接口名，5 个方法签名已存在，无需新增或修改方法体。`new MobaFlowActionContext(this, ...)` 隐式转换即可。
    - **原文件清空**：`MobaFlowActions.cs` 保留为注释文件（兼容 `.meta` 引用），无编译内容。
    - 覆盖：新增 `MobaFlowActionDispatchTests`（9 个用例）——action executor（2 个已知 id 派发 + 空 id 不派发 + 未知 id 返回 false）、switch executor（3 个已知 id 派发 + 空 id 不派发 + 未知 id 返回 false），通过 `FakeFlowActionTarget` mock 记录调用。dotnet test：view 159 通过（+9）；demo console 0 错误。

 4.3. **Step 4.3（ILogSink + IFeatureBinder 宿主抽象，已完成）**：消除 `GameFlowDomain` 对 `Log.*` 静态调用和 `IEntity.WithRef/RemoveComponent` 的直接依赖，使 domain 的日志和 feature 绑定操作通过注入的抽象接口执行，为后续在无 Unity 环境下测试 domain 核心逻辑铺路。
    - **复用 ILogSink**：发现 `AbilityKit.Core.Logging.ILogSink` 已是 `public` 接口且签名兼容（`Info`/`Error`/`Exception`），无需在 Flow.Core 自建重复接口。原 `Core/ILogSink.cs` 清空为注释文件（兼容 `.meta`）。
    - **新增 IFeatureBinder**：`Core/IFeatureBinder.cs`——暴露 `AttachFeature(object)` / `DetachFeature(object)` 两个方法，抽象 Entity 级别的 feature 绑定操作，使 domain 不再直接操作 `IEntity`。
    - **构造函数注入**：`GameFlowDomain` 新增核心构造函数 `GameFlowDomain(GameEntry, IEntity, ILogSink, IFeatureBinder)`，所有日志和 feature 绑定通过注入实例执行。旧构造函数保留为便利包装——`ILogSink` 传 `Log.Sink`（全局静态日志 sink），`IFeatureBinder` 传 `new EntityFeatureBinder(root)`。
    - **Log.* → _log.* 替换**：全文件约 20 处 `Log.Info/Log.Error/Log.Exception` 静态调用替换为 `_log.Info/_log.Error/_log.Exception` 实例调用。`using AbilityKit.Core.Logging` 保留（`Log.Sink` 需要它），但不再有 `Log.*` 静态方法调用。
    - **EntityFeatureBinder 适配器**：作为 `GameFlowDomain` 的 `private sealed` 内部类，桥接 `IFeatureBinder` 到 `IEntity.WithRef((object)feature)` / `IEntity.RemoveComponent(feature.GetType())`。保持与迁移前完全一致的行为。
    - 覆盖：新增 `IFeatureBinderContractTests`（3 个用例）——mock 的 `AttachFeature`/`DetachFeature` 调用记录、多次操作顺序验证，证明接口可无 Unity 依赖 mock。dotnet test：view 162 通过（+3）；demo console 0 错误。

 4.4. **Step 4.4（IGameHost 宿主抽象，已完成）**：消除 `GameFlowDomain` / `GamePhaseContext` 对 `GameEntry`（Unity `MonoBehaviour`）的直接类型依赖，使 Flow 层只依赖宿主抽象接口，Unity 成为可选运行项。
    - **新增 IGameHost**：`Core/IGameHost.cs`——暴露 `IEntity Root`、`bool DebugEnabled`、`T Get<T>()`、`bool TryGet<T>(out T)`、`void StartCoroutine(IEnumerator)` 五个成员，覆盖 Flow 层对 `GameEntry` 的全部使用点（`_entry.Root`、`_entry.DebugEnabled`、`_entry.StartCoroutine`、`ctx.Entry.Get<T>()`、`ctx.Entry.DebugEnabled`）。
    - **GameEntry : IGameHost**：`GameEntry` 类声明加接口名即可——`Root`、`DebugEnabled`、`Get<T>()`、`TryGet<T>()`、`StartCoroutine`（继承自 `MonoBehaviour`）签名已与接口完全匹配，无需新增或修改方法体。
    - **GamePhaseContext.Entry 类型收口**：`GamePhaseContext.Entry` 从 `GameEntry` 改为 `IGameHost`。Features（`BootMenuOnGUIFeature`、`BootPhase`）通过 `ctx.Entry.DebugEnabled` / `ctx.Entry.Get<T>()` 访问，签名不变，零行为变化。
    - **GameFlowDomain 构造函数收口**：字段 `_entry` 和三个构造函数参数从 `GameEntry` 改为 `IGameHost`。`_entry` 的使用点（`_entry.Root`、`_entry.StartCoroutine`、`_entry.DebugEnabled`）均为 `IGameHost` 成员，无需修改方法体。
    - **Flow.Core 项目引用**：`AbilityKit.Game.Flow.Core.csproj` 新增 `AbilityKit.World.ECS` 项目引用——`IGameHost.Root` 返回 `IEntity`（定义于 `AbilityKit.World.ECS`），此前 `Flow.Core` 仅传递性引用 `AbilityKit.Core`（不含 `IEntity`）。
    - 覆盖：无新增测试（本步为类型收口，行为零变化）。dotnet test：view 162 通过（0 回归）；demo console 0 错误。

 4.5. **Step 4.5（OnGUI 表现代码移出 Domain — 路径 C，已完成）**：把 `GameFlowDomain.OnGUI()` 内联的 IMGUI 绘制代码（Root 状态标签 + Enter Battle / Battle End / Return Lobby 三个按钮）抽到独立 Feature，Domain 只保留一行委托调用。同时引入 `IFlowCommandSink` 接口让 Feature 不再直接引用 `GameFlowDomain` 具体类型，为后续 Step 4.6（IPresentationSink 事件桥接）铺路。
    - **新增 IFlowCommandSink**：`Core/IFlowCommandSink.cs`——表现层向流程编排层提交命令的接口，暴露 `CurrentRootPhase`、`CurrentBattlePhase` 两个状态读取属性，以及 `RequestEnterBattle()`、`RequestBattleEnd()`、`RequestReturnLobby()` 三个命令方法。Feature / View 通过此接口请求流程变更，不直接引用 `GameFlowDomain`。
    - **GameFlowDomain : IFlowCommandSink**：类声明加接口名，新增 `CurrentBattlePhase` 公开属性（映射 `_activeBattle`），以及三个显式接口方法实现——`RequestEnterBattle()` 委托 `EnterBattle((IBattleBootstrapper)null)`、`RequestBattleEnd()` 触发 `_battleFsm.Trigger(MobaBattleEvent.Ended)`、`RequestReturnLobby()` 委托 `ReturnToBoot()`。`IFlowCommandSink.CurrentRootPhase` / `CurrentBattlePhase` 通过显式接口实现映射到已有字段。
    - **MobaBattleState 可见性提升**：`MobaFlowTypes.cs` 中 `MobaBattleState` 从 `internal enum` 改为 `public enum`——因为 `IFlowCommandSink.CurrentBattlePhase` 是 public 接口成员，不能暴露 internal 类型（CS0053）。
    - **新增 RootDebugOnGUIFeature**：`Boot/RootDebugOnGUIFeature.cs`——把 Domain `OnGUI()` 内的 Root 状态标签 + 三个按钮 GUI 代码整体搬入。通过 `ctx.Entry.Get<IFlowCommandSink>()` 获取命令接收器，读取 `CurrentRootPhase` / `CurrentBattlePhase` 决定是否绘制（Battle.InMatch 时隐藏），按钮回调调用 `RequestEnterBattle()` / `RequestBattleEnd()` / `RequestReturnLobby()`。
    - **简化 GameFlowDomain.OnGUI()**：从 ~35 行内联 GUI 代码缩减为一行 `_features.OnGUI(in _ctx)` 委托调用。`BuildBootFeaturePlan()` 新增 `"root_debug"` Feature 条目。
    - **BootMenuOnGUIFeature / BattleDebugOnGUIFeature 解耦**：两个已有 Feature 改用 `ctx.Entry.Get<IFlowCommandSink>()` 替代 `ctx.Entry.Get<GameFlowDomain>()` 读取状态和提交命令。`BootMenuOnGUIFeature` 仍通过 `ctx.Entry.Get<GameFlowDomain>()` 调用 `EnterBattle(new TestBattleBootstrapper())`（带 bootstrapper 重载不在接口上，属测试专用入口，可接受）。
    - 覆盖：无新增测试（本步为表现代码搬迁 + 接口抽取，行为零变化）。dotnet test：view 162 通过（0 回归）；Unity Editor 编译无错误（控制台干净）。

 4.6. **Step 4.6（IPresentationSink 事件桥接 — 路径 B，已完成）**：建立 Logic→View 的事件推送契约，使 Flow 编排层通过接口向表现层推送阶段变化 / 战斗开始 / 战斗结束 / 错误事件，完全不知道 View 的存在。与 Step 4.5 的 `IFlowCommandSink`（View→Logic 命令方向）互补，构成**双向通信契约**。
    - **新增 IPresentationSink**：`Core/IPresentationSink.cs`——Flow 编排层向表现层推送事件的接口，暴露 `OnPhaseChanged(MobaRootState, MobaBattleState)`、`OnBattleStart()`、`OnBattleEnd()`、`OnError(string)` 四个方法。灵感来自 ET 的 `IETViewEventSink`，但按关注点分离为精简成员（ET 是 20+ 方法平铺）。
    - **新增 NullPresentationSink**：`Core/NullPresentationSink.cs`——Null Object 模式空实现，当构造函数未提供 `IPresentationSink` 时用作默认值（`NullPresentationSink.Instance`），避免 null 检查散落各处。标记为 `internal sealed`。
    - **GameFlowDomain 构造函数注入**：新增 `_presentationSink` 只读字段。核心构造函数新增可选参数 `IPresentationSink presentationSink = null`，null 时回落到 `NullPresentationSink.Instance`。新增两个便利构造函数重载：`(IGameHost, IPresentationSink)` 和 `(IGameHost, IEntity, IPresentationSink)`，保持已有构造函数零破坏性（链式调用传 null）。
    - **状态机回调推送事件**：Root FSM 的 Boot / Lobby onEnter 回调增加 `_presentationSink.OnPhaseChanged(_activeRoot, _activeBattle)`；Battle FSM 的 `StateChanged` 处理器增加 `_presentationSink.OnPhaseChanged(_activeRoot, _activeBattle)`；InMatch onEnter 增加 `_presentationSink.OnBattleStart()`；End onEnter 增加 `_presentationSink.OnBattleEnd()`。
    - **新增 GamePresentationSink**：`Entry/GamePresentationSink.cs`——Unity 宿主的 `IPresentationSink` 实现，当前以日志输出为主（`Log.Info` / `Log.Error`），后续可扩展为 UI 状态切换、场景加载触发等。`GameEntry.Awake()` 构造 `GameFlowDomain` 时传入 `new GamePresentationSink()`。
    - 覆盖：新增 3 个 xUnit 测试（`PresentationSinkTests`）——验证 RecordingSink 正确记录事件、接口方法签名完整、`IFlowCommandSink` + `IPresentationSink` 双向通信契约共存于同一命名空间。dotnet test：view 165 通过（0 回归，+3 新增）。

### 8.4 测试承载方式：镜像编译是脚手架，asmdef 切分是终态

当前桌面 xUnit 工程通过 `<Compile Include="..\..\Unity\...\*.cs" Link="..." />` 把 Unity 包内的**纯 C# 源文件**链入编译来做回归（如 `BattleWorldModule.cs`、`BattleWorldScopeHost.cs`）。这是一种**有意识的过渡手段（脚手架）**，不是工业终态，需明确区分：

- **目标是工业化的**：让核心逻辑不依赖引擎、从而可在引擎外快速测试，是"逻辑/表现分离"的标准实践。
- **手段是权宜的**：镜像编译让同一份源码被两个工程各自编译，存在三类债务——
  1. **漂移风险**：新增文件要手动补 `<Compile Include>`，漏了会静默不测。
  2. **编译上下文不一致**：两边 `nullable`/`define` 不同（例如桌面工程 `Nullable=enable` 触发的 `WorldScope?` 警告），靠 `#nullable enable` 等补丁逐个对齐。
  3. **线性脆化**：文件越多清单越长，维护成本累积。

工业主流做法是 **asmdef 程序集切分 + 项目引用**：把纯逻辑独立成一个不勾选任何 Unity 引用的 asmdef（如 `AbilityKit.Game.Flow.Core`），编译成单一 dll，Unity 的 EditMode 测试与桌面 xUnit **都引用同一份产物 dll**，而非各自重新编译源文件。这样真相来源唯一、加文件零成本、编译行为一致。

**为何现在不直接切 asmdef**：`GameFlowDomain` 仍是与 UnityEngine 强缠绕的 god object，尚不具备干净切分的边界。因此采用绞杀者模式（Strangler Fig）的节奏——**先用镜像编译做脚手架边拆边测**，每剥离一块纯逻辑就立刻挂上回归网；待纯逻辑攒到一定体量、边界清晰后，再执行 Step 4 收口，把镜像编译替换为 asmdef 引用。
