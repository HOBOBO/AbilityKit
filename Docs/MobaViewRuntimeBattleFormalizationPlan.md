# Moba View Runtime 战斗流程正式化优化计划

## 1. 文档目的

本文档针对 `com.abilitykit.demo.moba.view.runtime` 当前战斗流程实现进行正式化治理，重点解决 `BattleSessionFeature` 和 `BattleContext` 通过大量 `partial class` 聚合生命周期、网络、模拟、输入、回放和表现资源的问题。

本计划不是机械地将分部文件改名为普通类，而是以状态和资源所有权为迁移依据，逐步形成可测试、可替换、可回滚的 Session Runtime 结构。

## 2. 审计结论

### 2.1 规模和分类

截至 2026-08-12 的复核结果，`BattleSessionFeature` partial 组为 35 个文件、约 2226 行；`BattleContext` partial 组为 7 个文件、约 553 行。整个包仍有 53 个 `partial` 声明结果，但实际只归属于 7 个类型组，不能将声明数量直接等同于问题数量：

| 分类 | 当前判断 | 处理策略 |
| --- | --- | --- |
| 生成代码注册表 partial | 合理使用 | 保留，不纳入迁移 |
| `BattleViewFeature` / `ConfirmedBattleViewFeature` 生命周期 partial | 风险较低 | 暂保留，后续按 View Runtime Host 评估 |
| `BattleContext` partial | 职责聚合 | P1，拆为上下文、状态和服务 |
| `BattleSessionFeature` partial | 巨型门面和实际 owner | P0，按资源闭包迁移 |

补充量化如下：

| partial 类型组 | 文件数 | 约行数 | 复核结论 |
| --- | ---: | ---: | --- |
| `BattleSessionFeature` | 35 | 2226 | 字段 owner 已显著收敛，但仍有业务算法和多领域 runtime adapter，继续治理 |
| `BattleContext` | 7 | 553 | pooled context 仍包含输入、loadout、actor/world 查询、Entity、VFX 和 Snapshot 镜像 |
| `BattleViewFeature` | 3 | 183 | 共享资源已集中到 `ViewFeatureRuntimeHostBase`，保留并补 teardown/隔离验证 |
| `ConfirmedBattleViewFeature` | 3 | 169 | 同上，不抢占 P0 |
| `SharedSnapshotRegistry` | 2 | 101 | 生成代码要求，排除 |
| `SessionReplayController` | 1 | 100 | 单文件 `partial`，无生成原因时直接去除修饰符 |
| `BattleSnapshotRegistry` | 2 | 62 | 生成代码要求，排除 |

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

- 新增 `BattleReplicationRuntime` 并由 `BattleSessionRuntime` 稳定持有，统一管理 transport 四类事件绑定、Options 三个 callbacks、interpolation controller、replication pipeline、同步健康度、snapshot admission、authoritative snapshot state、server ACK frame 和 pending state import。
- Build 先完成全部参数校验，再释放旧 generation；无效 Build 不改变当前有效 generation。重复 Build 通过统一幂等 Dispose 拆除旧 transport 绑定和 owner 状态。
- transport 事件使用 owner wrapper delegate，并在转发前同时校验 generation 与 transport reference；旧 transport 的 late callback 不得进入当前 Session 业务路径。
- Options callbacks 安装前保存原值；Dispose 仅在当前值仍引用 owner 安装的 delegate 时恢复前值，避免 stale owner 覆盖外部或后续 generation 的替代 callback。
- `BattleReplicationRuntime` 组合 `AuthoritativeStateRecoveryRuntime` 与 `ReliableBattleEventDeliveryRuntime`：前者统一持有 full baseline admission、world import、removed actors、prediction rebase、full-state request 和 reconnect timeline reset；后者统一持有 reliable cursor、pending batch、业务投递、ACK retry 与 checkpoint persistence。
- 两个 recovery owner 在异步 completion 前同时校验 generation、transport reference 与 cursor identity。可靠事件只在业务 sink 成功后 commit，并仅在 ACK 成功后确认和持久化；旧 generation 的 ACK、checkpoint 和 full-state completion 不得写入 active Session。
- `BattleSessionFeature.TransportFactory` 和 `Reconnect` 已收敛为 owner 组装、兼容访问器与 host bridge，不再持有 recovery 状态机或接收 transport callback 后进入 Feature 私有业务方法。
- 新增 Build/Dispose、重复 Build、无效 Build 保留当前 generation、外部替换 Options 后 Dispose 不覆盖，以及 full baseline、导入失败、removed actors、reconnect reset、ACK retry、checkpoint commit、pending batch ordering 和 stale completion 隔离测试。
- Runtime 外部构建通过（134 warnings、0 errors），UnitTests 外部构建通过（142 warnings、0 errors）；警告均为当前工程既有引用冲突、nullable 和未使用测试事件。
- Unity 2022.3.62f1 EditMode 聚焦执行通过：`AuthoritativeStateRecoveryRuntimeTests` 5 passed、0 failed，`ReliableBattleEventDeliveryRuntimeTests` 5 passed、0 failed。Unity 已自动收录新增测试源码，生成 `.csproj` 不作为手工维护的正式改动。
- owner-level authoritative recovery 测试通过可替换 world recovery port 验证调用契约与 Session 状态；真实 `MobaLogicWorldStateImporter` 的实体级导入细节继续由其所属 runtime 测试负责，不在本批重复声明覆盖。

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

## 13. 2026-08-12 剩余职责审计与优先级路线

### 13.1 判定原则

本轮不按文件行数或 `partial` 语法机械排序。优先级由以下因素共同决定：

1. 是否在 Feature 或 pooled Context 中保留跨领域业务算法，而不只是兼容转发。
2. 是否同时修改网络、模拟、输入、持久化或表现状态，导致生命周期和失败恢复无法独立验证。
3. 是否拥有 CTS、Task、事件订阅、Unity 对象或可替代 generation 的资源。
4. 是否存在 concrete Feature 反向依赖、静态可变发布点或旧 owner 清理新 owner 的风险。
5. 拆分后能否形成稳定 port，并为后续 Context、Lobby 或程序集治理解除前置阻塞。

本轮明确区分三种状态：

- **已完成 owner 化**：资源字段、订阅和 dispose 已归稳定 owner；不因兼容 facade 存在而重复实施。
- **过渡 facade**：Feature 只转发调用或投影只读状态，可在调用方迁完后清理。
- **未治理业务残余**：仍含准入、恢复、世界写入、策略计算或跨域状态修改，必须继续迁出。

### 13.2 P0：先消除战斗 Session 和 Context 的跨域业务残余

