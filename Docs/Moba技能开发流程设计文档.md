# MOBA 技能开发流程设计文档

## 1. 目标

建立一套可重复、可追踪、可分层收敛的技能开发流程，覆盖单元测试、冒烟测试、DSL 环境测试与严格模式验收，避免技能只“能跑”但缺少配置正式化、跨模块契约和回归保障。

## 2. 流程分层

### 2.1 单元测试

单元测试面向最小逻辑单元，验证技能参数解析、配置引用、时序计算、伤害/BUFF/区域触发等核心规则。

作用：
- 快速定位代码级回归。
- 保障技能逻辑在不依赖完整战斗环境下仍可验证。
- 适合补齐边界条件，例如 `DurationMs`、`IntervalMs`、`position_mode`、触发链路、配置缺失等。

典型覆盖：
- `SpawnAreaSchema` 参数解析。
- `SpawnAreaPlanActionModule` 生命周期计算。
- `MobaBattleConfigReferenceValidator` 配置引用合法性。
- 技能 trace 断言与 HP 变化断言。

### 2.2 冒烟测试

冒烟测试面向最短闭环，验证技能从输入到结果的主链路是否仍可运行。

作用：
- 检查基础战斗入口、UI、运行时服务、输入流是否可用。
- 用最少样本发现集成层破坏。
- 适合作为继续开发前的 P0 门禁。

典型覆盖：
- 启动战斗环境。
- 释放一个技能。
- 观察最基本的 trace、伤害、BUFF、区域生成是否出现。

### 2.3 DSL 环境测试

DSL 测试面向场景化、可读性更强的验收样本，使用 scenario JSON 描述 actor、timeline、setup、stateExpectations 和 contextExpectations。

作用：
- 让设计、策划、开发共享同一份场景描述。
- 便于复现复杂技能过程，例如延迟区域、周期伤害、复合触发链。
- 适合作为技能“正式需求”的验收载体。

典型覆盖：
- Xiao Qiao 技能 2 的延迟区域命中。
- Xiao Qiao 技能 3 的持续 BUFF + 间隔伤害。
- 复杂位置模式、目标模式、持续时间语义。

## 3. 这套流程带来的好处

### 3.1 开发效率

- 单测能最快反馈参数和逻辑错误。
- 冒烟测试能快速确认主链路没有断。
- DSL 测试能让复杂技能一次性验证完整行为。

### 3.2 需求正式化

- area 的 `DelayMs`、`DurationMs`、`VfxId`、触发数组不再只停留在约定层。
- `position_mode` 的语义可以被明确写进 scenario 与解析层。
- 配置缺字段、字段混用、隐式默认值问题更容易暴露。

### 3.3 回归控制

- 每个技能都有可回放样本。
- 重构 runtime、view、config、triggering 时不会只靠人工验证。
- 适合阶段性收口和批量回归。

### 3.4 协作成本下降

- 策划看 DSL 就能理解技能预期。
- 开发看单测就能定位实现缺陷。
- QA 看冒烟和 scenario 结果就能判断回归范围。

## 4. 推荐门禁链路

### 4.1 开发前

必须通过：
- 单元测试
- MOBA console smoke

目的：确保继续写功能之前，基础 runtime 没坏。

### 4.2 技能实现阶段

必须补齐：
- 参数解析单测
- 关键 trace 断言
- 场景级 stateExpectation

目的：把“能跑”提升为“行为正式”。

### 4.3 技能收口阶段

必须通过：
- DSL 场景验收
- 关键分支回归
- 配置引用校验

目的：确认技能在真实输入、真实时序、真实配置下稳定。

## 5. 适用对象

这套流程适用于：
- 子弹技能
- 区域技能
- 持续效果技能
- 召唤物技能
- 表现/特效驱动技能
- 需要 `position_mode`、目标集合、偏移、持续时间等参数的复合技能

## 6. 当前实践建议

- 新技能先写最小单测，再补 DSL scenario。
- area 必须区分 `DelayMs` 和 `DurationMs`，不能混用。
- `VfxId`、触发效果数组、位置参数必须进入配置校验。
- 复杂技能优先用 scenario JSON 固化期望，再补充 trace 和状态断言。
- 回归时优先跑 P0 门禁，再跑技能相关 acceptance，最后做批量回归。

