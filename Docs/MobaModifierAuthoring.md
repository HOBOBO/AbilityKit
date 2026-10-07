# MOBA 修饰器配置契约

正式版目标契约、扩展边界与放行条件见 [MobaModifierFormalDesign](MobaModifierFormalDesign.md)。只有活跃 `IContinuous` 实例或绑定其生命周期的桥接可以提供修改器效果；同一效果 owner 下通过阶段记录筛选参数。生命周期投影、部分阶段消费和类型化候选已接通；完整回滚、Unity 验收和规模基准尚未放行。

当前效果表有 21 个参数目录项和 33 个模板，召唤继承表有 1 条配置。修改共用模板前运行 `python LubanConfig/Moba/tools/modifier_impact_report.py --output local/modifier-impact-report.json` 查看 Buff/持续过程的反向引用，再运行 `python LubanConfig/Moba/export_pipeline.py --check-published`。本轮验收和放量边界见 [实施记录](../local/docs/MobaModifierP0P4ImplementationRecord.md)。

## 编辑来源与边界

`LubanConfig/Moba/Production/Datas/J-效果表.xlsx` 的 `修饰参数` 子表定义可修饰的参数目录，`修饰器定义` 子表定义可复用的数值修饰。Buff 和持续流程引用修饰器 ID。旧 Trigger `add_skill_param_modifier`/`remove_skill_param_modifiers` 由一对一持续桥接兼容运行，正式发布检查拒绝这两种动作；新内容应引用持续提供者。不要在编辑器或业务代码中维护第二份参数 ID 列表。Excel 是编辑源，Luban JSON、二进制与 `MobaSkillParamModifierKeys.g.cs` 是发布产物。

普通角色属性仍走属性修饰通道 (`TargetKind=1`)，`TargetId` 引用 `attr_types.Id`；领域参数走 `TargetKind=4`，`TargetId` 引用本表的 `修饰参数.Id`。两类 ID 是独立的命名空间，不能互相解释。发射器 ID、每轮数量、扇角和发射器持续时间属于 `Emitter` Domain；子弹 ID 和速度属于 `Projectile` Domain。`Category` 和 `SubCategory` 组成运行时 `ModifierKey`，与配置行 ID 不同。旧参数 ID 1–16 已整体迁移；键位和值类型保持原语义，旧号停用。

## 参数 ID 分段

`修饰参数.Id` 是全表唯一的配置引用 ID。旧 16 行已按 Domain 迁入下表区间，目前目录共 21 行；每个领域保留 10000 个编号，不要求连续填满，也不允许借用其他领域的空号。旧 1–16 和 17–9999 不再分配，80000 以上暂留。迁移映射记录在 `LubanConfig/Moba/migrations/migrate_modifier_parameter_ids.py`，导表器会拒绝越段 ID，并核对迁移后的键位与旧发布目录。

| ID 段 | Domain | 示例用途 |
|---|---|---|
| `10000–19999` | `Skill` | 施法消耗、冷却、距离、施法阶段参数 |
| `20000–29999` | `Buff` | 时长、层数、周期、刷新参数 |
| `30000–39999` | `Emitter` | 发射间隔、每轮数量、扇角、波次、发射器寿命 |
| `40000–49999` | `Projectile` | 飞行速度、寿命、命中次数、穿透与追踪参数 |
| `50000–59999` | `Aoe` | 半径、时长、命中周期、扩张速度 |
| `60000–69999` | `Summon` | 寿命、继承比例、初始资源 |
| `70000–79999` | `Damage` | 请求基础伤害、命中阶段系数 |

当前目录：`Skill` 为 `10001 SkillId`（仅保留地址，技能替换使用槽位候选）、`10002 ResourceCost`、`10003 CooldownMs`、`10004 CastRange`；`Buff` 为 `20001 DurationMs`；`Emitter` 为 `30001 LauncherId`、`30002 CountPerShot`、`30003 FanAngleDeg`、`30004 DurationMs`、`30005 IntervalMs`；`Projectile` 为 `40001 ProjectileId`、`40002 Speed`、`40003 LifetimeMs`、`40004 PierceHits`；`Aoe` 为 `50001 Radius`、`50002 DurationMs`、`50003 IntervalMs`；`Summon` 为 `60001 SummonId`、`60002 LifetimeMs`、`60003 InheritRatioScale`；`Damage` 为 `70001 RequestedBase`。这是工作簿中实际行 ID，不是仅供未来新增的规划。

角色属性不占用这张表的任何区间；属性类型的新增编号应在 `attr_types` 自身规划。此处的 10000 个 ID 是分配空间，不等于已经实现 10000 个可修改字段。当前 `ModifierKey` 的 Category/SubCategory 各为 8 位，且必须全表唯一；一个 Category 只有 255 个可用 SubCategory，扩量时可在同领域规划多个 Category。增加目录行还需要对应阶段的读取入口、合法范围、记录字段和测试，不能只补 Excel 行。

