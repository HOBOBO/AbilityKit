# MOBA 修改器参数映射设计（面向肉鸽扩展）

状态：设计提案。本文只描述「参数身份 → Excel 字段 → codegen → 运行时」的映射约定，不改生产表、不改运行时。目标契约见 [MobaModifierFormalDesign](MobaModifierFormalDesign.md)，作者字段契约见 [MobaModifierAuthoring](MobaModifierAuthoring.md)。召唤物与移动参数按约定暂不新增键位，仅作为 Domain 扩展样例说明。

## 1. 结论摘要

1. 不要把所有可改字段压进一张表。保持三层：参数目录（身份与能力声明）、修饰器定义（语义与来源）、修饰器绑定（谁挂在哪里）。当前 `J-效果表.xlsx` 的 `修饰参数` + `修饰器定义` 已经是前两层，缺的是第三层的显式化，以及「属性」与「领域参数」的对齐。
2. 参数身份由稳定字符串 `Key` + `Domain` + `Id` 三者共同定义。Excel 的 `Category/SubCategory` 只是运行时打包表示，不是身份。
3. 属性通道与领域参数通道要合并成同一套目录语义。现状 `MobaAttributeIds` 是硬编码枚举、`修饰参数` 是 codegen 表，两套命名空间不对称（`TargetKind=1` 走 `attr_types.Id`，`TargetKind=4` 走本表 `Id`）。肉鸽词条要同时改英雄属性和技能/子弹参数，两类必须在同一个 `ParameterKey` 抽象下表达。
4. `ModifierKey` 第三个字节 `CustomId` 目前完全未使用，Category/SubCategory 各 8 位意味着单 Domain 最多 255 个参数。扩量前应先把 CustomId 用作同 Domain 内的参数序号，容量提到 255×255。
5. Excel 只描述语义，不描述机制。弹道行为、碰撞拓扑、命中策略属于代码侧注册能力（`ProjectileHitPolicyFactory` 已改注册表）；参数表只负责引用型候选选择和数值修饰。

## 2. 现状问题清单

| # | 问题 | 证据 | 对肉鸽的影响 |
|---|---|---|---|
| A | 属性与领域参数是两套不对称的身份空间 | `TargetKind=1` → `attr_types.Id`（23 个硬编码 `MobaAttributeIds`）；`TargetKind=4` → `修饰参数.Id` | 词条同时改两者要走两套投影、两套 explain、两套 Selector |
| B | Category/SubCategory 各 8 位且必须全表唯一，单 Domain 上限 255 | `ModifierKey.Create(byte, byte, byte)`；codegen 生成 `Create(40, 1)` | 单 Domain 超 255 参数后必须换 Category，破坏同领域内聚假设 |
| C | CustomId 分配了但从未使用 | codegen 所有 `Create(cat, sub)` 第三参恒为 0 | 白丢 8 位容量，是扩量时唯一无痛的缓解手段 |
| D | 参数目录只有数值 + 引用候选，缺行为动词 | `TargetKind` 只有 6 种，无「改变命中策略」「追加一次发射」这类结构性动词 | 多重弹、弹射、穿透这类词条只能靠改引用 ID，组合能力不足 |
| E | 幅值来源表达力不足 | `MagnitudeSourceType=Attribute` 只支持单属性×系数；`MagnitudeBaseValue=0`/`Coefficient=0` 被当作未填写 | 无法区分「没填」和「有意填 0」，例如 +0% 暴击这类词条 |
| F | 参数身份用整数 TargetId，代码侧曾以 byte 传递 | 历史 TargetId 走 byte，超范围截断到 255 | 扩量后 ID 静默截断，最难排查的一类 bug |
| G | `modifier_parameters.py` 的 `CONSUMER_CONTRACTS` 是人工审计表 | 文档自述不能仅凭类名证明所有分支都已调用 | 容易出现有配置无消费或有消费无配置 |

## 3. 四层模型

把被修饰对象、参数身份、修饰语义、来源与生命周期四件事彻底分开，每层一个稳定身份：

