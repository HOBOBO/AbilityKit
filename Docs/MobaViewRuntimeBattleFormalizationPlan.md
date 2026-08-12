# Moba View Runtime 战斗流程正式化优化计划

## 1. 文档目的

本文档针对 `com.abilitykit.demo.moba.view.runtime` 当前战斗流程实现进行正式化治理，重点解决 `BattleSessionFeature` 和 `BattleContext` 通过大量 `partial class` 聚合生命周期、网络、模拟、输入、回放和表现资源的问题。

本计划不是机械地将分部文件改名为普通类，而是以状态和资源所有权为迁移依据，逐步形成可测试、可替换、可回滚的 Session Runtime 结构。

## 2. 审计结论

### 2.1 规模和分类

当前 `BattleSessionFeature*.cs` 匹配文件为 40 个，约 3051 行，是本次治理的 P0 对象。整个包中约有 53 个 `partial` 声明结果，但不能将其全部视为问题：

| 分类 | 当前判断 | 处理策略 |
| --- | --- | --- |
| 生成代码注册表 partial | 合理使用 | 保留，不纳入迁移 |
| `BattleViewFeature` / `ConfirmedBattleViewFeature` 生命周期 partial | 风险较低 | 暂保留，后续按 View Runtime Host 评估 |
| `BattleContext` partial | 职责聚合 | P1，拆为上下文、状态和服务 |
| `BattleSessionFeature` partial | 巨型门面和实际 owner | P0，按资源闭包迁移 |

### 2.2 当前分层的问题

现有 `State / Handles / Controllers / SubFeatures / Host Ports` 不是完全无效，但目前主要是名义分层：

- `BattleSessionState` 将 Session 生命周期、固定步长、网关时钟、远端世界和确认世界状态放在同一对象中。
- `BattleSessionHandles` 已按资源类型分组，但资源仍被 `BattleSessionFeature` 的多个 partial 直接读写。
- Controllers 多数通过 `ISession*Host`、`Func` 和 `Action` 回调回到 Feature，实际行为所有权没有转移。
- `SessionLifecycleHostOptions` 有 18 项委托/属性依赖，说明 `SessionOrchestrator` 依赖的是 Feature 内部实现集合，而不是稳定的 Session Runtime 聚合对象。
- `BattleSessionFeature` 同时实现至少 9 个 runtime port，并继续直接实现回放控制、生命周期、网络、模拟和表现资源管理。
- `BattleSessionRuntimeResources` 中确认世界同时持有模拟世界、输入源、表现快照管线和 View Feature，远端世界也重复持有世界和输入资源，领域边界仍未闭合。
- `BattleContext` 同时承担逻辑 Session、运行时世界、计划、输入队列、预测控制、实体表现、VFX、快照路由和玩家 loadout 变更。

### 2.3 主要风险

1. 生命周期顺序由多个 partial 和 lambda 共同维护，新增资源容易遗漏 teardown。
2. 资源 reset 与资源 dispose 的边界不清晰，存在重复释放、释放顺序错误和跨会话残留风险。
3. 远端插值、可靠事件、重连、Spectator 和 Gateway 时间同步都附着在主 Feature 上，难以独立测试。
4. 单一 `BattleContext` 和静态 Provider 限制多 Session、多 viewport 以及独立回放表现。
5. SubFeature 过薄，部分只承担接口转发，不能作为真正的领域 owner。
6. `async void` Spectator 流程和 Feature 内部 CTS 使取消、重启和 teardown 难以形成明确契约。
7. live fixed-step loop 当前未体现统一的 tick budget，异常大 delta 可能带来长循环风险。

## 3. 目标架构

### 3.1 依赖方向

目标依赖方向固定为：

```text
App / Flow
    -> Bootstrap
    -> Battle Session
        -> Networking / Gateway / Replication
        -> Simulation
        -> Input
        -> Replay
        -> Presentation Session Resources

各子域 -> 最小 Contracts
```

Presentation 只消费表现数据和表现资源，不负责网络连接、战斗 Session 编排或逻辑世界状态机。Client Flow 与 Presentation 是并列能力：前者负责进入战斗、连接、重连和 Scope 生命周期，后者负责投影和 Unity View 消费。

### 3.2 目标对象

`BattleSessionFeature` 最终只保留门面职责：

- 实现对外稳定的 `IBattleSessionFeature`。
- 接收并保存外部注入的配置、工厂和诊断端口。
- 装配 `BattleSessionRuntime`。
- 将生命周期和 Tick 请求委托给 Runtime。
- 转发必要的公共事件，不持有网络、世界和表现资源细节。

建议形成以下正式对象：

| 对象 | 唯一职责 | 主要资源 |
| --- | --- | --- |
| `BattleSessionRuntime` | Session 级装配、状态和 owner 协作 | Plan、Lifecycle、Tick、Events |
| `SessionLifecycleCoordinator` | 启动、停止、失败恢复和逆序 teardown | Cleanup transaction |

### 3.3 第一批资源所有权登记

第一批实施先固定 Session 级资源的唯一 owner，不改变现有生命周期步骤和公共接口：