## 修饰器定义 ID 分段

`修饰器定义.Id` 是供 Buff 与持续流程的 `ModifierIds` 引用的**定义身份**，与上节的 `修饰参数.Id` 是两个命名空间。定义 ID 按**目标类型**分段：`Kind=Buff/ContinuousProcess` 仅说明谁提供效果，不决定 ID 段。例如 Buff 提供的移速属性修饰归属性段；Buff 提供的子弹速度修饰归子弹段。`TargetKind=Attribute` 归属性段；`SkillSlotCandidate` 归技能段；`SkillParameter/ParameterReferenceCandidate` 按所引用修饰参数的 Domain 归段。导表器据此拒绝错段、重复或未知目标。

| 定义 ID 段 | 目标类型 | 当前使用情况 |
|---|---|---|
| `100001–199999` | 角色属性 | 22 条，`100001–100022` |
| `200001–299999` | Buff 参数 | 预留 |
| `300001–399999` | 技能参数与槽位候选 | 槽位候选 `300001` |
| `400001–499999` | 发射器参数 | `400001–400002` |
| `500001–599999` | 子弹参数与引用候选 | 预留 |
| `600001–699999` | AOE 参数 | 预留 |
| `700001–799999` | 召唤参数与引用候选 | 预留 |
| `800001–899999` | 伤害参数 | 预留 |

旧定义 ID `1–28` 已随 Buff/持续流程引用一次性迁移，映射保存在 `LubanConfig/Moba/migrations/migrate_modifier_template_ids.py`，不得重新分配。参数目录迁移后，原两条发射器目标定义又从子弹定义段移到发射器定义段。新编号从各段未占用的下一个 ID 递增，不按英雄或提供者分配子段；多个 Buff 共用一个定义时仍引用同一 ID。迁移更换定义身份和对应参数 `TargetId`；属性类型 ID、槽位、幅值、优先级和阶段语义不变。

技能 ID 使用 `TargetKind=5` 的整数候选，`TargetId` 填槽位 1–16；普攻按其装配槽位参与候选选择。墨子强化普攻引用修饰器定义 ID `300001`（原 `20`），定义已迁成槽位 4 候选。Launcher、Projectile 和 Summon 引用 ID 使用 `TargetKind=6` 的整数候选；引用 ID 不再作为浮点参数修饰量配置。

`修饰器定义` 的 `MagnitudeSourceType=Attribute` 表示修饰量取自另一项角色属性。此时必须填写 `MagnitudeAttributeTypeId`、`MagnitudeCoefficient` 和 `EvaluationPolicy=OnApplySnapshot`。`AttributeSourceRole` 可选 `Owner`（修饰器持有者，默认）、`Source`（施加来源）或 `Target`（效果目标）；Buff 的 `Owner` 与 `Target` 都是承受者。快照在持续对象首次投射时捕获；Buff 刷新、暂停后恢复和回滚重投射沿用捕获值，层数变化按首次捕获时的层数比例调整。持续对象结束后清除捕获值；回滚快照会读写每条修饰器的捕获值。属性来源的实时引用尚无完整失效通知，导出器会拒绝实时配置。

`MagnitudeSourceType=TimeDecay` 用于持续期间变化的属性或数值参数，例如短时间爆发移速逐渐衰减。填写正数 `MagnitudeDuration`（秒）和 `EvaluationPolicy=Realtime`；`MagnitudeCurve` 可选 2–64 个有限且非负的倍率采样点，均匀覆盖从 0 到 `MagnitudeDuration` 的时间区间。例如 `[1, 0.25, 0]` 表示起始全额、半程为四分之一、到终点归零；`[1, 0.5, 0.2]` 表示之后保持 20% 加成直到提供者结束。点间线性插值，到达曲线终点后保持最后一个采样值；未填曲线时使用 `MagnitudeDecayType` 并在时长终点归零。每个提供者按自身持续实例的已流逝时间独立采样，暂停冻结、恢复继续、结束撤销；先取当前曲线值，再按操作应用叠层。时间曲线不可使用应用时快照，也不可用作引用候选。

## 参数目录字段