```
被修饰对象类型  TargetKind  ──►  Attribute(1) | SkillParameter(4) | SkillSlotCandidate(5) | ParameterReferenceCandidate(6)
      │
      ├─ Attribute          → ParameterId 指向 attr_types（属性目录）
      └─ Parameter(4)       → ParameterId 指向 修饰参数（领域参数目录）

参数身份        ParameterKey ──► (Domain, Key) 稳定字符串，发布后不可改名
      │
      ├─ ValueKind  ReferenceId | Integer | Float
      ├─ OpMask     Add | Mul | Override | PercentAdd（位掩码，导出器校验）
      ├─ ResolveAt  取值阶段（Action/Launch/BuffApply/Emit/ProjectileSpawn/Hit/...）
      └─ Capture    CreateSnapshot | EventSample | LiveProperty（后者门禁见设计文档 §12）

修饰语义        ModifierOp + MagnitudeSource
      │
      ├─ Fixed / Attribute / Formula / TimeDecay / Context
      └─ 聚合顺序：Base → Flat(Add) → Percent → Mul → Override → Clamp（消费点校验）

来源与生命周期  ModifierSource
      │
      ├─ Owner / Source / Target（属性公式取值角色）
      ├─ 稳定实例身份：World/ContinuousInstanceId/Generation（不是配置 ID）
      └─ Register→Activate→Pause/Resume→End→Unregister，投影只能由生命周期写入
```

关键约束（应写进导出器）：

- 优先级语义统一声明：数值 Override、槽位候选、引用候选都以较大 Priority 优先，同级按较小 SourceId 决胜；默认组合器不依赖写入顺序。
- Flat / Percent 的落点固定在消费点。同一个「速度」在 ProjectileSpawn 取样是创建快照，在飞行中改是 LiveProperty，是两项能力而不是一个参数的两种模式。
- 每条投影反向关联一个持续实例，Pause/End 原子撤销。回滚先恢复实例与捕获值，再重建投影视图；投影视图不能脱离活跃实例独立存在。
- 每效果一个 owner。施法者的技能/发射器/子弹/AOE 参数向施法者 owner 提供效果；Buff 作用于目标时向目标 owner 提供。只有真正独立承受效果的对象才建独立 owner，并显式定义创建/销毁/血缘。

## 4. Excel 映射设计

### 4.1 层一：参数目录（身份与能力）

对应现有子表 `修饰参数`。建议只做增量：保留 `Id / Domain / Key / ValueKind / OpMask / ResolveAt`，把 `Category / SubCategory` 从「身份」降级为「运行时打包提示」，并由 codegen 从 `Id` 自动推导，不再要求策划手填。

推荐新增列：

| 列 | 类型 | 作用 |
|---|---|---|
| `DisplayName` | string | 调试与诊断显示名，不作身份 |
| `Unit` | enum | None/Ms/Percent/Flat/Count/Coefficient/Id；导出器校验 OpMask 与 Unit 相容 |
| `ValueMin` / `ValueMax` | float | 合法范围；ReferenceId 必须留空（引用完整性由外键校验） |
| `CapturePolicy` | enum | CreateSnapshot/EventSample/LiveProperty；LiveProperty 未注册更新适配器时导出器拒绝 |
| `LiveAdapter` | string | CapturePolicy=LiveProperty 时的领域更新适配器 ID，其他留空 |
| `IsMutable` | bool | 是否允许被 Override；引用型只允许 Override |
| `CapabilityVersion` | int | 能力版本，用于跨端 golden 与弃用检查 |
| `Deprecated` | bool | 弃用行；导出器警告并阻断新引用 |
| `Notes` | string | 策划说明，不参与 codegen |

字段语义的关键取舍：

- `Key` 是身份，`Id` 是引用。Excel 外键、Buff 的 `ModifierIds`、诊断日志用 `Id`；代码成员名、跨表 join、Profiler 标签用 `Key`。导出器强制一个 `(Domain, Key)` 只能有一行，并把发布后改名视为破坏性变更（靠生成产物 ABI 比对脚本把关）。
- `Domain` 是唯一允许按表拆分的自由度。当前 21 行规模一张表毫无压力；当某 Domain 超过约 120 行时允许拆成 `修饰参数_{Domain}` 子表，codegen 合并。拆分只影响作者体验，不影响 ID 空间与运行时。
- Id 段位继续保持按 Domain 分段（现有 10000/20000/.../70000，每域 10000）。肉鸽量级下每域 10000 个参数远超需要；真正稀缺的是 Category 的 8 位，不是 Id。
- `ValueKind` 决定通道，不决定消费点。数值型走 `MobaSkillParamModifierService`，引用型走优先级候选服务。这条已在实现里成立，Excel 只负责声明并由导出器强制 ReferenceId ⇒ OpMask=Override。