| 顺序 | 当前对象/文件组 | 仍混合的职责 | 目标边界 | 前置依赖 | 完成门禁 |
| ---: | --- | --- | --- | --- | --- |
| P0-1 | `BattleSessionFeature.TransportFactory.cs` + `Reconnect.cs` | 快照准入后的 world import、removed actor 应用、可靠事件 cursor/恢复队列/ACK/checkpoint、full-state 请求、重连 timeline reset、同步健康采样和表现插值投影 | 新建 `AuthoritativeStateRecoveryRuntime` 与 `ReliableBattleEventDeliveryRuntime`；health sampling 归 `BattleReplicationRuntime` 或专属 `SynchronizationHealthRuntime`；表现投影通过最小 port 注入。Feature 只组装和发布公共事件 | 复用已完成的 `BattleReplicationRuntime`、`BattleSimulationRuntime`、`BattleSessionDiagnostics`；先定义 world import、checkpoint、presentation 三个最小端口 | transport callback 不再进入 Feature 私有业务方法；旧 generation ACK/checkpoint/full-state completion 不得写入新 Session；world import、removed actors、可靠事件断点恢复、重连和 health tuning 有独立测试 |
| P0-2 | `BattleContext.Input.cs` + `BattleContext.Debug.cs` 的 prediction 控制镜像 | HUD buffer、input queue/writer、player-to-actor 映射、world service 查询、Unity aim 坐标投影和 prediction tuning 控制混在 pooled Context | `BattleInputRuntime` 持有 HUD/input 提交闭包；`BattleLocalActorResolver` 只做身份到 actor/world position 解析；`BattleAimProjectionService` 只做 aim projection；prediction stats/control 由 Replication/Input owner 通过只读 port 暴露 | 先为现有 HUD、submitter、aim preview 建立兼容接口；不先删除 `BattleContext` facade | pool release 不残留输入或控制端口；resolver 可用 fake world 独立测试；aim projection 无 Context 依赖；HUD click/aim 行为、record writer 和 local actor 切换回归通过 |

P0 固定执行顺序为 P0-1 后 P0-2。原因是 Context 中 prediction/health 控制目前仍被 Session 的 synchronization health 路径反向读取，先收敛 Replication/Recovery 边界才能避免 Input owner 再次依赖 Feature。

#### P0-2 Input 与 Prediction 正式化实施结果

- `BattleSessionRuntime` 现在稳定持有 `BattleInputRuntime` 与独立的 `BattlePredictionRuntime`。前者统一持有 HUD input buffer、local input queue 和 gameplay input submission closure；后者原子持有 prediction stats、reconcile 与 tuning ports。
- `BattleLocalActorResolver` 仅通过最小 resolution port 处理 cached actor、player-to-actor 映射和 world position 查询；`BattleAimProjectionService` 只接收 actor position、aim offset 与方向，不依赖 pooled `BattleContext`。
- `BattleContext.Input` 和 `BattleContext.Debug` 已降为兼容 facade。prediction 四个端口只读暴露，生产代码不再直接赋值；confirmed authority diagnostics 清理不再跨职责修改 remote prediction 状态。
- Input 与 Prediction owner 均记录当前 Context identity。rebind 先清除上一 generation 的瞬态状态或端口，unbind 使用 reference equality，旧 Context 的延迟清理不会覆盖替代 generation。
- pooled Context reset 统一释放 Input 与 Prediction binding；HUD buffer、queue frame/batch 和 prediction ports 不会跨池化生命周期残留。record writer 继续由 Replay owner 管理，Input submission 只保留写入和提交兼容路径。
- 新增 6 项 owner 测试，覆盖 cached/remapped actor resolution、无 Context aim projection、Input rebind/reset、pool release 和 Prediction stale unbind；既有 HUD bridge 14 项与 projection 3 项一并回归。
- `AbilityKit.Demo.Moba.View.Runtime.csproj` 构建通过（134 warnings、0 errors），`AbilityKit.Game.UnitTests.csproj` 构建通过（142 warnings、0 errors）；警告均为工程既有引用冲突、nullable 和未使用测试事件。
- Unity 2022.3.62f1 EditMode 聚焦执行通过（23 passed、0 failed）：`BattleInputRuntimeTests` 6 项、`BattleHudInputEventBridgeTests` 14 项、`BattleHudInputProjectionTests` 3 项。定向 ownership 搜索无生产代码 prediction facade 赋值残留，HUD state 与 local queue 的生产 ownership 仅位于 `BattleInputRuntime`；`git diff --check` 无 whitespace error，仅报告工作树 LF/CRLF 提示。

### 13.3 P1：收敛真实 owner 和跨 generation 资源边界

P1 只处理同时拥有业务状态与资源生命周期的对象。每个子批次独立提交、独立回归，不并行改动相邻 owner 的公共契约。

| 顺序 | 当前对象/文件组 | 当前问题 | 目标边界 | 完成门禁 |
| ---: | --- | --- | --- | --- |
| P1-1 | `FormalLobbyFeature.cs` | 1417 行内同时处理 attach generation、异步 operation、房间目录、自动准备/开战策略、Battle entry、presentation state 和完整 IMGUI | `FormalLobbyRuntime` 持有 lifetime、operation task/generation 和命令状态；`LobbyRoomDirectoryRuntime` 持有刷新事务；`LobbyAutomationPolicy` 只做纯计算；`LobbyBattleEntryCoordinator` 持有一次性 entry gate；renderer 只消费不可变 presentation snapshot | detach 或替代 attach 后的 late completion 不更新 active state；目录刷新、自动创建/准备/开战和 battle entry 可用纯 C# 测试；renderer 不直接调用 Gateway 或 Controller |
| P1-2 | `GatewayMultiplayerRoomSession.cs` | 906 行的 command adapter 同时持有 auth token、membership、权威快照写入、reliable checkpoint 和 wire/domain mapping；同文件还包含 snapshot provider | 先物理分离 `ClientRoomSnapshotProvider` 和无状态 `GatewayRoomProtocolMapper`，再建立 `GatewayRoomMembership` 与 `MobaReliableBattleEventCheckpointStore`；Session 只保留命令调用顺序与成功后的原子 commit | membership 只在权威成功后 commit；leave/restore 失败不泄漏部分状态；checkpoint 可独立于 Session 生命周期测试；mapping 使用表驱动测试覆盖空值、边界值和协议错误 |
| P1-3 | `MultiplayerRoomFlowController.cs` | 已增长到 1217 行；约前 409 行是 contracts，Controller 还同时持有状态转移、stage task/CTS/generation、asset loader、loading progress、恢复和 created-room ownership，旧“总体内聚、只拆 DTO”判断已失效 | `MultiplayerRoomFlowContracts` 承载 enum/DTO/spec/result；`MultiplayerRoomStageRuntime` 持有 stage task/CTS/generation；`MultiplayerAssetLoadingRuntime` 持有 loader、progress、resume/cancel；Controller 保留状态转移、命令编排和 public API | 先补 characterization tests 固化 create/join/restore/loading/leave 序列；stale stage completion 不提交状态；loading cancel/resume 不重复释放 lease；拆分前后状态、失败码和公开事件序列一致 |
| P1-4 | `BattleContext.Runtime.cs`、`Entity.cs`、`Snapshot.cs` | pooled Context 仍聚合 loadout、Session identity/clock、ECS Entity、VFX/Presentation 和 Snapshot compatibility state | 第一批只迁 `BattlePlayerLoadoutStore`；第二批拆 `BattleEntityContext` 与 `BattlePresentationContext`；最后确认 Snapshot owner 调用方后将字段降为只读 facade 或删除。不要在同一批重写 Context 全部接口 | loadout revision/effective loadout 与 pool reset 测试通过；remote/confirmed Context 不共享可变 Entity/VFX；旧 Context 或 stale owner 不清除替代 binding；完整 Context pool 门禁保持通过 |
| P1-5 | `BattleLoadingScreenFeature.cs` + Session asset lease | 454 行内同时负责 concrete Session 查找、coordinator、lease adoption、Flow callback、状态和 IMGUI，asset lease ownership 跨 Feature 隐式转移 | 引入最小 `IBattleAssetLoadSessionPort`；由 Battle scope 或 `BattleAssetLeaseOwner` 原子接收 lease；Loading Feature 只绑定 presenter、状态 snapshot 和 retry/cancel 命令；manifest adapters 独立文件 | lease 最多成功转移一次；attach/detach、retry/cancel、adopt 失败和 late completion 均无泄漏；Loading 不依赖 concrete `BattleSessionFeature` |
| P1-6 | `BattleSessionFeature.World.cs` | world root 创建/销毁、serializer installer 和配置路径解析仍混在 Session facade | world composition 归 bootstrap/factory；serializer 安装归应用启动 adapter；路径解析归配置服务 | Session facade 不直接解析文件路径或安装全局 serializer；world 创建失败按逆序清理且可重试；公共 Session API 不变 |