| 字段 | 约束 |
|---|---|
| `Id` | 全表唯一；现有行和新行均必须落在所属 Domain 的专用区间，旧 1–16 停用。 |
| `Domain`、`Key` | 生成 C# 的领域类与成员名；同一领域内键名唯一。 |
| `Category`、`SubCategory` | 各为 1–255；二元组全表唯一。 |
| `ValueKind` | `ReferenceId`、`Integer`、`Float`；引用 ID 只能 `Override`，不能由触发器数值动作写入。 |
| `OpMask` | 位 0–3 依次允许 `Add`、`Mul`、`Override`、`PercentAdd`；必须和引用它的修饰器操作匹配。 |
| `TriggerWritable` | 旧 Trigger 数值动作的兼容权限。生产导出拒绝该动作，新内容由 Buff 或持续流程引用模板提供。 |
| `ResolveAt` | `Action`、`Launch`、`BuffApply`、`AreaSpawn`、`Emit`、`ProjectileSpawn`、`Hit`、`SummonCreate`；声明预期读取阶段，仍需真实消费入口。`30001 LauncherId` 和 `40001 ProjectileId` 在 `Launch` 选择，`60001 SummonId` 在 `SummonCreate` 选择。 |

目前已接入的参数包含技能资源/冷却/范围、发射器和子弹引用 ID、每轮发射数/扇角、发射器时长/间隔、Buff 时长、子弹速度/寿命/穿透命中次数、AOE 半径/时长/命中间隔、召唤物 ID/寿命/继承比例倍率和请求伤害基础值。`70001 RequestedBase` 在 `give_damage` 的 Hit 阶段读取；`60002 LifetimeMs` 在召唤创建时读取，0 表示无限寿命。`40004 PierceHits` 仅作用于穿透策略，新子弹生成时取样，`-1` 表示无限穿透，其他结果必须至少为 1；`60003 InheritRatioScale` 在召唤创建时取样，只缩放继承表的 Ratio，不缩放 Add，结果必须非负且有限。Buff 层数/触发周期、其他子弹命中策略仍未登记。新增行必须先有真实消费点，再在 `modifier_parameters.py` 登记阶段消费契约，并在实际消费方法上声明 `MobaModifierConsumes` 的 ID、阶段和取样时机：`CreateSnapshot` 写入新对象，`EventSample` 在事件发生时读取，`DerivedView` 在查询有效视图时读取。目录测试逐项核对已发布 ID 与代码声明；声明不能替代真实对象结果验收。导出器拒绝未登记或阶段不符的行。

## 配置如何被 Buff 和子弹读取

1. 作者在 `修饰参数` 声明参数地址，在 `修饰器定义` 用 `TargetKind=SkillParameter` 和该 ID 配置操作、幅值、优先级与可选 Selector；属性修改则用 `TargetKind=Attribute` 加 `attr_types.Id`。Buff 或持续流程的 `ModifierIds` 引用修饰器定义。目录本身不保存技能、Buff、子弹的基础数值；基础值仍来自各对象原有配置或动作覆盖值。
2. Buff、技能 Pipeline 等持续实例在 `MobaContinuousManager` 注册时取得世界内递增的投影 `ModifierSourceId`，激活后 `MobaContinuousLifecycleBinder` 将修改器投影到该实例的 `OwnerActorId`；暂停或结束时按该实例 ID 撤销，恢复时重新投影。曲线幅值按该实例已流逝时间重投影。两个实例即使拥有相同来源上下文和模板，也分别取得 ID；注册前读取到的 ID 为 0，不能直接绕过管理器投影。分配序号进入回滚快照。激活、恢复、普通刷新或曲线重投影失败时，管理器结束并注销该实例，撤销已经写入的投影；清理继续尝试所有目标类型，并保留投影与清理的异常信息。
3. 领域消费入口以基础值、单一正 ID Actor owner、生成的 `ModifierKey` 和 `MobaParameterRecord` 调用 `MobaSkillParamModifierService`。服务先校验该 Key 的发布阶段，再筛选匹配的 Selector，按操作和优先级计算结果。施法实例锁定基础与有效技能 ID；下游记录携带阶段及可用的槽位、基础/有效技能、发射器/子弹/AOE/召唤模板 ID。未携带的字段为 0，不会命中指定该字段的 Selector；生产数值解析不提供无阶段记录入口。非 Actor scope 只用于识别并拒绝旧快照，不能作为新投影或读取入口。
4. Buff 施加路径 `BuffApplyFlow.Apply` 取请求覆盖时长或 `Buff.DurationMs`，以 `SourceActorId` 为修改器 owner、`BuffApply` 为阶段，记录 Buff 模板 ID，以及有效技能运行时存在时的槽位/技能 ID，调用 `Buff.ResolveDurationMs`，校验后写入 Buff 实例。`SelectorBuffTemplateId` 只在 BuffApply 阶段可用；刷新/重施加会重新取样，已创建实例不追溯。
5. 发射器路径 `MobaModifierProjectileSpawnPatternProvider.GetPattern` 在每轮发射时用根 actor 和 `Emit` 记录读取 `CountPerShot`、`FanAngleDeg`，再以 `ProjectileSpawn` 记录读取子弹模板的 `Speed`、`LifetimeMs` 和穿透策略下的 `PierceHits`；记录携带槽位、基础/有效技能、发射器与子弹模板。普通 `MobaProjectileService.Shoot` 也携带子弹模板记录读取这些字段。发射器的 `IntervalMs` 在 LauncherCreate 取样，影响后续发射计划；已安排的发射计划和已生成子弹不会被后续来源变化追溯修改。