### 4.2 层二：修饰器定义（语义与来源）

对应现有子表 `修饰器定义`。定位不变：可复用的数值/候选修饰，被 Buff 与持续流程用 `ModifierIds` 引用。字段分四组：

1. 目标：`TargetKind` + `TargetId`。TargetId 的引用对象由 TargetKind 决定（属性 → `attr_types.Id`；参数 → `修饰参数.Id`；槽位候选 → 槽位号；引用候选 → 具体模板 Id）。导出器拒绝错段、悬空、未知目标。
2. 操作与优先级：`Op`、`Priority`、叠层策略。叠层策略显式化为 ScaleMagnitude / RepeatContribution / HighestStack / NoStack；Override 默认 NoStack。
3. 幅值来源：`MagnitudeSourceType` + 相关列。建议修正问题 E：把 `MagnitudeBaseValue` / `MagnitudeCoefficient` 从「0 = 未填写」改为配合显式 `HasBaseValue` / `HasCoefficient` 布尔列；或更彻底地改为单一带类型的结构化 `MagnitudeSpec`，让 Attribute/Formula/TimeDecay/Context 各自只填需要的子字段，避免一列在五种来源下语义漂移。公式用有界后缀指令表，不执行任意脚本。
4. Selector 与求值策略：现有 8 个 `Selector*` 列（0 = any，正值 = 精确匹配阶段记录）+ `EvaluationPolicy`。建议把 8 个平铺列改成「一条 Selector 列表」bean（每条 `{Field, Id}`），从改列变成加行。肉鸽下字段会持续增长，平铺列必然先撞上限再被迫重构；codegen 仍可为高频字段生成快路径。

### 4.3 层三：修饰器绑定（谁挂在哪里）— 当前缺失

目前 Buff / 持续流程通过各自 `ModifierIds` 直接引用定义，隐含「谁提供」由挂载位置决定。这在只有一个提供者时够用，但肉鸽需要：

- 同一份修饰定义被多个不同来源复用，但挂载位置与阶段记录不同；
- 明确谁把它投影到哪个 owner、什么阶段、什么顺序；
- 互斥/叠加/刷新策略在绑定层声明，而不是散落在 Buff 各处的假设。

建议新增子表 `修饰器绑定`：

| 列 | 说明 |
|---|---|
| `Id` | 绑定身份，全表唯一 |
| `ModifierId` | → `修饰器定义.Id` |
| `ProviderKind` | Buff / ContinuousProcess / Equipment / Talent / RunUpgrade / Bridge 等 |
| `ProviderRef` | 具体 Buff / 持续流程 / 装备模板 ID，决定实例生命周期来源 |
| `OwnerRole` | Owner / Source / Target / Caster：本效果聚合到哪个 owner |
| `SelectorOverrides` | 覆盖定义级 Selector，如全局词条只匹配某 Domain |
| `StackPolicy` | AddStack / RefreshDuration / Independent / HighestPriority / MutuallyExclusive |
| `Priority` | 同 owner 同参数多来源的裁决顺序，缺省继承定义 |
| `Enabled` | 灰度开关，便于分批上线肉鸽词条 |
| `Conditions` | 条件表达式（谓词 ID 引用），留空即无条件 |

绑定层是把词条和机制解耦的关键：定义说「+1 颗子弹 / +25% 伤害」，绑定说「这份词条来自本局升级 B，只匹配 Slot=2 且子弹模板 P1，在 Emit 阶段生效」。肉鸽词条库因此可以独立增长，无需为每个词条改 Buff 或代码。

## 5. ModifierKey 容量与 CustomId 的使用

`ModifierKey` 是 32 位：`[Reserved:8][Custom:8][Sub:8][Category:8]`。现状 codegen 只用 Category/SubCategory，CustomId 恒为 0。

扩量建议（须在铺量前决定，不阻塞当前内容）：

