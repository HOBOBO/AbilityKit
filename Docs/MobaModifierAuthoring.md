# MOBA 修饰器配置契约

正式版目标契约、扩展边界与放行条件见 [MobaModifierFormalDesign](MobaModifierFormalDesign.md)。只有活跃 `IContinuous` 实例或绑定其生命周期的桥接可以提供修改器效果；同一效果 owner 下通过阶段记录筛选参数。生命周期投影、部分阶段消费和类型化候选已接通；完整回滚、Unity 验收和规模基准尚未放行。

当前效果表有 21 个参数目录项、33 条修饰效果定义和 33 条挂载，召唤继承表有 1 条配置。修改共用模板前运行 `python LubanConfig/Moba/tools/modifier_impact_report.py --output local/modifier-impact-report.json` 查看 Buff/持续过程的反向引用，再运行 `python LubanConfig/Moba/export_pipeline.py --check-published`。本轮验收和放量边界见 [实施记录](../local/docs/MobaModifierP0P4ImplementationRecord.md)。

## 编辑来源与边界

`LubanConfig/Moba/Production/Datas/J-效果表.xlsx` 的 `修饰参数` 子表定义可修饰的参数目录，`修饰效果定义` 子表定义效果语义，`修饰器定义` 子表只记录挂载。Buff 和持续流程通过 `ModifierIds` 引用挂载行。旧 Trigger `add_skill_param_modifier`/`remove_skill_param_modifiers` 由一对一持续桥接兼容运行，正式发布检查拒绝这两种动作；新内容应引用持续提供者。不要在编辑器或业务代码中维护第二份参数 ID 列表。Excel 是编辑源，Luban JSON、二进制与 `MobaSkillParamModifierKeys.g.cs`、`MobaModifierDefinitions.g.cs` 是发布产物。

## 模板与实例分离

修饰器拆成两层：**定义**（`修饰效果定义`）回答“改什么、改多少”，**挂载**（`修饰器定义`）回答“谁提供、在哪些范围内生效”。挂载行只有 `Id / Kind / DefinitionId / Selectors / Priority` 五列。

- 同语义的效果在定义表里只写一次，多个 Buff 复用同一个 `DefinitionId`；跨 Buff 复用是正常的，同一 Buff 内重复引用同一定义才是错误。
- `Selectors` 留在挂载行，因为生效范围属于挂载上下文；目标、运算、幅值、公式、候选一律来自定义。
- 导表时 `DefinitionId` 是权威来源，`Priority` 只存在于挂载行，用于同冲突组内调序，定义表不重复该语义。
- 发布出的 `modifier_templates.json` 仍是扁平的 `ContinuousModifierDTO` 形状，`ContinuousModifierMO` 与 `ModifierKey` 的 8/8/8 ABI 均未改变；运行时在加载期按 `DefinitionId` 合并语义，Buff 行仍然只保存 `ModifierIds`。

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

## 修饰效果定义 ID 分段

`修饰效果定义.Id` 是语义的**权威身份**，占 `1000000–8999999`，按目标域分段；`修饰器定义.Id` 是挂载身份，占 `100001–899999`，也按目标域分段。两套 ID 空间不重叠，Buff/持续流程只引用挂载 ID。

| 定义 ID 段 | 目标类型 | 当前使用情况 |
|---|---|---|
| `1000001–1999999` | 角色属性（`TargetKind=1`） | 24 条 |
| `2000001–2999999` | 技能槽候选（`TargetKind=5`） | 1 条 |
| `3000001–3999999` | 发射器参数 | 2 条 |
| `4000001–4999999` | 子弹参数 | 2 条 |
| `5000001–5999999` | 区域参数 | 1 条 |
| `6000001–6999999` | 召唤物参数 | 1 条 |
| `7000001–7999999` | 伤害参数 | 1 条 |
| `8000001–8999999` | Buff 参数 | 1 条 |

每条定义还带 `MountPoint`（Actor / Skill / Summon / Emitter / Projectile / Aoe / Buff / Damage），它决定 ModifierKey Category 的归属，并与目标域强校验一致。`ConflictGroup` 声明该效果与哪些键位竞争：`Attribute`、`SkillSlot`、`SkillParameter`，具体语义见下文“冲突裁决”。`StackingPolicy` 与 `MaxStacks` 描述同定义多次生效时的叠加方式。

`ConditionSetId` 只是预留列，本轮只允许留空。条件表达式仍然放在触发器（Trigger Authoring）中，因为“是否生效”属于事件与状态判定，而不是效果的固有属性。

## 生效时机