| 资源 | 当前实际 owner | 第一批目标 owner | 迁移状态 |
| --- | --- | --- | --- |
| `BattleSessionState` | `BattleSessionFeature` | `BattleSessionRuntime` | 已完成 |
| `BattleSessionHandles` | `BattleSessionFeature` | `BattleSessionRuntime` | 已完成 |
| `SessionOrchestrator` | `BattleSessionFeature` | `BattleSessionRuntime` | 已完成 |
| `SessionLifecycleHost` | `BattleSessionFeature` 装配的 host | `SessionLifecycleCoordinator` / Runtime Resource Port | 能力端口收敛已完成 |
| Gateway、Snapshot、World、Replay 句柄 | `BattleSessionHandles`，由 Feature partial 直接操作 | 按能力分组的 Runtime Resource Port | Gateway Connection/Room Client/Preparation/Clock、Snapshot Routing、Replication、Simulation、Replay、Spectator 与 confirmed Presentation 已迁移，其他待后续阶段 |
| `GatewaySessionRuntime` | Gateway session 资源聚合生命周期 | Connection、Room Client、preparation、clock、registry binding、network conditioning attachment | 已完成；preparation 与 clock 分别由专属 owner 持有 |
| `GatewayPreparationRuntime` | 单个 generation 的房间准备事务 | preparation task/CTS、局部 plan、world-start anchors | 已完成 |
| `GatewayClockSynchronizer` | 单个 generation 的 Gateway 时钟同步循环 | TimeSync task/CTS、EWMA estimate、连续失败计数 | 已完成 |
| `BattleReplicationRuntime` | 快照、可靠事件、插值、健康度、重连 catch-up | transport bindings、Options callbacks、pipeline、cursor、admission、authoritative state、health 与 pending state | 已完成；Feature 暂保留 world import、可靠事件业务投递、checkpoint 持久化和重连世界恢复 facade |
| `BattleSimulationRuntime` | Remote-driven 和 confirmed authority world | World runtime、input runtime、world tick、prediction view bridge、confirmed world-bound event pipeline 与分步 teardown 协调 | 已完成；Feature 保留 Session composition 和 presentation callback facade |
| `BattleSnapshotRoutingRuntime` | Snapshot routing composition 和 dispose | Dispatcher、pipeline、command handler、routing | 已完成 |
| `BattleReplayRuntime` | Record、replay、seek、pause、step 的 Session 资源 | 独立 Replay session owner、record writer | 已完成；Feature 保留 `IBattleReplayControl` facade，Context writer 仅为非拥有热路径镜像 |
| `SpectatorSessionRuntime` | Spectator 启动、推送订阅、追帧和世界生命周期 | generation operation/CTS、network client、push handler、candidate/active world driver | 已完成；Feature 仅保留 `Task` 启动、停止和 tick facade |
| `BattleInputRuntime` | HUD buffer、Session identity、actor resolver 和 aim projection | Input queue、resolver、submitter | 后续阶段 |
| `BattlePresentationSessionResources` | Confirmed View Context、View Feature 和表现快照 | confirmed presentation context、snapshot runtime 与 feature attach/detach | 已完成；world-bound event pipeline 仍归 Simulation owner |
| `BattleSessionDiagnostics` | Debug facade、health report 和生命周期观测 | session-scoped diagnostics；静态 Provider 仅作兼容读取面 | 已完成；jitter、time-sync、confirmed authority 与 synchronization health 已收敛 |

本批保留 `BattleSessionFeature` 上的同名只读兼容访问器，以支持分阶段迁移既有 partial 调用；测试反射 helper 同时兼容字段和属性，避免测试契约反向锁定旧 owner 结构。

### 3.3 State 和 Handles 规则

State 和 Handles 不再作为跨域万能容器，而改为 owner 内部实现细节：

- `SessionLifecycleState`：只保存生命周期、generation 和失败信息。
- `SessionTickState`：只保存 accumulator、last frame 和 fixed-step 预算状态。
- `GatewayClockState`：只保存时钟同步 EWMA 和采样数据。
- `SimulationWorldHandle`：只保存对应 world runtime、world 和输入资源。
- `ReplicationHandle`：只保存 replication pipeline、cursor、transport 和健康度。
- `PresentationHandle`：只保存 View Context、View Feature 和表现管线。
- 每个 owner 必须有明确的 `Start / Tick / Stop / Dispose` 契约；`Reset` 只清除纯状态，不承担隐式跨 owner 释放。

## 4. `BattleSessionFeature` 分部迁移矩阵

### 4.1 批次 P0：建立正式 Session Runtime 外壳

迁移范围：

| 当前文件组 | 目标 owner | 迁移动作 |
| --- | --- | --- |
| `BattleSessionFeature.cs` | `BattleSessionRuntime` | 保留构造和门面，停止新增业务字段 |
| `BattleSessionFeature.Runtime.cs` | Contracts/Runtime Adapter | 将显式接口实现移动到对应 Runtime Adapter，删除 Feature 上的多领域 port |
| `BattleSessionFeature.RuntimeContracts.cs` | Contracts | 保留最小公共端口，去除指向 Feature 私有方法的接口 |
| `BattleSessionFeature.StateAccessors.cs` | 各 State owner | 将属性映射改为直接依赖对应状态对象 |
| `BattleSessionFeature.Accessors.cs` | Lifecycle/Resource owner | 删除统一 `ResetHandles`，改为各 owner 的幂等 dispose |
| `BattleSessionFeature.HostInterfaces.cs` | Contracts | 将 Host 接口改为能力端口，不暴露 Feature 反向控制方法 |
| `BattleSessionFeature.HostBridges.cs` | Runtime composition | 迁移为显式对象依赖，禁止继续增加 lambda bridge |
| `BattleSessionFeature.OrchestratorHost.cs` | `SessionLifecycleCoordinator` | 已用逻辑、管线和 Runtime Resource 三组能力端口替代 18 项委托；后续继续迁移端口实现 owner |
| `BattleSessionFeature.EventsHost.cs` | `SessionEventPort` | 事件通知改为明确 publisher/port |
| `BattleSessionFeature.SubFeaturePipeline.cs` | `SessionPipeline` | 将 pipeline 视为独立装配对象，SubFeature 只注册 hook |

P0 的结束条件是 Session Runtime 能运行，但既有功能行为不变；此阶段允许保留兼容 wrapper，不要求立即删除所有 partial 文件。

### 4.2 批次 P1：迁移 Gateway、Transport 和 Replication

