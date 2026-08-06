# Ability-Kit

> 通用游戏战斗工具集合源码 | Logic-Presentation Separation | Ability System | 按需组合

**Ability-Kit** 是一个基于 Unity UPM 的通用游戏战斗框架，专注于**技能系统、战斗逻辑**。框架采用模块化设计，提供数据驱动的技能编排、事件触发系统、流程引擎等核心能力，支持按需组合以适配不同类型的游戏（MOBA、MMO、ARPG、RTS 等）。核心战斗逻辑以纯 C# runtime 形式实现，可脱离 Unity 环境运行（例如服务器/工具链/单元测试）；与 Unity 强相关的部分主要集中在表现层与少量适配层。

Ability-Kit 目前处于**开发期**。这个仓库保存的是 AbilityKit 相关模块包、示例工程、工具链和第三方适配的**完整源码集合**，方便统一开发、编译验证、示例演示和设计文档维护。

真实项目使用时不需要，也不建议把仓库内所有包一次性整体引入。更推荐按项目需求选择组合：

- 只做技能/触发：组合 `core`、`pipeline`、`triggering`、`ability` 等包。
- 只做逻辑流程：组合 `flow`、`hfsm`、`timer`、`context` 等包。
- 做帧同步/状态同步：组合 `world.framesync`、`world.snapshot`、`world.statesync`、`record`、`network` 等包。
- 做 Unity 表现和编辑器工具：按需加入 `unity.pool`、`base.editor`、`demo.moba.editor` 等包。
- 参考完整落地方式：阅读 `demo.moba.*` 最佳实践示例，但不要把示例包误认为所有项目的必选依赖。

---

## 仓库定位

这个仓库更接近一个“工具箱源码仓库”，而不是单一框架产品包。

| 目录 | 定位 |
|------|------|
| `Unity/Packages/` | Unity UPM 包源码。各 `com.abilitykit.*` 包是主要模块边界。 |
| `src/` | .NET 解决方案和样例工程，复用 `Unity/Packages/` 中的源码，用于纯 C# 编译、控制台运行和示例验证。 |
| `Server/` | 服务端、Orleans、网关等实验和集成代码。 |
| `Docs/` | 跨模块设计记录、规则说明和集成备忘。 |
| `LubanConfig/` | 配置表与生成相关素材。 |
| `tools/` | 本地开发、验证、smoke test 辅助脚本。 |

`Unity/Packages/` 中包目录较多，是因为这里集中存放了工具集合的全部源码，包括一个较完整的 MOBA 最佳实践示例。后续给其他项目使用时，应按需获取和组合，而不是默认全量使用。

---

## 当前状态

- 项目仍在开发期，部分包的 API、目录结构和依赖声明还会继续收敛。
- 许多模块已经具备独立包边界，但 `package.json`、`asmdef`、示例工程和服务端工程之间仍有一些历史依赖需要持续整理。
- `demo.moba` 是参考工程，用来展示框架组合方式、配置组织、协议同步、编辑器工具和运行时流程。
- 第三方包位于 `com.abilitykit.thirdparty.*`，主要作为源码/依赖承载，不建议直接放入 AbilityKit 业务扩展。

---

## 核心特性


| 特性          | 说明                                        |
| ----------- | ----------------------------------------- |
| **逻辑与表现分离** | 纯 C# 逻辑层可在服务器、客户端、编辑器环境下运行，通过事件与表现层解耦     |
| **帧同步确定性**  | 支持帧同步、回滚、客户端预测、断线重连，保证多人战斗的确定性            |
| **数据驱动**    | 技能、效果、触发器均可通过配置定义，配合可视化编辑器提升效率            |
| **高度可扩展**   | 模块化设计，支持 Hook/Feature/Blueprint 扩展机制，按需裁剪 |
| **高性能**     | 索引表查询、对象池、流式处理、零 GC 分配优化                  |

---

## 适用边界

Ability-Kit 更适合技能、Buff、投射物、被动、装备/天赋联动、多人同步和自动化验证会持续增长的中大型战斗项目；如果项目只是少量固定技能、单机轻量玩法或快速原型，直接使用简单的 `SkillManager` / `BuffManager` / MonoBehaviour 脚本通常更划算。

| 更适合 | 不建议优先使用 |
| ------ | -------------- |
| MOBA、ARPG、MMO、RTS、多人动作、带复杂技能的 Shooter | 技能数量少、生命周期短、同步要求低的小型项目 |
| 需要服务端/客户端复用纯 C# 战斗逻辑 | 所有逻辑都可以安全写在 Unity 场景脚本里的项目 |
| 需要长期维护大量配置化技能、Buff、触发器和投射物 | 以一次性硬编码交付为主、缺少配置和测试治理的项目 |
| 需要战斗日志、回放、自动化测试、预测回滚或状态同步 | 不需要解释战斗来源、也不需要网络同步的项目 |

---

## 框架价值

Ability-Kit 的目标不是替代所有项目中的 `SkillManager`、`BuffManager` 或 Unity 表现脚本，而是为**中大型战斗项目**提供一套可持续演进的底层拆分方案。当技能数量、Buff/被动/装备/天赋联动、投射物与区域效果、多人同步、战斗回放和自动化验证开始增长时，简单管理器式写法往往会因为互相调用、生命周期不清和调试困难而失控。

框架将战斗能力拆成几个稳定维度：