P1 固定顺序为 Lobby、Gateway Session、Room Flow、Context、Loading、World。前三项构成大厅到房间状态机的连续链路，但仍必须分批提交：先稳定上游 operation 和 membership commit，再拆 Controller 的 stage/asset owner。Context 与 Loading 不与该链路混批。

### 13.4 P2：删除过渡 facade 并收敛应用生命周期边界

| 顺序 | 当前对象/文件组 | 处理方案 | 完成门禁 |
| ---: | --- | --- | --- |
| P2-1 | `BattleSessionFeature*.cs` | 按下方清单删除空 wrapper 和已无调用方的 facade；调用方先迁到稳定 runtime port，不得为了减少文件数把业务重新合并回 Feature | Feature 最终只保留公共 Session facade、Unity lifecycle glue 和 composition；业务 partial 数量为零；新增业务字段禁止进入任何 Feature partial |
| P2-2 | `GatewayFrameTiming.cs`、`GatewayTimeSyncStats.cs` | 纯策略移入 `GatewayFrameTimingPolicy`；diagnostics DTO builder 只依赖 Clock/Diagnostics snapshot，不读取 Feature 字段 | 时钟来源、safety margin、frame clamp 和 by-world stats 有纯 C# 测试 |
| P2-3 | `MultiplayerGatewayEntryModule.cs` | `MultiplayerGatewayRecoveryRuntime` 持有 reconnect generation、room restore 和 server push refresh；Entry Module 仅装配 client、session、controller 和 recovery owner | 多次断线、过期 completion、detach 后 push、restore failure 和 reconnect exhausted reset 有测试；Entry detach 后无订阅或 CTS 残留 |
| P2-4 | `BattleDebugOnGUIFeature.cs`、`BattleFlowDebugProvider` | `BattleDebugPublicationOwner` 负责 Context/HUD/View 兼容发布，`IBattleDebugCommandService` 负责换英雄、生成单位、重置 CD 和 AI 控制；IMGUI 只消费 debug snapshot 与 command facade | development-only 边界明确；stale publisher 不清空新发布；非开发构建不创建命令资源；命令服务可无 IMGUI 测试 |
| P2-5 | `GameEntry.cs`、`GameEntryRuntimeGuiBridge` | 将 GUI bridge 和本地 debug UI 移出 Entry 文件；静态 `Instance` 只保留已有兼容访问面 | `GameEntry` 只负责 ModuleHost、应用 composition 和 Unity 根生命周期；GUI detach 不影响 Runtime owner |
| P2-6 | `SessionReplayController` | 搜索确认无生成器、条件编译或第二声明后移除单文件 `partial` 修饰符 | 全仓第二声明为零，Runtime 和测试项目编译通过 |
| P2-7 | `BattleViewFeature` / `ConfirmedBattleViewFeature` partial | 允许保留 lifecycle/runtime 物理分部；只补双 Context、重复 attach/detach 和 stale presentation owner 隔离验证 | `ViewFeatureRuntimeHostBase` 继续作为唯一资源 host；concrete partial 不新增 owner 字段 |

#### 13.4.1 `BattleSessionFeature` partial 收尾分类

分类依据是调用关系和 ownership，不是文件长度。删除动作执行前必须再次全仓搜索声明和调用方。

| 分类 | 当前候选 | 动作 |
| --- | --- | --- |
| 可直接删除的空壳 | `GatewayConnection.cs`、`GatewayPreparation.cs`、`GatewayTimeSync.cs`、`Reconnect.cs` | 若文件只剩 namespace、空声明或注释，连同对应 `.meta` 一起通过 Unity 感知的移动/删除流程清理；不得保留“防止项目生成变化”的空 partial |
| 调用方迁移后删除的 facade | `Accessors.cs`、`DispatcherDispose.cs`、`SnapshotAccessors.cs`、`SnapshotRouting.cs`、`ConfirmedAuthorityWorld.cs`、`RemoteDrivenLocalSim.cs`、`SimTick.Confirmed.cs`、`SimTick.RemoteDriven.cs`、`SimDispose.cs`、`NullRegistries.cs` 以及 Gateway/Replay/Spectator 的薄转发文件 | 先将调用方改依赖 `BattleSessionRuntime` 或对应最小 port，再删除 wrapper；一次只清理一个领域并执行定向测试 |
| 暂时必须保留 | `BattleSessionFeature.cs`、`Lifecycle.cs`、`PhaseAccessors.cs` 以及仍承担 composition、公共接口或 Unity/editor lifecycle 的文件 | 允许继续使用 partial 作为物理组织，但不得拥有 Gateway、Replication、Simulation、Input、Replay、Spectator 或 Presentation 资源；依赖收敛后再合并最小 facade |

目标不是把约 40 个文件一次压成一个大文件，而是删除无行为碎片，使剩余文件一一对应公共 facade、生命周期或 composition。任何 wrapper 只要仍修改业务状态、持有 CTS/Task/订阅或执行 cleanup，就不能按“薄 facade”删除，必须先回到 P1 owner 迁移流程。

### 13.5 P3：千行文件和表现代码的物理治理