| 当前文件组 | 目标 owner | 风险 |
| --- | --- | --- |
| `GatewayRoom.cs` | `GatewaySessionRuntime` | 已完成：Feature 仅保留装配、plan 镜像和事件通知 facade |
| `GatewayPreparation.cs` | `GatewayPreparationRuntime` | 已完成：登录、创建/加入、task/CTS、anchors 和原子 plan 发布形成 owner 闭包 |
| `GatewayConnection.cs` | `GatewaySessionRuntime` | 已完成：registry、factory、Open、Tick、Attach/Detach 和 teardown 形成 owner 闭包 |
| `GatewayTimeSync.cs` | `GatewayClockSynchronizer` | 已完成：TimeSync loop、task/CTS、EWMA 和连续失败计数形成 owner 闭包 |
| `GatewayTimeSyncStats.cs` | `GatewayClockSynchronizer` / Diagnostics facade | owner 提供线程安全 estimate 与 anchor snapshot，Feature 保留 diagnostics 镜像 |
| `GatewayFrameTiming.cs` | `GatewayFrameTimingPolicy` | 固定步长和时钟来源 |
| `TransportFactory.cs` | `BattleReplicationRuntime` | 已完成 owner 正式化：Feature 仅保留复制业务消费和兼容访问器 |
| `Reconnect.cs` | `BattleReplicationRuntime` | transport 连接事件 ownership 已迁移；Feature 暂保留重连后的 world/reconcile reset 业务 |
| `NetworkCondition.cs` | `GatewaySessionRuntime` / Diagnostics | Gateway attachment 生命周期已迁移，公开控制器兼容面保留在 Feature |
| `SnapshotRouting.cs` | `BattleSnapshotRoutingRuntime` | 已完成：Context 写回、事件订阅和 pipeline dispose 已形成 owner 闭包 |

迁移原则：先将 transport/replication 的字段、事件订阅和 dispose 收进独立对象，再把 Feature 方法改为转发；最后删除同名 partial 中的字段。禁止先改 namespace 或 asmdef。

### 4.3 批次 P1：迁移 Simulation 和 Tick

| 当前文件组 | 目标 owner | 迁移动作 |
| --- | --- | --- |
| `ConfirmedAuthorityWorld.cs` | `BattleSimulationRuntime` | 已完成；Feature 仅转发 confirmed world start 参数 |
| `RemoteDrivenLocalSim.cs` | `BattleSimulationRuntime` | 已完成；Feature 仅转发 remote world start 参数 |
| `SimTick.Confirmed.cs` | `ConfirmedAuthorityWorldTickDriver` | 已完成；frame 状态和 driver 调度归 Simulation owner |
| `SimTick.RemoteDriven.cs` | `RemoteDrivenWorldTickDriver` | 已完成；frame 状态、driver 调度和 prediction bridge 归 Simulation owner |
| `SimDispose.cs` | `BattleSimulationRuntime` / `BattlePresentationSessionResources` | 已完成；Simulation 保留兼容 facade，Presentation owner 负责 confirmed view 分步清理，orchestrator 顺序不变 |
| `ConfirmedViewSideInstaller.cs` | `BattlePresentationSessionResources` | 已完成；installer 仅保留 confirmed render policy，不再创建或发布表现资源 |
| `TickLoopController.cs` | `SessionTickRuntime` | 保留固定步长协调，但不直接持有各世界细节 |

`TickLoopController` 应接受 `ISimulationTickPort`、`IReplayTickPort` 和 `IReplicationTickPort`，并增加 live tick budget、异常 delta 处理和 tick 统计。每个辅助世界的 tick 结果必须能够独立报告，不再通过 Feature 私有字段回写。Confirmed tick 通过显式 `FrameSnapshotDispatcher` 端口向 Presentation owner 投递，不再从 Simulation handles 间接获取表现 snapshot runtime。

### 4.4 批次 P2：迁移 Replay、Spectator 和诊断

| 当前文件组 | 目标 owner | 处理策略 |
| --- | --- | --- |
| `Replay*.cs`、Feature 内 Replay Control | `BattleReplayRuntime` | 已完成：保留 live/replay session 隔离与公共 Replay Control facade；独立 session 和 live writer 均归稳定 runtime owner |
| `Spectator.cs` | `SpectatorSessionRuntime` | 已完成：`Task` 启动入口、generation operation/CTS、push subscription 和 candidate world 均归稳定 owner；Feature 仅保留 facade |
| `Debug*.cs`、Provider 发布逻辑 | `BattleSessionDiagnostics` | 优先 session-scoped，静态 Provider 只保留兼容层 |
| `EditorHooks*.cs` | `EditorSessionLifecycleAdapter` | 与生产 Session Runtime 隔离，保留 UNITY_EDITOR 条件编译 |

静态 Provider 不在 P0/P1 强行删除。先增加 owner generation 和 reference-equality 清理测试，再迁移调用方到显式 diagnostics/replay port。

### 4.5 允许保留的 partial

以下 partial 不作为本轮强制删除对象：

- 代码生成要求的注册表 partial。
- `BattleViewFeature` 和 `ConfirmedBattleViewFeature` 的低耦合生命周期/Runtime override partial。
- 迁移阶段用于保持 Unity `.meta` GUID、生成项目文件和兼容入口的临时 wrapper，但必须标注迁移批次和删除条件。

## 5. `BattleContext` 正式化方案

### 5.1 目标拆分

`BattleContext` 继续作为 Feature Registry 中的兼容入口，但不再作为所有模块的完整可变对象。建议拆为以下对象：