| 维度            | 解决的问题                                       | 代表模块                                     |
| ------------- | ------------------------------------------- | ---------------------------------------- |
| **输入与施法流程**   | Press/Hold/Release/Cancel、吟唱、引导、多阶段、取消与并行策略 | `pipeline`、`ability`、`demo.moba.runtime` |
| **事件触发与规则执行** | 主动、被动、命中、受击、Buff Tick、区域进入/离开等统一进入规则系统      | `triggering`、`ability`                   |
| **效果与战斗能力**   | 伤害、治疗、Buff、护盾、投射物、位移、召唤、目标选择等原子能力复用         | `combat.*`、`modifiers`、`gameplaytags`    |
| **运行实例与溯源**   | 追踪一次技能释放产生的子弹、区域、Buff、伤害来源和 Action 链路       | `trace`、`record`、`demo.moba.runtime`     |
| **同步与验证**     | 纯 C# 逻辑复用、客户端预测、回滚、快照、状态哈希、重连和验收矩阵          | `world.*`、`protocol.*`、`demo.shooter.*`  |

这种拆分的收益主要体现在长期维护：同一个 `giveDamage`、`addBuff`、`shootProjectile` 或 `playCue` 动作可以被主动技能、被动触发、投射物命中、区域效果和 Buff 周期事件复用；一次战斗结果也可以通过运行实例、来源上下文和 trace 链路反查到“哪个技能、哪个触发器、哪个 Action、哪个目标”。

### 端到端能力管线

Ability-Kit 的高价值点不只在于模块数量，而在于这些模块可以串成一条可调试、可验证、可同步的战斗能力管线：

```text
可读配置 / Trigger Plan
  -> 强类型 Action Schema
  -> ExecCtx / World Service 上下文解析
  -> 技能、投射物、召唤等参数 Modifier
  -> 技能 Runtime / Origin / Trace 血缘
  -> 投射物、区域、Buff、Continuous 生命周期
  -> 业务事件处理与 Snapshot 输出
  -> 纯 C# 验收、回放、同步或 Unity 表现消费
```

这意味着框架不是单独提供“一个技能触发器”或“一个投射物服务”，而是把内容生产、运行时执行、来源追踪和网络同步放在同一套边界里治理。典型例子包括：

| 能力                    | 价值                                                                 |
| --------------------- | ------------------------------------------------------------------ |
| **可读配置与反向导出**         | Trigger Plan 可以在运行时执行，也可以转换回更适合审查和维护的 Source 配置，降低配置黑盒化风险。         |
| **强类型 Action 执行**     | 配置化动作最终落到带参数 schema、服务解析、失败原因和日志的运行时代码，而不是散落的字符串脚本。                |
| **来源血缘追踪**            | 技能释放产生的投射物、区域、Buff 和后续伤害保留 root/parent/owner context，方便战斗归因、调试和回放。 |
| **运行时参数修正**           | Buff、装备、天赋或场景状态可以动态改变技能、投射物、召唤物参数，避免复制大量相似技能配置。                    |
| **Continuous 生命周期治理** | 持续效果不仅是 Tick 计时器，还能通过 Tag 规则控制激活、阻止、暂停、恢复、移除，并保留 explain 结果。       |
| **同步友好的事件模型**         | 投射物 spawn/hit/exit 等事件既能被业务系统消费，也能被 snapshot 服务读取，避免网络同步逻辑侵入模拟核心。  |

---

## 示例定位

仓库中的示例不是必选依赖，而是用来展示框架能力边界的参考工程：

| 示例               | 主要展示能力                                                                                      | 适合关注的问题                                            |
| ---------------- | ------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| `demo.moba.*`    | 复杂战斗玩法表达：技能输入、技能 Pipeline、Trigger Plan、Buff/Continuous、投射物、区域、伤害、位移、**寻路避障(A\*)**、**墙体交互(墙滑/穿墙/闪烁终点投影)**、**碰撞 grid broadphase**、**BT AI 寻路**、表现 Cue、配置加载与 Entitas 集成 | 如何把一个 MOBA/ARPG 风格从硬编码拆成配置、流程、触发、Action、运行实例、碰撞/寻路/墙体 统一基础设施 |
| `demo.shooter.*` | 多人同步与运行时工程：预测回滚、权威插值、混合同步、快照导入导出、状态 Hash、断线重连、Svelto ECS、纯 C# 验收和 Editor 展示外壳               | 如何验证框架在网络同步、高性能实体和可观测验收场景下的边界                      |

因此，`demo.moba` 更像“复杂战斗能力展柜”，`demo.shooter` 更像“同步与验收能力展柜”。真实项目可以从这两个示例中选取需要的组织方式，但不建议直接把示例包整体当作业务必选框架层。

---

## 架构总览

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Ability-Kit 框架                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                          游戏应用层                                   │   │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │   │
│  │   │   技能系统   │  │   战斗系统   │  │   录像回放   │             │   │
│  │   │  (Pipeline) │  │  (Combat)   │  │  (Record)   │             │   │
│  │   └──────────────┘  └──────────────┘  └──────────────┘             │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                          引擎层                                       │   │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │   │
│  │   │   流程引擎   │  │   触发系统   │  │   状态机     │             │   │
│  │   │    (Flow)    │  │ (Triggering) │  │   (HFSM)     │             │   │
│  │   └──────────────┘  └──────────────┘  └──────────────┘             │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                          世界层                                       │   │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │   │
│  │   │   依赖注入   │  │    ECS      │  │   帧同步     │             │   │
│  │   │  (World.DI)  │  │ (World.ECS) │  │(FrameSync)  │             │   │
│  │   └──────────────┘  └──────────────┘  └──────────────┘             │   │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │   │
│  │   │  状态同步    │  │ 帧数据层     │  │ 战斗传输层   │             │   │
│  │   │(StateSync)  │  │(NetworkFrag)  │  │(Battle.Trans)│             │   │
│  │   └──────────────┘  └──────────────┘  └──────────────┘             │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                          核心层                                       │   │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │   │
│  │   │   数学库     │  │   属性系统   │  │   效果系统   │             │   │
│  │   │    (Math)    │  │(Attributes) │  │  (Effects)   │             │   │
│  │   └──────────────┘  └──────────────┘  └──────────────┘             │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 运行环境装配

