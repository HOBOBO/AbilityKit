# MOBA 位移配置表迁移方案

## 当前归属

`dash`、`jump`、`pull` 是 Trigger Authoring 的 PlanAction。Action 参数保存速度、时长、优先级、碰撞与目标参数，也可携带 `ContinuousProcessId` 或 `ContinuousTagTemplateId`。动作创建 `MobaMotionContinuousRuntime`，将运动源挂到实体的 Motion Pipeline；持续标签模板负责激活、持续和执行阻断。`J-效果表.xlsx` 中的 `运动组` 只管理运动源之间的抑制关系，`持续流程` 保存通用持续行为参数。`blink` 当前是瞬时动作，不经过同一种运动持续对象。普通输入移动、寻路与 AI 移动则直接调用 `MobaCombatRulesService.CanMove()`，目前仍固定查询 `CannotMove` 结果位。

技能的先行实现已将 `技能.ExecutionBlockedFlagIds` 放入 `J-技能表.xlsx`，发布成掩码并在施法入口及运行期使用。位移表要复用同一个“配置结果标志 ID -> 编译掩码 -> 通用执行门禁”的契约，但不同位移类型必须分别选择结果位：禁足应阻止自主移动，不应默认阻止击飞或牵引。

## 建议表结构

新增 `LubanConfig/Moba/Production/Datas/J-位移表.xlsx`，先建立 `位移定义` 子 sheet；后续可将现有 `运动组` 迁入同一工作簿，不改变英文表名或运行时 ID。每条 `位移定义` 是可复用的参数模板，不是一段时间轴。

| 字段 | 含义与约束 |
|---|---|
| `Id / Name / Kind` | 稳定模板 ID、中文名称和强类型 `Locomotion/Dash/Jump/Pull/Blink` 类别。先迁 `Dash`，其他类别验证后启用。 |
| `ExecutionBlockedFlagIds` | 引用 `结果标志定义.Id`；导出时校验去重并编译掩码。允许为空。 |
| `ContinuousTagTemplateId / MotionGroupId` | 引用现有持续标签模板和运动组；`Blink` 不适用持续模板时必须显式校验。 |
| `Speed / DurationMs / Priority` | 运动源的静态默认参数；只对支持该字段的 Kind 生效。 |
| `CollisionPolicy / PassThroughWalls` | 强类型碰撞策略与穿墙选项；穿墙仍要明确是否扫描单位命中。 |
| `Height / TargetDistance` | 分别用于跳跃、牵引，按 Kind 校验，不把无关列解释为通用参数。 |

Action 以后只引用 `MotionProfileId` 并传运行时输入，例如施法者、目标、方向、瞄准位置和来源上下文。技能内部的启动时刻继续由 Pipeline/ActionEditor 决定；目标选择和“何时施加位移”继续由 Trigger Authoring 决定。可复用的运动数值与门禁才进入 Excel，避免 Action 参数和 Excel 同时成为速度、碰撞策略的编辑源。

## 分阶段实施

1. 增加 `位移定义` 的 Luban schema、引用校验与二进制/JSON 发布。以一条 Dash 模板验证读取、强类型字段和结果位掩码；暂不切换其他动作。
2. 让 `dash` Action 支持 `MotionProfileId`，按模板构造运动源；保留旧参数只用于尚未迁移的动作，并检查同一动作不能同时填写互相冲突的值。验证墙体穿越、单位命中、持续对象结束后停止运动源。
3. 将现有 Dash Action 逐条迁到模板引用并跑六英雄冒烟，随后按 Jump、Pull 的不同参数和对敌免控规则分别迁移。廉颇自身跳跃不能误用对敌击飞的霸体阻断模板。
4. 为普通输入移动与寻路配置单独的 `Locomotion` 门禁，替代代码固定读取 `CannotMove`。强制位移配置自己的掩码，默认不继承自主移动门禁。
5. 单独决定 `Blink` 的前置门禁与碰撞策略；它没有持续 Tick，不能仅接入 `MobaContinuousManager.CanExecute()`。全部消费者迁完后，删除 Action 中已失去编辑权的重复静态参数。

## 验收与风险

- 配置引用：不存在的模板、结果位、运动组和非法 Kind/字段组合在导出时失败；二进制与 JSON 读取结果一致。
- 自主移动：禁足和硬控停止输入移动、寻路；解除后恢复。多来源标签只在最后一个来源结束时解除阻断。
- 强制位移：禁足不误挡击飞；目标霸体、免控按目标有效标签拒绝控制。分别验证起跳、落地、高度、穿墙和单位命中。
- 生命周期：执行阻断时运动源停止或持续对象结束，不能出现标签结束后运动仍在推进；回滚恢复后掩码与运动源状态一致。
- 迁移期的主要风险是 Action 参数与 Excel 模板双重编辑，以及把 `Locomotion` 的门禁误套到 Dash/Jump/Pull。每批只切一种 Kind，并在全量迁移后清理旧字段。