P3 不改变 ownership 与公共行为，只降低导航、评审和测试维护成本。不得与 P1 owner 拆分放在同一提交。

| 顺序 | 当前对象/文件组 | 建议物理边界 | 完成门禁 |
| ---: | --- | --- | --- |
| P3-1 | `BattleHudAimPreview.cs`（约 635 行） | 按 coordinator、position resolver、object factory、render object 拆文件，保持同一表现职责链和现有 namespace | 无新增 service locator 或 owner；HUD aim preview 的显示、更新、取消和对象销毁测试保持通过 |
| P3-2 | `GatewayRoomClient.cs`（约 690 行） | 在 P1-2 mapper 抽取后，按 request transport、response validation 和 operation groups 物理组织；Client 仍是 wire adapter | op-code、request/response、取消和错误映射行为不变；不复制 protocol DTO |
| P3-3 | 千行测试 fixture | `SessionOrchestratorLifecycleTests` 按 startup/cleanup/gateway/simulation fixtures 拆分；`BattleReplaySessionOwnerTests` 将 Spectator tests 独立；共享 fake 仅在确有复用时提取 | 测试数量和断言语义不减少；fixture 不跨领域暴露可变状态；生产代码重构不与测试物理拆分混批 |

#### 13.5.1 P1-P3 实施结果

- P1 已按 Lobby、Gateway Session、Room Flow、Context、Loading 与 world composition 顺序完成首轮 owner 迁移。新增 owner 保持原 public API、namespace、协议映射、取消语义和 Unity 资源 GUID；但 `FormalLobbyFeature`、`BattleLoadingScreenFeature` 和 `BattleContext` 仍保留兼容编排或跨域 facade，不能据此认定表现流程已经完成收口。
- P2 已完成 Gateway recovery、部分 Session owner、development debug publication、Game Entry GUI bridge 等基础治理，但兼容面收尾尚未完成。当前 `BattleSessionFeature` 仍分布在 35 个 partial 文件、约 1659 行，包含 composition、生命周期、公共 facade、薄转发及少量待迁行为；`SessionReplayController` 仍是单文件 partial；`BattleDebugOnGUIFeature` 的命令服务与 IMGUI 仍位于同一文件。因此 P2 状态修正为“owner 迁移部分完成，partial 与表现边界待 P4/P5 收尾”。
- P3-1 已将 HUD aim preview 按 coordinator、position resolver、object factory 和 render object 物理拆分；P3-2 已将 Gateway room client 按 loading、room operations、state sync 和 response mapping 分部组织，wire adapter 的公开入口、op-code 和 DTO 映射保持不变。
- P3-3 已将 Session 聚合 fixture 拆为 simulation、gateway、startup/cleanup、diagnostics/replication 四个顶层 fixture，共保留 49 个测试特性和 6 个 startup failure source cases；Replay fixture 保留 22 个测试方法及 3 个额外 `TestCase`，8 个 Spectator `UnityTest` 已迁入独立 fixture。各 fixture 的可变 fake 均为私有，未建立跨领域共享状态。
- 原 Session 与 Replay 测试资源分别保留 GUID `70475a221a024bf98f2c5a0a13237af5` 和 `9934cb0f90371944980b768fa6e711e7`；四个新测试资源 GUID 在 `Unity/Packages` 范围内各出现一次。P1-P3 未跟踪文件 whitespace 门禁覆盖 83 个文件并通过，受影响 tracked diff 的 `git diff --check` 通过。
- 当前 Unity 2022.3.62f1 batchmode 在测试发现前被工作树既有编译错误阻断：MOBA 侧缺失 `UnityJsonSettingsBootstrap`，host.network 侧缺失 `IAsyncHostNetworkRequestHandler`。Unity 未生成本批测试结果 XML；生成的 `AbilityKit.Game.UnitTests.csproj` 也尚未收录新增 fixture，且外部构建同时受陈旧项目输入和既有依赖缺失阻断。因此 P3 的物理拆分与静态门禁已完成，但不得宣称新增 fixture 已由 Unity 编译或定向测试通过。

### 13.6 明确保留和观察项

- `BattleSnapshotRegistry`、`SharedSnapshotRegistry`：代码生成所需的合理 partial，保持不动。
- `BattleViewFeature`、`ConfirmedBattleViewFeature`：当前由 `ViewFeatureRuntimeHostBase` 统一持有资源，允许保留 lifecycle/runtime partial，但禁止新增 owner 字段。
- `GatewayRoomClient`：当前五个 partial 仍围绕同一 wire adapter，按 loading、room operation、state sync 和 response mapping 组织；只要不持有跨 operation 的业务 owner，可保留这种物理分部。
- `BattleReplaySessionOwner`、`ClientRoomStore`、`FeatureScheduler`、`BattleScopeManager`：没有仅凭规模即可判定的跨域 owner 问题，维持观察，不做预防性重构。
- asmdef 和 namespace 拆分继续后置。只有 Contracts、Flow、Session、Presentation 的反向引用完成收敛，才单独设计程序集迁移批次。

### 13.7 推荐实施批次

1. **P1-1 Lobby Policy/Presentation**：先提取纯策略和 presentation snapshot builder，以 characterization tests 固化现有自动化判定。
2. **P1-1 Lobby Runtime**：再迁 operation generation、room directory和 battle entry gate，最后缩小 Feature 与 IMGUI。
3. **P1-2 Gateway Session**：先移动 mapper/provider，再迁 membership/checkpoint；全程保持 `IMultiplayerRoomSession` 不变。
4. **P1-3 Room Flow Contracts**：先物理拆 contracts 并建立状态机 characterization baseline，不改变 Controller 行为。
5. **P1-3 Room Flow Owners**：依次迁 stage runtime 与 asset loading runtime；不同时修改状态枚举或 public result。
6. **P1-4 Context Resources**：按 loadout、Entity、Presentation、Snapshot facade 顺序迁移，每次只处理一个 pooled resource closure。
7. **P1-5/P1-6 Scope Resources**：先收敛 asset lease，再迁 world composition；分别验证失败回滚和 cleanup retry。
8. **P2 Compatibility Cleanup**：按领域删除 `BattleSessionFeature` wrapper，然后处理 Gateway recovery、Debug 和 Entry 生命周期。
9. **P3 Physical Split**：最后处理表现大文件、wire client 和千行测试 fixture；不作为架构完成的前置条件。

以上为原 P1-P3 执行顺序和历史记录。当前工作树的后续执行基线以 13.8 为准。

### 13.8 P4-P5 表现流程与 partial 收尾顺序

后续不在“先优化表现层”与“先拆大文件/partial”之间二选一。统一规则是：先迁移错误 ownership，再删除失去职责的 facade，最后做物理拆分。禁止用新增 partial 掩盖跨域聚合。