- `BattleSessionContext`：Plan、Session、时间、frame、local player identity。
- `BattleInputContext`：HUD 输入状态、输入队列、record writer、提交能力。
- `BattleEntityContext`：EntityWorld、Lookup、Factory、Query、dirty entities。
- `BattlePresentationContext`：View VFX、View node、插值开关和表现绑定资源。
- `BattleSnapshotContext`：Snapshot dispatcher、pipeline、command handler。
- `BattlePlayerLoadoutStore`：运行期 loadout 覆盖、revision 和 effective loadout 计算。
- `BattleLocalActorResolver`：依赖逻辑世界 service 的 actor 查询和缓存。
- `BattleAimProjectionService`：HUD aim 到逻辑世界坐标的投影。

### 5.2 迁移顺序

1. 先将现有接口调用改为 `IBattleRuntimeContext`、`IBattleEntityContext`、`IBattleInputContext` 和 `IBattleSnapshotRoutingContext`。
2. 把 `ApplyPlayerHeroChanged` 和 loadout dictionary 移到 `BattlePlayerLoadoutStore`。
3. 把 `TryGetRuntimeWorld`、local actor resolve 和 aim resolve 移到明确服务。
4. 将 Entity/VFX/Presentation 资源从 Session Context 中移出，支持 confirmed 和 remote view context 独立创建。
5. 最后再决定是否保留一个组合型 `BattleContext` facade；其 facade 不得新增领域行为。

## 6. 目录和程序集迁移

目录迁移按现有 `ViewRuntimeDirectoryLayout` 的方向执行：

```text
Runtime/Game/Battle/Client/Session
Runtime/Game/Battle/Client/Networking
Runtime/Game/Battle/Client/Simulation
Runtime/Game/Battle/Client/Input
Runtime/Game/Battle/Client/Replay
Runtime/Game/Battle/Presentation
Runtime/Game/Battle/Contracts
Runtime/Game/Battle/Diagnostics
```

执行顺序固定为：

1. 物理目录迁移，保持 namespace 和 asmdef 不变。
2. 依赖收敛后再调整 namespace。
3. 最后才评估拆分 asmdef。

所有 Unity 文件迁移必须保留 `.meta` GUID；每批只迁移一个职责组，并在迁移后检查生成的 `.csproj`、asmdef 引用和 Unity 编译错误。

当前主 runtime asmdef 依赖面较大，不在第一阶段拆分。只有当 Contracts、Session、Networking、Simulation 和 Presentation 的反向引用清零后，才允许拆程序集。

## 7. 分阶段实施计划

### Phase 0：基线和约束

- 固化 40 个 Session 分部文件清单及行数快照。
- 建立 owner 映射表和资源所有权表。
- 记录当前测试基线、Unity 编译基线和关键端到端流程。
- 修复 `BattleFlowDesign.md` 编码问题，作为独立文档任务，不与代码迁移混批。

### Phase 1：Session Runtime 外壳

- 新建 `BattleSessionRuntime` 和明确的 `SessionRuntimeResources`。
- 将 `SessionOrchestrator` 的 Host 委托收敛为资源聚合端口。
- 保持 `BattleSessionFeature` 对外接口不变。
- 增加生命周期、重复启动、失败清理和重试清理测试。

### Phase 2：Gateway 与 Replication

- 迁移 Gateway room、连接、时间同步、transport、插值、可靠事件和重连。
- 清除 Feature 对 replication 字段的直接读写。
- 为 remote snapshot、reliable event、reconnect catch-up 和网络条件注入增加独立测试。

### Phase 3：Simulation 与 Presentation Session Resources

- 迁移 confirmed/remote world 的创建、tick、输入和销毁。
- 分离模拟世界资源与 View Event/表现快照资源。
- 验证 confirmed view 与 remote view 的生命周期和上下文隔离。

### Phase 4：Context 与 Input 正式化

- 拆分 `BattleContext` 的状态和服务。
- 将 actor resolver、aim projection、loadout store 从 Context 移出。
- 保留兼容 facade，逐步减少完整 `BattleContext` 在生产模块中的传递。

### Phase 5：Replay、Spectator、Diagnostics 和边界强制

- Replay 和 Spectator 改为显式 Session owner。
- 静态 Provider 改为兼容适配层，并增加多 Session 约束说明。
- 清理已无调用方的 partial wrapper。
- 依赖稳定后再拆 namespace 和 asmdef。

## 8. 测试门禁

每个迁移批次必须满足以下门禁：

### 8.1 生命周期门禁

- 启动成功后 Session、world、snapshot routing、view 和 replay 资源均可观测。
- 任意启动步骤失败时，资源按逆序释放。
- 某个 cleanup step 失败后，再次 Stop 只重试失败步骤。
- 重复 Start 不产生旧事件订阅、旧 CTS、旧 world 或旧 Provider 残留。
- `BattleContext` pool release 后不保留 Session、world、队列、writer、snapshot handler 和 dirty entity。

### 8.2 网络和模拟门禁

- Gateway room preparation、连接、时间同步可独立测试。
- Remote-driven、confirmed authority 和 interpolation 可单独启动、tick、停止。
- Snapshot routing、可靠事件 cursor、health evaluator 和 reconnect catch-up 不依赖具体 Feature 类型。
- live fixed-step 和 replay pump 的预算、顺序和 frame 语义保持不变。

### 8.3 表现门禁

- Presentation 不直接创建网络连接或驱动 Battle FSM。
- confirmed view 和 remote view 不共享可变的表现 Context。
- 快照应用、实体绑定、VFX、HUD 输入和 view interpolation 的现有验收场景保持通过。

### 8.4 结构门禁

- 新增 Session 业务字段不得放回 `BattleSessionFeature` partial。
- 新增资源必须声明 owner、创建点、释放点和失败恢复策略。
- Controller 不得通过 `Action`/`Func` 访问 Feature 私有业务方法，兼容期只允许使用已登记的 port。
- 每批迁移完成后，目标 partial 文件必须为空 wrapper 或被删除。