`com.abilitykit.coordinator` 位于运行时装配层，负责把不同运行环境提供的 Host、Transport、View 外壳统一装配成可运行的战斗会话。业务工程只需要实现 `ISessionCoordinatorHost`，提供 WorldHost 创建、WorldCreateOptions 配置、服务注册、配置加载和出生数据；`SessionCoordinator` 则根据 `SessionConfig` 创建逻辑世界、选择同步适配器、挂载 SubFeature、驱动 Tick，并把逻辑事件转交给表现层或远端同步层。

```mermaid
flowchart TD
    subgraph Env[运行环境]
        Unity["Unity Client / PlayMode"]
        Console["Console / Pure C# Test"]
        Server["Server / Orleans Host"]
        Remote["Remote Client"]
    end

    Unity --> Host["ISessionCoordinatorHost"]
    Console --> Host
    Server --> Host
    Remote --> Host
    Host --> Config["SessionConfig<br/>SyncMode / HostMode / TickRate"]
    Config --> Coordinator["SessionCoordinator"]

    Coordinator --> WorldHost["IWorldHost"]
    WorldHost --> World["Logic World<br/>World.DI + ECS + Services"]

    Coordinator --> AdapterFactory["SyncAdapterFactory"]
    AdapterFactory --> Local["LocalSyncAdapter<br/>Lockstep / Pure Local"]
    AdapterFactory --> RemoteAdapter["RemoteSyncAdapter<br/>StateSync / SnapshotAuthority"]
    AdapterFactory --> Hybrid["HybridSyncAdapter<br/>Prediction + Server Authority"]

    Coordinator --> SubFeatures["Session SubFeatures<br/>TickLoop / Events / SnapshotRouting"]
    Coordinator --> ViewTimeline["ViewTimeline / IViewEventSink"]
    RemoteAdapter --> Transport["IRemoteBattleSyncTransport"]
    Hybrid --> Transport

    World --> Battle["Ability / Triggering / Combat / StateSync"]
    Battle --> ViewTimeline
    Battle --> SubFeatures
```

这层的价值是把“同一套战斗逻辑”放到不同运行环境中复用：本地 Demo 可以走 `LocalSyncAdapter`，远程状态同步可以走 `RemoteSyncAdapter`，带客户端预测的多人玩法可以走 `HybridSyncAdapter`，测试与 Console 环境可以复用纯 C# World 和同一套会话生命周期。

---

## 模块速览

### 核心基础设施


| 模块                          | 说明                                                          |
| --------------------------- | ----------------------------------------------------------- |
| `com.abilitykit.core`       | 数学库（Vec2/Vec3/Quat/Transform3）、对象池、日志、事件系统、序列化和通用工具 |
| `com.abilitykit.gameplaytags` | Gameplay Tag 状态标识系统，支持按标签组织 Buff、状态、规则和配置引用 |
| `com.abilitykit.modifiers`  | 通用参数/属性修正器，用于 Buff、装备、天赋或场景状态动态改写技能、投射物、召唤等参数 |
| `com.abilitykit.attributes` | 属性系统，支持 Buff/Debuff、自定义公式、脏标记优化                             |
| `com.abilitykit.trace`      | 溯源树运行时，用于追踪技能、效果、Action、投射物、Buff 等来源上下文和父子关系 |
| `com.abilitykit.diagnostics` | 开发期诊断与性能分析工具，支持 profiler、诊断窗口、导出和运行时观测 |


### 世界管理层


| 模块                                      | 说明                                                       |
| --------------------------------------- | -------------------------------------------------------- |
| `com.abilitykit.world.di`               | 依赖注入容器，支持 Singleton/Scoped/Transient 三种生命周期              |
| `com.abilitykit.world.ecs`              | 轻量级 ECS 框架：Entity、EntityWorld、ComponentTypeId            |
| `com.abilitykit.world.framesync`        | 帧同步：FrameSync、Rollback、ClientPrediction、输入历史             |
| `com.abilitykit.world.snapshot`         | 快照路由：按 opCode 解码并分发到处理器（与网络解耦）                           |
| `com.abilitykit.world.networkfragments` | 帧数据包：FramePacket、RemoteFrameBuffer、RemoteFrameAggregator |
| `com.abilitykit.world.statesync`        | 状态同步与客户端预测：Rollback、Per-Entity/ECSPrediction、StateHash   |
| `com.abilitykit.record`                 | 录像回放：Session、Container、Track，支持输入录制、状态哈希采样               |


### 技能与战斗层