| 优先级 | 批次 | 当前对象 | 先做什么 | 完成标准 |
| ---: | --- | --- | --- | --- |
| 1 | P4-1 | `BattleSessionFeature*.cs` | 先按 runtime/composition、公共 facade、生命周期、薄转发、空壳五类重新登记；优先迁出仍修改 Gateway、Simulation、Snapshot 或 cleanup 状态的行为，再删除空壳和无调用方 wrapper | 业务行为不再跨 partial 隐式共享字段；剩余文件只对应公共 facade、composition 或生命周期；不得把 35 个文件直接合并成新巨型 Feature |
| 2 | P4-2 | `BattleDebugOnGUIFeature.cs`、`GameEntry.cs` 中本地 debug 入口 | 将 publication owner、只读 debug snapshot/query、`IBattleDebugCommandService` 和 IMGUI renderer 分开；换英雄、生成单位、重置 CD、AI 控制不得由 GUI 类直接解析 world service | 命令服务可在无 IMGUI、无 `GameEntry.Instance` 的纯测试中运行；Feature 只 attach/detach publication 并绘制 snapshot；development-only 创建边界明确 |
| 3 | P4-3 | `BattleLoadingScreenFeature.cs` | 将 phase attachment/runtime generation、lease transfer transaction、不可变 loading snapshot、renderer 和 manifest adapters 分离；保持 `IBattleAssetLoadSessionPort` 与取消/错误语义 | late completion 不发布旧状态；lease 仅成功转移一次；renderer 不接触 coordinator、Session 或 Flow；adopt failure/cancel/detach 可独立测试 |
| 4 | P4-4 | `FormalLobbyFeature.cs` | 在已有 runtime/policy/renderer 基础上继续迁出 room-store subscription owner、自动化命令 coordinator、battle transition 和 scene exit；Feature 只解析 phase scope、生成 snapshot、转交命令和绘制 | IMGUI callback 不直接执行业务异步流程；subscription generation 与 operation generation 各有唯一 owner；大厅到战斗跳转可无 GUI 测试 |
| 5 | P4-5 | `GameEntry.cs`、`MultiplayerGatewayEntryModule.cs` | 收敛应用根 composition、可靠检查点 flush、GUI bridge 和 Gateway recovery 的依赖方向；仅保留 Unity 根生命周期胶水 | Entry 不持有具体 debug controller；Module detach 后无 CTS、订阅或恢复 task；表现模块不能反向操纵应用根对象 |
| 6 | P5-1 | `BattleContext*.cs` | 按调用方逐项删除已被 Input、Prediction、Entity、Presentation、Snapshot owner 替代的可变 facade；保留必要的只读兼容投影 | 七个 partial 不再作为跨域 service locator；pool reset 只调用明确 owner；stale binding 不清理替代 generation |
| 7 | P5-2 | `SessionReplayController` 与 `BattleSessionFeature` 收尾 | 搜索第二声明和条件编译后移除单文件 `partial`；将剩余一两行转发合并到对应最小 facade 或直接删除，保留 `.meta` GUID 约束 | 单文件 partial 为零；空 partial 为零；每个剩余 partial 有可陈述的独立物理职责 |
| 8 | P5-3 | 大型测试 fixture 与其他千行文件 | 仅在生产 ownership 稳定后按领域做物理拆分；共享 fake 保持不可变或 fixture-local | 测试特性、case source 展开和断言语义不减少；不与生产 ownership 重构混批 |

`FormalLobbyFeature` 虽然当前仍约 1235 行，但排在 Debug 与 Loading 之后：它已有 runtime、directory、automation policy、entry coordinator 和 renderer 边界，剩余问题主要是编排收口；Debug 与 Loading 仍直接跨越表现、命令和资源生命周期，依赖反向风险更高。`BattleSessionFeature` 排第一也不是为了减少文件数，而是先建立后续改动依赖的稳定 Session port，避免 Debug、Loading 和 Lobby 再次依赖 concrete Feature。

每批继续遵守 generation identity、commit-on-success、reference-equality cleanup、幂等 Dispose、失败重试和 Unity `.meta` GUID 保留规则。P4 owner 迁移必须执行 Runtime/UnitTests 构建与受影响 Unity 行为测试；P5 物理收尾至少执行受影响项目构建、测试数量/GUID 静态门禁和 `git diff --check`。

### 13.9 P4-P5 实施结果

P4-P5 已按 13.8 的顺序完成。实现过程中保持既有 public port、协议行为、取消语义、错误映射、资源所有权和 teardown 顺序不变；迁移重点是明确 owner 和依赖方向，而不是继续扩大 `BattleSessionFeature`、`BattleContext` 或入口类的聚合职责。

- **P4-1：`BattleSessionFeature` 收敛。** 按 runtime/composition、稳定公共 facade、生命周期和薄转发重新整理残余 partial，迁出仍修改 Gateway、Simulation、Snapshot 或 cleanup 状态的行为，使剩余入口只承担稳定 port、composition 和生命周期胶水。
- **P4-2：Debug 职责分离。** 将 debug publication、只读 query/snapshot、`IBattleDebugCommandService` 和 IMGUI renderer 分开。换英雄、生成单位、重置 CD、AI 控制等命令不再由 GUI 直接解析 world service，development-only 创建边界保留在明确的装配入口。
- **P4-3：Loading 职责分离。** 分离 phase runtime、lease transfer transaction、不可变 loading snapshot、renderer 与 manifest adapter；late completion 不再发布旧状态，lease 只在成功路径转移一次，取消、adopt failure 和 detach 路径保持可独立验证。
- **P4-4：FormalLobby 收口。** 将 room-store subscription、自动化命令、battle transition 和 scene exit 的 owner 从表现编排中收敛出来；IMGUI callback 只负责转交命令和绘制 snapshot，不直接执行业务异步流程，并分别维护 subscription generation 与 operation generation。
- **P4-5：根生命周期收敛。** 收敛 `GameEntry` 与 `MultiplayerGatewayEntryModule` 的 application composition、可靠 checkpoint flush、GUI bridge 和 Gateway recovery 依赖。入口保留 Unity 根生命周期胶水，不持有具体 debug controller；module detach 后清理 CTS、订阅和恢复 task，表现模块不反向操纵应用根对象。
- **P5-1：可变 facade 清理。** `InputRecordWriter` 改为私有 backing field 加只读 facade，所有权变更收敛到 `BattleReplayRuntime`；Entity/Presentation 相关 public setter 因 Editor tests 位于独立程序集而保留，避免以内部可见性改动破坏现有测试边界。`BattleContext` 不再作为跨域 service locator，pool reset 继续调用明确 owner，stale binding 由 generation identity 处理。
- **P5-2：shell/partial 审计。** 审计 Null Object、生成 registry、Editor hook 及接口要求的 no-op 后，没有发现可以安全删除且不改变装配或兼容性的剩余壳；已移除无职责的单文件 partial，不为适配陈旧生成工程而恢复废弃 production shell。
- **P5-3：测试物理拆分。** 将 15 个 Gateway room preparation、world-start anchor、time-sync 和 frame-timing 测试迁移至 `GatewayRuntimeTimingTests`，原 `BattleRuntimeOptimizationTests` 从 2790 行降至 2519 行，新 fixture 为 284 行；两个 fixture 合计保留 87 个测试特性。新 Unity source 配套 GUID 为 `4f86100b79514ae6a62c06cf71a9a3f8`，静态扫描确认唯一。