## 7. 新英雄制作总流程

新增英雄应按“需求拆解 -> ID 规划 -> 逻辑配置 -> 输入与指示器 -> 场景表现 -> 网络表现 -> 分层验收 -> 门禁收口”的顺序推进。不要先堆表现资源再反推逻辑，也不要只在 View 层实现逻辑范围或命中结果。

### 7.1 需求拆解

每个英雄先建立一份 Hero Definition，至少记录：

- 英雄定位、基础属性模板、模型和技能槽。
- 普攻、被动、主动技能的精确行为，不以技能名称代替规则描述。
- 每个技能的施法前置、输入方式、目标选择、施法距离和取消条件。
- 前摇、执行点、后摇、持续时间、周期、最大循环次数和中断规则。
- 伤害、治疗、位移、控制、BUFF、AOE、Projectile、Summon 的组合关系。
- 逻辑范围、HUD 预览范围、模型尺寸和 VFX 尺寸之间的对应关系。
- 本地预测内容、权威确认内容以及远端需要重放的表现。
- 可自动验证的状态结果、trace 结果和视觉资源引用。

技能需求必须拆成可判定语句。例如“指定方向连续发射飞剑”需要明确方向何时锁定、发射数量、间隔、每枚投射物的出生点、碰撞目标、是否可重复命中以及中断后是否停止后续发射。

### 7.2 ID 与命名规划

开发配置前先登记业务 ID，避免边做边分配造成跨表碰撞。一个英雄通常需要规划：

- Character、属性模板、模型、技能、技能等级表和技能按钮模板 ID。
- SkillFlow、Action/Effect、Buff、ContinuousProcess、Trigger ID。
- SearchQuery、AOE、ProjectileLauncher、Projectile、Emitter、Summon ID。
- VFX、音效、动画和 Presentation Template ID。

命名应包含英雄、技能槽和用途，例如 `ZhaoYun.Skill3.Projectile.HitVfx`。同一业务对象在 DTO、JSON、测试和资源命名中使用一致词根。复制旧英雄配置后必须重新分配所有业务 ID，并执行 ID 与跨表引用校验。

### 7.3 配置矩阵

| 配置 | 职责 | 新英雄检查点 |
| --- | --- | --- |
| Character | 英雄入口、模型、属性和技能槽 | 技能槽顺序、普攻/被动引用、模型存在 |
| AttributeTemplate | 基础战斗属性 | HP、攻击、防御、移速及默认值明确 |
| Skill | 冷却、范围、按钮、等级表和 Flow 入口 | `SkillButtonTemplateId`、`RequiredTargetQueryId`、`LevelTableId`、`PreCastFlowId`、`CastFlowId` 均可解析 |
| SkillLevelTable | 等级成长参数 | 每级数组长度、单位和公式一致 |
| SkillFlow | 技能阶段编排 | 选型正确、可结束、可中断、循环有上限 |
| Buff / ContinuousProcess | 持续状态与周期行为 | 刷新/叠层/移除策略、周期和 owner 生命周期明确 |
| AOE | 区域逻辑与表现引用 | 半径、延迟、持续时间、模型和 VFX 完整 |
| ProjectileLauncher / Projectile | 投射物生成和命中 | 数量、间隔、速度、碰撞、出生/命中/消失表现完整 |
| SearchQuery | 目标筛选 | 阵营、形状、数量、排序和空目标行为明确 |
| SkillButtonTemplate | HUD 输入与预览几何 | AimMode、UsePointMode、IndicatorShape 和尺寸匹配技能语义 |
| Presentation / VFX | 可重放表现 | 稳定 key、跟随对象、持续时间、缩放和资源存在 |

配置字段必须贯穿 DTO -> MO -> Resolver/Runtime -> Snapshot/View -> Test。新增字段只写入 JSON、但没有进入反序列化对象或消费层，不算完成。生产配置应通过严格模式加载，不能依赖静默默认值掩盖漏配。

## 8. 技能逻辑实现规范

### 8.1 主动技能、被动和普攻