| 模块                               | 说明                                                                           |
| -------------------------------- | ---------------------------------------------------------------------------- |
| `com.abilitykit.pipeline`        | **技能流程编排**：Phase 图模型，支持 Sequence/Parallel/Conditional/Repeat/Delay/WaitUntil/Timeline、暂停/中断/Trace |
| `com.abilitykit.triggering`      | **事件触发与规则执行引擎**：EventBus、TriggerRunner、TriggerPlan、强类型 Action Schema、ExecCtx、黑板/表达式/执行控制 |
| `com.abilitykit.actionschema`    | Action/Timeline 数据结构与运行时辅助，用于把时序动作、技能事件和编辑器数据表达为稳定 DTO |
| `com.abilitykit.ability`         | 技能聚合运行时：Ability、Effect、Triggering、EffectSource、配置加载、热重载和编辑器工具 |
| `com.abilitykit.ability.explain` | 技能解释/调试框架：Forest、Tree + Navigation Protocol                                  |
| `com.abilitykit.behavior`        | 行为运行时与 Pipeline 行为阶段：可将行为决策/执行器嵌入技能流程，适合复杂 AI、引导、锁定和持续决策 |
| `com.abilitykit.combat.motion`   | 移动系统：MotionPipeline、来源组合(dash/jump/path)、碰撞求解(墙滑/穿墙/终点投影)、per-skill CollisionPolicy 透传 |
| `com.abilitykit.combat.navigation`  | 导航系统：NavigationGrid + 确定性 A\* 寻路(无定点数学)、GridPathfinder、INavigationWorld/INavigationService |
| `com.abilitykit.combat.collision.abstractions` | 碰撞：ICollisionWorld Naive/Grid 双实现 + OBB sweep 窄相 + GridBroadphase + LayerFilter |
| `com.abilitykit.combat.entitymanager` | 实体管理器：索引表实现高效查询 |
| `com.abilitykit.combat.skilllibrary`  | 技能库：索引表实现高效技能查询 |
| `com.abilitykit.combat.targeting`     | 目标查找：查找目标、筛选、排序、流式处理、零 GC |
| `com.abilitykit.combat.projectile`    | 投射物系统：对象池、帧同步、命中策略、范围效果 |
| `com.abilitykit.combat.damage`        | 伤害系统：DamagePipeline、自定义伤害公式 |



### 运行时与流程层


| 模块                              | 说明                                                                                                 |
| ------------------------------- | -------------------------------------------------------------------------------------------------- |
| `com.abilitykit.host`           | 服务器端抽象：World 管理、客户端连接、消息广播                                                                         |
| `com.abilitykit.host.extension` | Host 扩展：Session（FramePacketNetAdapter）、FrameSync、Rollback、Hook、Feature、Blueprint                   |
| `com.abilitykit.coordinator`    | 战斗会话协调：统一创建 WorldHost/World、加载配置、注册服务、选择 Local/Remote/Hybrid SyncAdapter、挂载 SubFeature 与 ViewTimeline |
| `com.abilitykit.flow`           | **流程编排引擎**：IFlowNode 节点树（Sequence/Race/Parallel/If/Timeout/Await），FlowContext 作用域注入，WAKE/PUMP 事件驱动 |
| `com.abilitykit.hfsm`           | **分层状态机**：基于 UnityHFSM，ITriggerable 事件转换、IAction 行为层（BehaviorStatus）、Decorator AOP 包装              |


### 战斗传输层


| 模块                                             | 说明                                                           |
| ---------------------------------------------- | ------------------------------------------------------------ |
| `com.abilitykit.game.battle.runtime`           | 战斗逻辑传输接口：IBattleLogicTransport、请求/响应类型                       |
| `com.abilitykit.game.battle.transport.runtime` | 传输层实现：NetworkTransport、StateSyncAdapter（Moba）、INetworkClient |


### 网络层


| 模块                                   | 说明               |
| ------------------------------------ | ---------------- |
| `com.abilitykit.network.runtime`     | 网络运行时抽象          |
| `com.abilitykit.protocol`            | 协议定义：客户端/服务器共享协议 |
| `com.abilitykit.protocol.moba`       | MOBA 协议定义        |
| `com.abilitykit.protocol.memorypack` | MemoryPack 序列化实现 |


---

## 核心模块设计理念

### Pipeline（技能管线）

Pipeline 回答“技能流程如何被编排、等待、分支、并行和嵌套”。它不是只能表达“冷却 -> 吟唱 -> 施法 -> 后摇”的线性序列，而是把技能流程建模为 Phase 图：基础阶段负责动作、延迟、等待和时间轴；组合阶段负责顺序、并行、重复和条件分支；业务扩展阶段可以桥接 TriggerPlan、Timeline、Behavior/HFSM/Flow 等更复杂执行器。Pipeline 本身保持对战斗业务的低耦合，复杂规则和效果执行通常通过 Triggering 或业务 Phase 接入。

```mermaid
flowchart TD
    Start["Skill Input / Cast Request"] --> PreCast["PreCast Flow"]
    PreCast --> Cast["Cast Flow"]

    subgraph Pipeline["Pipeline Phase Graph"]
        Cast --> Sequence["Sequence"]
        Sequence --> Gate["Gate / WaitUntil\nconditions + timeout"]
        Gate --> Branch["Conditional\nOnEnter / Continuous"]

        Branch -->|"true"| Timeline["Timeline Phase\nframe/time events"]
        Branch -->|"false"| Fallback["Fallback / Skip / Fail / Wait"]

        Timeline --> Parallel["Parallel"]
        Parallel --> RulePlan["RulePlan Phase\nTriggering actions"]
        Parallel --> Behavior["Behavior Phase\noptional behavior runtime"]
        Parallel --> Delay["Delay / Repeat"]
    end

    RulePlan --> Triggering["Triggering\ngiveDamage / addBuff / shootProjectile"]
    Behavior --> Output["Movement / Effect / Event Output"]
    Delay --> Complete["Pipeline Complete / Interrupt / Trace"]
```

**核心抽象**：`IAbilityPipelinePhase<TCtx>` 是最小执行单元，负责 `Execute()`、`OnUpdate(deltaTime)`、`IsComplete`、`Reset()` 等生命周期；`AbilityCompositePhase<TCtx>` 让 `Sequence`、`Parallel`、`Conditional` 等复合阶段可以继续持有子阶段并递归嵌套；`IInterruptiblePhase<TCtx>`、`IDurationalPhase<TCtx>`、`IAbilityPipelinePhaseInstanceFactory<TCtx>` 则解决中断、持续时间、运行实例克隆等真实运行问题。Pipeline 还提供 `PipelineGraph` 作为代码侧图构建入口，并通过 `IPipelineTraceRecorder`/DebugHooks 输出执行轨迹。