验证结果如下：

- 受影响的 runtime 项目串行构建通过：0 errors、140 warnings；由于 Unity 生成项目共用 `Temp/bin`，最终构建使用 `-m:1`，并确认并行执行造成的缺失中间程序集属于构建竞争而非源代码错误。
- P5-1/P5-3 过滤测试及新 Gateway fixture 的 `dotnet test` 均以退出码 0 完成。该证据来自 Unity 生成的测试 `.csproj`，本批次未执行 Unity Editor Batchmode Test Runner，因此不宣称已生成 Unity Test Runner XML 结果。
- Unity source 元数据门禁通过：缺失 `.cs.meta` 为 0，重复 GUID 为 0；拆分前后测试特性总数为 87；`git diff --check` 通过，仅保留既有换行风格提示。
- 为验证尚未刷新生成工程中的新增 source，曾临时加入两个 `.csproj` 的 Compile 项；验证完成后均已移除，生成的 runtime 与 UnitTests `.csproj` 不保留本批次修改。

### 13.10 下一批 P0-1：字符串诊断去热路径化实施结果

- `SkillPipelineRunner` 新增一次调用级 `SkillPipelineStartResult`，统一返回 success、兼容 fail reason、结构化 start reject 与 pipeline failure。旧 `bool`/`out string` 入口保留并委托新入口，既有调用方无需同步迁移。
- `SkillCastCoordinator` 改为直接消费单一启动结果，`SkillResultFactory` 通过稳定 code/stage/message 映射施放失败；启动失败后的 runtime rollback cleanup 顺序保持不变。
- runtime validation 的 skipped、entry、suppressed count 与 summary 动态文本均移到 `Func<string>` 门控后。日志关闭时不会执行格式化 factory，避免在禁用诊断时产生字符串构造成本。
- 新增定向测试覆盖日志禁用时 factory invocation count 为 0，以及结构化入口稳定公开 `skill.start.castConfigMissing` 并与旧包装保持 fail reason 一致。测试程序集因直接调用包含 `IAbilityPipelineConfig` 的公开签名，已在 asmdef 正式声明 `AbilityKit.Pipeline` 直接依赖。
- `AbilityKit.Demo.Moba.Runtime.csproj` 串行构建通过（117 warnings、0 errors）。`AbilityKit.Game.UnitTests.csproj` 的首次编译已越过新增测试源码，仅暴露缺少 Pipeline 直接引用的 2 个 `CS0012`；补齐 asmdef 后，外部生成项目验证被既有 deterministic 源码遗漏和共享 `Temp/bin` 中间程序集缺失阻断，不将其归类为本批源码错误。
- Unity 2022.3.62f1 EditMode 定向命令因同一项目已有 Unity Editor 实例持锁而退出，未生成权威测试 XML，因此本批不宣称两个新增测试已执行通过。生产 runtime 编译证据、测试源码静态门禁和测试 asmdef 依赖修复已完成，Unity 定向执行仍是剩余验证边界。
- 为诊断 Unity 生成工程缺口，曾临时给 projectile 项目加入 deterministic 源码与 conversion bridge，并给 UnitTests 项目加入 Pipeline 引用。Unity 再生成后这些临时项均已清除，两个 `.csproj` 的 UTF-8 BOM 已恢复且 `git status` 无 retained diff。

### 13.11 下一批 P0-2：数组返回 API 与快照/查询边界分配治理实施结果

- 状态 read model、host runtime port 与正式 runtime facade 已具备 caller-owned `IList<T>` fill contract，旧数组查询入口继续作为兼容和低频 API 保留。本批未重复建立平行接口，而是让 `ILogicWorldDriverHost` 正式暴露 `FillLogicWorldEntityStates`，并直接转发 runtime port，移除数组 fallback 与运行时类型探测。
- `MobaSnapshotBuffer<T>` 新增语义明确的非消费式 `PeekTo`；destination 采用追加语义，由调用方负责按复用周期 `Clear`。`CopyTo(IList<T>)` 保留为兼容包装，`DrainTo(IList<T>)` 复用 `PeekTo` 后清空 owner buffer，emitter 模板同步公开对应入口。
- ET driver 的 snapshot collection 已复用字段级 `List<WorldStateSnapshot>`，View runtime 的 allocation smoke test 改为跨帧复用同一 caller-owned list。旧 frame dispatcher 的数组 ownership contract，以及 spawn、damage、projectile、skill 等 codec 需要独立数组的序列化边界继续保留；本批不把这些必要 materialization 误判为可消除的热路径分配。
- snapshot buffer 定向测试覆盖追加顺序、destination 复用、peek 不消费、drain 消费、旧 `CopyTo` 兼容和 null 参数。最终过滤执行结果为 4/4 通过；View runtime port 与 allocation budget 过滤测试为 11/11 通过，均为 0 failed、0 skipped。
- MOBA Core、MOBA Tests 与 View Runtime Tests 串行构建均为 0 errors，warning 分别为 10892、190、30，属于既有 nullable、XML 文档、包兼容性和已知依赖漏洞基线。scoped `git diff --check` 退出码为 0，仅有既有 LF/CRLF 提示；三个受影响 Unity source 的既有 `.meta` 均存在且未修改，本批没有新增 Unity 资源或 GUID。

### 13.12 表现层下一批：FormalLobby 纯 C# 边界收敛实施结果