- 主动技能由 Skill 配置连接按钮模板、等级表和 Flow；输入只表达施法意图，最终结果由战斗逻辑决定。
- 被动技能优先使用 owner-bound Trigger 或 Buff 生命周期安装，英雄销毁、死亡或被动移除时必须解除订阅。
- 普攻复用统一攻击、选敌、伤害和投射物链路；除非机制确实不同，不为单个英雄复制一套攻击运行时。
- 组合技能通过上下文、黑板或明确的 gameplay counter 传递状态，不依赖 View 对象或不稳定字符串查找。

### 8.2 SkillFlow 选型

| 阶段类型 | 使用场景 | 约束 |
| --- | --- | --- |
| Checks | 资源、目标、距离、状态前置校验 | 不产生不可回滚副作用 |
| Timeline | 前摇、执行点、后摇和动画时间轴 | 执行点与伤害/生成时刻一致 |
| Handlers | 兼容已有处理器 | 新逻辑优先迁移到可组合计划 |
| RulePlan | 调用 Action/Effect/Trigger 计划 | 参数 schema 和确定性必须可验证 |
| Sequence | 串行步骤 | 任一步失败、中断和完成语义明确 |
| Parallel | 同时启动独立步骤 | 汇合条件明确，不能永久等待 |
| Repeat | 多段攻击、连续发射和周期行为 | 必须配置次数或可靠终止条件 |
| Delay | 固定延迟 | 使用逻辑时间，禁止 View 协程决定结果 |
| WaitUntil | 等待状态、投射物或动作结束 | 必须有超时/取消路径，防止技能卡死 |

复杂技能优先组合这些基础阶段。只有已有阶段无法表达、且新增能力可被多个技能复用时，才新增通用 Action、Predicate 或 Flow 类型。

### 8.3 Trigger、Buff 与持续效果

- Trigger 必须声明事件来源、owner 绑定方式、条件和 Action；条件读取的 payload 字段需要有类型化 accessor。
- 位置参数与命名参数要遵守运行时协议。计划生成的 positional args 键为 `"0"`、`"1"` 等，不得自行假设为 `"_0"`、`"_1"`。
- Buff 必须定义添加、刷新、叠层、到期、主动移除和死亡清理语义。
- 周期效果明确 `DurationMs` 与 `IntervalMs`；二者不能互相替代，边界 tick 是否执行要有测试。
- 连续命中、叠层触发和命中去重应绑定稳定的技能实例/效果实例标识，避免不同施法互相污染。
- Trigger、Buff 和 ContinuousProcess 创建后必须有对称的 Stop/Dispose 路径。

## 9. HUD 输入与技能指示器

### 9.1 输入链路

HUD 的拖拽、长按和释放先形成 `BattleHudInputState`，经 `BattleContext.Input`、`BattleHudInputSource`、`BattleInputFeature` 转为同步技能命令。最终施法意图由命令工厂构造，网络层传输方向、目标点或锁定目标，而不是传输某个本地预览对象。

必须验证：

- 点按、长按、拖拽阈值和取消区行为。
- 拖拽方向归一化，零向量和超出最大半径的处理。
- TargetPoint、Direction 和 TargetActor 三种意图不会混用字段。
- 本地玩家身份、技能槽和输入帧正确写入命令。
- UI 预览结束后对象及时隐藏，不影响下一次施法。

### 9.2 指示器选型

| 技能语义 | 推荐 Shape | 关键配置 |
| --- | --- | --- |
| 无需瞄准/被动 | Hidden | `EnableAim=false` |
| 指定朝向直线技能 | DirectionLine / DirectionArea | 最大距离、宽度、是否随朝向旋转 |
| 指定落点区域 | TargetCircle | 最大拖拽半径、区域半径、UsePointMode |
| 以自身为中心区域 | SelfCircle | 自身半径，不发送伪目标点 |
| 扇形选区 | Sector / FanArea | 半径、角度 |
| 冲刺 | DashLine | 最大位移、阻挡和终点修正 |
| 锁定目标投射物 | LockProjectile | 搜索半径、锁定时长、RequiredTargetQueryId |

指示器必须表达真实施法语义。指定方向连续发射不是扇形 AOE；指定区域生成剑阵不是方向线；自身范围伤害不应伪装成目标点技能。

