# MOBA 触发器编辑

2026-11-24 已批准的数值扩展：`magnitude_terms` 是有序 JSON 数组字符串，与旧 Base/Secondary 来源字段互斥。每项声明 `type`（0 Fixed、2 Attribute、6 SkillLevelParameter、7 HeroLevelSkillParameter）、`value` / `attribute` / `parameter`、可选 `coefficient`、属性项的 `coefficient_parameter` 及 `source_role`。例如 `[{"type":6,"parameter":"damage"},{"type":2,"attribute":4,"coefficient_parameter":"physicalAttackRatio","source_role":1},{"type":2,"attribute":5,"coefficient_parameter":"magicAttackRatio","source_role":1}]`。各项先按声明顺序求和，所得单一数值再进入已有生命项、倍率和一次标准伤害事务。技能参数读取原 Cast 的冻结表；属性按既有实时语义求值。整体冻结可沿用 `magnitude_evaluation=1` 与 `magnitude_capture`，不增加逐项持久状态；旧公式配置继续有效。

`spawn_summon` 可配置 `birth_attributes` JSON 数组字符串，每项为 `{"attribute":2,"magnitude_terms":[{"type":6,"parameter":"summonHealth"}]}`；同一属性不得重复。动作在创建前求得全部值，召唤领域按“模板 → 继承 → 显式覆盖 → 资源初始化”应用；Trigger 不直接写 Actor。`duration_skill_level_parameter` 从原 Cast 读取毫秒值，与固定 `duration_ms` 互斥。没有新字段时保持旧召唤路径。恢复读取既有实例属性、资源及寿命，不重新执行出生覆盖或历史召唤动作。

`payload_actor_type(participant, main_type, unit_sub_type?)` 从事件参与者读取既有角色分类（0 为目标，1 为来源）；角色或组件缺失时失败，不要求参与者仍存活或可选择。建筑同样属于 Unit 子类型，内容必须显式允许战斗子类型，不能假定 MainType Unit 已排除防御塔。

没有原始 Cast 时，同步正式效果可在自身 EffectExecutionScope 临时黑板中显式读写 `skill_runtime:effect.*`。每个效果身份独立持有黑板，作用域结束即丢弃，不合成 Cast、目标或子对象的持久黑板。原 Cast 效果保留既有黑板及回滚契约。普通未过滤的 Self 动作直接解析施法者身份，包括进入死亡时的生命周期 Buff；模板或过滤查询继续采用战斗搜索过滤，伤害与控制消费方仍各自检查合法性。

`spawn_area.forward_yaw_deg` 是可选的纯形状偏航角（默认 0，正值将 +Z 转向 +X）。瞄准相对位置偏移保留原基准；解析后的 Forward 沿既有 Area 快照路径保存。

`sample_skill_parameter(skill_id, parameter, result)` 读取施法者当前已学习等级及指定生产等级参数；未学习技能返回零，不改变原 Cast 参数快照。`try_consume_resource(resource_type, amount, death_cast_skill_id, result)` 原子支付独立周期消耗，返回数值 1/0，不因原 Cast 已预留激活消耗而跳过支付。所有者死亡时，仅有 `death_cast_skill_id` 对应的有效死亡施法权限才允许本次免费步进，否则返回 0。调用方先按结果分支，再应用效果。间隔调度及清理由既有 Buff/Continuous 管理，包括其回滚时钟。数值输出必须写入已声明的可写目标。

## 相对瞄准的区域放置与投射物地形层

`spawn_area` 区分世界偏移（`offset_x/y/z`）和瞄准相对偏移。`direction_offset` 沿归一化平面前向移动；`lateral_offset`（默认 0）沿右向 `(forward.Z, 0, -forward.X)` 移动。`position_mode=2` 以已提交瞄准位置为中心。偏移仅影响创建几何，组认领、布防、Enter 交付及生命周期保留既有契约。

生产投射物表的 `CollisionLayerMask` 为 0 时使用既有默认 `UnitMask | WorldMask`，非零时显式选择碰撞层。直接 Shoot 与发射器发射均在构造 SpawnParams 前解析，结果沿既有投射物快照及恢复路径传递。单位子类型和队伍过滤独立于地形层选择。批准及首批内容映射记录于 `plans/JinxLiveGameplayRepairAcceptance.md`（2026-11-11）。

## Ownership

- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Packages/` 包含技能、Buff、被动和玩法四个生产触发器编辑模块。
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Sources/` 包含可编辑源快照，应通过工作区保持各 Source 同步。
- 相关技能流程展示位于 `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/`。
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability/ability_trigger_plans.json` 和 `src/AbilityKit.Demo.Moba.Console/Configs/luban/ability/ability_trigger_plans.json` 是生成的运行时产物。
- `LubanConfig/Moba/Production/Datas/J-资源节点表.xlsx` 的 `Ability节点` 页及 `Resources/ability/triggers/` 保留迁移基线，不再发布或加载生产触发效果。两个缺失配置行的旧教程引用（Buff 100001、查询 101）在一次性迁移中映射到既有示例 Buff 1、范围查询 1。
- 空被动条目 10060000 因墨子被动表引用而保留为仅调试标记。

## 运行时规则叠加

生产先加载必需聚合文件，再应用目录规则。相同 Trigger ID 的规则整体替换聚合记录，验证针对最终有效记录执行；这不允许聚合发布输入内出现重复 ID。宿主行为及验证见[加载基线](TriggerPlanLoadingBaseline.md)。

## 作用域契约

触发器 `scope` 属于引用契约，应按消费位置验证，不能根据其存放在技能、被动、Buff 或玩法模块中推断：

| 引用位置 | 要求的作用域 |
|---|---|
| 主动技能 Flow RulePlan、Timeline 效果；投射物及区域事件；玩法事件；Buff 添加、移除及间隔效果；Continuous 间隔事件 | `Global` |
| `PassiveSkill.TriggerIds`、`Buff.TriggerIds`、`Continuous.TriggerIds` | `OwnerBound` |
| `advance_gameplay_counter.trigger_id`、`consume_gameplay_counter.trigger_id` | `Global`、`Callable` |

引用作用域错误时，运行时战斗配置校验器阻断会话启动。发布后运行运行时校验、`CheckRuntimeBatch` 及相关技能验收或冒烟测试，确认 Unity 和 Console 计划与生产模块一致。Console 运行时产物为 `src/AbilityKit.Demo.Moba.Console/Configs/luban/ability/ability_trigger_plans.json`。

计数器回调通过效果服务直接执行，不能指向所有者事件订阅，也不能加入 `PassiveSkill.TriggerIds`；已发布计数器回调契约测试同时检查作用域和可调用入口。载荷角色字段必须匹配订阅事件：施法事件使用 `caster.actor_id`，伤害事件使用各自攻击者及目标字段。

全局技能效果不能使用所有者本地黑板。临时目标查询及有界 `for_each` 应使用可写 Context 值 `skill_runtime:effect.target_collection` 和 `skill_runtime:effect.current_target`。它们使用效果运行时数据作用域，避免不同所有者共享模块变量。

共享停留采样应在消费前显式配置 `set_num_var`：将 Payload `area_residence_ratio` 写入可写 Context `skill_runtime:cast.residence_ratio`。伤害表达式从原 Cast 读取 `skill_runtime.cast.residence_ratio`。Context 写目标必须注册为可写值来源；仅注册字段不会自动采样。输入由原 Cast 黑板拥有，并纳入既有回滚快照。

数值属性 Modifier 可显式选用 `skill_runtime:cast.<key>`、`skill_runtime:target.<key>` 或 `skill_runtime:child.<key>` 作为 ContextFloat 键，并采用 OnApplySnapshot 求值。投影器首次应用时读取确切原 Cast 句柄及实际目标或 Continuous 子对象身份，再将结果冻结到 Continuous 既有 Modifier 捕获中。输入缺失或来源身份无效时拒绝应用。Cast 结束后，后续投影及暂停恢复仍使用捕获值。普通刷新保留首次捕获，重写黑板不会刷新活动 Modifier。无前缀 ContextFloat 键保留既有属性上下文含义。此契约不增加 Buff 参数存储，也不向持久 Modifier 开放 Effect 作用域。

只关心是否存在匹配目标时，使用条件 `has_query_target`，提供必需的正整数常量 `query_template_id`。查询模板负责来源、中心、过滤、半径、排序及数量。谓词在当前效果执行作用域内通过既有确定性目标解析器运行；无匹配或缺少执行上下文时返回 false，在 `finally` 释放池化列表，不写黑板、不分配集合句柄。编辑阶段拒绝无效模板 ID；模板缺失属于运行时配置错误。它同时支持触发器条件和 `conditional` 动作条件，见[已批准的查询作用域设计](design/09-ImplementationExamples/MOBA/43-MobaExplicitTriggerQueryScopeProposal.md)。

`give_damage` 接受一种基础伤害来源：固定 `damage_value`、`damage_value_skill_level_parameter`、`source_attack_ratio` 或 `magnitude_*` 公式。可选 `damage_value_ref` 从当前触发器载荷解析额外数值，可与固定、等级或攻击比例基础值组合；不能与已定义完整基础公式的 `magnitude_*` 组合。`damage_value_ref_skill_level_parameter` 将载荷值乘以技能等级系数。`target_missing_hp_ratio_coefficient` 或 `target_missing_hp_ratio_skill_level_parameter` 按目标追加 `target_max_health * current_missing_health_ratio * coefficient`，可选最小及最大限制仅作用于该追加值。比例和最大生命在每个目标的伤害请求前读取，因此主命中及后续区域目标使用各自当前生命。`damage_scale` 或 `damage_scale_skill_level_parameter` 缩放合成结果，再应用可选 `damage_min`/`damage_max`。`NumericValueRef` 的缩放、偏移、限制、必需及回退策略作用于载荷值。只有引用事件实际公开的字段才有效；字段缺失时，允许回退则使用显式回退，否则解析失败并拒绝动作。

已批准的可选 `target_missing_hp_damage_scale_coefficient` 和 `target_missing_hp_damage_scale_max_bonus` 是成对的有限非负常量（省略即禁用）。全部加法项及固定 `damage_scale` 之后，乘以 `1 + min(maxBonus, coefficient * clamp(1 - hp/maxHp, 0, 1))`，再应用最终 `damage_min/max`。命中时读取各目标当前生命，不新增状态，也不在发射时采样。

`add_buff` 接受互斥的可选 `duration_ms`（非负整数）或 `duration_skill_level_parameter`（非空名称）。后者要求有效原 Cast，并读取其冻结等级参数，不读取目标或所有者当前技能等级，也不在 Buff 存储 Modifier 参数。采样时长传给既有 Buff 时长覆盖入口；零保留使用配置时长的约定。Cast 参数缺失或无效时拒绝动作。既有 Buff 计时、刷新、控制免疫及快照仍为权威。


`adjust_damage_number` 也接受既有可选 `magnitude_*` 公式输入，与 `value` 及其别名、重复目标衰减和缺失生命输入互斥。求值使用当前战斗执行上下文及公式来源角色；`Target` 在每次命中时读取当前受伤者属性，不将属性冻结进 Buff，也不新增快照字段。省略公式输入时保留既有固定、衰减和缺失生命行为。批准范围及真实伤害验证见 [W 减伤数值公式](../plans/GragasDamageReductionMagnitudeDecision.2026-11-12.md)。

`give_damage` 还接受 `[0, 1]` 常量 `projectile_impact_kill_bonus`（默认 `0`）。正值要求针对本次命中目标的即时 `projectile.hit` 动作、存活投射物及匹配的发射上下文。伤害管线前，按 `1 + bonus * prior confirmed impact kills` 缩放合成请求。仅该伤害操作已提交的存活到死亡 HP 转换计数，每次接受的命中最多一次；当前命中不享受自身击杀奖励。外部或延迟死亡不计数，弹跳选择不变。计数及最后击杀命中由活动投射物关联持有，并由帧回滚 Provider 恢复，不属于状态同步表现字段。

## 来源 Buff 认领与原 Cast 护盾组

[已批准的拉克丝方案](../plans/LuxImplementationReadiness.2026-11-13.md) 增加 `source_buff`。其 `buff_id` 及普通目标请求选择由当前施法者拥有的目标 Buff。默认通过普通 Buff 应用策略刷新；`claim=true` 选择唯一明确的来源实例，在移除回调前按精确运行时身份和修订移除。过期或重复认领失败。`result`、`eligible_result` 是可选数值黑板输出。必须满足敌方、搜索和伤害资格，包括护盾吸收全部 HP 伤害时。Buff 运行时身份沿用既有生命周期恢复格式。

可选 `magnitude_*` 公式在认领移除回调前采样，成功时写入 `magnitude_result`。`magnitude_level_source=1` 选择当前来源英雄等级；默认 0 保留当前技能等级行为。等级曲线使用配对的等级和值点。临时认领结果属于当前同步 Effect 作用域，不是持久目标映射。

护盾叠加策略 `MergeSameShieldAndCast`（4）匹配护盾 ID、来源角色及非零原始 `RootContextId`。再次应用时增加剩余护盾并刷新到期时间；不同施法或施法者仍为独立层。既有护盾恢复携带此分组键。

`shoot_projectile.use_aim_distance` 默认 true。false 时保留提交方向，使用编辑的完整去程距离，适用于指针目标较近时也不能缩短路径的定向飞弹。

## 投射物、区域与位移组合验收

投射物到达触发器可将查询伤害、Motion 和 Area 创建编排为同级动作。到达时创建的区域不自动成为伤害或位移权威，必须检查其编辑效果。已发布酒桶 R 到达使用直接查询伤害及径向 Pull，区域仅为后续表现对象；此组合无需英雄身份分支。

当前模拟在 Execute 执行 Motion，随后在 PostExecute 执行 ProjectileSync 和 AreaSync。因此，到达时新增 Motion 来源会在到达帧结算伤害，位置直到下一 Motion 步进才变化。测试需同时断言此边界、最终位移及仅结算一次。

共享区域组认领中，敌方和配置的 UnitSubType 资格先于原 Cast、组、目标认领。拒绝的接触不能消耗后续合法接触资格；成功认领在重复物理 Enter/Cross 中保留，后续资格变化不撤销它。恢复前后应通过实际 Motion 驱动穿越、Buff 状态和数值投影验证，不能只用传送或直接调用认领 API。

恢复证据需区分同帧快照往返、从命令前检查点重演同一移动命令，以及回退已活动 Motion 来源。已批准首批由联合生命周期 v4 保存同一存活 Motion Continuous 所有的 FixedDelta 来源精确原始时间及冻结值，并静默恢复。其他来源、已销毁实例、外部 Pipeline 来源及跨名单恢复仍不支持。完整酒桶 R 重演仍暴露恢复 SkillLoadout 通知后的被动间隔对账问题，这是独立待定契约，不证明 Motion 状态恢复失败。见 `local/docs/Moba受管位移精确恢复验收.2026-11-17.md`、`local/docs/Moba投射物区域位移组合验收.2026-11-17.md` 及[被动重绑定方案](../plans/MobaPassiveContinuousRollbackRebindDecision.2026-11-17.md)。

## 发布

`spawn_area`、`spawn_area_batch` 及其别名 `spawn_area_pattern` 接受可选 `claim_unit_sub_type_mask`。第 n-1 位选择正数 `UnitSubType` n，仅允许已定义子类型。零保留旧版仅英雄组认领。该值冻结于每个区域，并随生命周期检查点恢复，旧载荷默认零。Enter/Cross 中，敌方和子类型资格先于既有共享原 Cast、组、目标认领，不新建认领表。`claim_targets_within_group` 为 false 时此参数无效，见已批准的[区域资格方案](../plans/MobaAreaGroupQualificationDecision.2026-11-14.md)。

1. 在 **Window > AbilityKit > Trigger Authoring Workspace** 编辑并保存生产模块。若编辑维护中的 `Sources/*.trigger.json`，先运行 `AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration.ImportProductionSourcesBatch`，将四份生产源导入模块；若直接编辑模块，发布前从工作区导出其 Source。
2. 运行 **Tools > AbilityKit > MOBA > Publish Trigger Plans**，或 Unity 批处理方法 `AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration.PublishRuntimeBatch`。导入或校验失败时停止，不得将先前运行时产物当作本次结果。
3. 运行 `AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration.CheckRuntimeBatch`，检查未发布模块变化及 Unity/Console 一致性。
4. 检查每次批处理日志，确认目标方法实际执行，Unity 以 `Exiting batchmode successfully` 和返回码 0 退出。项目占用、校验错误或方法异常均判失败，即使命令启动器返回或产物时间戳改变。
5. 运行 `python LubanConfig/Moba/export_pipeline.py --check-published`，再运行六英雄 .NET 冒烟、运行时校验和 Unity EditMode 验收。确认两份运行时文件匹配生产模块，启动校验无阻断 `invalid_trigger_scope` 错误。

`GenerateBatch` 仅用于初始迁移，会从归档的拆分 JSON 覆盖四个生产模块，不得用于常规发布。

运行时仅加载一次编辑器聚合文件。Unity 另加载 `ability/rules`，Console 另加载 `luban/ability/rules`；归档 `ability/triggers` 不属于任一加载配置。
# 已批准的 CastFrame 捕获

`spawn_area` 支持可选 `capture_cast_frame`（默认 false），已获 `plans/LuxCastPresentationFrameDecision.2026-11-13.md` 批准。要求无显式目标请求的单一位置及存活原 Cast 句柄。它在该 Cast 回滚黑板中仅记录一次解析后、偏移前的原点及形状偏航前的归一化水平方向，不旋转角色，不改变区域几何或伤害时序。正式 CharacterAction v3 表现投影将此可选空间基准传给显式配置的 CastFrame 模板。
