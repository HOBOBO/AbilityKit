---
name: moba-skill-authoring
description: 开发、修改或审查 AbilityKit MOBA 英雄技能及相关共享机制时使用。约束 HFSM、Flow、Trigger、Luban 和验收，禁止英雄专属执行特例；规范未覆盖或需新增、改变机制时，先提方案等用户确认。
---

# MOBA 技能开发规范

技能机制按下文判断落点；生产配置和发布以本节为准。详细命令与运行时路径见 `Docs/MobaSkillPipeline.md`、`Docs/MobaTriggerAuthoring.md` 和 `LubanConfig/Moba/README.md`。不要把下文的历史计数或旧案例当作当前配置来源。

覆盖：**HFSM 优先的技能规划、技能扩展的落点判定、交付清单、门禁与反模式**。
不覆盖：调用链细节 → [ability-kit](../ability-kit/SKILL.md)（`skill_buff/`、`triggering/`、`combat_*`）；包装配/配置加载链 → [moba-demo](../moba-demo/SKILL.md)。

## 机制准入：实现前必读

先读 [`Docs/MobaMechanismGovernance.md`](../../../Docs/MobaMechanismGovernance.md)。本节约束下文所有“新增组件/动词/服务”“开放扩展点”和历史案例；技术落点判定不等于扩展授权。

- **禁止英雄专属执行特例。** 共享 Cast、Counter、HFSM、AI、投射物、表现和编辑器/发布链，不得按英雄身份、具体技能/资源 ID 或英雄专属 profile 追加玩法算法。改名、换类、搬到生成器或用配置开关包装同一特例均不合规。
- **规范内组合可继续；新机制须确认。** 复用已定义能力编辑配置、恢复明确契约的修复可按任务推进。新增或改变动词、参数/计数投影、槽位/瞄准策略、状态维度、组件、服务、协议、所有权或恢复语义，即使已有开放注册点，也必须先提出具体方案，由用户明确确认。
- **不确定就先查证并提案。** 规范未覆盖、相互冲突或只有英雄特例可参考时，完成只读调查和可审阅设计，列出事实、缺口、通用方案、影响、验收和待确认决策。等待时不实施依赖该决策的代码、配置或发布物；可继续独立的规范内工作。
- **确认必须有依据。** 用户沉默、测试通过、“继续做英雄”或 Agent 默认建议都不代表确认。既有会话中对相同方案/范围的明确确认继续有效；只对新增决策提问，记录在对应设计中。
- **已有特例不是先例。** 烬等现存专属分支是待治理事项，不代表合规。不得复制或扩大；先提通用迁移方案，确认后保持行为回归再迁移。本次规范收紧不自动授权重构。

英雄技能 ID 可以出现在内容绑定、查询条件、资源映射、验收和样例装配中；区别在于由通用消费者执行已有语义，而非按身份添加算法。具体边界、机制映射及提案模板统一见上述规范。

## 生产配置归属与发布