## 9. 第一批实施记录

- 新增 `BattleSessionRuntime`，实际持有一个 Session 的 `BattleSessionState`、`BattleSessionHandles` 和 `SessionOrchestrator`。
- `BattleSessionFeature` 改为 facade 装配 Runtime，并保留暂时性的兼容访问器。
- Runtime 拒绝空 host 和重复配置 orchestrator，相关单元测试已补充。
- 将原 `SessionLifecycleHostOptions` 的 18 项直接委托收敛为 `ISessionLogicPort`、`ISessionPipelinePort` 和 `ISessionRuntimeResourcesPort` 三组能力端口。
- `BattleSessionFeature` 仅作为端口兼容适配器，`SessionLifecycleHost` 不再复制 18 个委托字段；后续资源 owner 迁移可逐端口替换 Feature 适配实现。
- 现有启动失败、逆序清理、清理重试、重启和停止幂等测试继续作为第一批生命周期回归门禁。
- Runtime 项目构建通过（136 warnings、0 errors），UnitTests 项目构建通过（131 warnings、0 errors）。
- Unity EditMode 测试尚未实际启动，当前工程仍被无关 Shooter 包的既有 `CS0234` 编译错误阻塞；日志未发现本次 MOBA 文件对应的编译错误。

### 9.1 Snapshot Routing 正式化实施记录

- `BattleSessionRuntime` 现在稳定持有 `BattleSnapshotRoutingRuntime`，Snapshot routing 不再由 Feature 字段创建和管理。
- `BattleSnapshotRoutingRuntime` 持有本 generation 的 Context、Logic Session、FrameReceived handler、dispatcher、pipeline、command handler、routing instance 和网络 adapter，形成完整资源闭包。
- Build 在创建新 generation 前释放旧 generation；创建失败时执行同一幂等 Dispose 路径回滚已创建资源和事件订阅。
- Context 和 `BattleSessionHandles` 仅在引用仍指向当前 owner 资源时清理，旧 owner Dispose 不会覆盖后续 generation 或其他 owner 的替代绑定。
- `BattleSessionFeature.SnapshotRouting` 已收敛为兼容转发，不再保存 Snapshot controller 字段。
- 新增 Build/Dispose、重复 Dispose、重复 Build、稳定 owner 和 stale owner 引用隔离测试；测试 asmdef 增加 `AbilityKit.World.Snapshot` 直接依赖。
- 本批 Runtime 项目构建通过（155 warnings、0 errors），UnitTests 项目构建通过（131 warnings、0 errors）；警告均为当前工程既有告警。
- Unity EditMode 实际执行仍受无关 Shooter 包既有 `CS0234` 编译错误阻塞，本批未修改 Shooter 代码。

### 9.2 Gateway Connection 与 Room Client 正式化实施记录

- `BattleSessionRuntime` 通过一次性配置稳定持有 `GatewaySessionRuntime`；Feature 构造签名、公共 API、namespace 和 asmdef 均未改变。
- `GatewaySessionRuntime` 统一持有 registry lookup/create、connection factory、Open、Tick、NetworkCondition Attach/Detach、room client factory 和 teardown。
- Build 在创建新 generation 前执行幂等 Dispose；Open 或 room client 创建失败时复用同一 teardown 路径回滚 registry、connection、handles 和 attachment。
- Gateway handles 新增内部 connection owner token。即使 registry 按 role 向多个 generation 返回同一 connection，旧 owner Dispose 也不会解绑或清除 active generation。
- Registry Remove 前同时校验 owner token 与 registry 当前 connection 引用；handles 只由当前 token owner 清理，避免 stale owner 删除替代连接。
- `BattleSessionFeature.GatewayRoom` 已收敛为 Build/Tick/Dispose 协调转发；`GatewayConnection` 仅保留 Unity 生成项目兼容文件；`NetworkCondition` 仅保留公开控制器。
- 房间登录、创建/加入、preparation task、TimeSync、CTS 和 world-start anchors 在后续 9.3 批次迁入专属 owner；本条连接批次记录保留其阶段性边界。
- 新增 Build/Open/publish/Tick、重复 Dispose、失败回滚、重复 Build 和 stale owner 隔离测试；旧工厂注入测试改为通过真实 preparation 入口验证。
- 本批 Runtime 项目构建通过（136 warnings、0 errors），UnitTests 项目构建通过（135 warnings、0 errors）；警告均为当前工程既有告警。UnitTests 外部构建需临时补齐 Unity 生成项目遗漏的 `AbilityKit.World.Snapshot` 直接引用。
- Unity EditMode 实际执行仍受无关 Shooter 包既有 `CS0234` 编译错误阻塞，本批未修改 Shooter 代码。

### 9.3 Gateway Preparation 与 Clock Synchronization 正式化实施记录