AOE 在 `spawn_area` 创建时读取半径和时长；召唤在创建时读取寿命及继承比例倍率；伤害在 `give_damage` 请求进入战斗管线前读取请求基础值。它们都是阶段性读取，已有对象不会因后续修改器变化自动更新。引用型 Launcher/Projectile/Summon ID 使用独立的优先级候选服务，不能把引用 ID 当浮点数做 Add/Mul；代码必须在对应动作阶段调用该候选服务才会更换引用。新增 Buff 层数或其他子弹命中策略时，应先增加含所需身份字段的记录和对应消费入口，再分配新 ID 与选择器权限。

## 解析与生命周期

叠层按操作解释幅值：Add 与既有 PercentAdd 按层数线性放大幅值；Mul 将每层倍率连乘，即 `factor^stack`；Override 不随层数放大。快照值在层数变化和回滚重投影时沿用相同规则。PercentAdd 的跨来源聚合仍保留既有 `(1+x)` 连乘，若要改变该语义须迁移存量配置。

修饰器按 `ModifierKey` 聚合。数值 Override、槽位和引用候选均以较大的 `Priority` 优先，同级按较小 `SourceId` 决胜；Add 求和，PercentAdd 仍按 `(1+x)` 连乘，Mul 连乘，整数按 `Math.Round` 舍入。Buff 或持续对象生效时投射到同一角色 owner，暂停/结束时按持续实例来源清除；Actor 正常退场提交后结束该 owner 的持续实例并移除旧 Trigger 桥接索引，回滚静默注销由快照恢复处理。发射器每轮数量/扇角及子弹生成速度/寿命用模板、槽位、有效技能记录筛选；普通 `Shoot` 路径也有子弹模板记录。Buff、AOE 和召唤寿命在施加或生成时快照；请求伤害在 Hit 事件采样。直接子弹命中或子弹事件触发的伤害可以按 `ProjectileTemplateId` 筛选；由该事件创建的执行上下文经多层效果节点和 payload 转发后也保留来源子弹模板，供 AOE 和 Buff 阶段筛选。脱离该上下文独立重建的间接链路仍须逐条验证。来源结束不会追溯改变已创建对象；活跃对象同步需要独立的更新与回滚协议。

参数服务是派生投影视图；生产回滚清空该视图后由持续实例及其捕获值重建。回滚导入会先完整校验 owner 和 Selector，再替换当前视图；旧非 Actor owner 快照明确失败。数值范围由消费点校验，例如子弹速度和 AOE 半径必须大于零，请求伤害与召唤寿命不能小于零；配置校验负责 ID、类型、操作、Selector 阶段可用性与引用完整性。

战斗调试工具的角色「属性」页列出当前修改器的声明值、叠层值、当前值、投影值、优先级、来源及评估策略；时间曲线还显示已流逝时间、总时长、衰减类型和倍率采样点。点击属性行的「曲线」可记录该属性的基础值与最终值随帧变化；图表从打开时开始采样，最多保留 240 点，不是历史战斗回放。实时「活跃对象」页可展开持续实例查看属性、领域参数和引用候选，还显示最近 128 次阶段参数解析中的帧号、owner、参数 ID、阶段记录、基础/最终值和匹配条数。「导出」生成版本化 JSON，保存最近解析、淘汰条数及导出时的数值参数投影摘要。该记录为有界实时诊断，回滚导入时清空，不进入战斗快照；导出文件不含历史逐条贡献或属性曲线，来源详情仍需结合持续实例视图检查。

## 扩展步骤

1. 确认参数属于属性、动作参数还是持续对象参数，明确取值时机、Actor owner 和阶段记录；引用 ID 不做数值运算。
2. 在 `修饰参数` 追加稳定 ID、类型、操作掩码和解析阶段；为领域解析器加入有名方法。
3. 在发射、Buff 施加、区域生成等真实消费路径解析并校验最终值；与触发器动作的可写权限一致。
4. 添加目录 ABI、非法操作、作用域/来源清理和实际消费路径测试，再运行 Luban 发布检查及英雄冒烟测试。

发布命令与英雄验收入口见 [Luban 配置管线](../LubanConfig/Moba/README.md) 和 [MOBA 技能规范](../.claude/skills/moba-skill-authoring/SKILL.md)。