- 大厅决策、展示投影和文案分别迁入 `FormalLobbyDecision`、`FormalLobbyPresenter` 与 `LobbyNoticeFormatter`。这些组件不依赖 `UnityEngine`，负责本地玩家解析、owner 缺席判定、battle entry gate、默认 loadout 冲突调整、按钮可用性、玩家标签、同步状态以及 membership/player state/phase rollback 文案。
- 新增纯 C# `FormalLobbyCommandCoordinator`，统一编排 restore、prepare、自动 loading、create、join、leave、return 和退出前离房。协调器通过最小 `ILobbyRoomCommandPort` 操作房间流程，并复用 `FormalLobbyRuntime` 的 attachment/operation generation、取消 token 与自动化 marker；late completion、prepare 失败和 automatic start 失败均不会提交过期状态。
- `ILobbyRoomCommandPort` 下沉至 Flow Core 的 Multiplayer 边界，由 `MultiplayerRoomFlowController` 直接实现。Core 不反向依赖 Boot，`MultiplayerRoomFlowController` 继续作为房间状态机和 snapshot 的唯一 owner；协调器只组合命令，不复制房间状态。
- `FormalLobbyFeature` 收敛为 phase attach/detach、scope 解析、Unity 配置资产到纯 spec 的适配、目录刷新回调、时间输入、battle/scene exit adapter 与 IMGUI 绘制。GUI callback 只转交协调器命令，不再直接编排房间异步流程；场景退出中的同步 `Cancel` 保留为 Unity cleanup adapter。
- 普通 .NET 测试程序集直接编译同一份 package 源码。5 个决策/投影/文案/policy 测试与 3 个命令协调测试共 8/8 通过，覆盖 stale snapshot、authenticated player 解析、owner/capacity 自动开始约束、prepare marker 回滚、leave-refresh 顺序及 detach 后 late completion 拒绝。
- 普通 .NET View Runtime 项目构建为 0 errors；Unity 自动刷新后的 View Runtime 生成项目构建为 0 errors，证明 Boot coordinator、Core port 与 Feature adapter 的 asmdef 依赖方向成立。5 个新增 Unity source metadata 均存在，GUID 在全部 `*.meta` 中各唯一命中一次；scoped `git diff --check` 通过，仅有既有换行风格提示。
- 推荐顺序中的下一项为 `BattleContext`：按 Input、Entity、Runtime、Presentation 和 Snapshot 调用方继续削减跨域可变 facade，使可脱离 Unity 的绑定决策、reset policy 与查询投影进入普通 .NET 可测试边界。

### 13.13 后续表现层优化优先级与实施边界

后续工作继续以“先明确可变状态 owner，再缩小 concrete Unity facade，最后整理物理文件”为排序原则。文件行数和 partial 数量只作为风险信号，不作为优先级本身；每一批必须先建立普通 .NET 可运行的行为测试，再迁移 Unity adapter。

| 顺序 | 优先级 | 批次 | 主要问题 | 目标边界 | 退出条件 |
|---:|:---:|---|---|---|---|
| 1 | P0 | `BattleContext` ownership | pool reset 同时清理 Session、Snapshot、Input、Prediction、Entity、VFX；`InputRecordWriter` 等资源的创建 owner 与 dispose owner 不完全一致 | 提取纯 C# `BattleContextLifetime`/binding owner，分别管理 runtime session、input/prediction、snapshot、entity/presentation binding generation；`BattleContext` 只聚合只读 ports 和兼容投影 | reset 顺序、幂等释放、reference-equality unbind、stale generation 拒绝可由普通 .NET fake 测试；pool release 不再直接知道具体跨域资源类型 |
| 2 | P0 | `BattleContext` consumer ports | HUD、Snapshot applier、Debug、Session、View controller 大量接收完整 concrete context，形成跨域 service locator | 按调用方使用面迁移至 `IBattleRuntimeReadModel`、`IBattleEntityReadModel`、`IBattleInputPort`、`IBattleSnapshotRoutingPort`、`IBattlePresentationPort`；命令与查询分离 | 新增生产调用不再以 `BattleContext` 作为通用参数；核心 resolver、loadout projection、snapshot routing decision 可在普通 .NET 测试中运行；兼容 facade 仅保留明确清单 |
| 3 | P0 | Entry 异步生命周期 | `MultiplayerGatewayEntryModule` 同时创建 SDK/dispatcher/session/controller/assets、管理订阅、恢复、push 和 teardown；`async void` push completion 无可等待 owner | 提取纯 C# `MultiplayerGatewayEntryRuntime` 与 `GatewayPushOperationRuntime`，统一 attachment generation、pending task、异常汇报和 dispose 顺序；Entry module 只负责 Unity dispatcher、配置资产、transport factory 与 root publication | detach 后 push/recovery late completion 不提交；所有订阅成对解除；pending task 可等待；teardown 顺序和部分构造失败回滚由普通 .NET 测试覆盖；Module 不再持有十余个独立可变资源字段 |
| 4 | P1 | `GatewayRoomClient` transport/wire 分层 | transport delegate、push subscription、wire session、请求 codec、response mapping 与 battle input sequence 聚在同一 partial facade | 建立 `IGatewayRequestTransport`、纯 C# room/state-sync protocol client、独立 mapper 和 `BattleInputSequence` owner；Unity/SDK adapter 只实现 transport 与 push source | wire request/response、错误映射、sequence 单调性、取消传递可普通 .NET 测试；协议 client 不引用 SDK concrete type；`Dispose` 只处置明确 owned transport/subscription |
| 5 | P1 | `BattleSessionFeature` facade 收尾 | 已有 runtime/controllers/handles owner，但 Feature 仍以大量 partial 转发私有状态、生命周期 cleanup 和 Unity log/editor hook | 将 detach transaction、tick projection、log/editor hooks 分离；subfeature 依赖改为稳定 runtime ports；Feature 只保留 phase adapter、稳定 public facade 和 composition | detach transaction 的顺序、异常隔离、幂等性可普通 .NET 测试；subfeature 不依赖 concrete Feature；单行 accessor partial 合并或删除；不复制 `BattleSessionRuntime` 状态 |
| 6 | P2 | Debug 与物理文件整理 | debug query 仍有 world service 解析，测试和 partial 文件较碎；但行为 owner 风险低于前五批 | Debug 仅消费只读 ports 和命令服务；生产 ownership 稳定后再合并空壳/单行 partial、按领域拆大型测试 fixture | Debug/IMGUI 不直接解析 world service；单文件/空 partial 为零；测试数量、case source、`.meta` GUID 和条件编译行为保持不变 |

实施约束：

- 第 1、2 批可以连续设计，但必须分批提交：先迁移 owner 和 reset policy，再缩小调用方参数。否则 context facade 与新 owner 同时可写，反而增加双状态风险。
- 第 3 批先处理 Entry runtime，再调整 Gateway client。Entry 需要先形成稳定 transport/session composition port，避免 Gateway 拆分后仍由 Unity module 直接拼装所有 concrete 资源。
- 第 4 批不改变 op-code、wire codec、timeout、错误码或 command sequence 语义；只调整依赖方向和 ownership。
- 第 5 批不得再创建平行 Session 状态机。`BattleSessionRuntime`、`BattleSessionHandles` 和现有 controllers 是迁移目标 owner，Feature 只减少转发和 Unity 耦合。
- 第 6 批必须最后执行。物理文件减少不应与高风险生命周期迁移同批，避免 `.meta`、条件编译和行为变更混在同一差异中。

统一门禁：每批至少包含普通 .NET 定向测试、受影响 SDK-style 项目构建、Unity 生成项目编译、Unity `.meta` GUID 唯一性检查、禁止反向程序集依赖的源码搜索以及 scoped `git diff --check`。涉及 MonoBehaviour、Editor hook、Resources 或主线程 dispatcher 的 adapter 行为，再补 Unity EditMode/PlayMode 测试；不以 Unity 测试替代可在纯 C# 边界完成的逻辑验证。