指示器尺寸来自 SkillButtonTemplate。`IndicatorWorldWidth`、`AimMaxRadius`、`SectorAngleDegrees`、`DashDistance`、`FanRadius`、`SelfRadius` 等字段必须与逻辑配置对照测试。兼容回退只用于旧配置迁移，生产英雄不应依赖 Resolver 内的隐式常量。

## 10. 场景预览、模型与特效

### 10.1 AOE 三层场景对象

AOE 快照由 View 创建三类对象：

- Model：由 AOE `ModelId` 创建区域实体模型或机关模型。
- Range：由 `Radius` 和 `DelayMs` 创建范围圈、预警圈或延迟填充效果。
- VFX：由 `VfxId` 创建区域出生、持续或爆发特效。

三层对象可以独立为空，但配置必须明确。逻辑碰撞半径是权威值，范围圈和 VFX 只负责表现；View 不得反向决定命中。对象位置使用快照位置叠加配置 offset，预测 View 与 Confirmed View 必须使用同一套位置与池化规则。

场景验收至少检查：

- 范围圈半径与逻辑 AOE 半径一致。
- Delay 期间显示预警，生效时刻与逻辑执行点一致。
- Duration 结束、技能取消和快照移除后，Model/Range/VFX 均回收。
- 多个同模板区域同时存在时不串位置、不串生命周期。

### 10.2 Projectile 表现

投射物配置需要完整描述：模型/飞行 VFX、速度、半径、存活时间、跟随或朝向方式，以及 Spawn/Hit/Expire 表现。

- 出生位置和方向由权威逻辑或可重演输入决定。
- 飞行朝向应由速度/目标方向驱动，延迟绑定目标时仍能正确 follow。
- 命中和消失是不同生命周期事件，不能共用一个含糊的销毁特效。
- 快照重复到达时不得重复生成同一投射物或重复播放一次性命中特效。
- 资源缺失时测试必须失败或输出可定位诊断，生产配置不能长期依赖 placeholder。

### 10.3 Presentation Cue

Presentation Cue 用于把逻辑事件映射为可网络重放的表现。Start 阶段创建 Cue，Tick/Refresh/StackChanged 更新 Cue，Interrupted/Expired/Removed/Completed 等 Stop 阶段销毁 Cue。

持续 Cue 必须有稳定 request key。优先使用逻辑实例提供的 `InstanceKey` 或 `RequestKey`；自动生成 key 时，参与字段必须在客户端和服务端一致。稳定 key 用于：

- 重复快照去重。
- Refresh 更新已有实例，而不是重复生成。
- Stop 精确销毁对应实例。
- 预测结果与权威结果对齐。

Cue 快照中只传稳定、确定的数据，如 VFX ID、目标 actor、位置、offset、scale、radius 和 duration override。Unity `GameObject`、资源实例和本地字典 ID 不得进入网络协议。

## 11. 网络同步约束

技能开发必须同时考虑离线逻辑、预测视图和权威确认视图：

- 输入命令记录施法意图和帧号，逻辑世界产出权威状态。
- Actor、Projectile、AOE、Buff/Cue 等需要远端一致的对象进入快照或确定性重演链路。
- 完整快照用于初始/恢复状态，增量快照只更新变化内容；两种 wire round-trip 都要有测试。
- 快照实体使用稳定 NetId，非法 ID、空 payload 和重复 entry 必须有定义明确的处理。
- 回滚后重新应用输入历史，不重复创建区域、投射物和持续 VFX。
- Presentation Cue 的 Start/Keep/Stop 顺序必须容忍延迟、重复或缺失目标 View；目标稍后出现时可延迟 follow binding。
- 预测 View 与 Confirmed View 的资源创建、位置解析和销毁语义必须一致，差异只来自数据来源和确认时机。

网络相关变更至少覆盖实体快照 round-trip、ECS spawn/update、输入帧重放、预测 reconcile、投射物去重和 Cue key 生命周期。

## 12. 测试与交付门禁

### 12.1 Headless acceptance 模板

每个主动技能至少提供一个成功释放场景，并按机制补充无目标、越界、中断、重复命中、持续结束等分支。验收测试应：

