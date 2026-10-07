---
name: moba-skill-authoring
description: 开发或修改 AbilityKit MOBA 英雄技能时使用。约束 Luban Excel、Pipeline + ActionEditor、Trigger Authoring 的配置归属、发布顺序、标签与控制效果契约，以及技能验收；也指导何时扩展 PlanAction、组件或领域服务。
---

# MOBA 技能开发规范

技能机制按下文判断落点；生产配置和发布以本节为准。详细命令与运行时路径见 `Docs/MobaSkillPipeline.md`、`Docs/MobaTriggerAuthoring.md` 和 `LubanConfig/Moba/README.md`。不要把下文的历史计数或旧案例当作当前配置来源。

覆盖：**技能扩展的落点判定、交付清单、门禁与反模式**。
不覆盖：调用链细节 → [ability-kit](../ability-kit/SKILL.md)（`skill_buff/`、`triggering/`、`combat_*`）；包装配/配置加载链 → [moba-demo](../moba-demo/SKILL.md)。

## 生产配置归属与发布

| 内容 | 可编辑的权威来源 | 发布产物 |
|---|---|---|
| 英雄、技能等级、Buff、区域、投射物、目标查询、模型和特效资源等表 | `LubanConfig/Moba/Production/Datas/*.xlsx`，其中模型在 `M-模型表.xlsx`，特效资源及跟随模板在 `T-特效表.xlsx` | Unity/Console 的 Luban JSON、`.bytes`、生成代码；特效另发布 `Resources/vfx/vfx.json` |
| 主动技能施法阶段、等待、提交点和效果触发时序 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset` | Unity/Console 的 `skill_flows.json` |
| Timeline Phase 的逻辑动作和表现轨道 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/action_timeline/skill_*.json`，用 ActionEditor 编辑 | 同目录的 `*.moba.logic.json` 和 `*.moba.presentation.json` |
| 技能、被动、Buff 和玩法事件的条件、伤害数值与效果动作 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Packages/*.Module.asset`，与 `Sources/*.trigger.json` 保持同步 | Unity/Console 的 `ability_trigger_plans.json` |
| 标签、规则组、模板与结果映射 | `LubanConfig/Moba/Production/Datas/J-标签表.xlsx`；业务门禁枚举在 `Production/Defines/strong_types.xml`，门禁子表的 `Id` 使用该 Luban enum；规则逐行配置，模板引用规则组 ID | Luban JSON、二进制、生成的 `MobaTagBehaviorGate` 与 `MobaTagResultFlags`；规则说明见 `Docs/MobaTagAuthoring.md` |
| 角色 HFSM 与投射物状态机 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring/*.json`，通过 MOBA 状态机编辑器维护 | Unity/Console 的 `moba/character_hfsm.json`、`moba/actor_state_machines.json` |

`Production/Archive/skill_flows.xlsx`、`Production/Datas/J-资源节点表.xlsx` 的 `Ability节点` 子 sheet 中旧触发器节点、`Resources/ability/triggers/` 分文件和 `MiniTemplate` 均不是这些生产内容的编辑入口。状态机与旧 `effect_plans` 也不再从 Excel 发布；`J-效果表.xlsx` 中未被战斗使用的 `效果`、`旧Buff`、`持续效果` 子 sheet 已删除。不要直接改发布后的 `Resources/moba/*.json`、`Resources/luban/`、`Resources/ability/ability_trigger_plans.json` 或 Console 副本来修技能。

1. 先列清 Skill ID、Flow ID、Effect/Trigger ID、Buff/Area/Query ID 及标签契约；确认每个引用的生产来源和运行时消费者。
2. 按依赖编辑 ActionEditor、Pipeline、Trigger Authoring 和 Excel。先导出 ActionEditor 逻辑/表现 JSON，再发布 Pipeline 和 Trigger Authoring，最后执行 `python LubanConfig/Moba/export_pipeline.py --apply`；新技能的 Flow ID 需先由 Pipeline 发布，Luban 才能校验 Excel 中的 Flow 引用。
3. 修改角色或投射物状态机时，先在 MOBA 状态机编辑器保存并运行 `MobaStateMachinePublisher.Publish`。用 `MobaSkillPipelineExporter.Check`、`TriggerAuthoringMobaMigration.CheckRuntimeBatch`、`MobaStateMachinePublisher.Check` 和 `python LubanConfig/Moba/export_pipeline.py --check-published` 检查发布一致性。Trigger Authoring 的 `GenerateBatch` 会从迁移旧文件覆盖生产模块，日常开发不可运行。
4. 先跑受影响技能的 Unity EditMode 验收，再跑六英雄 .NET 冒烟和 `MobaLubanConfigPipelineTests`。复杂技能至少断言真实状态或运动结果，以及关键 trace；只看到 Action trace 不代表位移、击飞或 Buff 生效。

标签模板要区分施法者自身运动与对敌控制。施法期霸体可以保护自身，但不能让自身 `jump` 套用“目标霸体免疫击飞”的阻断模板；对敌 `pull`/击飞仍应按目标有效标签检查免疫。涉及位移时分别验证墙体穿越、起跳/落地、目标击飞高度和霸体免疫，不用一条效果触发断言代替这些行为。

普通标签是配置事实；不能移动、不能施法等代码结果通过规则映射生成并缓存 flag。不要为普通标记标签添加手写比较分支，也不要直接设置派生 flag。已发布结果的 Key 和 BitIndex 必须保持稳定；新增规则优先复用规则组，修改映射后验证多来源撤销与持续行为结束时的结果位。
技能表的 `Tags` 默认留空；只有 AI 或配置筛选确实需要跨技能语义分类时，才在 `Skill.*` 下按需定义标签并为相关技能赋值。不要按英雄、技能槽位批量建标签；指定单个技能时直接使用技能 ID。`J-标签表.xlsx` 中预置的战斗标签只是词汇目录，接入新机制时还需明确持续对象授予源、消费规则和验收场景。
MOBA 战斗标签只由活跃 `IContinuous` 的 `ApplicationTags` 提供；Buff、位移和技能 Pipeline 应持有对应持续对象。不得向独立 Owner 标签服务写入同一事实，也不得通过硬编码标签名推导控制结果。行为查询需要标签时读取有效标签服务；修改标签生命周期时同时验证表现 Cue 的添加、暂停、恢复、结束与多来源叠加。

## 0. 一句话规范

> **能力看生命周期，参数留配置，逻辑进触发器，关系交服务。**

配套的设计理由（Why）不在这里复述，见 `Docs/design/09-ImplementationExamples/MOBA/`：
`13-ContinuousCapabilityCompositionDesign.md`（持续能力组合）、`14-HeroSkillFormalDesign.md`（六英雄落点与扩展规则）、`18-SkillFlowPipelineConfigDesign.md`（Flow 编排与应用层边界）。

---

## 1. 落点判定树（核心）

拿到一个技能需求，**不要先想"要不要加个组件"**，先问"这个状态挂在哪一维、生命周期归谁"。

| 要落的东西 | 落在哪 | 判据 | 现有实例 |
|---|---|---|---|
| 战斗数值基线 | 通用属性（23 个，`MobaAttributeIds`） | 是所有单位共享的战斗属性 | HP/攻防/移速/暴击/穿透 |
| 当前值 | `ResourceContainer`（Hp / Mana / Rage） | 是"会被消耗的当前值" | 廉颇怒气 |
| 数值修正 | Buff / Continuous 的 `Modifiers[]` | 引用属性 id + 幅度来源表达式 | `MagnitudeCoefficient` + `MagnitudeContextKey` |
| **永久能力** | **ECS 组件** | 生命周期 == 实体的生命周期 | `MotionComponent`（会动）、`FlyingProjectileTag`（是飞行物） |
| **有起止的能力** | **continuous runtime**（`IContinuous`） | 有激活/结束；"短时间给某系统一个能力" | `MobaMotionContinuousRuntime`（dash/jump/pull/blink 共用） |
| 一次施放的临时状态 | `SkillRuntime` / Continuous runtime 字段 | 生命周期 == 一次施放 | 技能冷却、充能 |
| **跨实体关系** | **世界级关系服务** | 状态维度是 (A, B) 而非单实体 | 只有 `MobaPlayerActorMapService` 一个先例 |
| **世界级仲裁** | **领域服务 / 系统** | 多来源叠加后的最终结果 | `MobaEffectiveTagQueryService`、`MobaContinuousTagRuleService` |
| 内容参数 | **配置**（Excel/MO 表 → JSON） | 每个技能/等级不同、策划要改 | `DashArgs` 的 speed/duration/priority |
| 逻辑编排 | **Pipeline + Trigger Authoring** | 施法阶段和事件效果各有权威编辑源 | 见本节生产配置归属 |
| 表现 | presentation cue / 客户端本地 | 只有本人可见、不影响模拟 | `MobaPresentationCueSnapshotService` |

### 逻辑（行为）放哪：从内容到机制

状态那一维之外，**逻辑**也要选落点。顺序是从上往下问，**能落在上面就别往下走**：

| 级 | 落在哪 | 判据 | 实例 |
|---|---|---|---|
| 1 | **内容参数**（Excel/MO 表或 Trigger Action 参数） | 是"数值/开关"差异 | 触发器伤害值、技能冷却、投射物速度 |
| 2 | **skill_flow 阶段树** | 是**这一次施放内部的时序** | Pipeline 的 RulePlan/Timeline/WaitUntil/AwaitEvent/CommitPoint |
| 3 | **TriggerPlan（触发器）** | 是"**当…就…**"，且动作用现有动词能拼出来 | Buff 的 `TriggerIds`、被动、伤害阶段响应 |
| 4 | **PlanAction 动词** | 触发器缺一个动作，但它是**可复用的领域能力** | `dash`/`shoot_projectile`/`spawn_area` |
| 5 | **领域服务 / 系统** | 需要**新状态维度**、每帧遍历实体、或与生命周期/回滚强耦合 | 护盾服务、Motion 服务、Visibility（尚无） |

**第 2 级和第 3 级的分界是这里最容易做错的地方**，也是仓库唯一有成文规则的一条：

> **技能内部的时序归 skill_flow，不归 TriggerPlan。**（原文：*Cast phases and charge state remain responsibilities of the skill pipeline rather than Trigger Plan nodes*，见 `demo.moba.editor/Tests/Fixtures/TriggerAuthoring/README.md`）

具体说：**蓄力/引导/充能、提交点、资源结算、阶段等待** → Pipeline phase；**Timeline 内的动作轨道** → ActionEditor；**事件发生时执行条件与效果** → Trigger Authoring。廉颇三技能已经使用多段阶段和等待，新增复杂技能先检查现有 phase 能否表达，再考虑扩展机制。

**⚠ 命名陷阱**：`Parallel` 有两套不同语义——flow 级 `AbilityParallelPhase` 是**真并行**（子阶段同帧启动、未完成者逐帧推进）；触发器级 `ParallelTriggerPlanExecutable` 是**顺序遍历 + fail-fast**。按名字选用会出错。

**⚠ 扩展点开放性不一致**（2026-09-15 查证）：targeting（`[MobaTargetFilter]` / `[MobaTargetOrder]`）和 PlanAction（`[PlanActionModule]`）都是**特性 + codegen 清单**的开放扩展点，项目侧加一个类即可；但投射物命中策略 `ProjectileHitPolicyFactory` 是 **`internal` 硬编码 switch**，要加命中策略/反射**必须改框架包**。判断"这个需求是便宜还是贵"时，先看它落在哪一类扩展点上。

### 什么时候**不该**用触发器（该写代码）

| 情况 | 该去哪 |
|---|---|
| 需要新的**状态维度**（(来源,目标) 关系、世界级仲裁） | 领域服务 |
| 需要**每帧遍历实体**（高频、确定性敏感） | 系统 |
| 技能内部的**时序/阶段**（蓄力、引导、提交点、资源结算） | skill_flow phase |
| 需要**实体增删的事务性**（召唤/生成/销毁的原子提交与补偿） | 服务（触发器只能"请求"，不负责事务；参考 `MobaHeroReplacementTransactionService`） |
| **纯表现且只有本人可见** | 表现层 / 客户端本地 |

### 什么时候**该**用触发器

同时满足越多越该用：是**内容差异**（不同英雄/技能不同）而非机制共有；是**事件响应**且有明确 event；用**现有动词能拼出来**；需要被 **trace/回放/调试**；需要**策划能改**（热更）。反过来，只要"内容作者改不了、也不该改"，它就不属于触发器层。

### 三个必须回答的问题

1. **它的生命周期等于什么？** 实体 → 组件；一段持续行为 → continuous runtime；一次施放 → runtime 字段；永久但策划要调 → 配置。
2. **它是一维还是多维？** 单实体 → 组件；(来源, 目标) 对 → 关系服务；观察者 × 目标 → 算出来，别存。
3. **它要过几条契约？** 见 §5。要过回滚 / 重连恢复的，按"一等机制"做，别当顺手加的字段。

### 什么情况下**不开**新组件

- 它只服务**一个技能**或**一个英雄**（组件类型数应跟"能力种类"走，不跟"技能数量"走）
- 它是**内容参数**（进了组件就发不了版、改不动数值、长不了等级表）
- 它的生命周期跟着一次施放走（用 runtime 字段）
- 一个实体上会有**多个实例**（多层护盾、多来源标记）→ 用**容器**组件，不要让组件类型本身表示实例（见 `ShieldContainer{ List<ShieldLayer> }`）

### 什么情况下**必须**开组件

- 能力被多个技能/内容条目共用，且生命周期 == 实体
- 状态跨多个来源聚合（如隐身有技能/装备/地形多个来源时的"暴露度"）

### 开组件时的强制约束：让它是**自包含纯数据**

这直接决定它日后回滚/重连的代价。组件按形态分三档（实测自 `Application/Rollback/*.cs`）：

| 组件形态 | 回滚成本 | 实证 |
|---|---|---|
| **自包含纯数据**（值字段 + 普通对象列表） | **低**，模板化照抄 | `MobaActorResourceRollbackProvider` 102 行 / `MobaActorHpRollbackProvider` 156 行 |
| 持有句柄 / 服务引用 | 中，要写重建 + 重新绑定 | `MotionComponent` 的 `Pipeline/Solver/Events`；`BuffRuntime.SkillRuntimeHandle` |
| 状态回滚需要**增删实体** | **高**，是生命周期问题不是字段问题 | `MobaShieldRollbackProvider` 头注释：*cannot safely create or destroy simulation entities* |

**所以：组件里只放值和普通数据，把句柄、服务引用、实体增删挡在组件外面**（重建路径放 Provider / Factory）。这样你就把回滚成本压到最低档。

写 Provider 照抄这两个模板即可：`Pools.GetPool` 取临时 list + `try/finally` 归还，**绝不缓存引用**（池化与快照并存时的唯一读写真坑）。

---

## 2. 新增一个技能：交付清单

按顺序做完，缺一项就到不了"可验收"。

### 2.1 内容（零代码，能覆盖大多数技能）

| 步骤 | 位置 |
|---|---|
| 数值表与技能 Flow 绑定 | `LubanConfig/Moba/Production/Datas/*.xlsx`；Flow ID 由 Pipeline 发布并供导表校验 |
| 技能阶段 | `Configs/Moba/SkillFlowCO.asset`；需要动作轨道时组合 ActionEditor 的 `*.moba.logic.json` |
| 触发器与效果 | `Configs/MobaTriggerAuthoring/Packages/*.Module.asset`；同步 `Sources/*.trigger.json` 后发布聚合 TriggerPlan |
| 表现资源 | `presentation_templates.json` + `Resources/effect/*.prefab` |

编辑与发布命令以本节和 `Docs/MobaSkillPipeline.md`、`Docs/MobaTriggerAuthoring.md` 为准。上表省略的路径前缀均为 `Unity/Packages/com.abilitykit.demo.moba.view.runtime/`。

### 2.2 验收（必须，不是可选）

| 步骤 | 位置 |
|---|---|
| Unity EditMode 验收 fixture | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTest/Acceptance/Heroes/<Hero>/<Hero>SkillAcceptanceTests.cs` |
| 登记门禁 | `tools/test-gates.json` 加 `moba-<hero>-unity` |
| 登记覆盖账 | `tools/moba-hero-acceptance-coverage.json`（逐英雄 5 个能力键：`repeatedRelease/buff/projectile/area/presentation`） |
| 更新校验脚本 | `tools/validate_moba_hero_acceptance_coverage.ps1`（**英雄列表是硬编码的**） |

> 新增英雄要动**三个手工点**（manifest + gates.json + validator 脚本），这是当前扩展面最笨的地方，做的时候别漏。

### 2.3 自检

- [ ] Excel 的角色、技能、等级与技能 Flow 引用能通过 Luban 校验
- [ ] `SkillMO` / `PassiveSkillMO` 能找到 trigger ids
- [ ] Trigger Authoring 产物能被 `TriggerPlanJsonDatabase` 加载
- [ ] 每个 action 的动词都在 §3 的注册表里
- [ ] 自身运动、对敌控制和施法期间标签的组合有行为验收
- [ ] 表现资源存在且被 `moba-content-contracts` gate 的"configured presentation resources"检查覆盖

---

## 3. 新增一种 PlanAction 动词：6 处同步点

按当前注册与生成链检查以下接点；模块数量以源码为准：

| # | 改哪 | 位置 |
|---|---|---|
| 1 | **动作名常量** | `Application/Systems/Bootstrap/TriggeringConstants.cs` 的 `Actions` 类 |
| 2 | Args + Schema + Module 三件套 | `.../Services/Triggering/PlanActions/<分类>/`，基类 `MobaPlanActionModuleBase<TArgs,TModule>`，特性 `[PlanActionModule(order: ...)]` |
| 3 | order 常量 | `PlanActions/Core/PlanActionModuleAttribute.cs:22`（`MobaPlanActionModuleOrders`） |
| 4 | 作者描述符 | `com.abilitykit.demo.moba.editor/Editor/TriggerAuthoring/MobaTriggerAuthoringExtension.cs` 的 `RegisterActions` |
| 5 | 生成清单 | `PlanActions/Core/MobaGeneratedPlanActionManifest.cs`（`AddGenerated` 由 MOBA Source Generator 生成，**不要手改**） |
| 6 | 注册与生成契约测试 | `src/AbilityKit.Demo.Moba.Tests/Skill/PlanActionModuleRegistryTests.cs` |

**权威步骤清单在 `PlanActions/README.md`（9 步，含模块模板代码），以它为准。** 但注意该 README 的目录路径**已过期**——它写 Args 放 `PlanActions/Args`、Schema 放 `PlanActions/Schemas`，实际是按业务分类的 `PlanActions/{Skill,Motion,Gameplay,Presentation,Debug,Targeting}/` 三件套同目录。以实际目录为准。

注册测试应检查每个 `[PlanActionModule]` 都被生成清单覆盖，避免依赖固定模块数量。

---

## 4. 反模式清单（每条都有实证）

| 反模式 | 为什么错 | 实证 |
|---|---|---|
| 给一个技能开一个组件 | 组件类型数跟着技能数膨胀；内容与代码耦合；策划改不动 | 22 个组件全是通用/类别级，runtime 里六个英雄 id 零命中——**这条底线目前守住了** |
| 给特殊数值开新属性 | 属性表/快照/回滚对账面线性膨胀 | `MobaAttributeIds` 23 个全是通用战斗属性，无一个 per-skill |
| 把通用抽象做成万能容器 | 抽象最终退化成几个 setter | `component_templates` 只有 2 个 op（`SetModelId`/`SetLifetimeMs`）、2 条内容、唯一召唤物的引用是空数组 |
| 参数解析了不用 | 能力落差伪装成"已实现" | `SpawnSummonPlanActionModule` 读了 `interval_ms/duration_ms/total_count/query_template_id`，一个都没用 |
| 两套引擎混用 | 事件收不到、行为不一致 | 两套触发器（旧 `MobaTriggerIndexService` 仍挂在 bootstrap 里但**零消费者**）、两套树引擎、两个 HFSM 内核、两套 Effect |
| 只跑过滤后的门禁 | 其他技能或跨模块回归可能未执行 | 按改动范围补充六英雄冒烟、相关验收和发布一致性检查 |
| 英雄 id 进 runtime 代码 | 示例退化成英雄脚本集合 | 目前零命中，必须保持 |
| **以为"不支持"就自己新造** | 能力可能已在更下一层（键控/注册表）实现了 | "多来源独立标记"我判过 C，实际 `BuffRuntimeKey.MatchApplyRequest` 用的是 `MatchBuffAndSource(buffId, **sourceActorId**)`，还有更细的 `MatchInstance(..., sourceContextId)`——**一直是支持的** |
| **给模拟创建/销毁的实体写 state provider 管增删** | 结构性做不到，provider 不能增删实体 | 见 §5.1 裁定的分档 |
| **信注释不信调用方** | 注释会过期、会描述另一条数据流 | importer 写"小兵/召唤物同样按 Unit 处理（P1 简化）"，实际那份注释描述的是**开局 roster**（`PublishSpawnPayload` 下发），战斗中召唤物根本不进快照 |

---

## 5. 扩展门禁：什么算"做完了"

一个状态进入模拟层后，贵不贵取决于它**要过几条契约**：

| 契约 | 要配什么 | 现状 |
|---|---|---|
| 帧回滚 | `IRollbackStateProvider` + 注册进 `MobaRollbackRegistryBuilder` | 15 个 provider |
| 快照 / 重连恢复 | snapshot emitter + `MobaStateImport` | 按领域配 |
| 跨端一致 | 协议 DTO + 状态哈希 | 状态哈希目前只覆盖 transform + HP（`MobaStateHashBuilder.cs`） |
| 确定性模拟 | `Fixed64`、遍历序稳定、禁止 `new Random()` | — |

**判定**：只影响表现、每帧重算的状态 → 四条都不用管，随便加。影响模拟结果的累积量（如"暴露度决定何时显形"）→ 回滚 + 重连两条**必须**配，否则预测/重连会不一致，而且重连归零等于送一次免费重置。

### 5.1 实体生命周期与回滚分档（做召唤/陷阱/分身类技能前必读）

技能**创建实体**时（`spawn_summon`、投射物、区域、未来的分身/陷阱），回滚走哪条路是有裁定的：

| 实体的东西 | 怎么回滚 | 不要做什么 |
|---|---|---|
| **增删本身** | **销毁预测实体 + 重执行创建**（权威侧维护一份存活实体 id 集合；客户端把"本地有、权威集合没有"的预测实体删掉，再重执行命令帧创建） | **不要写 state provider 去管实体增删**——`MobaShieldRollbackProvider` 头注释已写明 provider *cannot safely create or destroy simulation entities* |
| **幸存实体的状态** | 正常写 `IRollbackStateProvider`（§5 上表） | — |
| **跨会话恢复**（重连 / 中途加入） | **暂不承诺**，后置处理 | 不要以为快照会带上——见下 |

**这条裁定的前提必须先补：`ActorIdAllocator` 要进回滚。** 它当前是个纯计数器（`MobaActorSpawnService.cs:119` 注入），**15 个 provider 里没有它、`Reset()` 全仓无调用点**；importer 是**绕开**它（`MobaLogicWorldStateImporter.cs:261` `AllocateActorIdIfMissing = false`）而非同步它。计数器不回退 → 重建会拿到**更高的新 id** → 与 owner link / 命令 target / trace / 快照 netId 全部对不上。修复约三十行（单 int provider，抄 `MobaOwnerBlackboardRollbackProvider`）。**在这条补上之前，重执行重建路径是不可靠的。**

**为什么召唤物不进快照**（别误判成 bug）：`MobaActorSpawnSnapshotService.Enqueue` 的调用方只有**英雄生成 / 英雄替换 / 投射物**三处，召唤物在 spawn 与 despawn **两侧都不进快照**——这是一致的取舍，不是半成品。MOBA 默认走 **Lockstep**，正常对局中各端跑同一套定点模拟、召唤物自然重建，快照只在**跨会话恢复**时才需要。

### 5.2 写 Provider 时最容易踩的两件事（都不是代码问题）

1. **先确认哪个字段是权威源。** `MobaActorHpRollbackProvider` 的 v2 注释是现成的教训：初版快照 HP attribute 的 `BaseValue`，后来才发现真实血量落在 `ResourceContainer.Current`（定点）——**provider 跑通了，但回滚的是错的东西**。这类错误不会让测试变红，只在真实回滚时表现成数值抖动。
2. **回滚的单位是"某一帧的整个世界"，不是"一个组件"。** 真正的工作量在覆盖面有没有缝。已知的缝：投射物运动状态**没有**注册 provider（`ProjectileRollbackProvider` 零注册），但 projectile actor 的 transform 每帧被 `ProjectileWorld` 反写 → 回滚结果下一帧就被盖掉。

### 门禁速查

```
tools/run_test_gate.ps1 -Gate moba-content-contracts      # P1 内容契约（资源归属/业务ID/依赖图/触发器聚合/表现资源/区域时序）
tools/run_test_gate.ps1 -Gate moba-console-smoke          # P0 控制台冒烟
tools/run_test_gate.ps1 -Gate moba-complete-battle-journey # P1 完整对局旅程
tools/run_test_gate.ps1 -Gate moba-<hero>-unity           # 逐英雄 Unity EditMode 验收
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --filter "FullyQualifiedName~BattleScenarioRealSkillTests.Luban_six_heroes_spawn_and_cast_first_skill|FullyQualifiedName~MobaLubanConfigPipelineTests"
```

测试失败应核对当前工作树、发布产物和测试报告；不要沿用历史失败计数或旧基线推断结果。

---

## 6. 配置漂移排查

从失败的技能 ID 向前检查 Excel 行、Pipeline Phase/ActionEditor 引用、Trigger Authoring 模块，再检查各发布器的 Unity/Console 产物。对比源与发布产物的 ID、标签模板及动作参数；不要从历史 `Resources/ability/triggers/` 或旧 Excel 快照回写生产源。运行结果以当前测试报告为准。

---

## 7. 做之前先查（按这个顺序，别跳步）

### 7.1 查证阶梯

1. **查矩阵**：`Docs/design/09-ImplementationExamples/MOBA/21-SkillCapabilityGapMatrix.md`——六条机制轴 × 61 条形态。落在"✓ 能表达"→ 直接配；"A 差动词/规则"→ 按 §3 或开放扩展点；"C"→ 才需要设计。
2. **查有没有现成同类**：位移看 `MobaMotionContinuousRuntime`；周期/持续看 `BuffContinuousRuntime` 和对应 Luban 表；投射物发射看 `MobaProjectileLauncherContinuous`；目标选择看 `Production/Datas/J-技能表.xlsx` 的 `目标查询` 子 sheet——先查正式来源及现有消费形态。
3. **查开放扩展点**（下一节表）——很多需求是"加一个类"，不是"缺机制"。
4. **确认参与哪些契约**：照 §5，特别是有没有创建实体（→ §5.1）。
5. 以上都走不通，才按 §1 判定树设计新机制。

### 7.2 开放扩展点速查

| 要加什么 | 入口 | 开放度 |
|---|---|---|
| 新 PlanAction 动词 | `[PlanActionModule]` + 生成清单 | **开放**（项目侧，6 处同步，见 §3） |
| 新目标过滤规则 | `[MobaTargetFilter((int)SearchTargetRuleKind.X)]` | **开放**（`MobaTargetQueryFactories.cs`，走 `MobaGeneratedTargetQueryFactoryManifest`） |
| 新目标打分器 | `[MobaTargetOrder((int)SearchTargetScorerKind.X)]` | **开放**（同上）。仇恨/威胁打分就落在这里 |
| 新**按 tag 过滤**的目标规则 | 同上——现有 `Whitelist/Blacklist` 只按 ID，不按 tag；"排除隐身单位"需要新规则 | 开放 |
| **投射物命中策略 / 反射 / 弹射** | `ProjectileHitPolicyFactory` 是 **`internal` 硬编码 switch**，`IProjectileHitPolicy` 无注册 API | **关闭**——必须改框架包。注意别把 targeting 包反向拉进 projectile 包，走注入 provider（已有 `IProjectileTrackingTargetProvider` 先例） |

### 7.3 排查纪律（这几轮踩过的坑，写下来防复发）

- **判"某处有缺陷"必须追到数据是谁产生、谁消费**，不能只读消费侧一行。本轮的"召唤物丢 `SummonMeta`"就是这么误判的——importer 收不到召唤物，改它无效。
- **注释可能是过期或描述另一条数据流**。看到注释说的行为，去调用方验证一遍。
- **判"不支持"之前先 grep 键控/注册表**。能力常藏在下一层：多来源标记在 `BuffRuntimeKey` 里，扩展点在特性 + 清单里。
- **结论标"待核"而不是结论**。矩阵初版 8 条 C 里，2 条判错、2 条降级，全是"读一层就下结论"造成的。
- **改动前先确认前提**：如 §5.1 的 `ActorIdAllocator` 未进回滚时，重执行重建是不可靠的——前提没补，写了也白写。

## 能力基线（判断"这个技能要新东西吗"）

`Docs/design/09-ImplementationExamples/MOBA/21-SkillCapabilityGapMatrix.md` —— 六条机制轴 × 61 条技能形态的判定表，经三轮源码重验。**动手前先查它**（见 §7.1）。

**整体结论（别被"缺机制"吓到）**：机制完备度比表面高。真正需要新机制的很少，缺的主要是 **(a) 几个开放扩展点**（§7.2）和 **(b) 关系型存储 + 它们的回滚/重连契约**。**短板在契约层，不在机制层。**

## 相关 skill

- 技能/触发器/BUFF 调用链 → [ability-kit](../ability-kit/SKILL.md)
- 包装配/配置加载/门禁运行 → [moba-demo](../moba-demo/SKILL.md)
- 定点化与确定性约束 → [determinism](../determinism/SKILL.md)
- 帧同步预测回滚 → [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)
- 触发器 Wire/协议字段 → [protocol-wire](../protocol-wire/SKILL.md)

---

*本 skill 与 `Docs/design/09-ImplementationExamples/MOBA/{13,14,18}` 互为镜像：本文是执行手册，文档是设计理由。改动规范时两边都要动。*