- 新增 `GatewayPreparationRuntime`，统一持有 preparation task/CTS、generation 和 world-start anchors；GuestLogin、CreateRoom、JoinRoom 的取消 token 由同一 generation 贯穿。
- preparation 使用局部 `preparedPlan` 累积 session token、room id 和 world id，只在全部 await 与 generation guard 通过后一次性发布，旧 generation completion 不得泄漏部分 plan 或 anchor。
- 新增 `GatewayClockSynchronizer`，统一持有 TimeSync loop、task/CTS、EWMA estimate 和连续失败计数；取消不报告失败，连续失败达到阈值后才通知 Feature 事件端口。
- 每个 await、plan/anchor 发布点及 clock sample/failure 回调前均校验 generation、CTS token identity 与取消状态；callback 在 owner 锁外执行，避免 diagnostics 回读导致重入死锁。
- `GatewaySessionRuntime.CompletePreparation()` 停止 preparation 与 clock 工作并释放 connection，但保留 clock estimate 和 world-start anchors；完整 `Dispose()` 再清除 session data，且两条路径均保持幂等。
- `BattleSessionFeature.GatewayRoom`、`GatewayFrameTiming` 和 `GatewayTimeSyncStats` 已收敛为兼容 facade、plan/diagnostics 镜像和事件通知；旧 task/CTS/anchors 字段已从 Feature 与 handles 中移除。
- 新增 preparation 成功调用序列、取消后 late completion、重复启动 stale generation、首个 clock sample、失败阈值、stale clock callback 和聚合 owner 完成/销毁语义测试；异步等待均带 5 秒超时，避免测试进程永久挂起。
- 本批 Runtime 外部构建通过（114 warnings、0 errors），UnitTests 外部构建通过（135 warnings、0 errors）；Unity 生成项目仅用于外部编译验证，其 `.csproj` 被仓库忽略。
- Unity 2022.3.62f1 EditMode 定向执行已实际发起，但测试发现前被无关 Shooter 文件 `ShooterNetworkTransportOptionsFactory.cs` 的既有 `CS0234` 阻塞，因此没有生成测试结果 XML，不能宣称新增 NUnit 行为测试已执行通过。本批未修改 Shooter 代码。

### 9.4 Replication Owner 正式化实施记录

- 新增 `BattleReplicationRuntime` 并由 `BattleSessionRuntime` 稳定持有，统一管理 transport 四类事件绑定、Options 三个 callbacks、interpolation controller、replication pipeline、同步健康度、snapshot admission、authoritative snapshot state、reliable event cursor、pending reliable event queue、server ACK frame 和 pending state import。
- Build 先完成全部参数校验，再释放旧 generation；无效 Build 不改变当前有效 generation。重复 Build 通过统一幂等 Dispose 拆除旧 transport 绑定和 owner 状态。
- transport 事件使用 owner wrapper delegate，并在转发前同时校验 generation 与 transport reference；旧 transport 的 late callback 不得进入当前 Feature 业务路径。
- Options callbacks 安装前保存原值；Dispose 仅在当前值仍引用 owner 安装的 delegate 时恢复前值，避免 stale owner 覆盖外部或后续 generation 的替代 callback。
- `BattleSessionFeature.TransportFactory` 和 `Reconnect` 已收敛为 owner 组装、复制业务消费及兼容访问器；world snapshot 导入、reliable battle event 业务投递、checkpoint/ACK 持久化、full-state 请求和 reconnect 后 world/reconcile reset 暂由 Feature 保留。
- 新增 Build/Dispose、重复 Build、无效 Build 保留当前 generation、外部替换 Options 后 Dispose 不覆盖四项生命周期测试；测试 asmdef 已增加 `AbilityKit.Network.Battle` 直接依赖。
- 定向静态检查确认 transport 事件订阅和 Options callback 写入仅存在于 owner，Feature 不再保留 Hook/Unhook ownership；`git diff --check` 未发现 whitespace error。
- Runtime 与 UnitTests 外部构建验证已执行，但完整结果受无关 FrameSync Rollback `RollbackCoordinator.cs` 中 `CollectionsMarshal` 的三个既有 `CS0103` 阻塞；Unity 生成项目还未自动同步新增源码和 asmdef 引用。因此本批不能宣称完整构建或新增 NUnit 行为测试已通过，且未修改该无关阻塞代码。

### 9.5 Simulation Owner 正式化实施记录

- 新增 `BattleSimulationRuntime` 并由 `BattleSessionRuntime` 一次性配置和稳定持有，统一管理 Remote-driven 与 Confirmed-authority handles、world installer、双 world last-ticked frame、tick driver 调度和 prediction view bridge。
- `BattleSessionFeature.ConfirmedAuthorityWorld`、`RemoteDrivenLocalSim`、`SimTick.Confirmed`、`SimTick.RemoteDriven` 与 `SimDispose` 已收敛为兼容 facade；Feature 不再持有 world installer、prediction bridge 或 Simulation frame 字段，也不再直接调用 tick driver 和 `SessionSimRuntimeDisposer`。
- 双 world frame 状态保持独立；Remote state import 通过 Simulation owner 对齐 prediction frame，不再回写 Feature 私有字段。
- 首次启动失败按对应 world 资源闭包回滚：Remote 依次销毁 world、释放 input 和重置 frame；Confirmed 依次释放 view、销毁 world、释放 input/world resources 和重置 frame。原始启动异常保留，回滚失败时与 cleanup 异常聚合。
- 正常 teardown 未合并为单一 Dispose，继续保留 orchestrator 的 confirmed view、world destroy、confirmed world、remote world 分步顺序和失败重试语义；各分步清理保持幂等且不得交叉重置另一 world 状态。
- 新增稳定 owner、空/重复配置、双 world 启动与 frame 隔离、Remote/Confirmed 启动失败回滚、重复启动不重建或重置、重复释放与 world 隔离测试。
- 本批 Runtime 与 UnitTests 外部项目曾连续构建通过（0 errors）；补测后关闭项目引用的 UnitTests 顶层编译再次通过（21 warnings、0 errors）。当前完整项目引用重建被无关 `RecordIdHash.cs` 的三个 `CS0266` 和 `MobaProjectileService.cs` 的一个 `CS0103` 阻塞，本批未修改这些文件。
- 新增 NUnit 测试尚未通过 Unity Test Runner 实际执行，不能宣称行为测试已运行通过。新增 `BattleSimulationRuntime.cs.meta` 已登记固定 GUID，Unity 生成项目应由编辑器重新生成，不提交临时 `.csproj` 修改。

### 9.6 Presentation Session Resources 正式化实施记录