时机由两个**互相独立**的机制决定，不要把它们混为一谈。

`修饰参数.CapturePolicy` 回答“这个字段在它的消费点什么时候被读取”。运行时通过导表生成的 `MobaSkillParamModifierKeys.g.cs` 里的 `ModifierFieldSchema.Capture` 暴露，配合 `ResolveAt` 决定读取发生的生命周期阶段：

| CapturePolicy | 行为 |
|---|---|
| `LiveProperty` | 每次解析都现场读。已经在跑的 Buff 改了字段，下一次读取立刻看到新值，因为运行时**不缓存**任何计算结果。 |
| `CreateSnapshot` | 每个快照读一次；该快照内稳定，下一个快照取新值。 |
| `EventSample` | 事件触发时采样一次并冻结。例如发射器参数只影响下一次发射，已在飞的子弹不受影响。 |

`EvaluationPolicy` 不再是作者填写的列，而是导出期从**幅值来源**推导出来的结论，回答的是另一个问题：这一条修饰器实例的幅值要不要冻结。

| 幅值来源 | 推导出的 EvaluationPolicy | 原因 |
|---|---|---|
| `Attribute`、`LinearFormula` | `OnApplySnapshot` | 幅值读取了别的状态，采样一次后存入捕获库；Buff 刷新、回滚重投射都读回同一个值，保证确定性。 |
| `Fixed`、`Scalable`、`TimeDecay`、`Pipeline`、`ContextFloat` | `Realtime` | 只依赖持续实例自身状态，本身已经确定，实时重算即可。 |

推导逻辑在 `LubanConfig/Moba/business/modifiers.py::derive_evaluation_policy`，C# 二进制加载路径在 `MobaLubanConfigGroups.cs::DeriveEvaluationPolicy` 做了同样的推导，两条路径必须保持一致。

典型回答：

- 改一个 Buff 的攻击力百分比 → 属性通道实时计算，**现存实体立刻生效**。
- 改发射器的弹道数量 → `EventSample`，**只影响下一次发射**，在飞的子弹不受影响。
- 改 Buff 的持续时间公式 → `OnApplySnapshot`，**已经存在的 Buff 不变**，新挂上的按新公式算。

## 冲突裁决

运行时的仲裁完全发生在 `AbilityKit.Modifiers` 内：**只有共享同一个 `ModifierKey` 的修饰器才会进入同一次 `SortByPriority`**。`MobaSkillParamModifierService` 按 `ModifierKey` 建索引，属性通道按 `AttributeId` 分组，所以不同 Key 的修饰器结构上不可能互相覆盖。

`ConflictGroup` 是这条不变式的**编排侧声明**——它说“这些效果写同一个键，所以 Priority 决定胜者”，而不是运行时读取的开关。`Priority` 只在挂载行上填写，定义表不再重复。导出器校验两件事：

- 同一个 `ModifierKey` 不能横跨两个冲突组（它们实际在竞争，组声明就没意义了）。
- 冲突组必须覆盖该定义的所有挂载点。

裁决顺序为：操作分组固定（Override 先行）→ 同组内 `Priority` 大者胜 → 同级按 `SourceId` 稳定排序，保证确定性。

## 修饰器挂载 ID 分段

`修饰器定义.Id` 是供 Buff 与持续流程的 `ModifierIds` 引用的**挂载身份**，与 `修饰效果定义.Id`、`修饰参数.Id` 是三个独立命名空间。挂载 ID 按其定义的**目标类型**分段：`Kind=Buff/ContinuousProcess` 仅说明谁提供效果，不决定 ID 段。例如 Buff 提供的移速属性修饰归属性段；Buff 提供的子弹速度修饰归子弹段。`TargetKind=Attribute` 归属性段；`SkillSlotCandidate` 归技能段；`SkillParameter/ParameterReferenceCandidate` 按所引用修饰参数的 Domain 归段。导表器据此拒绝错段、重复或未知目标。

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

`修饰效果定义` 的 `MagnitudeSourceType=Attribute` 表示修饰量取自另一项角色属性。此时必须填写 `MagnitudeAttributeTypeId` 和 `MagnitudeCoefficient`；`EvaluationPolicy` 由导出器自动推导为 `OnApplySnapshot`，不需要手填。`AttributeSourceRole` 可选 `Owner`（修饰器持有者，默认）、`Source`（施加来源）或 `Target`（效果目标）；Buff 的 `Owner` 与 `Target` 都是承受者。快照在持续对象首次投射时捕获；Buff 刷新、暂停后恢复和回滚重投射沿用捕获值，层数变化按首次捕获时的层数比例调整。持续对象结束后清除捕获值；回滚快照会读写每条修饰器的捕获值。