**条件与等待能力**：`AbilityConditionalPhase` 支持多分支、`OnEnter`/`Continuous` 条件检查和无命中时的 `Wait/Complete/Fail/Skip` 行为；`AbilityGatePhase` 适合做入口门控；`AbilityWaitUntilPhase` 支持等待条件满足、超时完成或继续等待；`AbilityRepeatPhase` 可以围绕子阶段或动作重复执行并设置间隔。

**与 Triggering/Behavior 的关系**：Pipeline 负责“流程什么时候推进”，Triggering 负责“事件发生后执行哪些规则和动作”。MOBA 示例中 `TableDrivenMobaSkillPipelineLibrary` 已经把配置表中的 `Timeline`、`RulePlan`、`Sequence`、`Parallel`、`Repeat`、`Delay`、`WaitUntil` 转成运行时 Phase；复杂行为可通过 `com.abilitykit.behavior` 的 `AbilityBehaviorPhase` 嵌入 Pipeline，让行为决策、移动输出、待触发效果和事件输出作为技能流程的一部分运行。

**适用场景**：技能释放流程（预施法、吟唱、引导、施法、后摇）、条件化连招、多段技能、蓄力/松手释放、等待外部信号、并行表现与逻辑、重复波次、Timeline 事件、复杂 AI/行为树阶段、与 TriggerPlan 组合的配置化技能流程。

---

### Triggering（触发器系统）

Triggering 回答“当战斗事件发生时，应该按什么规则执行什么动作”。它不是单纯的事件总线，而是 AbilityKit 中承接主动技能、被动技能、Buff Tick、投射物命中、区域进入/离开、属性变化等事件的规则执行层。事件进入后，Triggering 负责筛选触发器、按 Phase/Priority/Order 排序、构建 `ExecCtx`、解析 Payload/Blackboard/NumericExpr、执行 TriggerPlan 或强类型 Action，并通过 ExecutionControl、Cue、Lifecycle、Tracer 把中断、表现回调、诊断和确定性约束纳入同一条链路。

```mermaid
flowchart TD
    subgraph Sources["事件来源"]
        Skill["技能阶段 / Timeline"]
        Passive["被动 / 属性变化"]
        Buff["Buff / Continuous Tick"]
        Projectile["投射物 Hit / Exit"]
        Area["区域 Enter / Stay / Exit"]
    end

    Skill --> EventBus["EventBus / DirectTrigger"]
    Passive --> EventBus
    Buff --> EventBus
    Projectile --> EventBus
    Area --> EventBus
    EventBus --> Runner["TriggerRunner\nPhase -> Priority -> Order"]

    Runner --> ExecCtx["ExecCtx TCtx\nWorld Services / EventBus / Registries"]
    Runner --> Plan["TriggerPlan / Executable Tree"]

    Plan --> Conditions["Conditions\nPayload / Blackboard / NumericExpr"]
    Conditions -->|"pass"| ActionCall["ActionCallPlan"]
    Conditions -->|"reject"| Control["ExecutionControl\ninterrupt / skip / short-circuit"]

    ActionCall --> Schema["ActionSchemaRegistry\nvalidate / resolve args"]
    Schema --> Executor["PlannedTriggerActionExecutor"]
    ExecCtx --> Executor

    Executor --> CombatActions["Typed Actions\ngiveDamage / addBuff / shootProjectile / playCue"]
    Executor --> Cue["Cue / Lifecycle / Trace"]
    CombatActions --> Services["Combat Services\nDamage / Buff / Projectile / Motion / Snapshot"]
```

**核心职责**：`TriggerRunner<TCtx>` 负责事件订阅、触发器排序、条件评估、执行控制、生命周期通知和 ActionScheduler 推进；`TriggerPlan` 负责把配置化规则表达成可执行节点树；`ActionSchemaRegistry` 与 `PlannedTriggerActionExecutor` 负责把配置化 Action 落到强类型参数、服务解析和运行时代码；`ExecCtx<TCtx>` 将 `EventBus`、`FunctionRegistry`、`ActionRegistry`、`BlackboardResolver`、`PayloadAccessorRegistry`、`NumericDomains`、`ExecutionControl` 传入条件与动作执行边界。

**框架价值**：Triggering 让主动技能、被动技能、Buff、投射物、区域和表现 Cue 共享同一套“事件 -> 条件 -> Action -> 服务”的执行模型。这样新增一个 `giveDamage`、`addBuff`、`shootProjectile` 或 `playPresentation` Action 后，可以被多个玩法来源复用；同时 Trace、Cue、执行控制和确定性策略也不需要在每个业务系统里重复实现。

**适用场景**：伤害事件触发 Buff、属性变化监听、被动技能生效、投射物命中后继续触发效果、区域进入/停留/离开触发规则、技能 Timeline 节点触发配置化 Action、帧同步/回放要求下的确定性规则执行。

---

### Flow（流程引擎）

Flow 回答"如何组织异步/时间驱动的复杂逻辑"。基于 IFlowNode 节点树，支持事件驱动的 WAKE/PUMP 机制。

```mermaid
flowchart TD
    subgraph Flow["Flow"]
        FR["FlowRunner\nStep(deltaTime) / Wake()"] --> FN["IFlowNode\nEnter / Tick / Exit / Interrupt"]
        FN --> Comp["Composite Nodes\nSequence / Race / Parallel / If / Timeout / Await"]
        Comp --> Leaf["Leaf Nodes\nDo / Wait / WaitUntil"]
        Comp --> Nested["Nested Flow Nodes"]
        Nested --> HFSM["HFSM\nHfsmFlowRunner adapter"]
    end
```