- 新增 `BattlePresentationSessionResources` 并由 `BattleSessionRuntime` 稳定持有，再注入 `BattleSimulationRuntime`；owner 统一持有 confirmed `BattleContext`、`ConfirmedViewSnapshotRuntime` 和 `ConfirmedBattleViewFeature`。
- `ConfirmedAuthorityWorldInstaller` 不再安装 view side；confirmed world runtime handles 已删除 View Context、snapshot runtime 和 feature 字段及其 bind/clear/dispose API，`BattleSessionHandles.ResetSessionResources()` 不会绕过 Presentation owner 释放表现资源。
- Confirmed world-bound event pipeline 继续归 Simulation：`ConfirmedViewEventPipeline`、event sink、snapshot adapter 和 trigger bridge 仍随 confirmed world teardown；Presentation owner 只管理独立表现 Context、snapshot routing 和 feature 生命周期。
- `BattleSimulationRuntime.DisposeConfirmedView()` 保持 Feature-facing compatibility facade；正常 teardown 继续遵守 snapshot routing、confirmed view、battle worlds、confirmed world、remote world 的既有 orchestrator 顺序，confirmed 首次启动失败仍按相同边界回滚并聚合 cleanup 异常。
- Confirmed tick options 新增显式 Presentation snapshot dispatcher，tick driver 不再从 confirmed world handles 间接读取 view snapshot runtime。factory 创建失败时会分别尝试释放 snapshot/context；cleanup 同时失败时保留原始创建异常并聚合全部清理异常。
- owner 的 detach、snapshot dispose 和 context dispose 分步执行；字段仅在仍引用本次捕获资源时清空，防止清理重入覆盖后续 generation。重复释放、disabled install、stale context replacement、稳定 owner 和双 View Context 隔离已补充生命周期测试。
- Runtime 完整外部构建通过（0 errors）；关闭项目引用重建的 UnitTests 顶层编译通过（21 warnings、0 errors）；定向 ownership 搜索无旧 handles 表现 API 残留，`git diff --check` 无 whitespace error，仅报告工作树既有 LF/CRLF 提示。
- 新增 NUnit 行为测试尚未通过 Unity Test Runner 实际执行，不能宣称测试已运行通过。新增 Presentation 目录、owner 文件及 `.meta` 已由 Unity 登记，生成 `.csproj` 已自动收录 owner 源文件。

### 9.7 Replay Runtime 正式化实施记录

- `BattleSessionRuntime` 现在稳定持有 `BattleReplayRuntime`；该 owner 组合既有 `BattleReplaySessionOwner` 复用独立 Replay session、checkpoint、seek 和 playback 算法，并统一持有 live recording writer。
- `BattleSessionFeature` 继续提供 `IBattleReplayControl` 公共 facade。Replay subfeature 通过显式 runtime contract 获取 owner；live/replay registry 仍隔离，Replay session 不发布或覆盖 live debug facade。
- `BattleContext.InputRecordWriter` 降为输入热路径镜像。writer replacement 使用 commit-on-success：旧 writer 清理失败时不发布候选并回收候选；双方清理均失败时按 ownership 顺序聚合异常。
- owner 仅在 dispose 成功后清引用，并以 reference equality 清理 Context 镜像；失败时保留 owner 状态供 orchestrator cleanup bitmask 后续重试，stale Context 或其他 runtime 的替代 writer 不会被清除。
- orchestrator 通过 `ISessionRuntimeResourcesPort.DisposeReplayRecordWriter()` 执行 writer teardown，不再直接读取 Context writer。旧 `BattleSessionReplayRuntime`、handles Replay 字段及无生产赋值点的 Replay driver 已删除。
- 新增 writer 替换、Context 迁移、stale 镜像、幂等释放、失败回滚、双重失败聚合、双 runtime 隔离及 orchestrator writer-step 重试测试；测试追加到已被当前 Unity 生成工程收集的既有测试文件。
- `AbilityKit.Game.Battle.Runtime.csproj` 构建通过（34 warnings、0 errors），`AbilityKit.Game.UnitTests.csproj` 构建通过（137 warnings、0 errors）；定向 ownership 搜索无旧 handles Replay 引用，`git diff --check` 无 whitespace error，仅有工作树 LF/CRLF 提示。
- 本批新增 NUnit 测试已通过外部项目编译，但尚未由 Unity Test Runner 实际执行，因此不宣称行为测试已运行通过。

### 9.8 Spectator Runtime 正式化实施记录

- `BattleSessionRuntime` 现在稳定持有 `SpectatorSessionRuntime`。Feature 的 Spectator partial 已收敛为 `Task` 启动、停止和 tick facade，不再持有 network client、CTS、push handler 或 world driver。
- 每次启动创建独立 generation operation，并由该 operation 统一持有 client、CTS、缓存 token 和 push handler。每个 await 后同时校验 generation、client 与 operation identity；停止或替代启动后的 late completion 不得发布旧 world。
- 订阅成功和追帧完成前，`SpectatorWorldDriver` 仅作为 candidate 存在；全部步骤成功后才提交为 active driver。启动失败、取消或 stale completion 会回滚订阅并释放 candidate world。
- `SpectatorWorldDriver` 已实现幂等 `IDisposable`。world dispose 失败时保留引用供后续 Stop 重试；成功后才清除内部状态，避免资源失去 owner。Feature detach 对各资源执行 best-effort cleanup，单项异常不会阻断 Replay、主 Session 和其余资源释放。
- 新增 8 个生命周期测试，覆盖延迟发布、订阅取消与 late completion、替代 generation、world dispose 失败重试、catch-up 取消、world factory 失败回滚、重复 Stop 和双 runtime 隔离。测试使用 Unity 兼容的 `UnityTest` coroutine 入口桥接异步主体，并保留原始异步异常。
- Unity 2022.3.62f1 EditMode 定向执行通过（8 passed、0 failed）；`AbilityKit.Game.Battle.Runtime.csproj` 构建通过（34 warnings、0 errors），`AbilityKit.Game.UnitTests.csproj` 构建通过（156 warnings、0 errors）。
- 定向 ownership 检查确认 driver 创建和 server push 订阅仅位于 `SpectatorSessionRuntime`，Feature 无旧 client/CTS ownership 残留；`git diff --check` 无 whitespace error，仅报告工作树 LF/CRLF 提示。