1. 使用真实配置加载英雄和技能。
2. 设置施法者、目标、位置、朝向、HP、Buff 等初始状态。
3. 发布真实技能输入或调用统一 harness 入口。
4. 使用统一 `TickUntilSkillStops` 推进到技能结束，并设置最大 tick 防止死循环。
5. 断言技能确实结束，且未超过最大帧数。
6. 断言 HP、位置、Buff、Projectile/AOE 数量、trace 和上下文计数。
7. 对多段技能断言每段时刻和总次数，而不只断言最终 HP。

测试辅助能力应集中在 `MobaSkillConfigTestHarness`。英雄 fixture 不重复实现 tick、查 actor、加载配置和通用 trace 解析。

### 12.2 HUD 与表现测试

- Resolver 测试：Skill + SkillButtonTemplate 能解析成正确 Shape、Range、Width、Radius、Angle 和 UsePointMode。
- 输入桥测试：拖拽释放能发布正确方向、目标点或目标 actor。
- AOE View 测试：Model、Range、VFX 创建、定位、刷新和回收。
- Projectile View 测试：模型、朝向、follow binding、命中和到期表现。
- Presentation Cue 测试：Start、Refresh、Stop、稳定 key、重复快照和 duration 语义。
- 资源测试：Character model、AOE model、Projectile model 和所有 VFX ID 均能加载。

### 12.3 配置与 CI 门禁

- 使用 BootstrapStrict 运行生产配置引用校验。
- 检查业务 ID 唯一性、跨表引用、数组长度、时间非负、循环有界和资源存在。
- Unity EditMode `-runTests` 让 TestRunner 在执行完后自行退出；当前工程不要额外传 `-quit`，否则域重载后可能在测试执行前退出。
- CI 不仅检查进程返回码，还必须解析 NUnit XML，确认文件存在、`failed=0` 且 `total>0`。`0/0` 不属于通过。
- 技能相关 fixture 通过后再跑网络、配置和资源批量回归。

### 12.4 新英雄 Definition of Done

- Hero Definition 已评审，所有行为可判定且没有含糊的“类似某英雄”描述。
- ID 和命名已登记，无碰撞、无复制遗留。
- Character、属性、技能、等级、Flow、按钮模板和依赖对象引用完整。
- 主动、被动、普攻均有明确生命周期和中断路径。
- HUD 输入和指示器与技能语义、逻辑范围一致。
- AOE、Projectile、Buff、Summon 和 Presentation Cue 表现完整，无生产 placeholder。
- 网络快照、预测、回滚和重复消息行为明确。
- 每个技能有 headless acceptance；关键边界有单元测试或 DSL scenario。
- BootstrapStrict、资源校验、目标 fixture 和 P0 门禁全部通过。
- NUnit XML `total>0`，测试结果可追踪。
- 文档、配置、代码、资源和测试在同一变更中交付。

## 13. 代码优化路线

### 13.1 P0：先补正确性门禁

- 扩展生产配置验证：增加按钮几何语义、Flow 有界性、AOE/Projectile 生命周期、VFX/Model 完整性和英雄技能槽一致性检查。
- 增加 Presentation Cue 从快照解码、Resolver、View Handler 到 VFX Spawn/Refresh/Stop 的端到端测试。
- 固化 Unity 测试脚本：禁止 `-runTests` 与提前 `-quit` 组合，强制校验 NUnit XML 存在且 `total>0`。
- 给新英雄建立机器可读 manifest，列出必须存在的配置、资源和验收 fixture，防止漏表、漏特效和漏测试。

### 13.2 P1：收敛重复实现与隐式语义

- 抽取预测 View 与 Confirmed View 共享的 Area Pool object factory，统一 Model/Range/VFX 创建、配置和回收。
- 将 Summon Spawn/Death/Despawn 与 Character Death/Respawn VFX 变为正式配置字段和 resolver，移除投射物 placeholder 作为生产默认值。
- 明确 Presentation Cue Refresh 的 duration 语义：要么更新剩余生命周期，要么移除未生效参数并禁止配置误用。
- 把廉颇、墨子等 fixture 内重复的 `TickUntilSkillStops` 迁移到公共 harness。
- 拆分 HUD Resolver 中重复的 shape 构造和 fallback，建立统一几何参数对象，避免同一种 TargetCircle 在不同分支出现不同默认 width/radius。
- 对 DTO -> MO -> Runtime/View 的字段贯通增加反射或生成式契约测试，避免配置字段“可写但无人消费”。