**核心特性**：FlowContext 作为作用域 Type→object 字典，在节点树间传递数据（RAII 风格的 UsingResource）。WAKE/PUMP 机制让 Flow 节点可以等待外部信号（异步完成、事件）再继续执行。AwaitCompletionNode 支持外部 `FlowCompletion.Set()` 信号，RunUntilCompletionNode 在 Flow 内部调度异步任务。**与 HFSM 的集成**：`HfsmFlowRunner` 将 HFSM 作为 Flow 节点嵌入，使状态机可以被 Flow 的 Sequence/Parallel/Timeout 等组合器管理。

**适用场景**：异步技能演出序列、UI 动画编排、跨系统协调流程（等待多个异步操作全部完成）。

---

### HFSM（分层状态机）

HFSM 回答"实体的状态是什么，以及如何在不同状态间切换"。基于 UnityHFSM，提供 ITriggerable 事件驱动转换和 IAction 行为层。

```mermaid
flowchart TD
    subgraph HFSM["HFSM"]
        SM["StateMachine\nRequestStateChange / Trigger"]
        SM --> S1["State\nOnEnter / OnLogic / OnExit"]
        SM --> S2["State\nOnEnter / OnLogic / OnExit"]
        S1 -->|"TransitionAfter / Transition"| S2
        S2 -->|"ITriggerable<TEvent>"| S1
        SM -.->|"Decorator"| Deco["DecoratedState\nBeforeEnter / AfterEnter / ..."]
    end
```



**状态转换**：Transition 支持条件谓词（ShouldTransition） + BeforeTransition/AfterTransition 钩子。TransitionAfter 支持时间延迟转换（可选）。**Trigger 系统**：ITriggerable 接口让状态可以订阅事件并触发转换，实现事件驱动的状态切换（如"受到伤害时切换到受击状态"）。**Action 层**：IAction 接口返回 `BehaviorStatus { Running, Success, Failure }`，支持行为树风格的动作（与 HFSM 正交）。**Decorator 模式**：DecoratedState 包装器为状态提供 AOP 钩子（BeforeEnter/AfterEnter/BeforeExit/AfterExit），用于日志、统计、横切逻辑。

**适用场景**：NPC AI（巡逻→追击→攻击→撤退）、角色状态机（站立/移动/跳跃/受击）、Boss 阶段切换。

---

### 战斗基础设施（Motion + Collision + Navigation）

战斗基础设施回答"实体如何移动、如何感知空间、如何找到路径"。三者都是纯 math/logic 包（不依赖 Entitas/Unity Physics），互相解耦但通过共享接口协作：

- **Motion**（`com.abilitykit.combat.motion`）：MotionPipeline 负责多源（locomotion/dash/jump/path）按 group 优先级和叠加策略求和，`ConfigurableMotionSolver` 负责碰撞约束（墙滑切向迭代、穿墙跳过 sweep、终点按 `EndOverlapPolicy` 处理）。`MotionOutput.DominantCollisionPolicy` 把主导源（dash/blink 等）的 per-skill 墙体策略透传到 solver，无需改 `IMotionSource` 实现者。
- **Collision**（`com.abilitykit.combat.collision.abstractions`）：`ICollisionWorld` 定义碰撞体生命周期（Add/Update/Remove）和查询（Raycast/OverlapSphere/SweepOrientedBox）。`NaiveCollisionWorld` 是参考实现；`GridCollisionWorld` 用 `GridBroadphase` 空间哈希分桶，现在完整实现 `IOrientedBoxSweepCollisionWorld`（含 OBB sweep 窄相），demo 生产已翻转至 Grid。
- **Navigation**（`com.abilitykit.combat.navigation`）：`NavigationGrid` 均匀方格存储 blocked 标记，`GridPathfinder` 用整数格 A\*（cell 坐标 + 代价 10/14 + 固定邻居序 + searchId 计数器）在确定性前提下找到路径，`INavigationWorld` 提供 `FindPath/IsWalkable/TryProjectToWalkable`。demo 的 `MobaNavigationBake` 从 `BattleMapMO` + `ICollisionWorld.OverlapSphere` 采样烘焙 grid。

**设计原则**：三者的约束是"不依赖 Unity、不依赖 ECS、不依赖帧同步"——可在服务器/客户端/编辑器/控制台任何环境复用。运动管线消费导航输出；导航烘焙复用碰撞世界；碰撞广相支撑运动求解和导航烘焙的查询效率。MOBA 示例中 `MobaMotionInitSystem` 装配 `ConfigurableMotionSolver`，`MapRuntimeStage` 调 `nav.Build()`，`MobaPathFollowingSystem` 读 BT 决策目标调 `FindPath` 驱动 `PathFollowerMotionSource`。

---

## src/ 源码结构

`src/` 包含多个 .NET SDK 项目，通过 `<Compile Include>` 引用 `Unity/Packages/` 中的唯一源码。同一套源码既用于 Unity 编译，也用于 `dotnet build` 纯 C# 测试。

### 编译模式


| 模式       | 说明                           | 示例项目                                         |
| -------- | ---------------------------- | -------------------------------------------- |
| **纯引用**  | 直接引用 Unity/Packages 源码，无本地覆盖 | AbilityKit.Core, AbilityKit.Host             |
| **局部覆盖** | 引用源码时排除某些文件，本地提供 .NET 专用实现   | AbilityKit.World.ECS (`Impl/EntityWorld.cs`) |
| **聚合入口** | 不含源码，仅引用其他项目作为依赖             | AbilityKit.Demo.Moba.Infrastructure          |