### 9.9 Session Diagnostics 正式化实施记录

- `BattleSessionRuntime` 现在稳定持有 `BattleSessionDiagnostics`，并向 Snapshot Routing、Gateway、Remote-driven input 和 Confirmed Authority publisher 注入同一 session-scoped owner；Feature detach 通过 best-effort cleanup 释放 diagnostics。
- jitter buffer、Gateway time-sync current/by-world 和 confirmed authority 的静态兼容发布统一收敛到 diagnostics owner。清理仅在 Provider 仍引用本 owner 发布对象时执行，stale Session dispose 不会覆盖新 Session 的诊断数据。
- synchronization health snapshot/report 通过 diagnostics facade 读取 `BattleReplicationRuntime` 状态，不转移 replication evaluator、transport、world 或 UI-bound Context/HUD/View 的 ownership。
- 新增幂等清理、stale owner 隔离、双 Session 独立发布和 health facade 四项生命周期测试；Unity 2022.3.62f1 EditMode 聚焦执行通过（4 passed、0 failed）。完整 `SessionOrchestratorLifecycleTests` 执行 51 项，其中 44 passed、7 failed；失败均为既有无关项（6 个异步测试在当前 Runner 下 NotRunnable，1 个 Presentation root 为空）。
- `AbilityKit.Demo.Moba.View.Runtime.csproj` 构建通过（153 warnings、0 errors），`AbilityKit.Game.UnitTests.csproj` 构建通过（175 warnings、0 errors）。定向静态检查确认四类 Provider 写入仅位于 `BattleSessionDiagnostics`，Snapshot Routing 生产构造使用稳定 diagnostics owner；`git diff --check` 无 whitespace error。

## 10. 风险和回滚

| 风险 | 防护 | 回滚方式 |
| --- | --- | --- |
| 生命周期顺序改变 | 先保持 `SessionOrchestrator` 顺序，增加调用序列测试 | 保留旧 Feature adapter，切回旧 composition |
| Unity `.meta` 丢失 | 物理移动保留 `.meta`，禁止重建 asset | 恢复目录映射，不改 GUID |
| 事件重复订阅 | generation、reference equality 和 subscriber count 测试 | 禁用新 owner，使用旧事件桥 |
| 远端插值行为回归 | 录制 frame/snapshot 序列做前后对比 | 回退 Replication adapter |
| 双世界资源交叉释放 | 每个 world owner 独立 dispose 和 fault injection | 恢复旧 handles cleanup |
| 静态 Provider 多会话污染 | 增加 owner identity 和兼容层告警 | 保留单会话 Provider，不扩大其使用范围 |
| asmdef 循环依赖 | 最后阶段才拆分程序集 | 回退 namespace/asmdef，不回退已验证的 owner 拆分 |

## 11. 验收标准

正式化完成必须同时满足：

1. `BattleSessionFeature` 不再拥有 Gateway、Replication、Simulation、Replay 和 Presentation 资源字段。
2. `BattleSessionFeature*.cs` 只剩门面、兼容 adapter 或明确标记的 Unity 生命周期 glue；业务 partial 数量降为零。
3. `BattleContext` 不再实现所有领域写入行为；loadout、actor resolve、aim projection 和 snapshot composition 均有独立 owner。
4. `SessionOrchestrator` 依赖明确的 runtime aggregate，而不是 18 项 Feature lambda。
5. 所有资源都有唯一 owner、创建路径、tick 路径和 teardown 路径。
6. 现有 Session、Gateway、Replay、Snapshot、Input、View 和多客户端验收测试全部通过。
7. 新增多 Session、双 View Context、重复启动、teardown 失败重试和静态 Provider 隔离测试并通过。
8. 目录、namespace 和 asmdef 依赖符合目标方向，且 Unity `.meta` GUID 未变化。
9. 文档中的状态、资源和行为边界与实际代码一致，不再存在“名义 Controller、实际 Feature owner”的描述偏差。

## 12. 第一批实际任务

第一批只做低风险基础设施，不改变运行行为：

1. 新建 owner 映射清单和资源生命周期表。已完成。
2. 新建 `BattleSessionRuntime` 组合对象及兼容 adapter。已完成。
3. 将 `SessionLifecycleHostOptions` 收敛为分组能力端口。已完成；具体为 `ISessionLogicPort`、`ISessionPipelinePort` 和 `ISessionRuntimeResourcesPort`。
4. 为 `BattleSessionRuntime` 增加启动、停止、失败、重复停止和清理重试测试。已完成。
5. 将 `BattleSessionFeature` 现有 partial 中的字段迁移逐项登记，禁止未登记字段继续增加。进行中；Snapshot Routing、Gateway Connection/Room Client/Preparation/Clock、Replication、Simulation、Replay、Spectator、Diagnostics 与 confirmed Presentation 已迁移。
6. 完成基线测试后再开始迁移 Gateway/Replication。已完成 Snapshot Routing、Gateway、Replication、Simulation、Replay、Spectator、Diagnostics 与 confirmed Presentation owner 闭包；Spectator 和 Diagnostics 新增生命周期测试已通过 Unity Test Runner 定向执行，其余历史批次的 Unity 行为验证状态以各实施记录为准。

第一批不修改 asmdef，不修改公共接口，不改变现有目录 namespace，也不删除 Unity `.meta` 文件。
