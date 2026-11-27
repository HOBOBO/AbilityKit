# AbilityKit 包使用文档索引

> 本页帮助包消费者选择接入文档。包根 README 负责安装与首次使用，包内文档负责使用契约；跨包架构见[设计索引](../../Docs/design/00-index.md)。

## 从这里开始

| 任务 | 阅读入口 |
| --- | --- |
| 选择整个仓库的阅读路径 | [文档门户](../../Docs/README.md) |
| 查某个包如何使用 | 本页模块列表 → 包根 README → 包内文档索引；缺包根入口的包见[结构盘点](../../plans/2026-11-24-DocumentationSystemInventory.md) |
| 查包之间的职责、依赖和设计取舍 | [框架能力地图](../../Docs/design/01-OverviewAndGettingStarted/00-AbilityKitCapabilityMap.md)、[设计索引](../../Docs/design/00-index.md) |
| 新增或整理文档 | [文档体系与维护规范](../../Docs/design/10-EngineeringQuality/20-DocumentationSystemStandard.md) |

本页的模块列表和组合说明为存量导航，尚未逐包核校全部正文。目录中已有文档、已有示例工程及旧验收结论，均不等于当前版本通过独立消费或发布验证；以目标包的适用范围和实际证据为准。

2026-11-24–25 已完成 D1 基础包的入口与关键契约核校：[Core 首次使用](com.abilitykit.core/Documentation~/Usage.md)、[Deterministic 使用索引](com.abilitykit.deterministic/Documentation~/README.md)、[World DI 使用索引](com.abilitykit.world.di/Documentation~/README.md)。具体范围、实测与未覆盖项见[批次记录](../../plans/2026-11-24-DocumentationSystemInventory.md#7-d1-基础包核校记录2026-11-2425)；Core 的全部历史专题不因本次入口整理统一获得新核校日期。

2026-11-25 已完成 D2 技能与流程包的入口及限定契约核校：[Continuous](com.abilitykit.continuous/README.md)、[Triggering](com.abilitykit.triggering/README.md)、[Pipeline](com.abilitykit.pipeline/README.md)、[Attributes](com.abilitykit.attributes/README.md)、[Flow](com.abilitykit.flow/README.md)、[HFSM](com.abilitykit.hfsm/README.md)。范围和验证限制见[批次记录](../../plans/2026-11-24-DocumentationSystemInventory.md#8-d2-技能与流程包核校记录2026-11-25)。

2026-11-25 完成 D3 核心十包与网络接入/ECS 适配的入口和限定契约核校，见下方 D3 导航与[核心批次记录](../../plans/2026-11-24-DocumentationSystemInventory.md#9-d3-核心世界同步与网络包核校记录2026-11-25)、[网络接入和适配记录](../../plans/2026-11-24-DocumentationSystemInventory.md#10-d3-网络接入与ecs-适配核校记录2026-11-25)。本批选定网络测试 307/308 通过，一项 SDK catalog diagnostic decoder 覆盖断言失败；不将 D3 整域标为完成。

2026-11-25 继续完成 D3 支撑三包及[多人接入指南](../../Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md)：[Room Protocol](com.abilitykit.protocol.room/Documentation~/Usage.md)、[Game Battle Runtime](com.abilitykit.game.battle.runtime/Documentation~/Usage.md)、[GameFramework Network](com.abilitykit.gameframework.network/Documentation~/Usage.md)。本批测试 233/234、完整本地示例 3/3，仍有既有诊断 decoder 缺口，详细范围见[支撑批次记录](../../plans/2026-11-24-DocumentationSystemInventory.md#11-d3-支撑契约与多人接入指南核校记录2026-11-25)。

2026-11-25 补齐 D3 [Protocol Editor](com.abilitykit.protocol.editor/Documentation~/Usage.md) 与 [Coordinator](com.abilitykit.coordinator/Documentation~/Usage.md) 的正式使用入口：区分协议窗口的源写入/编译/导出、旧资产冻结迁移，以及配置/驱动/DTO 的调用契约。限定验证和兼容基线失败见[本批记录](../../plans/2026-11-24-DocumentationSystemInventory.md#12-d3-协议工具与会话协调契约核校记录2026-11-25)；Unity、独立安装与完整会话不在本批验收范围。

### D4 上下文、数据流与战斗查询导航

2026-11-25 完成下列四包的入口和限定契约核校，旧文档路径保留，具体范围见 [D4 记录](../../plans/2026-11-24-DocumentationSystemInventory.md#13-d4-上下文数据流与战斗查询核校记录2026-11-25)。132/132 局部 .NET 测试与四个完整指南示例通过；Damage 已复现的行为限制单独记录，未改运行时。Unity、独立安装、完整结算与性能预算未验收。

| 阅读任务 | 包内使用入口 |
| --- | --- |
| 流程归属、精确实时身份和受管快照 | [Context](com.abilitykit.context/README.md) → [使用指南](com.abilitykit.context/Documentation~/Usage.md) |
| 同步处理阶段、异形传递、槽位与失败 | [Dataflow](com.abilitykit.dataflow/README.md) → [使用指南](com.abilitykit.dataflow/Documentation~/Usage.md) |
| 默认伤害计算、Context 复用及未实现的 Flags | [Combat Damage](com.abilitykit.combat.damage/README.md) → [使用指南](com.abilitykit.combat.damage/Documentation~/Usage.md) |
| 候选、规则、排序与查询租约 | [Combat Targeting](com.abilitykit.combat.targeting/README.md) → [使用指南](com.abilitykit.combat.targeting/Documentation~/Usage.md) |

### D4 战斗索引、碰撞与投射物导航

2026-11-25 核校下列四包的使用入口与限定契约。三个现有本地测试入口共 116/116 通过，另有四个完整指南示例通过；测试与限制的分项证据见[批次记录](../../plans/2026-11-24-DocumentationSystemInventory.md#14-d4-战斗索引碰撞与投射物核校记录2026-11-25)。

| 阅读任务 | 包内使用入口 |
| --- | --- |
| 注册实体、显式换键、查询结果复制 | [EntityManager 使用指南](com.abilitykit.combat.entitymanager/Documentation~/Usage.md) |
| 派生键、后加索引回填、数据替换 | [SkillLibrary 使用指南](com.abilitykit.combat.skilllibrary/Documentation~/Usage.md) |
| 碰撞体生命周期、层过滤、Naive/Grid 选型 | [Collision Abstractions 使用指南](com.abilitykit.combat.collision.abstractions/Documentation~/Usage.md) |
| 发射调度、单一时钟、通知和服务恢复 | [Projectile 使用指南](com.abilitykit.combat.projectile/Documentation~/Usage.md) |

### D4 运动与导航

| 阅读任务 | 包内使用入口 |
| --- | --- |
| 来源装配、固定步长、路径跟随与局部恢复 | [Motion 使用指南](com.abilitykit.combat.motion/Document/Usage.md#11-装配固定步长与局部恢复) |
| 网格构建、查询状态、越界和半径限制 | [Navigation 使用指南](com.abilitykit.combat.navigation/Documentation~/Usage.md) |

本地 .NET suite 85/85、完整指南示例 2/2；[批次记录](../../plans/2026-11-24-DocumentationSystemInventory.md#15-d4-运动与导航核校记录2026-11-25)区分包级证据与宿主恢复语义。

### D4 时间线创作工具

- 编辑源与 MOBA 双产物：[ActionEditorImpl 导出链路](com.abilitykit.actioneditor.impl/Document/ActionEditorImpl动作编辑器运行实现模块开发设计文档.md#四导出链路)。
- DTO 分层、版本校验与可运行示例：[ActionSchema 使用指南](com.abilitykit.actionschema/Documentation~/README.md)。
- 编译、发布与运行时消费：[仓库时间线设计](../../Docs/design/05-CommonModules/08-ActionTimelineDataAndPlayback.md)。

### D4 Excel 配置编辑工具

| 阅读任务 | 包内使用入口 |
| --- | --- |
| 环境、模板、首次生成和导入 | [Excel Sync 包入口](com.abilitykit.excel-sync/README.md)、[使用指南](com.abilitykit.excel-sync/Documentation~/Usage.md) |
| 三方导出、baseline、冲突与项目发布交接 | [Excel / ScriptableObject 设计](../../Docs/design/05-CommonModules/09-ExcelScriptableObjectSync.md) |

现行契约与局部验证见[本批记录](../../plans/2026-11-24-DocumentationSystemInventory.md#17-d4-excel-sync-核校记录2026-11-25)；旧包文档原路径保留为历史。主工程 `moba-config-sync` gate、GUI 与独立安装未重跑。

### D4 配置数据库与代码生成

| 阅读任务 | 权威入口 |
| --- | --- |
| 普通配置登记、加载、查询和 reload | [Ability 包入口](com.abilitykit.ability/README.md)、[ConfigDatabase 使用契约](com.abilitykit.ability/Documentation~/ConfigDatabase.md) |
| MOBA 编译期清单、DLL 构建和诊断 | [CodeGen 包入口](com.abilitykit.demo.moba.codegen/README.md)、[构建与使用](com.abilitykit.demo.moba.codegen/Documentation~/Usage.md) |
| Luban 二进制缓冲区与兼容层 | [Luban Runtime 包入口](com.abilitykit.thirdparty.luban.runtime/README.md)、[使用契约](com.abilitykit.thirdparty.luban.runtime/Documentation~/Usage.md) |
| 跨包设计与内容发布操作 | [配置系统设计](../../Docs/design/05-CommonModules/04-ConfigurationSystem.md)、[生成与发布设计](../../Docs/design/05-CommonModules/07-CodeGenAndLubanProductionPipeline.md)、[生产管线操作](../../LubanConfig/Moba/README.md) |

源码核校、17/19/187 项局部测试、两个完整示例、所有权 SingleStep 和当次只读发布检查见[本批记录](../../plans/2026-11-24-DocumentationSystemInventory.md#18-d4-配置数据库codegen-与-luban-核校记录2026-11-25)。完整 gate、Unity 和原包 CodeGen DLL 源码对应关系仍未验收。

### D3 网络接入与 ECS 适配导航

| 阅读任务 | 包内使用入口 |
| --- | --- |
| 基础连接、请求与共享 SDK | [Network SDK](com.abilitykit.network.sdk/Documentation~/Usage.md) |
| 房间流程、数据面与双连接组合 | [Room](com.abilitykit.network.room/Documentation~/Usage.md)、[Battle](com.abilitykit.network.battle/Documentation~/Usage.md)、[Config](com.abilitykit.network.battle.config/Documentation~/Usage.md)、[Client](com.abilitykit.network.client/Documentation~/Usage.md) |
| 服务端 session、路由与 Host 身份绑定 | [Network Host](com.abilitykit.network.host/Documentation~/Usage.md)、[Host Network](com.abilitykit.host.network/Documentation~/Usage.md)、[KCP listener](com.abilitykit.network.host.kcp/Documentation~/Usage.md) |
| 可选传输与证据边界 | [InMemory](com.abilitykit.network.transport.inmemory/Documentation~/Usage.md)、[KCP](com.abilitykit.network.transport.kcp/Documentation~/Usage.md)、[LiteNet](com.abilitykit.network.transport.litenet/Documentation~/Usage.md)、[WebSocket](com.abilitykit.network.transport.websocket/Documentation~/Usage.md) |
| 协议、战斗组合与外部通道桥接 | [Room Protocol](com.abilitykit.protocol.room/Documentation~/Usage.md)、[Game Battle Runtime](com.abilitykit.game.battle.runtime/Documentation~/Usage.md)、[GameFramework Network](com.abilitykit.gameframework.network/Documentation~/Usage.md) |
| ECS 后端装配与释放 | [Entitas](com.abilitykit.world.entitas/Documentation~/Usage.md)、[Svelto](com.abilitykit.world.svelto/Documentation~/Usage.md) |
| 协议创作、冻结迁移与会话共享契约 | [Protocol Editor](com.abilitykit.protocol.editor/Documentation~/Usage.md)、[Coordinator](com.abilitykit.coordinator/Documentation~/Usage.md) |

---

## 总览文档

Tiny 示例按用途选择包：[纯规则与检查点](com.abilitykit.demo.tiny.logic/README.md)、[实时联机客户端](com.abilitykit.demo.tiny/README.md)、[可选回合制客户端](com.abilitykit.demo.tiny.turn/README.md)。仓库装配与运行门禁见 [Tiny 教程](../../Docs/tutorials/tiny/00-从这里开始.md)，职责和同步选择见 [Tiny 接入基线](../../Docs/design/09-ImplementationExamples/TinyStarterImplementation.md)。

| 文档 | 定位 |
|------|------|
| [技术选型文档](./技术选型文档.md) | 从零开发通用战斗框架的技术选型，涵盖逻辑表现分离、ECS、管线系统、依赖注入、帧同步等核心设计 |

---

## 内部推广分级

AbilityKit 当前按“先基础包收口，再推进 Starter，再扩大到战斗/同步组合”的顺序推广。业务项目不应直接全量引入 `Unity/Packages`，而应按包分级和推荐组合选择依赖。

| 等级 | 包 | 当前定位 | 推广建议 |
|------|------|------|------|
| P0 基础底座 | `com.abilitykit.core`、`com.abilitykit.world.di` | 日志、稳定集合、所有权、时间、兼容中的事件/对象池、World 生命周期和服务装配 | 先补齐 README、样例、测试命令，作为所有 Starter 的第一层依赖 |
| P1 技能核心 | `com.abilitykit.continuous`、`com.abilitykit.triggering`、`com.abilitykit.pipeline`、`com.abilitykit.attributes` | 持续过程生命周期、事件触发、技能阶段编排、属性/修饰器能力 | 作为 `SkillCore` 组合试点，不依赖 Demo 包运行 |
| P2 流程与状态 | `com.abilitykit.flow`、`com.abilitykit.hfsm.core`、`com.abilitykit.hfsm` | 跨帧流程、状态迁移、角色/玩法状态管理 | 只需状态机运行时时使用 HFSM Core；需要 Unity 适配和编辑时再安装 HFSM |
| P3 战斗领域 | `com.abilitykit.combat.targeting`、`com.abilitykit.combat.projectile`、`com.abilitykit.combat.damage`、`com.abilitykit.combat.skilllibrary`、`com.abilitykit.combat.entitymanager` | 目标、投射物、伤害、技能索引、实体索引 | 在 `SkillCore` 跑通后按玩法需要接入 |
| P4 同步与服务端 | `com.abilitykit.world.framesync`、`com.abilitykit.world.snapshot`、`com.abilitykit.world.statesync`、`com.abilitykit.record`、`com.abilitykit.protocol`、`com.abilitykit.host`、`com.abilitykit.host.extension` | 帧同步、快照、回放、协议、Host 和服务端组合 | 只对多人、回放、权威服项目推广 |
| 示例/参考 | `com.abilitykit.demo.*`、`Server/Orleans` | MOBA、Shooter、Orleans 参考实现 | 作为最佳实践阅读，不作为默认业务依赖 |

## 推荐组合

| 组合 | 包含模块 | 适用场景 | 验收标准 |
|------|------|------|------|
| `Foundation` | `core` + `world.di` | 干净项目启动、基础设施验证、服务作用域验证 | 能在纯 C# 或 Unity 中运行最小示例，输出结构化日志，不依赖 Demo |
| `SkillCore` | `Foundation` + `continuous` + `triggering` + `pipeline` + `attributes` | 技能、Buff、被动、事件规则的最小战斗核心 | 能跑 2 到 3 个技能、1 个 Buff、1 个触发规则和对应测试 |
| `BattleRuntime` | `SkillCore` + `combat.targeting` + `combat.projectile` + `combat.damage` | 中大型战斗玩法、命中、投射物和伤害链路 | 能验证目标选择、命中、伤害和 Trace 输出 |
| `SyncRuntime` | `BattleRuntime` + `framesync` + `snapshot` + `statesync` + `record` + `protocol` | 多人同步、回放、重连、状态恢复 | 能验证输入帧、快照应用、状态哈希和回放 |
| `ServerRuntime` | `protocol` + `host` + `host.extension` + 项目服务端适配 | 权威服、房间服、网关服务 | 能启动房间/战斗宿主，并通过 Smoke 验证基础流程 |

## Starter 推进顺序

第一版 Starter 只证明基础包可以独立启动；技能核心不重复造新示例，而是收编 `Samples.Logic` 中已有的 Pipeline、Triggering、Modifiers/属性正式示例与 Web 导出能力。

**源码入口**：以下五个 Starter 工程在当前仓库存在。命令用于定位运行入口；本轮未执行这些工程，不据此声明全部组合已验收。运行前按仓库产物规范将日志写入 `local/Logs/`：

```bash
dotnet run --project src/AbilityKit.Samples.Foundation     # core + world.di：日志/事件/对象池/World DI/宿主 Tick
dotnet run --project src/AbilityKit.Samples.SkillCore      # 上面 + triggering/pipeline/modifiers：技能/Buff/触发规则
dotnet run --project src/AbilityKit.Samples.BattleRuntime  # 上面 + targeting/projectile/damage：目标选择→命中→伤害链路
dotnet run --project src/AbilityKit.Samples.SyncRuntime    # framesync+statesync+record：帧驱动/状态哈希/录制回放/确定性校验
dotnet run --project src/AbilityKit.Samples.ServerRuntime  # host+host.extension：权威服闭环（输入→服务器帧循环→帧包广播）
```

1. `Foundation Starter`：只接 `core`、`world.di`，展示日志、事件、对象池、World 服务注册和一次宿主驱动 Tick。
2. `SkillCore 路线`：复用 `pipeline/basic-phases`、`triggering/basic-event-trigger`、`triggering/condition-blackboard`、`modifiers/attribute-basic` 等现有示例，展示一次技能释放、属性变化、触发规则和结构化输出。
3. `BattleRuntime Starter`：增加目标、投射物和伤害，展示一个可测试的命中链路。
4. `SyncRuntime Starter`：增加输入帧、快照和回放，只面向需要多人同步的项目。

Starter 必须满足：不依赖 `demo.moba.*`、不依赖 `demo.shooter.*`、可纯 C# 运行、可被 Unity 宿主接入、输出可被测试或 Web/Unity 面板消费。

---

## 模块文档列表

### 1. [Host 扩展包](./com.abilitykit.host.extension/README.md)

| 文档 | 定位 |
|------|------|
| [使用索引](./com.abilitykit.host.extension/Documentation~/README.md) | 当前模块选择、固定步长示例、时间与世界启动契约 |
| [开发设计文档](./com.abilitykit.host.extension/Document/Host模块开发设计文档.md) | 整体架构、设计理念、数据流程 |
| [扩展指南](./com.abilitykit.host.extension/Document/Host模块扩展指南.md) | Hook、Feature、Blueprint 扩展机制 |

**核心内容**：游戏服务器运行时框架，管理世界、客户端连接、消息广播

---

### 2. [Flow 模块](./com.abilitykit.flow/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用契约](./com.abilitykit.flow/Documentation~/Usage.md) | Session、Wake、Scope、资源释放及异常边界 |
| [历史文档索引](./com.abilitykit.flow/Document/README.md) | 保留旧概念材料，当前 API 以 Usage 和 Runtime 为准 |
| [开发设计文档](./com.abilitykit.flow/Document/Flow模块开发设计文档.md) | 设计理念、核心概念、节点详解 |
| [扩展指南](./com.abilitykit.flow/Document/Flow模块扩展指南.md) | 自定义节点、组合模式、协作示例 |

**核心内容**：流程编排引擎，用节点树组织异步/时间驱动的逻辑

---

### 3. [通用录像模块](./com.abilitykit.record/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用契约](./com.abilitykit.record/Documentation~/Usage.md) | 记录入口、轨道、随包示例 |
| [仓库回放设计](../../Docs/design/07-NetworkSynchronization/04-ReplaySystem.md) | Record、FrameRecord 与项目回放边界 |

**核心内容**：帧同步游戏的输入录制和回放，支持确定性验证

---

### 4. [能力管线模块](./com.abilitykit.pipeline/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用契约](./com.abilitykit.pipeline/Documentation~/Usage.md) | Run 推进、Context 所有权、阶段隔离和恢复边界 |
| [历史设计索引](./com.abilitykit.pipeline/Document/README.md) | 调试器与设计说明，按页面事实基线阅读 |

**核心内容**：技能/Buff 系统，用管线阶段组织复杂的能力逻辑

---

### 5. [实体管理模块](./com.abilitykit.combat.entitymanager/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用契约](./com.abilitykit.combat.entitymanager/Documentation~/Usage.md) | 注册、显式改键、后加索引、结果视图与失败边界 |
| [仓库索引设计](../../Docs/design/08-GameplayModules/09-EntityAndSkillIndexing.md) | 领域数据与派生索引的交接和验证范围 |
| [历史开发说明](./com.abilitykit.combat.entitymanager/Document/实体管理模块开发设计文档.md) | 保留早期背景，自动更新示例不作现行承诺 |

**核心内容**：用索引表实现高效实体查询，支持单键和多键索引

---

### 6. [技能库模块](./com.abilitykit.combat.skilllibrary/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用契约](./com.abilitykit.combat.skilllibrary/Documentation~/Usage.md) | 派生索引、更新和失败边界 |
| [仓库索引设计](../../Docs/design/08-GameplayModules/09-EntityAndSkillIndexing.md) | EntityManager 与 SkillLibrary 的分工和证据 |

**核心内容**：用索引表实现高效技能查询，支持单键和多键索引

---

### 7. [触发器模块](./com.abilitykit.triggering/Documentation~/README.md)

| 文档 | 定位 |
|------|------|
| [模块设计概览](./com.abilitykit.triggering/Documentation~/Overview.md) | 设计理念、事件驱动与现行接入边界 |

**核心内容**：基于事件的触发器引擎，支持 RPN 条件表达式、确定性回放

---

### 8. [属性系统模块](./com.abilitykit.attributes/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用契约](./com.abilitykit.attributes/Documentation~/Usage.md) | 注册、修改器与静默求值 |
| [仓库设计](../../Docs/design/08-GameplayModules/05-AttributeSystem.md) | Attributes、Modifiers 与玩法集成边界 |

**核心内容**：高性能属性系统，支持 Buff/Debuff 管理、自定义公式、脏标记优化

### D2 其他技能包入口

| 包 | 消费者入口 | 当前核校说明 |
| --- | --- | --- |
| [Continuous](./com.abilitykit.continuous/README.md) | [使用契约](./com.abilitykit.continuous/Documentation~/Usage.md) | 管理器生命周期、准入、绑定器和失败补偿；未验证 Unity Player 或项目恢复链 |
| [Triggering](./com.abilitykit.triggering/README.md) | [文档索引](./com.abilitykit.triggering/Documentation~/README.md) | 即时主线与可选调度分开；正式/兼容 API 以 FormalApiBoundary 为准 |
| [Triggering Action Scheduling](./com.abilitykit.triggering.actionscheduling/README.md) | 可选扁平动作调度扩展 | 按需注入，宿主推进；不属于 `SkillCore` 的必装依赖 |
| [HFSM Core](./com.abilitykit.hfsm.core/README.md) / [HFSM](./com.abilitykit.hfsm/README.md) | [使用契约](./com.abilitykit.hfsm/Documentation~/Usage.md) | Core 提供 Definition → RuntimeBindings 主线；HFSM 附加 Unity 适配、编辑和示例 |

---

### 10. [目标查找模块](./com.abilitykit.combat.targeting/Documentation~/)

| 文档 | 定位 |
|------|------|
| [运行时手册](./com.abilitykit.combat.targeting/Documentation~/Manual.md) | 当前管线契约、性能边界与扩展方式 |
| [组合示例](./com.abilitykit.combat.targeting/Documentation~/Examples.md) | 常见查询组合方式 |
| [历史开发设计文档](./com.abilitykit.combat.targeting/Documentation~/Design/目标查找模块开发设计文档.md) | 早期设计理念与演进背景 |

**核心内容**：通用目标查找框架，支持圆形/扇形/矩形范围、流式处理、零GC

---

### 11. [Core 模块](./com.abilitykit.core/README.md)

| 文档 | 定位 |
|------|------|
| [Core 使用文档索引](./com.abilitykit.core/Documentation~/README.md) | 数学、集合、所有权、日志、事件、对象池和兼容契约 |
| [首次使用](./com.abilitykit.core/Documentation~/Usage.md) | 完整示例、安装条件、所有权和失败边界 |
| [独立 Modifiers 包](./com.abilitykit.modifiers/README.md) | 修改器计算、Handler 扩展和使用示例；不属于 Core 数值系统 |

**核心内容**：Core 提供基础设施；修改器计算通过独立 Modifiers 包接入。

---

### 12. [移动系统模块](./com.abilitykit.combat.motion/Document/README.md)

| 文档 | 定位 |
|------|------|
| [使用文档](./com.abilitykit.combat.motion/Document/Usage.md) | 实体接入、移动来源、策略、求解与状态使用 |
| [跨包设计入口](../../Docs/design/08-GameplayModules/10-MotionPipeline.md) | 移动意图组合、约束求解及相关执行链 |

**核心内容**：通用移动系统框架，支持多来源组合、分组优先级、碰撞解耦、确定性设计

---

### 13. [投射物系统模块](./com.abilitykit.combat.projectile/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用契约](./com.abilitykit.combat.projectile/Documentation~/Usage.md) | 装配、发射、事件消费 |
| [仓库投射物设计](../../Docs/design/08-GameplayModules/04-ProjectileSystem.md) | 运动、碰撞、玩法绑定与回滚边界 |

**核心内容**：高性能投射物系统，支持对象池、帧同步、命中策略、范围效果

---

### 14. [MOBA 逻辑运行时](./com.abilitykit.demo.moba.runtime/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用与源码导航](./com.abilitykit.demo.moba.runtime/Documentation~/Usage.md) | 装配顺序、逻辑职责与源码入口 |
| [仓库 MOBA 专题](../../Docs/design/09-ImplementationExamples/MOBA/00-Overview.md) | 逻辑、表现、工具与验收的整体分工 |

**核心内容**：基于通用能力管线的 MOBA 技能实现，支持时间轴事件、技能库、效果追踪

---

### 15. [MOBA 表现与会话运行时](./com.abilitykit.demo.moba.view.runtime/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用与源码导航](./com.abilitykit.demo.moba.view.runtime/Documentation~/Usage.md) | 客户端装配、表现、调试与兼容入口 |
| [仓库客户端流程设计](../../Docs/design/04-PresentationLayerDesign/04-ClientGameFlowAndPhaseArchitecture.md) | 流程状态机、Feature 与会话边界 |

**核心内容**：基于分层状态机和 Feature 扩展的表现层游戏流程管理

### 16. [World 依赖注入模块](./com.abilitykit.world.di/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用契约](./com.abilitykit.world.di/Documentation~/Usage.md) | 生命周期、所有权与解析 |
| [使用索引与完整示例](./com.abilitykit.world.di/Documentation~/README.md) | 安装、示例运行预期及历史入口 |
| [仓库设计](../../Docs/design/02-LogicalWorldDesign/05-ServiceContainer.md) | 服务容器与逻辑世界装配 |

**核心内容**：逻辑世界的服务依赖注入框架，支持 Singleton/Scoped/Transient 三种生命周期，提供模块化服务注册

### 17. [World ECS](./com.abilitykit.world.ecs/README.md)

| 文档 | 定位 |
|------|------|
| [包内使用契约](./com.abilitykit.world.ecs/Documentation~/Usage.md) | 实体标识、组件和资源边界 |
| [仓库实体设计](../../Docs/design/02-LogicalWorldDesign/02-EntityDesign.md) | 实体生命周期及 Unity/.NET 实现边界 |

### D3 世界、同步与网络核心入口

| 包 | 包内使用入口 | 重点 |
| --- | --- | --- |
| [World ECS](com.abilitykit.world.ecs/README.md) | [索引](com.abilitykit.world.ecs/Documentation~/README.md) | 实体身份、组件、查询与资源边界 |
| [Host](com.abilitykit.host/README.md) | [索引](com.abilitykit.host/Documentation~/README.md) | 宿主、Builder、blueprint 与模块清理 |
| [Host Extensions](com.abilitykit.host.extension/README.md) | [索引](com.abilitykit.host.extension/Documentation~/README.md) | 唯一模拟驱动、模块依赖与世界实例绑定 |
| [FrameSync](com.abilitykit.world.framesync/README.md) | [索引](com.abilitykit.world.framesync/Documentation~/README.md) | 帧就绪、命令历史、Provider 预检及重演 |
| [StateSync](com.abilitykit.world.statesync/README.md) | [索引](com.abilitykit.world.statesync/Documentation~/README.md) | 本地检查点与在线提交的区别 |
| [Snapshot](com.abilitykit.world.snapshot/README.md) | [索引](com.abilitykit.world.snapshot/Documentation~/README.md) | 类型化路由、顺序、所有权与异常 |
| [Network Fragments](com.abilitykit.world.networkfragments/README.md) | [索引](com.abilitykit.world.networkfragments/Documentation~/README.md) | 缓冲、聚合、裁剪与作用域 |
| [Record](com.abilitykit.record/README.md) | [索引](com.abilitykit.record/Documentation~/README.md) | 轨道初始化、格式选择、加载与载荷所有权 |
| [Protocol](com.abilitykit.protocol/README.md) | [索引](com.abilitykit.protocol/Documentation~/README.md) | 生成来源、受约束解码与版本协商 |
| [Network Runtime](com.abilitykit.network.runtime/README.md) | [索引](com.abilitykit.network.runtime/Documentation~/README.md) | 连接、请求、成帧和回调缓冲生命周期 |

跨包交接从[同步能力地图](../../Docs/design/07-NetworkSynchronization/00-SynchronizationCapabilityMap.md#13-核心包消费路径与恢复所有权2026-11-25)进入。以上是本批已核范围；SDK、Room/Battle、服务端监听、可选 transport 和 Entitas/Svelto 适配不在本批结论内。

---

## 🎯 快速导航

### 想了解 Ability-Kit 是什么？

👉 阅读 [Host 模块开发设计文档](./com.abilitykit.host.extension/Document/Host模块开发设计文档.md)

### 想开发新功能？

| 需求 | 推荐文档 |
|------|----------|
| 扩展 Host 模块 | [Host 模块扩展指南](./com.abilitykit.host.extension/Document/Host模块扩展指南.md) |
| 编排复杂流程 | [Flow 模块扩展指南](./com.abilitykit.flow/Document/Flow模块扩展指南.md) |
| 开发自定义节点 | [Flow 模块扩展指南 - 第二章](./com.abilitykit.flow/Document/Flow模块扩展指南.md#二自定义节点开发) |
| 管理世界服务依赖 | [World DI 包入口](./com.abilitykit.world.di/README.md) |

### 想理解架构设计？

| 主题 | 推荐文档 |
|------|----------|
| 为什么用 Hook/Feature | [Host 模块扩展指南 - 设计理念](./com.abilitykit.host.extension/Document/Host模块扩展指南.md#一设计理念host-为什么是一个扩展框架) |
| 为什么用节点树 | [Flow 模块开发设计文档 - 设计理念](./com.abilitykit.flow/Document/Flow模块开发设计文档.md#一设计理念为什么要做-flow-模块) |
| 为什么用 Scope | [服务容器 - 作用域与播种](../../Docs/design/02-LogicalWorldDesign/05-ServiceContainer.md#6-作用域与播种) |
| 为什么用 Module | [服务容器 - 设计意图](../../Docs/design/02-LogicalWorldDesign/05-ServiceContainer.md#9-设计意图与解决的问题) |
| 为什么分 Track | [回放系统 - 通用 Record 容器](../../Docs/design/07-NetworkSynchronization/04-ReplaySystem.md#4-通用-record-容器) |
| 为什么用 Phase | [能力管线模块开发设计文档 - 设计理念](./com.abilitykit.pipeline/Document/能力管线模块开发设计文档.md#一设计理念为什么要做-pipeline-模块) |
| 为什么用索引 | [战斗索引 - SkillLibrary 主数据与派生索引](../../Docs/design/08-GameplayModules/09-EntityAndSkillIndexing.md#4-skilllibrary主数据与派生索引) |
| 为什么用 Trigger | [触发器模块设计概览](./com.abilitykit.triggering/Documentation~/Overview.md#一定位) |
| 为什么用修饰器 | [属性系统 - 系统定位](../../Docs/design/08-GameplayModules/05-AttributeSystem.md#1-系统定位) |
| 为什么用修改器计算 | [Modifiers 包设计原则](./com.abilitykit.modifiers/README.md#设计原则) |
| 为什么用管线模式 | [目标查找模块开发设计文档 - 设计理念](./com.abilitykit.combat.targeting/Documentation~/Design/目标查找模块开发设计文档.md#一设计理念为什么要做目标查找模块) |
| 为什么用来源组合 | [Motion Pipeline 与约束求解](../../Docs/design/08-GameplayModules/10-MotionPipeline.md) |
| 投射物如何创建和回收 | [投射物系统 - 生命周期主线](../../Docs/design/08-GameplayModules/04-ProjectileSystem.md#5-生命周期主线) |
| 为什么用阶段化 | [MOBA 技能 Flow 与 Pipeline 配置设计](../../Docs/design/09-ImplementationExamples/MOBA/18-SkillFlowPipelineConfigDesign.md) |
| 为什么用分层状态机 | [客户端游戏流程运行时架构](../../Docs/design/04-PresentationLayerDesign/04-ClientGameFlowAndPhaseArchitecture.md) |

---

## 架构与使用文档的关系

包内可以解释正确使用 API 所需的局部模型与生命周期。本页“想理解架构设计”中的既有链接按此边界逐项核校；全局分层、跨包装配和执行链以[设计索引](../../Docs/design/00-index.md)中的对应专题为入口。

## 文档贡献与核校

1. 先按[维护规范](../../Docs/design/10-EngineeringQuality/20-DocumentationSystemStandard.md)确定读者和契约所有权，更新现有权威正文。
2. 新建包文档使用 `Documentation~/README.md` 导航；已有 `Document/`、`Documentation/`、`docs/` 保留，先核对消费者再迁移。
3. README 给出定位、环境、安装和最短接入路径；包内正文说明 API、所有权、失败行为和迁移。包内本地链接必须在独立安装后可读。
4. 模板、事实语言和验收规则在维护规范维护；[首轮盘点](../../plans/2026-11-24-DocumentationSystemInventory.md)记录逐包缺口和批次。只修改链接、排版不刷新正文事实基线。

---

## 更新日志

| 日期 | 模块 | 说明 |
|------|------|------|
| 2026-03-19 | All | 初始文档创建 |
| 2026-11-24 | 文档入口 | 明确包使用文档与仓库设计分工，统一贡献入口，标明存量待核范围；不重验包行为或发布资格 |
| 2026-11-25 | D3 核心包入口 | 接通世界、Host、同步、快照、记录、协议及网络运行时十包使用索引；核校范围与验证失败见批次记录，SDK 和适配包待后续 |
| 2026-11-25 | D3 支撑契约 | 接通 Room Protocol、Game Battle Runtime、GameFramework Network 使用正文，修订多人接入指南；局部测试与未覆盖范围见批次记录 |
| 2026-11-25 | D4 Excel Sync | 新增包入口与使用指南，核正导出后的 baseline 刷新与 SO 对齐风险；隔离 Unity 11 项局部检查通过，既有 gate 未重跑；旧说明保留为历史 |
| 2026-11-25 | D4 Config / CodeGen / Luban | 补三包使用入口与契约，纠正双事件通道、旧生成器包与过期 Luban 发布模型；局部 E3、示例和只读产物一致性通过，完整 gate / Unity / 正式发布未覆盖 |

---

*导航最后更新：2026-11-25；正文核校按包分别记录*
