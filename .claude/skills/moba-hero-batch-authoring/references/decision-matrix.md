# 能力落点决策矩阵

使用顺序：先看生命周期，再看状态维度，再看逻辑编排，最后才考虑新增代码。每一行都要能回答“谁产生、谁消费、何时开始、何时结束、如何验收”。

| 需求 | 首选落点 | 判断 | 常见例子 | 需要暂停的信号 |
|---|---|---|---|---|
| 技能/效果/持续时间/范围/伤害等内容参数 | Luban Excel | 同一机制只因英雄或等级改变数值 | 技能冷却、Buff 时长、投射物速度、区域半径 | 字段没有明确表或消费者 |
| 施法阶段、蓄力、引导、等待、提交点、资源结算 | Skill Flow | 它描述一次施法的内部时间序列 | 廉颇三段大招、等待事件、阶段提交 | 试图用 TriggerPlan 节点表达阶段 |
| 时间轴逻辑动作 | ActionEditor logic | 动作属于时间轴且需要按阶段导出 | 发射点、信号、逻辑 Effect 动作 | authoritative clip 没有导出支持 |
| 时间轴可见轨道 | ActionEditor presentation | 只影响客户端表现 | 粒子、动画、声音、Cue | 表现影响模拟结果 |
| 事件条件和效果动作 | Trigger Authoring | 已有事件和 PlanAction 可以拼出行为 | 命中后伤害、加 Buff、区域进入 | 需要新动词或隐含顺序 |
| 持续中的运动/位移/标签 | 现有 Continuous Runtime | 有明确开始、更新、结束生命周期 | dash、jump、pull、blink、持续 Buff | 需要跨实体关系或新状态维度 |
| 目标过滤/排序 | Targeting 注册扩展 | 规则可独立表达并可注册生成 | 按标签排除、按威胁排序 | 规则依赖未定义的关系状态 |
| 新的触发动作动词 | PlanAction 扩展点 | 多个技能可能复用且动作是领域能力 | spawn_area、shoot_projectile 类动作 | 只服务一个技能或需要事务/实体生命周期 |
| 跨实体关系、世界级仲裁、逐帧遍历 | 领域服务/系统 | 状态维度不是单实体 | 绑定伤害、仇恨、观察者视野 | 试图塞入 Buff 或 Owner 标签服务 |
| 创建/销毁投射物、区域、召唤物 | 既有实体服务 + 生命周期契约 | 必须明确 ID、所有权、回滚/重连边界 | spawn_summon、projectile、area | 快照、Actor ID 或重建契约不完整 |
| 纯客户端可见效果 | Presentation Cue/资源 | 不影响模拟和判定 | VFX、HUD、动画提示 | 逻辑依赖客户端状态 |

## 复用检查

在新增类型前依次搜索：

1. `MobaMotionContinuousRuntime`、`BuffContinuousRuntime`、`MobaProjectileLauncherContinuous` 等同生命周期实现。
2. `J-技能表.xlsx` 的目标查询、现有 Area/Projectile/Buff/Effect 表和既有 Trigger 动词。
3. `[PlanActionModule]`、`[MobaTargetFilter]`、`[MobaTargetOrder]` 生成清单和注册测试。
4. 六英雄验收中的真实行为断言和表现资源绑定。
5. `Docs/design/09-ImplementationExamples/MOBA/21-SkillCapabilityGapMatrix.md` 对应能力行。

## 需要向用户确认的决策格式

```text
事实：现有机制/注册点/权威源是什么。
缺口：需求在哪一处无法由现有机制表达。
候选：A 复用现有能力；B 增加项目侧扩展；C 修改框架或协议。
影响：配置源、运行时、回滚、表现、验收和发布门禁。
问题：请确认采用哪个候选，或确认暂不支持该机制。
```

不要把“我暂时没搜到实现”直接判断为缺少机制；先检查下层注册表、消费者和测试。