`MagnitudeSourceType=TimeDecay` 用于持续期间变化的属性或数值参数，例如短时间爆发移速逐渐衰减。填写正数 `MagnitudeDuration`（秒）和 `EvaluationPolicy=Realtime`；`MagnitudeCurve` 可选 2–64 个有限且非负的倍率采样点，均匀覆盖从 0 到 `MagnitudeDuration` 的时间区间。例如 `[1, 0.25, 0]` 表示起始全额、半程为四分之一、到终点归零；`[1, 0.5, 0.2]` 表示之后保持 20% 加成直到提供者结束。点间线性插值，到达曲线终点后保持最后一个采样值；未填曲线时使用 `MagnitudeDecayType` 并在时长终点归零。每个提供者按自身持续实例的已流逝时间独立采样，暂停冻结、恢复继续、结束撤销；先取当前曲线值，再按操作应用叠层。时间曲线不可使用应用时快照，也不可用作引用候选。

## 字段目录与字段 Schema

`attr_types`（角色属性）和 `modifier_parameters`（领域参数）是同一类东西：**可被修饰的字段目录**。两张表共用同一套字段 schema 与同一个校验函数 `validate_field_schema`，只是 ID 空间和消费路径不同——属性走 `AttributeRegistry`，领域参数走 `ModifierKey` + 阶段记录。两张表不合并：`attr_types` 的 ID 1-23 被 `BattleAttributeType` 枚举和 `MobaAttributeIds` 的 23 个 `Request()` 钉死，`modifier_parameters` 按 ID 段预留了 Summon / Motion 扩展空间。

新增可修改字段时，先在对应目录声明它的量纲、合法范围和允许的操作，运行时从 codegen 产物读取，而不是在消费点硬编码。

### 共用 schema

| 字段 | `attr_types` | `modifier_parameters` | 约束 |
|---|---|---|---|
| `Id` | 1-23，顺序等同 `BattleAttributeType` | 按 ID 段分配（10000 Skill / 20000 Buff / 30000 Emitter / 40000 Projectile / 50000 Aoe / 60000 Summon / 70000 Damage） | 全表唯一；发布后不可改动 |
| `Key` | 必须等于 `BattleAttributeType` 枚举名 | 领域内唯一的 C# 参数名 | 发布后不可改名 |
| `Name` / `DisplayName` | 编辑器显示名 | 编辑器显示名 | 不作身份，不可用于引用 |
| `ValueKind` | `Flat` / `Percent`（**聚合方式**） | `ReferenceId` / `Integer` / `Float`（**存储类型**） | 两者是不同轴，不可互相替代 |
| `Unit` | 量纲：`Point` / `Distance` / `Ms` / `Count` / `Ratio` / `Degrees` / `Percent` | 同左，参考型填 `Id` | 数值字段必填量纲；参考型必须填 `Id` |
| `ValueMin` / `ValueMax` | 合法上下界 | 同左 | 填 0 表示该侧无界；参考型不得声明范围 |
| `Ops` | 允许的修饰操作 | 同左 | 逗号分隔，至少一个；参考型只能 `Override` |
| `CapturePolicy` | 无（属性不走阶段取样） | `CreateSnapshot` / `EventSample` | 与 `ResolveAt` 语义一致；`LiveProperty` 需注册领域更新适配器，尚未开放 |
| `ResolveAt` | 无 | 取值阶段 | 声明预期读取阶段，仍需真实消费入口 |
| `DefaultValue` | 模板未覆盖时的默认值 | 无 | — |
| `Deprecated` | 无 | 弃用标记 | 弃用行不得发布 |

`Category` / `SubCategory` 是 `ModifierKey` 的打包 ABI（各 1-255，二元组全表唯一），由发布器锁定，**不是作者视图**；扩量请新分配 ID 而非改这两列。

`Ops` 是作者可读的列表，导出器派生出运行时的 `OpMask` 位字段；两者由同一份数据保证一致，运行时 `SupportsOperation` 读的是派生结果。

导出器拒绝：不声明量纲的数值字段、参考型声明数值范围、参考型填了非 `Override` 操作、`Ops` 为空或含未知操作、`LiveProperty` 而未注册更新适配器、以及 `Deprecated` 行。

`codegen` 产出 `MobaSkillParamModifierKeys.TryGetFieldSchema(parameterId, out ModifierFieldSchema)`，返回量纲、上下界、取样策略、操作位、领域和显示名，运行时据此校验最终值。

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
