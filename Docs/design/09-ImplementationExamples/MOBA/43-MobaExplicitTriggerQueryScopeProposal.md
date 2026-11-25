# 显式技能 Trigger 查询与 Scope 冲突：实施方案

> 状态：本方案实现、正式发布与专项验收完成；旧 Authoring 样例整组仍有 11 项失败，见下文。
> 日期：2026-11-07。
> 与方案 41 的关系：A–E 不包括 Global Trigger 的新查询谓词或执行临时变量语义，不能由原批准推定授权。
> 确认记录：2026-11-07，用户在方案 43 推荐后回复“按推荐的下一步继续”，批准目标存在查询谓词及对应迁移、发布和验收范围。

## 已核实的冲突

孙尚香技能 `10070101` 的 Timeline 显式调用同号 Trigger，按当前契约必须使用 Global/Callable。其权威 Source 却是 OwnerBound，内部用 `query_target_collection` 将结果写入 `module:nearby_targets`，再由 `for_each` 使用 `module:nearby_target`；查询最多一个对象，有结果时给施法者添加 `10070103` Buff。

`TriggerAuthoringRuntimeExporter.CompileLocalBlackboard` 只允许 OwnerBound 使用 LocalBlackboard；直接改 Global 后正式导入报 TRG2057。GlobalBlackboard 具有共享所有权，不能作为不同角色、Cast 或重入之间的临时查询结果容器。Global 包装 Trigger 再调用 OwnerBound 也违反同一直接调用契约。

证据：

- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Sources/moba-skills.trigger.json`：孙尚香 Trigger 的显式查询、遍历和 Buff。
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/action_timeline/skill_10070101.moba.logic.json`：Timeline 调用。
- `Unity/Packages/com.abilitykit.ability/Editor/Utilities/TriggerAuthoringRuntimeExporter.cs`：CompileLocalBlackboard/TRG2057。
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/Execution/MobaTriggerPlanExecutor.cs`：ExecuteRulePlan/ValidatePlanScope。
- `local/moba-convergence/main-final7.trx`：95 个用例在启动校验阶段被此 Scope 冲突阻断，另一个批跑汇总也带有相同错误，共 96 个失败；不是 96 个独立玩法缺陷。Unity `unity-acceptance4.xml` 的唯一失败亦为 HUD 会话启动时的同一 Scope 校验。

## 建议：增加有限的目标存在查询谓词

本消费者只需要判断“查询是否至少返回一个对象”，没有消费遍历中的目标 ID。建议新增通用 `has_query_target` 谓词，复用现有目标查询请求和确定性排序，不引入临时黑板与跨帧存储。

输入为已有目标来源、中心、过滤、半径、排序和数量约束；输出仅为布尔值。查询必须在当前权威效果上下文中执行，结果列表使用现有池并在 finally 释放，不创建持久集合句柄。无匹配返回 false；非法配置在发布时拒绝，缺失必需运行上下文按现有失败规则处理。

迁移时保留原查询参数 `target_source=6`、`target_filter=257`、`target_radius=4`、`target_order=8196`、`target_max_count=1`。以该谓词条件包围原 `add_buff`，删除这一次的查询集合与 for_each，Trigger 改为 Global/Callable。翻滚、命中回调、强化普攻等其他动作维持其既有配置。谓词实现不识别英雄、技能或 Buff ID。

职责落点为通用谓词注册、Authoring 描述/编译及原目标查询服务；新增谓词属于机制扩展，需要用户确认。具体 ID 仅留在内容配置与验收中。

实施契约：首批 `has_query_target` 接受必需的正整数常量 `query_template_id`，由既有查询模板承载来源、中心、过滤、半径、排序和数量；沿用 Runtime Plan 的双参数函数编码，第二参数保留为 0。模板 ID 由 Authoring 语义引用检查和执行时模板解析验证；执行时发现模板缺失会报配置错误，本方案不新增遍历 Runtime Plan 条件树的启动扫描协议。迁移模板 `50070101` 使用上述原始查询参数与 PreferExplicitTarget 策略，保持原内联查询对准确上下文目标的处理。不引入任意内联查询表达式、执行临时变量域或新的协议字段。

## 可选方案及取舍

| 方案 | 适用性与代价 |
|---|---|
| 目标存在查询谓词（推荐） | 直接表达当前需求，执行结束即无额外状态；覆盖面有限，不支持任意临时值与复杂多次遍历。 |
| 新增执行实例临时变量域 | 支持 Global/Callable 内的查询集合与遍历；必须另行定义准确执行实例、嵌套/重入隔离、集合句柄释放、延迟 continuation、回滚和恢复，影响编译器、运行时与快照，范围明显更大。 |

不采用放宽 Scope 校验、以共享黑板保存临时值、绕过正式执行入口或增加孙尚香运行时分支。

## 验收与待确认决策

验证无目标/有目标/过滤不符/半径边界；施法者自身获得 Buff 且只有一次；两个角色同帧施法及嵌套效果没有结果串用；原位移、强化普攻和命中效果不回归。测试恢复后重放仍确定，并确认无额外权威状态需要加入快照。最后正式导入发布，运行孙尚香完整验收、单机 HUD 会话与 .NET 主模块全量测试。

已确认：采用“目标存在查询谓词”及上述迁移、发布和验收范围；不实施“执行实例临时变量域”。

## 实施记录

- 通用谓词位于 `MobaPlanPredicateFunctions`，通过现有 `MobaActionTargetResolver` 和 `SearchTargetService` 执行模板查询。临时列表从既有池获取，并由 `finally` 归还；不写黑板、不注册集合句柄，不增加回滚或重连字段。
- `MobaTriggerAuthoringExtension` 注册条件和查询模板语义引用；编译器拒绝非正整数常量。孙尚香权威 Source 与查询 Excel 已迁移，历史迁移脚本同步采用相同表达。
- 正式发布时发现旧安妮 Flow 的三个 EffectId 与当前 Trigger Source 不一致，已将旧 Flow 的引用关联到当前 `30030101`、`30030201`、`30030301`；保留其他会话新增的 Flow。全量回归发现小乔时间轴资源引用丢失，已在权威 FlowCO 恢复。Brain 二进制加载测试改为逐条核对表内实际定义，适配新增记录。
- `query-unity-publish3.log`：状态机发布/检查、Source 导入、55 个 Flow 发布/检查、Trigger 两端发布/检查全部完成，Unity 返回 0。
- `query-check-published.log`：Luban 正式产物一致性检查通过；`query-python2.log`：配置工具 135 项测试通过。
- `query-main2.trx`：MOBA .NET 主模块 1010/1010 通过，零跳过；原孙尚香 `invalid_trigger_scope` 启动阻断解除。

- `query-unity-acceptance2.xml`：完整选择了 `MobaTriggerSkillHeadlessTests` 和 `SunShangxiangSkillAcceptanceTests`，45 项中 34 通过、11 失败，零跳过。其中本方案新增查询用例 **12/12**、孙尚香完整验收 **7/7** 全部通过，包括位移、强化普攻、无附近敌方时不加条件移速、投射物和命中区域。
- `query-unity-hud2.xml`：烬 W 本地 HUD 输入 → 正式战斗会话 → 投射物造成真实伤害 **1/1** 通过，Unity 返回 0。首轮组合测试进程未生成完整 XML，不计为通过；第二轮有完整 XML 的技能测试返回 2，HUD 独立返回 0，二者结果不能合并表述为整组通过。

查询专项验证了空查询、同队过滤、半径内外和等于 4 的边界、自身 Buff 只生成一个活跃实例、连续施法者隔离、嵌套执行后外层结果恢复，以及权威位置恢复后的重新求值。恢复用例限定为通过现有 `MobaActorTransformRollbackProvider` 恢复查询事实后重放，不能将它扩大表述为完整战局断线重连验收。

## 仍未解决的旧样例问题与后续顺序

下列失败来自同次完整 XML，未删除或跳过这些用例，也未放宽 Scope 和订阅校验。本方案已解除孙尚香的生产配置启动阻断，但不能据此认定全部 Unity 门禁通过。

| 数量 | 失败事实与证据 | 后续处理边界 |
|---|---|---|
| 8 | SelfBuff、ForwardDash、ShieldAndResource、TrackedProjectile、BoundedGroupPull、PersistentArea、OneShotArea、TargetCollection 在显式 `ExecuteRulePlan` 调用处失败；对应 fixture 仍声明 OwnerBound，与显式效果要求 Global 的契约冲突。 | 优先修正无本地变量样例的 Scope。TargetCollection 使用 LocalBlackboard，不能只改 Global；先明确采用正式 OwnerBound 事件验收，还是另行提案扩展执行临时变量。不能据此扩大本方案的授权。 |
| 2 | HealthPercentDamage 两项预期 20/26，实得约 10.695/13.904；测试使用 Physical，目标已有护甲，两项结果比例一致。 | 将“百分比公式”验收与护甲结算隔离，保留真实伤害和 HP 断言；不改变运行时伤害公式来迎合测试。 |
| 1 | OverhealToShield 初始化拒绝 `MobaHealthChangeResult`：`heal.apply.after` 注册为值类型，但 `MobaTriggerPlanSubscriptionService.BuildArgsTypeCache` 要求 OwnerBound 参数为引用类型。现有治疗发布器另有 object 装箱发布路径。 | 这是事件类型、装箱桥接和订阅约束的契约冲突，须先提出统一方案并确认；不能用放宽全部类型校验或英雄专属订阅绕过。 |

推荐先修复有明确既定契约的样例 Scope 和公式验收，再对集合样例的调用方式及治疗事件类型桥接分别完成机制映射/提案。按 [技能规范](../../../../.claude/skills/moba-skill-authoring/SKILL.md) 的“**规范内组合可继续；新机制须确认**”执行；本次没有实现新的临时变量域或值类型订阅机制。