### 13.3 P2：提升生产效率和可视化能力

- 提供英雄 scaffold：按 manifest 自动生成 Character、Skill、SkillLevel、SkillFlow、SkillButton、Trigger、Buff、AOE、Projectile 和 acceptance 骨架。
- 生成跨表依赖图与资源完整度报告，按英雄聚合展示缺失引用、未消费字段、placeholder 和无测试配置。
- 建立技能场景预览工具，在同一视图显示逻辑范围、HUD 指示器、AOE Range、模型包围盒和 VFX 尺寸，并自动报告偏差。
- 增加网络表现回放样本：录制输入和快照后，在预测/确认两种 View 上比较对象数量、位置、生命周期与 Cue key。
- 将英雄 acceptance 覆盖率、资源完整率和配置严格校验结果接入持续集成趋势报告。

### 13.4 本轮 P0-P2 落地状态

已完成的正确性与维护性改造：

- P0：`MobaBattleConfigReferenceValidator` 已校验 SkillButtonTemplate 的原始枚举范围和负数几何/时间字段；零值继续保留给既有 runtime fallback。`MobaProductionConfigReferenceValidationTests` 覆盖非阻断 warning 行为。
- P0：Presentation Cue 已覆盖 Snapshot -> Resolver -> View Handler 的 Start / Refresh / Stop 闭环，断言稳定 key 复用同一 VFX 实体、正数 duration 重启生命周期，以及 Stop 销毁实体并移除 active key。
- P0：Unity batchmode 测试继续禁止与 `-quit` 组合；本轮结果以 NUnit XML 的存在性、`total>0` 与 `failed=0` 判定。
- P1：预测与 Confirmed View 共用 Area Pool object factory；廉颇、墨子 fixture 已改用公共 `MobaSkillConfigTestHarness.TickUntilSkillStops`。
- P1：Cue Refresh 仅在 duration override 为正数时重启已有 VFX lifetime，零或负数不改变当前生命周期，并有直接回归测试。
- P2：已提供 `tools/moba-hero-manifest.json`、`validate_moba_hero_manifest.ps1` 和带 `SupportsShouldProcess` 的 `new_moba_hero_manifest.ps1`。清单校验生产 Character、资源路径、验收覆盖和 fixture 源码，目前覆盖 6 名英雄。

本轮验证：`powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate_moba_hero_manifest.ps1` 通过；`new_moba_hero_manifest.ps1 -WhatIf` 仅输出创建预览；Unity 2022.3.62f1 定向 EditMode 结果为 75/75 通过，NUnit XML 显示 `total=75`、`failed=0`。

未完成的后续项仍包括 Summon/Character 生命周期表现配置、HUD 几何对象统一、DTO 字段贯通生成式契约、跨表依赖图、场景预览和 CI 趋势报告。

## 14. 当前阶段验证基线

本轮针对网络同步改动完成 Unity 2022.3.62f1 批处理编译，日志正常退出且未发现 C# 编译错误，因此没有修改网络同步实现。

关键 EditMode 测试基线：

- 实体快照完整/增量序列化、wire round-trip 和 ECS 应用：8/8。
- 输入身份、帧重放、预测回滚、Cue、投射物去重、资源及生产配置校验：79/79。
- 合计：87/87。

这组结果是当前阶段的回归基线，不替代新英雄自身的 acceptance、HUD 和 DSL 场景验收。

## 15. 结论

单元测试负责“逻辑正确”，冒烟测试负责“链路可用”，DSL 环境测试负责“需求正式化与可读验收”；配置严格校验、HUD/表现测试和网络快照测试负责保证英雄能进入生产链路。按本文的 Definition、配置矩阵、表现约束和门禁执行后，新增英雄可以从“修到能跑”升级为“需求可判定、配置可追踪、表现可重放、网络可回归”的工程化交付。