| 内容 | 可编辑的权威来源 | 发布产物 |
|---|---|---|
| 英雄、技能等级、Buff、区域、投射物、目标查询、模型和特效资源等表 | `LubanConfig/Moba/Production/Datas/*.xlsx`，其中模型在 `M-模型表.xlsx`，特效资源及跟随模板在 `T-特效表.xlsx` | Unity/Console 的 Luban JSON、`.bytes`、生成代码；特效另发布 `Resources/vfx/vfx.json` |
| 主动技能施法阶段、等待、提交点和效果触发时序 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset` | Unity/Console 的 `skill_flows.json` |
| Timeline Phase 的逻辑动作和表现轨道 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/action_timeline/skill_*.json`，用 ActionEditor 编辑 | 同目录的 `*.moba.logic.json` 和 `*.moba.presentation.json` |
| 技能、被动、Buff 和玩法事件的条件、伤害数值与效果动作 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Packages/*.Module.asset`，与 `Sources/*.trigger.json` 保持同步 | Unity/Console 的 `ability_trigger_plans.json` |
| 标签、规则组、模板与结果映射 | `LubanConfig/Moba/Production/Datas/J-标签表.xlsx`；业务门禁枚举在 `Production/Defines/strong_types.xml`，门禁子表的 `Id` 使用该 Luban enum；规则逐行配置，模板引用规则组 ID | Luban JSON、二进制、生成的 `MobaTagBehaviorGate` 与 `MobaTagResultFlags`；规则说明见 `Docs/MobaTagAuthoring.md` |
| 角色 HFSM 与投射物状态机 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring/*.json`，通过 MOBA 状态机编辑器维护 | Unity/Console 的 `moba/character_hfsm_<HeroId>.json`、`moba/character_hfsm_default.json`、`moba/character_bindings.json`、`moba/actor_state_machines.json` |

`Production/Archive/skill_flows.xlsx`、`Production/Datas/J-资源节点表.xlsx` 的 `Ability节点` 子 sheet 中旧触发器节点、`Resources/ability/triggers/` 分文件和 `MiniTemplate` 均不是这些生产内容的编辑入口。状态机与旧 `effect_plans` 也不再从 Excel 发布；`J-效果表.xlsx` 中未被战斗使用的 `效果`、`旧Buff`、`持续效果` 子 sheet 已删除。不要直接改发布后的 `Resources/moba/*.json`、`Resources/luban/`、`Resources/ability/ability_trigger_plans.json` 或 Console 副本来修技能。

模型动作资料由 `Production/Datas/model-animations.xlsx` 独立维护，导出 `moba/model_animations`；导出目标是模型内稳定动作键，不依赖 HFSM 状态或技能。状态表现 `play` 使用 `modelId + animationKey` 引用资料，引用与内联动画字段互斥；未迁移的内联动作兼容保留。导出工具只同步正式资源元数据，不能自动把素材时长写成施法锁定或状态退出时间。完整契约见 `Docs/MobaStateMachineAuthoring.md` 与 `plans/ModelAnimationCatalogDecoupling.md`。

### 技能动作时长与提前退出检查

新英雄动作接入、动画未播完、固定 500ms 退出、调整动作延迟或播放速度时，先读
[ActionEditor 时长与 Cast 接线检查](references/action-timeline-timing.md)。
单次施法时间轴阶段以 ActionEditor 审阅后的完整导出 `length` 为准，包含延迟、串并行编排及显式尾段；
不能用裸 Clip.length、最后一个伤害事件、仅剩逻辑轨道的最晚事件或固定占位值代替。
先确认源时间轴有真实内容、片段结束没有越过总长、Flow 实际引用对应 `*.moba.logic.json`，
再核对发布、HFSM Action 和真实 Animator 播放。logic/presentation 分流保留相同总长，不能在分流后各自缩短。
对 HFSM 播放的动作，另核对它相对 Cast/阶段的起点和退出边；空时间轴不证明覆盖了这些动作。
多阶段 Cast 必须按阶段树计算，独立投射物/区域和长期模式仍由各自生命周期管理。
完整导出时长是该时间轴阶段的依据，不代表整次 Cast 时长、冷却或所有对象寿命都相等。
先修源与既有导出/引用链；另设正常收尾窗口、改取消/冲突规则或快照语义时，仍须单独确认机制。

角色条件组合、状态 Cast 职责、瞄准作用域和 Cast 参数快照必须在 `character_bindings.json` 中显式声明，按 `DefinitionId` 关联；完整字段、恢复和发布契约见 [`Docs/MobaStateMachineAuthoring.md`](../../../Docs/MobaStateMachineAuthoring.md)。Counter 的线性投影、Cast 的提交快照、Brain 的方向意图和 Pull 的距离缩放各自使用独立配置，不得在通用逻辑中通过英雄身份、固定参数编号或路径后缀推断这些职责。这些已批准能力的配置组合可以继续推进；本段不授权新增规范外机制。

### Trigger Scope 契约

Trigger 的 `scope` 必须与配置引用场景匹配；仅 ID 存在、计划可加载并不代表引用有效。修改 Trigger 或技能引用时按下表核对，并运行运行时配置校验及 Trigger 发布一致性检查：

| 引用位置 | 要求的 Scope |
|---|---|
| 主动技能 Flow RulePlan、Timeline 效果、HFSM 逻辑进入/退出动作、Counter 显式回调、投射物和区域事件、玩法事件、Buff 的添加/移除/间隔效果、Continuous 间隔事件 | `Global` |
| PassiveSkill.TriggerIds、Buff.TriggerIds、Continuous.TriggerIds | `OwnerBound` |

不要因 Trigger 位于技能/被动模块，就推断其 Scope；以引用合同为准。`Packages/*.Module.asset` 是发布输入，若编辑了 `Sources/*.trigger.json`，发布前必须先运行 `TriggerAuthoringMobaMigration.ImportProductionSourcesBatch` 导入模块；若直接编辑了模块，则用 Authoring workspace 导出 Source 保持同步。然后严格依次运行 `PublishRuntimeBatch`、`CheckRuntimeBatch`；上一步失败时停止，不得继续发布或把已有产物当成成功结果。每个 Unity 批处理都要确认对应日志实际执行了目标方法并以 `Exiting batchmode successfully` / `return code 0` 结束；项目被其他 Unity 实例占用、Source 校验失败或方法异常都算失败，即使命令启动或产物时间戳更新也不例外。必须确认 Unity `Resources/ability/ability_trigger_plans.json` 与 Console `Configs/luban/ability/ability_trigger_plans.json` 均与 Authoring 模块一致，并在单机 Battle Session 启动校验中没有 `invalid_trigger_scope` 阻断错误。

Global Trigger 若由 Flow、Buff、投射物等位置显式引用，应配置为 Callable；不要同时把它订阅到宽泛的全局事件（例如所有角色的 `skill.cast.complete`），除非动作确实应对该事件的每个发布者执行。发布后检查订阅清单和运行日志，确认没有无关事件进入需要正式 Effect Execution Scope 的效果动作。

## HFSM 优先：先定义状态，再编排技能

新增英雄、重做技能机制或扩展跨多次输入的动作循环时，**先完整规划角色 HFSM，再接 SkillPipeline、Trigger 和表现资源**。HFSM 是角色长期玩法模式与阶段的权威；Pipeline 是一次技能施放的短期流程。不要让一个长 Flow 代替角色状态机。

| 需求的生命周期/职责 | 首选机制 | 规则 |
|---|---|---|
| 持续模式、可被打断/恢复的角色阶段、跨多次输入的动作循环 | 角色 HFSM | 先建状态层级、转换优先级和状态动作组。死亡、控制、施法、移动等通用状态的优先级要明确。 |
| Buff/持续能力开启或结束时改变状态选择、阶段或技能槽位 | Modifier → HFSM 参数 | Modifier 按来源贡献参数值，移除时撤销该来源贡献；HFSM 条件读取合成后的有效值并自行转移。不得由 Trigger、Pipeline 回调或表现回调直接切 HFSM 状态。 |
| 一次有明确开始/结束的技能施放 | SkillPipeline | 只编排这次施放内的校验、短暂等待/蓄力、提交点、取消和有界时序。流程结束后由 HFSM 与持续 Modifier 管理仍然存在的模式。 |
| 命中、计数、加 Buff、伤害、召唤等事件效果 | Trigger Authoring | 事件触发器执行效果或更新可配置数据；若结果要改变角色阶段/槽位，写入对应 Modifier/参数，让 HFSM 完成状态转移。 |
| 当前 HFSM 状态中的动画、特效、音效动作组 | 表现层 HFSM / 状态动作组 | 状态决定播放哪些动作及其循环、次数、串并行组合。表现完成事件不能作为玩法阶段转换或伤害提交的权威条件。 |

### 规划与实现顺序

1. 画出完整角色状态层级，包含通用状态和英雄专属模式/阶段；为每条转换写清来源、条件、优先级、进入/退出清理行为。
2. 列出 HFSM 参数契约：稳定 ID、类型、有效值含义、默认/清理值、多个 Modifier 同时存在时的合成规则，以及每个参数值对应的状态转换。Buff 或持续能力只负责贡献、撤销参数，不直接指定状态名。
3. 将每个机制按生命周期归类：持续模式/跨输入循环归 HFSM；单次施放内有界的连续效果归 Pipeline；事件响应归 Trigger；动作、特效和音效归状态动作组/表现层 HFSM。
4. 为状态动作组定义逻辑与表现职责。逻辑动作在权威阶段提交效果或参数；表现动作播放动画/特效。需要多次攻击时，每次已提交的攻击是独立的技能/事件；由 HFSM 状态和参数决定下一发类别及是否继续。
5. 按本 Skill 的生产源顺序接线和发布，验收状态进入/退出、参数贡献和撤销、打断/恢复、每次提交次数及表现恢复。

### 必须遵守的边界

- **SkillPipeline 不持有长期模式。** 架枪、变身、蓄势姿态等会跨多个输入存续的状态，由 Buff/Continuous 的 Modifier 维持参数贡献，HFSM 管理其阶段和退出。
- **一次 Pipeline 对应一次技能流程。** 不用长时间 `Repeat` 把若干次独立普攻/射击伪装成一次施放。每次输入/射击按当前参数选择对应槽位技能与独立 Flow；HFSM 管理瞄准、射击、终结、收势等跨次阶段。
- **Modifier 改参数，HFSM 做转移。** 参数写入必须有来源和清理路径；禁止用硬编码状态切换绕过参数合成、状态转换条件或优先级。
- 若单次施放确实有界且不可拆的多段时序（例如一次蓄力后在提交点发射），可以用多个 Pipeline phase；这不授权 Pipeline 接管施放完成后仍持续的模式。
- 状态动作可配置单次、多次、循环及串行/并行动作组合。动作播放结束只更新表现播放位置；玩法转移由 HFSM 参数/条件或权威逻辑事件触发。

### HFSM 逻辑动作与表现动作双通道

HFSM 的状态拓扑、状态 ID 和转换条件/优先级是玩法与表现共同遵循的状态契约；执行动作按权威性分为逻辑通道和表现通道。两者可以引用相同状态/转换，也可以按通道分别配置动作组，不能因为状态图相同就让表现层拥有玩法决策权。

| 通道 | 执行者与用途 | 允许的动作 | 禁止事项 |
|---|---|---|---|
| 逻辑动作 | 确定性模拟，在权威 HFSM 的状态进入/退出或转换边界执行 | 引用 Trigger ID 执行已发布的战斗效果；提交/清理由配置定义的 HFSM 参数或其他可回滚领域状态 | 播动画/本地特效；直接依赖客户端对象；由表现完成回调决定伤害或状态转移 |
| 表现动作 | 客户端表现 HFSM，消费权威状态快照与动作游标 | 播放模型 Action/动画、粒子/音效及串并行动作组，可配置次数与循环 | 评估玩法条件、写战斗参数、触发伤害/Buff、决定权威状态转移 |

- 逻辑动作触发的战斗效果必须引用 Trigger Authoring 的稳定 Trigger ID；伤害、Buff、投射物等效果定义仍以 Trigger Authoring 为唯一权威来源，不在 HFSM 动作上复制效果参数。
- 逻辑动作只在权威状态边界执行一次，并纳入确定性、快照/回滚/重演语义。不可逆副作用不得直接放在可能重演的回调中；需走可回滚的模拟命令/事件路径，或以稳定事件键去重。
- 表现 HFSM 由权威状态快照驱动。快照恢复时应能重建当前状态动作组及其游标；表现动作被打断/状态退出时要清理其持有的动画、特效和音效句柄。
- 状态进入动作与转换动作要区分：前者描述进入某个状态后执行的逻辑/表现动作组；后者仅描述特定边界的一次性动作。禁止为了复用动作而在每帧 Tick 中重复触发逻辑效果。
- 角色 HFSM 的 `StateDefinition` 支持 `logicEntryActionKeys` 和 `logicExitActionKeys`，角色状态机 Authoring 会编辑并校验这些字段，`MobaStateMachinePublisher` 将同一状态图发布到 Unity 与 Console。当前逻辑动作键采用 `trigger.enter.<id>` / `trigger.exit.<id>`，运行时在状态边界调用 Trigger Authoring 已发布的 RulePlan；需要改变状态时，Trigger 通过 Modifier 写入 HFSM 参数，再由状态机条件完成转移。Trigger 执行不直接指定目标状态。
- 表现通道由 `moba/character_view_actions` 独立导出，按 HFSM 状态路径和实体变体绑定 Action 组合；表现运行时消费状态快照与动作游标。它可与逻辑通道使用相同状态 ID/转换图，但不执行玩法条件或效果。ActionEditor 技能时间轴仍是另一组按 `*.moba.logic.json` / `*.moba.presentation.json` 分流的施法片段，不能用时间轴动作替代 HFSM 的长期状态动作。
- 状态进入/退出逻辑动作只在权威状态边界执行一次；快照恢复重建状态时不重播动作。可重演的 Trigger 效果必须使用现有确定性模拟/回滚路径；不可逆表现或外部副作用不能从逻辑通道直接发出。新增动作类型或字段时，必须同时维护 Authoring 校验、运行时绑定、两端发布一致性、回滚行为和验收，不能只给 JSON 增加未消费字段。

换弹等“状态进入即发生一次逻辑结算，并伴随一段表现”的机制应拆成同一 HFSM 状态的双通道动作：逻辑动作在进入边界通过 Trigger ID 执行换弹结算/参数更新，表现动作播放 `AA_Reload`；状态离开由权威参数条件驱动，动画播完只影响表现游标，不得让客户端动画回调完成换弹。

烬 R 是这套边界的参考案例：架枪入口 Flow 结束后，R 模式 Modifier 持续贡献 HFSM 阶段和攻击槽参数；每次 R 射击使用独立技能流程，射击计数/类别通过参数贡献影响 HFSM 后续状态。完整状态与参数规划见 `Docs/design/09-ImplementationExamples/MOBA/23-CharacterHfsmPlanningAndJhinMapping.md`。该案例描述目标架构时，以本节规则为准，不要把文档里的阶段性接入状态当成新英雄模板。

1. 先完成 HFSM 状态层级图与参数契约，确认持续模式、单次技能流程和每次输入的技能槽位如何衔接；需要新增/修改角色状态机时，先在 Authoring 定义并验证它们。
2. 再列清 Skill ID、Flow ID、Effect/Trigger ID、Buff/Area/Query ID 及标签契约；确认每个引用的生产来源和运行时消费者。
3. 再按依赖编辑 ActionEditor、Pipeline、Trigger Authoring 和 Excel。先导出 ActionEditor 逻辑/表现 JSON，再发布 Pipeline 和 Trigger Authoring，最后执行 `python LubanConfig/Moba/export_pipeline.py --apply`；新技能的 Flow ID 需先由 Pipeline 发布，Luban 才能校验 Excel 中的 Flow 引用。
4. 修改角色或投射物状态机时，先在 MOBA 状态机编辑器保存并运行 `MobaStateMachinePublisher.Publish`。用 `MobaSkillPipelineExporter.Check`、`TriggerAuthoringMobaMigration.CheckRuntimeBatch`、`MobaStateMachinePublisher.Check` 和 `python LubanConfig/Moba/export_pipeline.py --check-published` 检查发布一致性。Trigger Authoring 的 `GenerateBatch` 会从迁移旧文件覆盖生产模块，日常开发不可运行。
5. 先跑受影响技能的 Unity EditMode 验收，再跑六英雄 .NET 冒烟和 `MobaLubanConfigPipelineTests`。Unity 测试必须等待 Test Runner 完成，并检查 NUnit XML 中匹配用例数及 `total/passed/failed/skipped`；仅有进程退出码、日志文件或 XML 文件存在不构成通过证据。复杂技能至少断言 HFSM 状态与参数变化、真实战斗结果及关键 trace；只看到 Action trace 不代表状态转移、位移、击飞或 Buff 生效。

标签模板要区分施法者自身运动与对敌控制。施法期霸体可以保护自身，但不能让自身 `jump` 套用“目标霸体免疫击飞”的阻断模板；对敌 `pull`/击飞仍应按目标有效标签检查免疫。涉及位移时分别验证墙体穿越、起跳/落地、目标击飞高度和霸体免疫，不用一条效果触发断言代替这些行为。

普通标签是配置事实；不能移动、不能施法等代码结果通过规则映射生成并缓存 flag。不要为普通标记标签添加手写比较分支，也不要直接设置派生 flag。已发布结果的 Key 和 BitIndex 必须保持稳定；新增规则优先复用规则组，修改映射后验证多来源撤销与持续行为结束时的结果位。
技能表的 `Tags` 默认留空；只有 AI 或配置筛选确实需要跨技能语义分类时，才在 `Skill.*` 下按需定义标签并为相关技能赋值。不要按英雄、技能槽位批量建标签；指定单个技能时直接使用技能 ID。`J-标签表.xlsx` 中预置的战斗标签只是词汇目录，接入新机制时还需明确持续对象授予源、消费规则和验收场景。
MOBA 战斗标签只由活跃 `IContinuous` 的 `ApplicationTags` 提供；Buff、位移和技能 Pipeline 应持有对应持续对象。不得向独立 Owner 标签服务写入同一事实，也不得通过硬编码标签名推导控制结果。行为查询需要标签时读取有效标签服务；修改标签生命周期时同时验证表现 Cue 的添加、暂停、恢复、结束与多来源叠加。

## 0. 一句话规范

> **长期状态归 HFSM，Modifier 改参数驱动转移；Pipeline 只管单次短流程，事件效果归触发器。**

配套的设计理由（Why）不在这里复述，见 `Docs/design/09-ImplementationExamples/MOBA/`：
`13-ContinuousCapabilityCompositionDesign.md`（持续能力组合）、`14-HeroSkillFormalDesign.md`（六英雄落点与扩展规则）、`18-SkillFlowPipelineConfigDesign.md`（Flow 编排与应用层边界）。

---

## 1. 落点判定树（核心）

拿到一个技能需求，**先问它是跨输入存在的角色状态，还是一次施放内的短期流程**，再判断生命周期和落点；不要先想"要不要加个组件"。

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
| 逻辑编排 | **HFSM + Pipeline + Trigger Authoring** | 长期角色模式/阶段归 HFSM；单次施放内时序归 Pipeline；事件响应归 Trigger | 见本节 HFSM 优先规则 |
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

### 什么时候**不该**用触发器（可能需要提出代码扩展方案）

下表只判定技术落点；新增或改变机制先按“机制准入”提交方案并获得用户确认。

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

新增英雄或包含持续模式/跨输入阶段的技能，先完成上述 HFSM 状态图和参数契约，再开始填写下表。

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

- [ ] 机制映射有规范和消费者证据；新增/改变机制已获用户明确确认并记录范围
- [ ] 受影响共享链没有英雄身份驱动的特殊算法；现存特例未被复制或扩大
- [ ] Excel 的角色、技能、等级与技能 Flow 引用能通过 Luban 校验
- [ ] `SkillMO` / `PassiveSkillMO` 能找到 trigger ids
- [ ] HFSM 状态层级、转换条件/优先级和参数契约已定义；持续 Modifier 有来源、合成与撤销路径
- [ ] Pipeline 只描述单次有界施放；跨多次输入的模式/循环由 HFSM 管理，每次输入使用对应技能流程
- [ ] Trigger Authoring 产物能被 `TriggerPlanJsonDatabase` 加载
- [ ] 每个 action 的动词都在 §3 的注册表里
- [ ] 自身运动、对敌控制和施法期间标签的组合有行为验收
- [ ] 表现资源存在且被 `moba-content-contracts` gate 的"configured presentation resources"检查覆盖

---

## 3. 新增一种 PlanAction 动词：6 处同步点

**前置条件：动词的通用语义、职责、生命周期和验收方案已由用户明确确认，并写入对应机制契约。** 本节只说明获准方案的接线步骤，不能因注册点开放就自行新增动词。

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
| 用长 SkillPipeline/Repeat 托管持续模式或多次独立输入 | Pipeline 生命周期被错误拉长，状态优先级、打断恢复、槽位切换和表现状态失去 HFSM 的统一管理 | 烬 R 目标架构：架枪入口 Flow 结束，Modifier 驱动 R 阶段参数；每次射击是独立技能流程 |
| Trigger/表现回调直接切 HFSM 状态 | 绕过参数合成、来源清理、转换条件和优先级，状态结果无法由配置契约解释 | HFSM 参数由持续 Modifier 投影；状态机条件按有效参数转换 |
| 给一个技能开一个组件 | 组件类型数跟着技能数膨胀；内容与代码耦合；策划改不动 | 按通用能力与生命周期建模；不能用历史组件数量或英雄 ID 搜索宣称当前已合规 |
| 给特殊数值开新属性 | 属性表/快照/回滚对账面线性膨胀 | `MobaAttributeIds` 23 个全是通用战斗属性，无一个 per-skill |
| 把通用抽象做成万能容器 | 抽象最终退化成几个 setter | `component_templates` 只有 2 个 op（`SetModelId`/`SetLifetimeMs`）、2 条内容、唯一召唤物的引用是空数组 |
| 参数解析了不用 | 能力落差伪装成"已实现" | `SpawnSummonPlanActionModule` 读了 `interval_ms/duration_ms/total_count/query_template_id`，一个都没用 |
| 两套引擎混用 | 事件收不到、行为不一致 | 两套触发器（旧 `MobaTriggerIndexService` 仍挂在 bootstrap 里但**零消费者**）、两套树引擎、两个 HFSM 内核、两套 Effect |
| 只跑过滤后的门禁 | 其他技能或跨模块回归可能未执行 | 按改动范围补充六英雄冒烟、相关验收和发布一致性检查 |
| 按英雄/技能身份在共享执行链添加算法 | 示例退化成英雄脚本集合；枚举、profile 或配置开关包装同样有问题 | 既有 Cast/Counter/HFSM/AI 专属分支须审计迁移，不能作为模板；数据绑定与验收 ID 不在此禁令范围 |
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

**判定**：只影响表现、可由权威快照每帧重建的派生状态，通常不需新增模拟回滚字段，但仍须定义表现清理和恢复。新增/改变表现机制也受机制准入约束，不能自行扩展。影响模拟结果的累积量（如“暴露度决定何时显形”）必须明确回滚与重连契约，否则预测/重连会不一致，重连归零还可能造成玩法重置。

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

### 快速迭代验收（小步开发默认）

不要每改一个技能细节就重复启动完整英雄门禁。Unity 冷启动和全英雄夹具通常比单个断言耗时更多；开发迭代按“改动边界 → 最小有行为意义的测试 → 本批完整门禁”分层，快速结果不替代合并/交付门禁。

1. **先判断改动落点并选测试边界。** 纯配置引用/发布漂移先跑对应 Authoring `Check`、`--check-published` 或内容契约；单个技能逻辑选该技能的 acceptance 方法；HFSM 参数、状态进入/退出、打断恢复选覆盖完整状态转换的测试；模型动作/时间轴特效选真实 Unity 消费测试；运行时共享机制则选受影响模块测试，不能用一个英雄用例代表跨英雄回归。
2. **用程序集和完整测试名过滤，只启动一次 Unity。** 仓库已有 `tools/run-unity-editmode-tests.ps1`，例如：

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 `
     -TestAssembly AbilityKit.Game.UnitTests `
     -TestFilter AbilityKit.Game.Tests.JhinUnityPresentationConsumptionTests.WAndEFromAttackSlotRestoreTheSelectedNormalAttackAction `
     -ResultsPath local/Logs/jhin-focused.xml
   ```

   将程序集、测试 fixture/method 和结果路径替换为当前改动对应项。也可在完整 gate 中用 `-StepName` 只执行一个已登记步骤；需要仅跑一个 NUnit 方法时优先用上面的 `-TestFilter`。同一 Unity 项目已被编辑器锁定时，不要并发启动 batchmode；等待/关闭 AbilityKit 项目对应的实例后再运行，不要影响其他 Unity 项目。
3. **每次快测都核对测试确实执行。** 必须检查 NUnit XML 的 `total > 0`、`failed = 0`、`result = Passed`，并确认测试方法名匹配；编译失败、零匹配、缺失 XML 或仅有进程退出码都不算通过。将 XML 与 Unity log 留在 `local/Logs/` 便于失败定位。
4. **按本批改动升级验收。** 连续修改同一技能时，可重复运行该技能的聚焦测试；当同批配置/逻辑/表现接线完成后，运行一次对应 `moba-<hero>-unity` 完整门禁。改了跨英雄共享运行时、发布器、Authoring schema 或参数合成规则时，再加对应内容契约、模块/.NET 冒烟和相关英雄覆盖。发生失败先读本次 XML/log 与当前源/发布产物，不要直接重跑完整门禁来替代定位。
5. **把行为链断言写在同一个高价值场景里。** 涉及技能接入时，优先让一次场景覆盖输入/施法成功、权威状态/参数、效果提交或命中结果、状态/槽位恢复、实际模型动作/特效创建与清理。状态参数驱动切换时，断言状态路径和参数值；被短 Pipeline 打断后恢复时，断言模式/槽位贡献仍在且动作对应；表现播放完成不得代替逻辑状态或伤害断言。不要为每个中间 helper 另起一个完整 Unity gate。

6. **区分快速验收与最终目视。** `-nographics`/EditMode 能证明资源被加载、Animator 状态被选择、时间轴或投射物 VFX 被消费及清理；不能证明最终画面观感、指示器形状、朝向、挂点/缩放和特效时序。上述视觉属性仍需在有画面的本地模式验收，除非已有可重复的图像/空间断言覆盖。

7. **投射物特效按完整消费链验收。** 对“从角色发射、飞行后落地/命中”的技能，单测不能止于配置里有 VFX ID 或 prefab 可加载。至少追踪并断言：逻辑生成的 projectile snapshot → 表现 handler 解析模板并实例化 → 跟随对象/位置绑定正确 → prefab 所需 Animator trigger 实际触发（trigger 名必须与 prefab Animator 参数一致）→ Projectile Exit、取消或打断时释放句柄。若通用 spawn handler 没有消费某个已配置的 trigger，应修正通用消费契约并加消费测试；不要只在英雄层制造一个绕过通用链路的补丁。
8. **区分部署结果和部署过程。** 陷阱/召唤物最终出现在地面，不代表“从角色飞出并抵达落点”的表现链已实现。逻辑测试要验证实体只在权威到达/部署点创建；表现测试要验证飞行 VFX 从施法者产生、随投射物移动，并在退出时清理；部署动作 trigger 也要单独验证。只有结果、过程和清理都覆盖，才能称为投射物部署链自动化通过。
9. **测试过滤器必须落到真实用例。** 初次运行若 NUnit XML 的 `total/testcasecount` 为 0，或测试方法名与预期不符，应视为过滤器/程序集选择错误，修正后重跑；退出码为 0、Unity 成功启动或生成了 XML 都不能替代用例确实执行。最终记录测试全名、`total/passed/failed` 和报告路径，避免把零用例误记为通过。

---

## 6. 配置漂移排查

从失败的技能 ID 向前检查 Excel 行、Pipeline Phase/ActionEditor 引用、Trigger Authoring 模块，再检查各发布器的 Unity/Console 产物。对比源与发布产物的 ID、标签模板及动作参数；不要从历史 `Resources/ability/triggers/` 或旧 Excel 快照回写生产源。运行结果以当前测试报告为准。

---

## 7. 做之前先查（按这个顺序，别跳步）

### 7.1 查证阶梯

1. **查矩阵**：`Docs/design/09-ImplementationExamples/MOBA/21-SkillCapabilityGapMatrix.md`——能力行仅作检索入口。标为“能表达”仍需核对当前规范、消费者与证据；“差动词/规则”及机制缺口都须先提通用方案，用户确认后再按 §3 或开放扩展点实现。
2. **查有没有现成同类**：位移看 `MobaMotionContinuousRuntime`；周期/持续看 `BuffContinuousRuntime` 和对应 Luban 表；投射物发射看 `MobaProjectileLauncherContinuous`；目标选择看 `Production/Datas/J-技能表.xlsx` 的 `目标查询` 子 sheet——先查正式来源及现有消费形态。
3. **查开放扩展点**（下一节表）——判断技术代价；“加一个类”也可能改变机制，不能绕过用户确认。
4. **确认参与哪些契约**：照 §5，特别是有没有创建实体（→ §5.1）。
5. 规范没有明确覆盖或需改变机制时，按 §1 形成设计方案并等待用户确认；不能先接入再补规范。

### 7.2 开放扩展点速查

“开放”仅描述技术注册能力；下表任何新语义均须先经用户确认。

| 要加什么 | 入口 | 开放度 |
|---|---|---|
| 新 PlanAction 动词 | `[PlanActionModule]` + 生成清单 | **技术开放，机制方案先确认**（项目侧，6 处同步，见 §3） |
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
## 投射物拦截配置（2026-11-12 已批准契约）

既有单位分类与静态障碍可用投射物表 `CollisionRules` 有序数组配置 Ignore/Hit/Block。Role 1 冻结追踪目标、2 其他 Actor、3 静态世界；MainType/UnitSubType/Relation 省略或 0 为任意。Relation 1 敌方、2 友方、3 中立/未分队；Response 0 Ignore、1 Hit、2 Block。首项匹配，未匹配沿用既有响应；自身忽略，Hit 不放宽友伤和 HitUnitSubType，世界只允许 Ignore/Block。规则在发射时冻结，随值快照恢复并绑定当前世界 registry；不得捕获旧世界或按英雄识别。碰撞层掩码先决定参与集合，规则再决定响应。完整契约见 `Docs/design/08-GameplayModules/04-ProjectileSystem.md` §10.1。主动拦截墙/护盾等仍须另提方案。

## 8. 通用英雄接入基线（必须先完成）

新英雄或重做英雄技能前，先填写并评审 [`27-MobaHeroSkillIntegrationBaseline.md`](../../../Docs/design/09-ImplementationExamples/MOBA/27-MobaHeroSkillIntegrationBaseline.md) 的英雄登记表。该基线是跨英雄复用的职责和验收契约，英雄文档只记录差异，不复制发布流程。

登记每个机制的“规范内复用 / 待确认 / 已确认 / 既有违规待治理”状态与依据；待确认项不得进入后续实现批次，记录用户决定后才更新契约并推进。

必须先完成以下四项再改 Trigger、Flow 或运行时代码：

1. 画出角色 HFSM，标明长期模式、阶段状态、优先级、打断和恢复。
2. 建立参数契约，记录参数 ID、类型、默认值、Modifier 来源、合成和撤销规则。
3. 将每个机制归类为 HFSM、短期 SkillPipeline 或 Trigger，并写出唯一提交点。
4. 列出逻辑/表现双通道 Action、模型动作、特效模板、Payload 字段和 P0-P5 验收证据。

执行顺序固定为“契约 → HFSM/参数 → 单链路逻辑 → 状态转移 → 双通道表现 → 英雄完整门禁”。R 等跨多次输入的模式必须由 HFSM 和 Modifier 持有；每次射击或输入使用独立短 Pipeline。Trigger 只能更新效果、计数或 Modifier，不能直接指定 HFSM 目标状态；表现回调不能决定伤害、计数或状态退出。

排查失败时按“权威源 → 发布产物 → 运行时消费者 → 验收证据”逐层回溯，禁止直接修改生成 JSON 作为修复方案。