### 13.14 P0-P2 边界收敛实施结果

本轮已按 13.13 的顺序完成 P0-P2。实现保持现有 public facade、wire op-code/codec、timeout、取消传递、错误映射、command sequence、push 订阅和 teardown 顺序不变；主要变化是将可变 owner、纯策略和协议行为迁到可由普通 .NET 项目直接编译同一份 package 源码的边界。

- **P0 BattleContext 与 consumer ports。** 收敛 pooled Context 的资源 owner、binding generation、reference-equality unbind 和 reset policy，并按 runtime、entity、input、snapshot routing、presentation 的实际使用面缩小 consumer 依赖。旧 Context 仅保留明确兼容投影，stale generation 不会清理替代 binding。
- **P0 Entry 异步生命周期。** Entry 的 attachment generation、pending push/recovery operation、异常汇报、订阅配对和 teardown 编排进入独立纯 C# runtime；Unity module 只保留 dispatcher、配置资产、transport factory 和 root publication adapter。detach 后的 late completion 不提交状态，pending operation 可等待。
- **P1 Gateway 分层。** `GatewayRoomClient` 保留 facade 和 composition，transport ownership/push source、wire protocol client、无状态 response mapper 与线程安全 `BattleInputCommandSequence` 分别迁入独立物理文件。普通 .NET 项目显式链接这些 package 源码，既有边界测试继续覆盖参数转发、订阅配对、owned dependency 单次释放、wire 字段和并发 sequence 单调唯一性。
- **P1 Session facade 收尾。** teardown failure isolation 与 tick projection 分别迁入 `SessionTeardownPolicy` 和 `BattleSessionTickProjector`；现有 runtime/controllers 继续作为状态 owner，没有建立平行 Session 状态机。Unity lifecycle/log/editor adapter 仍留在 Feature 边界。
- **P2 Debug 与 metadata。** 新增 Actor ID 语义的 `BattleDebugEntityId`，避免将 ECS world index/version 兼容类型暴露到 debug API；obsolete `EcsEntityId` 只保留在 `DefaultBattleDebugFacade` 的单点 adapter。新增 equality、hash、ordering、sentinel 契约测试。Gateway、Session、Debug 共 7 个新 Unity source 均具备标准 metadata 和稳定 GUID。

验证结果：

- 普通 .NET View Runtime 全量测试 147/147 通过，0 failed、0 skipped；SDK-style Runtime 项目构建为 0 errors。
- Unity 2022.3.62f1 batchmode 完成脚本编译并通过 `SyncVS.SyncSolution` 自动刷新 generated projects；7 个新源码全部进入 `AbilityKit.Demo.Moba.View.Runtime.csproj`，`AbilityKit.Game.UnitTests.csproj` 保持对 Runtime 的项目引用，两个 generated `.csproj` 均无 retained tracked diff。
- generated Runtime 串行构建通过（135 warnings、0 errors），generated UnitTests 串行构建通过（154 warnings、0 errors）。警告为工程既有 Unity framework 引用冲突、nullable 和未使用测试成员。
- 7 个新纯 C# source 的 `UnityEngine` 引用为 0；7 个新 GUID 在全部 Unity metadata 中各唯一命中一次；拆分类型各只有一个声明；Debug 范围内 `EcsEntityId` 仅命中单点兼容 adapter。
- scoped `git diff --check` 通过，仅报告工作树既有 LF/CRLF 转换提示；额外扫描 15 个新增 source、metadata 和 test 文件，尾随空格错误为 0。

### 13.15 P3-P4 HUD 与 Formal Lobby 实施结果

本轮继续按推荐顺序完成 P3-P4，并收尾 P0-P2 的表现层边界审计。实现保持既有 Unity Feature、Gateway、GameEntry 和 public compatibility facade 的生命周期入口不变，新增纯 C# owner 通过 source link 镜像到普通 .NET View Runtime 项目。

- **P3 HUD session model。** 新增纯 C# `BattleHudSessionModel`，负责 local player/actor 解析、loadout revision 与 binding decision、snapshot actor resolution 以及会话状态投影；`BattleHudFeature` 保留 Unity 生命周期装配，`BattleHudInputController` 保留输入适配，Unity UI 类型不进入 model。
- **P4 Formal Lobby screen boundary。** 新增纯 screen snapshot、presenter 和 renderer 边界；`FormalLobbyFeature` 负责 phase attach/detach、配置适配、snapshot 构建和命令转交，`FormalLobbyRenderer` 统一负责 IMGUI 状态绘制。`FormalLobbyRuntime` 继续负责 attachment generation、成对解绑、取消 token 和反向 async teardown，Entry/GameEntry 不复制 Lobby 状态机。
- **P1 diagnostics owner。** 将 input submission statistics 从 Unity 聚合器拆为纯 C# `InputSubmissionStatsSnapshot` 与 `InputSubmissionStatsProvider`；`BattleReplicationRuntime` 只依赖纯 owner，Unity `BattleFlowDebugProvider` 保留透明兼容代理，并维持 reference-equality 清理语义。
- **P2 capability narrowing。** projectile、damage 和 presentation cue handlers 不再接收完整 `BattleContext`；cue handler 进一步收窄为 `IECWorld`。remote interpolation 使用 `IBattleEntityContext`。world service locator 命中仅存在于 attach/bind/composition 阶段，callback/tick 热路径使用缓存端口；Unity Feature 生命周期字段属于明确的 attach/detach 边界，不作为共享 handler 参数。

验证结果：

- 普通 .NET View Runtime SDK-style 项目构建为 0 errors；全量测试 174/174 通过，0 failed、0 skipped。
- Unity 2022.3.62f1 generated Runtime 项目构建通过（0 errors），generated UnitTests 项目构建通过（0 errors）；本轮 UnitTests 生成程序集构建为 172 warnings、0 errors。此次未实际运行 Unity Test Runner，因此上述结果仅证明生成项目编译和程序集引用有效，不宣称 Unity 行为测试已执行。
- `BattleHudSessionModel.cs`、`FormalLobbyScreenSnapshot.cs`、`InputSubmissionStatsSnapshot.cs`、`InputSubmissionStatsProvider.cs` 均存在对应 `.meta`，四个文件均无 `UnityEngine`/`UnityEditor` 引用；全 Unity `*.meta` GUID 重复组为 0。
- `BattlePresentationCueViewEventHandler(BattleContext ...)`、ViewEvents 中长期持有 concrete context 的共享 handler 残留均为 0；scoped `git diff --check` 通过。generated Runtime 项目已包含本轮新增 source link，保留其 BOM，未产生额外手工 generated 文件差异。

本轮 P0-P4 的代码、测试/source link、metadata、依赖方向与 scoped diff 门禁均已完成；后续仅需按更大范围继续削减 Unity Feature 生命周期上下文和整理物理 partial 文件，不作为本轮完成条件。