### 目录树

```
src/
├── AbilityKit.Core/                          # 数学库、日志、事件、GameplayTag、数值系统
├── AbilityKit.GameplayTags/                  # 标签系统
├── AbilityKit.Modifiers/                    # 属性修改器
├── AbilityKit.Diagnostics/                   # 诊断工具
├── AbilityKit.GameFramework/                # 游戏框架基础
│
├── AbilityKit.World.DI/                     # 依赖注入容器
├── AbilityKit.World.ECS/                    # ECS 框架（含 Impl/EntityWorld.cs）
├── AbilityKit.World.Entitas/                 # Entitas ECS 适配
├── AbilityKit.World.FrameSync/              # 帧同步运行时
├── AbilityKit.World.NetworkFragments/       # 帧数据包
├── AbilityKit.World.Snapshot/               # 快照路由
├── AbilityKit.World.StateSync/              # 状态同步与预测
│
├── AbilityKit.Behavior/                     # Behavior 行为系统
├── AbilityKit.BTCore/                      # Behavior Tree 核心
├── AbilityKit.Context/                      # 上下文抽象
├── AbilityKit.Dataflow/                    # 数据流处理
├── AbilityKit.Threading/                    # 线程抽象
├── AbilityKit.Timer/                       # 定时器
├── AbilityKit.Trace/                       # 追踪系统
│
├── AbilityKit.Pipeline/                     # 技能管线编排
├── AbilityKit.Triggering/                   # 事件触发引擎
├── AbilityKit.Triggering.Abstractions/       # 触发器抽象
├── AbilityKit.Ability/                     # 技能系统聚合入口（引用多个子项目）
├── AbilityKit.Ability.Config/              # 技能配置数据模型
├── AbilityKit.Ability.Explain/             # 技能解释框架
│
├── AbilityKit.Flow/                        # 流程引擎
├── AbilityKit.HFSM.Core/                   # 分层状态机
├── AbilityKit.ActionSchema/               # 时序数据格式（DTO + TimelinePlayer）
│
├── AbilityKit.Host/                         # 服务器端抽象
├── AbilityKit.HotReload/                  # 热重载支持
├── AbilityKit.Record/                      # 录像系统
├── AbilityKit.Record.MemoryPack/           # MemoryPack 序列化
│
├── AbilityKit.Network.Runtime/              # 网络运行时
├── AbilityKit.Protocol/                     # 协议定义
├── AbilityKit.Protocol.Moba/               # MOBA 协议
│
├── AbilityKit.Combat.EntityManager/         # 实体管理器
├── AbilityKit.Combat.SkillLibrary/         # 技能库
├── AbilityKit.Combat.Targeting/            # 目标查找
├── AbilityKit.Combat.Motion/               # 移动系统
├── AbilityKit.Combat.Projectile/           # 投射物
├── AbilityKit.Combat.Damage/              # 伤害系统
├── AbilityKit.Combat.Collision.Abstractions/ # 碰撞抽象
├── AbilityKit.Combat.Navigation/          # 导航网格 + 确定性 A\*
│
├── AbilityKit.Game.Battle.Runtime/          # 战斗传输接口
├── AbilityKit.Game.Battle.Transport.Runtime/ # 传输层实现
│
├── AbilityKit.Samples/                      # 示例聚合入口
├── AbilityKit.Samples.Abstractions/         # 示例抽象
├── AbilityKit.Samples.Logic/                # 示例逻辑代码
├── AbilityKit.Demo.Moba.Core/              # MOBA 示例核心
├── AbilityKit.Demo.Moba.Infrastructure/    # MOBA 示例基础设施
├── AbilityKit.Demo.Moba.Console/           # MOBA Console Demo（可执行）
│
├── AbilityKit.CodeGen/                     # 代码生成器
├── AbilityKit.Analyzer/                    # Roslyn 分析器
├── AbilityKit.ThirdParty.Luban.Runtime/    # Luban 配置热更
```

---

## 快速开始

### 环境要求

- Unity 2022.3 LTS 或更高版本
- .NET SDK 6.0+（用于 `src/` 目录下的纯 C# 开发）

### 安装

1.  按需选择所需的包进行复制即可，暂时没拆分对应的package url（参见各模块的 README）

### 运行 Console Demo

```powershell
cd src/AbilityKit.Demo.Moba.Console
dotnet run
```

### 触发器与流程

| 模块 | 核心概念 | 入口 |
|---|---|---|
| **Triggering** | EventBus + TriggerRunner，按 phase/priority 调度触发器 | `com.abilitykit.triggering/Samples/` |
| **Flow** | FlowSession + IFlowNode，支持异步/时间驱动的流程编排 | `com.abilitykit.flow/Samples~/` |

> 完整示例代码见各模块 `Samples/` 目录，包含 TriggerPlan、DSL 写法、Flow 组合等参考实现。

---

## 推荐阅读路径

不同读者可以按目标选择入口，避免一开始陷入所有包和示例细节：

| 目标 | 建议入口 |
| ---- | -------- |
| 快速判断框架是否适合项目 | 先读 `框架价值`、`适用边界`、`示例定位` |
| 理解技能系统主线 | 读 `Pipeline`、`Triggering`、`Ability` 相关模块文档，再看 `demo.moba.*` 的技能运行链路 |
| 理解复杂战斗落地 | 从 `demo.moba.*` 入手，重点看技能输入、Trigger Plan、Buff/Continuous、Projectile、Trace 与配置加载 |
| 理解网络同步能力 | 从 `demo.shooter.*` 和 `world.statesync`、`world.snapshot`、`world.networkfragments` 文档入手 |
| 理解战斗基础设施 | 读 `com.abilitykit.combat.{motion,collision,navigation,projectile,damage,targeting}` 模块文档 + ability-kit skill 的 combat_* 子目录 |
| 只想复用基础模块 | 按 `模块速览` 选择 `core`、`flow`、`hfsm`、`timer`、`context` 等轻量包 |
| 准备接入真实项目 | 先按需裁剪包，再建立配置规范、Trace/诊断入口和自动化回归门禁 |