1. 不再手填 Category/SubCategory，改由 codegen 从 `修饰参数.Id` 分配：同 Domain 内参数序号低位放 SubCategory，SubCategory 满 255 后进位到 CustomId。策划新增参数只需加一行 Id/Key，不用再决定归哪个 Category，同时解决问题 B 和 C；已发布键位按「旧 Category/Sub 保持不变」迁移即可。
2. Category 退化为 Domain 的紧凑编号，只用于调试分组与分桶，不再是身份。
3. 迁移期由导出器强制已发布 Category/SubCategory 与生成产物一致，不静默重排，避免跨端 golden 位锁漂移。

## 6. 什么进 Excel、什么留代码

| 能力 | 归属 | 理由 |
|---|---|---|
| 属性值、技能/发射器/子弹/AOE/伤害的数值 | Excel 修饰参数 + 定义 | 策划可改、可热更、可组合 |
| 引用型选择（用哪个技能/发射器/子弹/召唤模板） | Excel 修饰参数（ReferenceId）+ 优先级候选 | 同上，不做数值运算 |
| 弹道行为、碰撞拓扑、命中策略、轨迹转向 | 代码侧注册表 | 属于领域能力不是数值；`ProjectileHitPolicyFactory` 已改注册表，新增命中策略是加一个类而不是加一行表 |
| 新的状态维度、跨实体关系、每帧遍历 | 领域服务 / 系统 | 需要新生命周期与回滚契约，配置改不出来 |
| 技能内部时序（蓄力、阶段、提交点） | skill_flow phase | 触发器不承载施法内部时序 |
| 表达式节点与公式编译器 | 代码，配置只引用节点 ID | 配置执行脚本是确定性禁区 |
| 叠层/互斥/刷新的策略枚举 | Excel 声明，语义实现留代码 | 配置选策略，代码执行策略 |

一句话：Excel 选「改哪个参数、怎么改、什么时候生效」；代码决定「这个动作怎么执行」。

## 7. 一个词条的完整映射链路

肉鸽升级词条「多重弹」：二技能每轮多一颗子弹，仅对子弹模板 P1 生效，Emit 阶段取样。

1. 层一参数目录：已有行 `30002 / Emitter / CountPerShot / Integer / OpMask 含 Add / ResolveAt=Emit`，词条不改目录，直接复用。
2. 层二修饰器定义：新增 `Id=400003`（发射器段）、`TargetKind=4`、`TargetId=30002`、`Op=Add`、`Value=1`、`Priority=50`、`MagnitudeSourceType=Fixed`、`EvaluationPolicy=OnApplySnapshot`，可选 `SelectorLauncherTemplateId=L1`。
3. 层三修饰器绑定：`Id=9001`、`ModifierId=400003`、`ProviderKind=RunUpgrade`、`ProviderRef=upgrade_double_shot`、`OwnerRole=Owner`、`SelectorOverrides=Slot=2,Launcher=L1`、`StackPolicy=Independent`。Buff 与持续流程不再直接挂 ModifierIds，改挂绑定。
4. codegen：`修饰参数.Id` → `ModifierKey`（CustomId 位自动分配，不手填）；绑定与定义生成查询索引；`modifier_parameters.py` 的 `CONSUMER_CONTRACTS` 校验 30002 在 Emit 阶段有具名消费者 `MobaModifierProjectileSpawnPatternProvider`（已登记）。
5. 运行时：升级实例激活 → binder 按绑定向施法者 owner 投影一条 Add 1（Selector：Slot=2、Launcher=L1）→ 下一轮 Emit 取样命中 → 实际多生成一颗子弹。Pause/End 按实例撤销；已生成子弹保持创建快照；回滚重放一致。
6. 纵向验收：激活多一颗 → Pause 后回基础 → Resume 再多一颗 → End 回基础 → 回滚到四个阶段任一点重放一致 → 换技能后旧子弹仍按创建快照命中。

## 8. 导出与校验规则

每个参数行发布前必须同时满足：