根 README 只保留项目总览和导航。具体实现细节应优先阅读对应包的 `Document/`、示例文档或源码旁的设计记录。

---

## 文档导航

详细设计文档位于各模块的 `Document/` 或 `Documentation~/` 目录下：


| 模块                                                                                                | 文档                                         |
| ------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| [技术选型](./Unity/Packages/技术选型文档.md)                                                                | 从零开发战斗框架的技术选型                              |
| [Host 模块](./Unity/Packages/com.abilitykit.host.extension/Document/)                               | 游戏服务器运行时框架                                 |
| [状态同步与预测](./Unity/Packages/com.abilitykit.world.statesync/Document/StateSyncDesign.md)            | 客户端预测、Rollback、StateHash 校验                |
| [快照路由](./Unity/Packages/com.abilitykit.world.snapshot/Document/SnapshotRoutingBoundary.md)        | 快照路由与解码层边界                                 |
| [帧数据层](./Unity/Packages/com.abilitykit.world.networkfragments/Document/NetworkFragmentsDesign.md) | FramePacket、RemoteFrameBuffer 帧数据结构        |
| [World DI](./Unity/Packages/com.abilitykit.world.di/Document/)                                    | 依赖注入与组合系统                                  |
| [Flow 模块](./Unity/Packages/com.abilitykit.flow/)                                                  | 流程编排引擎（参考 Samples~/FlowExamples/README.md） |
| [Pipeline](./Unity/Packages/com.abilitykit.pipeline/Document/)                                    | 技能管线编排                                     |
| [Triggering](./Unity/Packages/com.abilitykit.triggering/Document/)                                | 事件触发器系统                                    |
| [ActionSchema](./Unity/Packages/com.abilitykit.actionschema/Document/ActionSchema动作时间线数据模块开发设计文档.md) | 动作时间线 DTO 与运行时数据结构                        |
| [GameplayTags](./Unity/Packages/com.abilitykit.gameplaytags/Document/GameplayTags标签系统模块开发设计文档.md) | 标签系统与状态标识                                  |
| [Modifiers](./Unity/Packages/com.abilitykit.modifiers/Document/ModifiersDesign.md)                 | 通用修正器与参数计算                                 |
| [Trace](./Unity/Packages/com.abilitykit.trace/Document/Trace溯源树模块开发设计文档.md)               | 溯源树、上下文和父子链路追踪                            |
| [Diagnostics](./Unity/Packages/com.abilitykit.diagnostics/Document/Diagnostics诊断与性能分析模块开发设计文档.md) | 诊断、Profiler 与可观测性工具                         |
| [Behavior](./Unity/Packages/com.abilitykit.behavior/Document/Behavior行为执行模块开发设计文档.md)     | 行为运行时与 Pipeline 行为阶段                         |
| [帧同步](./Unity/Packages/com.abilitykit.world.framesync/Document/)                                  | 帧同步与回滚                                     |
| [Targeting](./Unity/Packages/com.abilitykit.combat.targeting/Documentation~/)                     | 目标查找框架                                     |
| [Projectile](./Unity/Packages/com.abilitykit.combat.projectile/Document/)                         | 投射物系统                                      |
| [Motion](./Unity/Packages/com.abilitykit.combat.motion/Document/)                                  | 战斗移动、轨迹和碰撞求解                               |
| [战斗传输层](./Unity/Packages/com.abilitykit.game.battle.runtime/Document/BattleTransportDesign.md)    | 战斗传输层架构、NetworkTransport                   |


---

## 网络同步架构

### 分层模型

```
┌────────────────────────────────────────────────────┐
│                  游戏应用层                         │
│         (MOBAGame、FPSTest、RPGDemo)               │
├────────────────────────────────────────────────────┤
│                  战斗传输层                         │
│  IBattleLogicTransport ← NetworkTransport           │
│                  (game.battle)                     │
├────────────────────────────────────────────────────┤
│               状态同步与客户端预测                   │
│  IClientPredictionModule / IPredictionCoordinator  │
│                  (world.statesync)                 │
├──────────────────────┬─────────────────────────────┤
│        帧数据层       │         快照路由层          │
│  FramePacket、Buffer │  FrameSnapshotDispatcher     │
│   (networkfragments) │         (world.snapshot)    │
├──────────────────────┴─────────────────────────────┤
│                  Host / Session                    │
│           FramePacketNetAdapter                    │
│              (host.extension)                     │
├────────────────────────────────────────────────────┤
│                  网络层                            │
│          INetworkClient、Orleans                   │
│             (network.runtime)                     │
└────────────────────────────────────────────────────┘
```

### 两种同步风格


| 风格                  | 推荐游戏            | 特点                     |
| ------------------- | --------------- | ---------------------- |
| **帧同步（FrameSync）**  | MOBA、格斗、RTS     | 服务器统一驱动，每帧输入同步，客户端本地计算 |
| **状态同步（StateSync）** | MMORPG、大型多人、FPS | 服务器权威，客户端接收快照，可选预测回滚   |
| **混合同步（Hybrid）**    | FPS+技能          | 移动帧同步，伤害状态同步           |


---

## License

MIT License