1. Id 落在所属 Domain 区间且唯一；(Domain, Key) 唯一；打包后的 ModifierKey 唯一。
2. ValueKind 与 OpMask 相容（ReferenceId ⇒ OpMask=Override；PercentAdd 与 Unit 相容）。
3. CapturePolicy=LiveProperty ⇒ LiveAdapter 已注册且该阶段允许活跃更新，否则拒绝。
4. ResolveAt 在 `CONSUMER_CONTRACTS` 中已登记，且代码侧对应消费方法带 `MobaModifierConsumes` 声明（ID + 阶段 + 取样时机），两侧一致。
5. ValueMin/ValueMax 存在时，导出器与运行时消费点共用同一范围，避免配置合法而运行时拒绝。
6. TargetKind=4/6 的 TargetId 必须落在与所选 Domain 匹配的参数段；TargetKind=1 必须存在于 attr_types。
7. 定义的 Op 必须在目标参数 OpMask 内；Priority 语义文档化为越大越优先。
8. 绑定行必须指向存在的定义与存在的 Provider 模板；灰度词条 Enabled=false 不做行为验收但仍做引用完整性校验。
9. 弃用行不得被新绑定引用；发布时输出弃用影响报告（复用现有 modifier_impact_report.py）。

## 9. 性能边界与索引

- 主路径已有反向索引：`MobaSkillParamModifierService` 按 `(owner, ModifierKey) → indices`。这是每帧热点，保持它。
- `EvaluateNumeric` 是诊断/explain 辅助路径，不是热点；已改 ArrayPool + Span 降分配，无需为它再投资源索引。
- 投影视图按 `(OwnerRef, ParameterId)` 分桶，再匹配 Selector；用版本号缓存已筛选的活跃投影集合，缓存键含 owner、Selector 所需模板/槽位、阶段。公式引用实时数据时不得误复用旧缓存。
- 高频读取用有界缓冲区或池化，避免每颗子弹排序与分配。发布器限制单持续实例投影条数、单 owner 活跃条数、公式深度与节点数。
- 若将来要给 `Collect` 建索引：必须先给 `IContinuousManager` 加生命周期订阅契约，由 Continuous 生命周期维护索引；不能挂在 projector registry 上，因为 `Collect` 允许直接构造并扫描 manager，会漏结果。
- 性能门槛按典型肉鸽高弹量场景测 CPU、分配、快照体积，而不是英雄 6 对 6 的日常对局。

## 10. 建议落地顺序（不含召唤/移动新增键）

1. 扩 ModifierKey 容量并让 codegen 从 Id 自动分配（用满 CustomId 位，迁移保持已发布位不变）。这是扩量前置，其他都建立在它之上。
2. 合并属性与领域参数的身份抽象：给属性也产出 ParameterKey（可在 `修饰参数` 里以 Domain=Attribute 表达，或让投影器把 attr_types.Id 统一编到同一 Key 空间），消掉 MobaAttributeIds 硬编码与 codegen 表的不对称。
3. 新增 `修饰器绑定` 子表，把「谁挂哪里」从 Buff 的隐式位置显式化；Buff 与持续流程改挂绑定。
4. Selector 从平铺 8 列改为列表 bean，并把幅值来源收敛为结构化 MagnitudeSpec，顺带修掉「0 = 未填写」（问题 E）。
5. 补齐第 8 节的导出校验，把有配置无消费拦在发布期，而不是靠人工审计表。
6. 召唤物与移动参数（Domain Summon / Motion）在 1 到 5 就位后按同一模型扩，只是多两行目录加两个领域消费适配器，不改架构。

## 11. 与现有文档的关系

- 本文是参数到 Excel 的映射层设计，与 [MobaModifierFormalDesign](MobaModifierFormalDesign.md)（目标契约与 P0-P4 放行条件）、[MobaModifierAuthoring](MobaModifierAuthoring.md)（作者字段契约与扩展步骤）互补，不重复其生命周期与验收条款。
- 本文不修改发布顺序与权威来源；任何落地改动仍按 `.claude/skills/moba-skill-authoring/SKILL.md`：先改源表与 Trigger Authoring，最后 `python LubanConfig/Moba/export_pipeline.py --apply`，并以 `--check-published` 与英雄门禁收口。

---

本文只做设计与映射约定，未改生产配置、未改运行时。落地任一条时同步更新 MobaModifierAuthoring.md 字段表与 modifier_parameters.py 校验项。
