# MOBA 机制准入与实现约束

2026-11-27 用户回复「确认这套规则实现」，批准通用所有者跟随限速：超出停止距离较远时正常追赶，接近停止距离时根据单帧可关闭的距离和所有者已结算的水平远离速度限速，所有者停下且进入停止范围后保持闲置。直线和导航来源采用同一规则；不改敌方追击距离、攻击优先级、移动/转向门禁或 HFSM，不增加跟随历史和网络字段。

2026-11-26 用户确认复活边界方案：生命恢复提交后，现有角色 HFSM 在当前帧以存活事实重评估，执行原有死亡态退出动作；退出动作拥有的 Buff 及其 Cue 结束后，再写复活位置并发布复活事件。全局系统顺序、Buff/Cue 所有权、回滚字段和状态同步协议不变；未为英雄或资源增加特殊分支。验收分别检查帧同步战斗中的复活事件、状态同步权威世界的角色投影及 Cue 基线、Unity 中旧光环不随复活而活体光环照常跟随。

2026-11-19 用户回复「按推荐的下一步继续」，确认[预测回滚 T1](../plans/MobaPredictionCoreStateExecutionProposal.2026-11-19.md#5-t1-已验证的调度器边界)。调度器接管值进度捕获、只读预检和静默恢复；MOBA 10019/v1 保留等待项投影与确认帧清理。仅支持同一有效历史的存留定义，不凭复用 ID 证明代际，不新增定义重建或网络恢复资格。Triggering 31/31、MOBA 定向 53/53；未改 Continuous、Core 或发布物。

2026-11-19 用户回复「确认上述范围，可以开始改」，批准[预测回滚状态拆分 P1](../plans/MobaPredictionCoreStateExecutionProposal.2026-11-19.md#3-推荐第一批p1pipeline-内置进度)。已将管线通用进度的捕获、预检、静默恢复移回 Pipeline；MOBA 保留自定义状态和绑定，载荷、重建资格及正式结构阶段不变。包级 20/20、MOBA 相关回归 286/286（含正式 Battle Registry 下在途施法的客户端纠偏与逐帧重放）；未做 Unity/Player 验收。仅形成 Pipeline 试点规范，未批准 T1/C1 或向 Core 下沉公共契约。

2026-11-19 D2 源码核实与具体方案已整理为[Motion 目标描述与标准绑定提案](../plans/MobaMotionTargetBindingDecision.2026-11-19.md)，**尚未批准实施**。10047/v1 已充分描述标准绑定，但预检的 DI TryResolve 可构造实例，空 Pipeline 也受原 Locomotion 槽位锚点约束。建议先确认 D2-A：可选非构造 DI 查询，以及原 Pipeline 上双 Solver 标准模式与 Policy 恢复；保留 Initialized/Actor/来源同实例范围、10047 普通阶段和两入口既有失败边界。新 Pipeline 构造与四项结构顺序归 D2-B，与 D3 重绑共同细化；宿主检查点的单项准备不等于 Registry 全快照预检，不能据此宣称 Motion 重建或世界事务已完成。

2026-11-19 用户以“按推荐的下一批继续”批准[阶段顺序提案第 4 节 D1](../plans/MobaMotionRebuildStageOrderDecision.2026-11-19.md)。已实施 Registry 可选完整结构顺序，预检与实际导入共享本次不可变有效计划；MOBA Battle 要求 Actor 名单、Command、生命周期载荷各一份，显式顺序为 10000 → 10009 → 10011。未配置的通用 Registry 保留输入次序和部分快照；10047 仍为普通 Provider，Key、载荷版本、checkpoint schema 和现有重建资格不变。详见`local/docs/Moba显式结构恢复顺序验收.2026-11-19.md`。D2 标准 Motion 结构绑定、D3 来源重绑和 D4 Actor 聚合资格未随本批批准，不将顺序能力表述为 Motion 重建已完成。

2026-11-19 用户以“按文档中的进行实现”确认 [Motion 依赖提案第 4.1 节 D3-B1](../plans/MobaMotionDependencyStateSplitDecision.2026-11-19.md)。本轮将默认 Solver 缓存移至正常业务世界服务，MotionInit 保持原 PreExecute 调度；独立外部 Provider 10047/v1 保存两个 Solver 槽位的标准模式及独占 Pipeline.Policy 完整值，Battle Registry 强制新载荷。ActorMotion 10001/v5 和来源生命周期 10011/v9 的职责不变。实施核实：原 Command 可重建 Projectile/Launcher 的正式骨架均无 Motion，不能借本轮扩展 Motion/Pipeline 重建；自定义依赖的任意闭包/Events 状态不能凭同引用认定完整恢复，未知组合在显式回滚入口拒绝，普通业务仍能正常使用。原提案中自定义依赖参与回滚和 Motion 重建的验收未完成，不能把标准子集通过表述为整批全部完成；失败补偿只覆盖本 Provider，非协调器跨 Provider 世界事务。详见`local/docs/MobaMotion标准运行依赖与Policy恢复验收.2026-11-19.md`。D3-C、Path D2 和新的自定义依赖合同不随此批准。

2026-11-19 用户确认按推荐顺序推进状态与逻辑职责统一。首批已建立[业务状态与执行职责规范](design/10-EngineeringQuality/12-BusinessStateAndExecutionBoundaries.md)，核对 PathFollower、Policy 和 FixedDelta 的完整/局部状态与工具支持差异；本批仅落地规范与查证，不新增运行时接口、标记、全局管理器或 Analyzer。普通项目保留写法自由，回滚适配仍由外部承担。D3-A 及后续依赖接线继续按各自明确方案确认，不能将本次规范推进解释为任意接口或恢复范围获批。

2026-11-19 Path D1 后依赖核实已完成，独立修复 GridPathfinder 查询序号复用前未清历史标记的确定性缺陷；不保存搜索工作区、不改恢复载荷，Path 世界恢复拒绝继续保留。导航核心 13/13、MOBA 定向 52/52、既有 Motion 合同 20/20、precheck 通过，见`local/docs/MobaMotion导航依赖核实与寻路工作区修复.2026-11-19.md`。随后经用户确认实施 D3-A：Policy 提供完整纯值捕获、只读验证及静默完整替换，Motion 72/72 通过，见`local/docs/MobaMotionPipelinePolicy纯值状态验收.2026-11-19.md`。Solver/Events/导航世界接线和 Path D2 未获批，旧依赖提案的版本和原引用假设不直接用于实施。

2026-11-19 用户回复“确认，可以按方案的进行实现”，批准 `plans/MobaPathRuntimeStateAndRosterDecision.2026-11-19.md` D1。已实施 Path 逐绑定充分值状态、Actor 引用/实体版本/原 Pipeline 核验和同来源修订期间的静默恢复；正常帧仍由原 System 驱动，不捕获局部凭证。来源归池后即使复用同一个对象，旧凭证也拒绝；恢复不查询导航、属性或门禁，不补通知、不修改来源名单和次序。验收见`local/docs/MobaPath服务充分状态与同代绑定验收.2026-11-19.md`。未新增 Provider 或升级载荷，Path 世界恢复拒绝门禁继续保留；D2 来源/名单重建及 D3 导航与通用 Motion 依赖仍未批准。

2026-11-19 用户回复“按文档的开始实现”，批准 `plans/MobaPathFollowerStateExecutionDecision.2026-11-19.md` D1，现已完成共享 PathFollower 状态拆分：完整路径及推进参数归 `PathFollowerState`，包内静态 `PathFollowerStepper` 只操作显式数据；Source 提供独立捕获、只读验证及静默恢复，保留旧局部快照与正常移动语义。Motion 59/59、受影响 MOBA 22/22 通过，见`local/docs/MobaPathFollower状态与无状态推进验收.2026-11-19.md`。核心包未引入回滚接口或帧历史；本批不授予 MOBA Path 世界级恢复资格，身份、结构及导航依赖另行确定，未知来源捕获/恢复拒绝门禁保留。Unity/Player 未验收。

2026-11-18 用户在首批来源名单方案后回复“按推荐的下一批继续”，批准 `plans/MobaLocomotionSourceRosterDecision.2026-11-18.md`：同代存活 Actor、既有初始化 Pipeline 上普通移动逻辑槽位的双向存在性和值恢复；完整槽位名单归联合生命周期 10011/v8，宿主 checkpoint schema=5，拒绝旧版。仅 Locomotion 槽位允许纯骨架重建，失败补偿保留并还原原引用和值及完整次序。Actor、Pipeline、受管 FixedDelta 的既有存活门禁不变，不放开 Path/Trajectory/CollisionDash，不补发历史事件；标准结构阶段顺序须在导入前核验。

2026-11-18 后续 FixedDelta 聚合恢复已完成只读核实，提案见 `plans/MobaFixedDeltaAggregateRecoveryDecision.2026-11-18.md`。现状仅支持存活同一 Motion Continuous/source/Pipeline 原位恢复，缺少 FixedDelta 总 DurationRaw，不能独立重建；连续体配置键不含 ModifierSourceId，且 `10031` allocator capture 不枚举无 Modifier 的 Motion 身份。原提案曾将 Modifier、Tag、interval、hit/landing 回调列为一律拒绝项；用户指出这会丢失 Continuous 的核心组合能力。该白名单确认请求已撤回，草案已改为先逐项核实能力状态、唯一恢复者及静默重绑边界，再提出具体首批范围。**修订方案及实际 payload 版本待完成并经用户确认；此前草案范围不得实现。**

2026-11-18 用户回复“可以按上述的进行实施”，批准 `plans/MobaRollbackStateBoundaryRefactorDecision.2026-11-18.md` Brain D1+D2：完整值状态、只读验证、静默恢复、同存活实例代数和新载荷拒旧；不扩大来源或已销毁行为重建。用户同时澄清回滚准入仅适用于需要回滚的业务及依赖链，普通项目写法不受此项限制；接入回滚时诊断具体不支持项并要求改造，启用前不得静默部分恢复。MOBA 帧同步使用链继续要求提供完整支持。

该批已实现 Brain 10006/v3、checkpoint schema=4 和正式 System 重演验收；结果及同代存活实例限制见`local/docs/MobaBrain完整状态与可选回滚验收.2026-11-18.md`。资源/组件载荷版本不变，不代表 Motion、全部对象重建或跨进程恢复已完善。

2026-11-18 用户回复“按推荐的先收紧规范，对于上述造成额外成本来源的方式看怎么按新规范重新实现”，确认收紧回滚接入代码准入。正式要求集中于[业务状态回滚契约第 2.1、5 节](../.claude/skills/framesync-prediction-rollback/business-state-rollback.md)：充分状态与运行依赖分离、状态账本、正常业务与恢复入口隔离、引用失效及失败不得静默遗漏。普通项目保留写法自由，继续允许非 ECS 业务和可选回滚，保留 System / 组件或 Provider / Command 分工。该次确认允许规范落地与逐域设计；Brain 后续获批范围见上一条，不自动批准任意新接口、所有权迁移或对象重建。

2026-11-18 用户异步回复“确认静默属性求值方案（推荐）”，批准 `plans/MobaResourceSilentAttributeProjectionDecision.2026-11-18.md` D2.1：属性上下文提供同步、同上下文可嵌套的静默实时求值，抑制 Instance/Group/Context 通知，保留公式、Modifier、约束、缓存和依赖标脏；异常退出恢复模式，不补历史通知。资源组件恢复使用显式静默投影入口，正常刷新不变。不将静默范围扩大到普通 Modifier 投影或跨上下文，不新增属性全量 Provider。

2026-11-18 用户在资源批建议后回复“按推荐的下一批继续”，确认 `plans/MobaManagedComponentRollbackDecision.2026-11-18.md` 的 D1+D2：生成器支持显式托管纯数据有限形状，ResourceContainer 字段与存在性归生成组件快照唯一恢复；HP Provider 仅恢复属性 HP BaseValue。静默刷新资源属性投影，不补发事件；拒绝旧载荷并清理资源单项 Provider。Brain D3、冷却和 Motion 结构重建不在本次范围。

2026-11-18 用户对明确提出的 D1+D2 回复“按推荐的下一批继续”，确认 `plans/MobaLocomotionManagedMotionRecoveryDecision.2026-11-18.md`。普通移动来源由世界内业务服务唯一持有，原 System 保持原帧序驱动；联合生命周期 v7 使用 ActorId、实体版本、来源种类和领域 ID，增加 locomotion-local 值数据，静默恢复同实例 Locomotion 与受管 FixedDelta 的完整来源次序及缓存关联，并纳入局部失败补偿。拒绝旧版、未知来源、缺失实例及不同归属；不包含 Path、CollisionDash、来源重建、资源 codec 或 Brain。

2026-11-18 用户异步决策回复“批准 D1 + D2（推荐）”，确认 `plans/MobaManagedMotionStateAndOrderDecision.2026-11-18.md`：原暂存布局由 ActorTransform v4 保存 Motion 碰撞资格；后续 A+B 所有权迁移将其收窄为 key 10001 的 `MobaActorMotionRollbackProvider` v5，旧版拒绝。联合生命周期 v6 按 ModifierSourceId 保存并静默恢复已有 Pipeline 的来源次序。首批仅支持来源全部由同实例受管 FixedDelta 唯一寻址的受影响 Pipeline，混用未支持来源在预检拒绝；不改变正常仲裁、不补发事件、不重建来源。Pipeline 顺序纳入局部失败补偿。

2026-11-17 用户在 AreaEnter 调查及方案后回复“按推荐的下一批继续排查和推进有问题的回顾逻辑”，确认 `plans/MobaAreaFrameBoundaryDecision.2026-11-17.md` D1–D3。MOBA 生成系统和显式外部 ProjectileTick 候选先按 Phase/Order/TypeName 统一排序，再建立唯一阶段树；移除尾部重复 ProjectileSync/ActorCleanup。物理每帧一次，结果由后续消费者同帧处理；物理之后创建的区域首次检测在下一次正常物理 Tick。保留 schedules→AreaWorld→ProjectileWorld 及移动前碰撞同步，不新增事件历史快照、回调日志或补跑物理。历史证据见 `local/docs/MobaAreaEnter帧边界排查.2026-11-17.md`，当前装配合同见运行时 `Runtime/Docs/SystemOrderGuide.md`。派生链残留、schedule 恢复、碰撞采样变更和输出合同仍需独立方案确认。

2026-11-17 后续查证：真实 Gragas R 到达后第 1 帧检查点的差异来自 ProjectileService 清空检查点未消费的 AreaEnter，并非 Cast/retain 字段漂移。`plans/MobaPendingCombatEventRollbackDecision.2026-11-17.md` 的十队列快照方案现暂缓，不再作为默认推荐。用户随后明确要求按“数据恢复 + 对应 Command 按帧回退/重演”清理多余事件/回调实现，执行边界见下文“回滚数据、命令与回调边界”。已移除无业务生产/消费方的被动事件日志 10020；本次清理不调整系统顺序、输出提交合同，也不实施十队列恢复。分析和本批记录见 `local/docs/Moba回滚状态与事件必要性复核.2026-11-17.md`、`local/docs/Moba回滚冗余事件清理验收.2026-11-17.md`。

2026-11-17 用户针对上一轮 D1–D4 采用问题回复“按推荐的下一批继续”，确认 `plans/MobaManagedMotionRecoveryDecision.2026-11-17.md`：受管 FixedDelta 精确 raw 时间和完整来源值归联合生命周期 continuous-local，envelope v4 拒绝 v3。只静默恢复同一存活 Continuous 的原来源，检查类型、冻结配置和 Pipeline 归属；缺失／不支持来源预检零导入拒绝，运动状态纳入组内失败补偿。不授权其它来源或已销毁对象重建。

2026-11-17 用户确认 `plans/MobaCastRetentionRecoveryDecision.2026-11-17.md` 的 D1–D3：Cast 投影最后观察帧作为可恢复状态，保留期到期请求及确认门限固定；Entitas component rollback payload 升级 v2，拒绝并清理该链路 v1/旧布局兼容。仍不授权已确认销毁 Cast 的重建或跨名单恢复。

维迦 W 的历史已确认范围见 `plans/VeigarSkillAudit.2026-11-12.md`：有效成长层数每50层线性减少10%冷却，最低0.1倍，提交时采样并冻结，运行冷却不追溯。当前通用接线和验证记录在 `plans/VeigarFidelityRepair.2026-11-15.md`，合同见 `Docs/MobaSkillPipeline.md` 的 Commit-sampled cooldown factors；不授权指数递减。2026-11-15 用户“按推荐的下一批继续”明确批准该文 D1–D4：R 缺血倍率、P/Q 奖励资格、原 Cast 等级冻结的 Buff 时长，以及显式地面选点裁剪。仅实施文中最小范围，不含助攻账本或大型单位新分类。

2026-11-15 用户确认 `plans/JhinNextMechanismDecision.2026-11-15.md` 的 D3、D4：分类命中后继续/停止响应，以及输入方向受既有模式扇区约束。首批接烬 W/R 和两种 R 射击；原有碰撞响应、瞄准策略与快照兼容，禁止英雄执行特例。

2026-11-23 用户明确确认 [烬 E 实例退场方案](../plans/JhinSkillImplementationOrder.md#e-原展示对照与已确认的实例退场方案)：`spawn_area.replace_event_area` 仅适用于 `area.enter` / `area.cross` 的单区域生成，仅可退场事件指向的存活、同施法者实例，并在新区域创建成功后执行。首个消费者仅 `30010311`；其他 AOE、认领规则、同步协议和恢复语义不变。

2026-11-14 已确认的最小补充见 `plans/MobaSevenHeroNextMechanismDecision.2026-11-14.md`：D1 保留快照诊断编号槽但新导出写零、恢复不还原诊断关联；D2 显式 Timeline 正常后摇入口，合法输入正常交接，原 Cast 的已提交子对象自然存续。两项均不得扩展为英雄特例或自动裁剪动作。

同日用户确认 `plans/MobaAreaGroupQualificationDecision.2026-11-14.md`：Area 组认领增加显式 UnitSubType 掩码，0 兼容旧仅 Hero；随原 Area 冻结和恢复，复用原 Cast/Group/Target 一次资格。首个配置仅卡尔萨斯 W 的 Hero/Minion/Neutral/Boss，不能扩大到其它未确认机制。

本规范适用于英雄、技能、被动及相关共享运行时、编辑器、配置和发布链。它落实仓库所有者的要求：**禁止英雄专属执行特例；规范未覆盖或未确定的机制，先提交方案，获得用户明确确认后再推进。**

本文件定义机制准入规则；字段、生产源、发布命令和验收方法仍以对应技能规范及当前权威配置为准。既有代码、旧文档或能力矩阵中的“可扩展”描述不能替代用户对新机制方案的确认。

## 规范内实现与待确认机制

| 情况 | 处理方式 |
|---|---|
| 用已定义、已接通的 Excel、HFSM、Flow、Trigger、Buff、Modifier、查询和表现能力组合新内容 | 当前任务授权内可实现；记录来源、消费者和验收，不为每个配置项请求确认 |
| 修复与明确既定契约不一致的实现、错误引用、生命周期清理或发布漂移 | 当前任务授权内可修复；证明预期契约，验证受影响范围；需要改变契约时转入待确认 |
| 新增动词、事件或 Payload 语义、参数合成规则、槽位/瞄准/计数投影策略、组件、服务、状态维度、协议或快照字段 | 先提方案，用户确认后实现；即使已有注册点或只增加一个类，也属于机制扩展 |
| 扩展既有机制的职责、生命周期、时序、所有权、确定性或回滚/重连语义 | 先提方案；“修 bug”“补通用能力”不自动授予改变语义的权限 |
| 规范没有明确落点，或者规范、需求、代码、权威配置相互冲突 | 查证并列出冲突，等待用户决定；不得自行选择有利于继续实现的一方 |
| 仅找到某英雄的特例代码，无法找到通用契约与消费者 | 标记为待治理实现，提交通用替代方案；不得复制、扩展或据此认定已有通用能力 |

现有机制是否覆盖，应同时看规范、生产源、真实消费者和行为证据；能力矩阵或函数名称只能作为检索入口。没有搜到实现也不能直接认定需要新机制。

## 禁止的实现方式

共享执行链不得根据英雄 ID/名称、某个技能/被动/Buff/投射物/召唤物 ID、专属 profile 或某个英雄的固定槽位，决定额外的玩法行为。该边界同样适用于 runtime、view、host、editor、发布器、生成器和装配层，不能通过换文件、包装类、生成代码或委托搬移规避。

以下方式均不能算作合规扩展：

- 在 Cast 中识别“烬 R”后冻结瞄准，或在通用 Counter 中识别某英雄后改写技能槽、攻击类别或 HFSM 参数。
- 为鲁班七号或其他英雄另写绕过正式 Flow/Trigger/Projectile 链的扫射执行分支。先确认当前问题实际落点，不能仅凭英雄名推断所有相关代码都违规。
- 把相同分支改成 `HeroKind`、`IsXxxHero`、某个专属 profile/类，或把常量移入配置后仍以“英雄身份”选择硬编码算法。
- 在表现、动画完成、编辑器工具或发布同步脚本中暗中补发伤害、计数、技能替换或状态转移。
- 为了通关测试直接改生成物、放宽 scope/生命周期校验、删除有效行为断言，或先写临时英雄补丁再承诺后续抽象。

英雄 ID、技能 ID 并非禁止出现在所有文件中。以下用途允许，但不得夹带特殊执行语义：

| 用途 | 边界 |
|---|---|
| 内容与资源映射 | 英雄绑定技能/状态图，技能引用 Flow/Trigger，资源引用和查询条件使用具体 ID，由通用消费者执行 |
| Hero 专属 HFSM 配置 | 不同状态拓扑、阈值、计数和参数值由已定义机制表达，不增加识别该英雄的运行时捷径 |
| 验收、样例装配、调试显示、数据迁移 | 可定位具体英雄与资源；不能成为正式战斗行为的补偿入口 |
| 已定义的策略选择 | 内容选择具有独立领域语义的通用策略；策略输入、输出和生命周期不依赖英雄身份 |

通用能力允许先有一个使用者，但必须能用领域输入解释行为，而非把英雄代码改成通用名称。不能为“看起来可复用”预造万能服务，也不要求先制造第二个英雄。新增这种能力仍须先确认方案。

长期模式、一次施法和事件效果的分工按技能规范执行。参数权威源必须明确区分角色本地状态与外部 Modifier 贡献；如果现有规范对两者的职责有冲突，先提交冲突和建议，不能自动强制改成 Buff 或直接切状态。

## 实现前的机制映射

新增/重做英雄时，在英雄接入登记中为每个机制记录下表；局部修复只需覆盖受影响机制，不重做无关英雄设计：

| 项目 | 必须回答 |
|---|---|
| 需求与落点 | 输入是什么，属于长期 HFSM、单次 Pipeline 还是事件效果，唯一提交点在哪里 |
| 现成能力证据 | 对应规范条款、权威配置、消费者、已有测试分别在哪 |
| 状态与所有权 | 谁持有参数/计数/瞄准快照/槽位选择；来源键、默认值和撤销时机是什么 |
| 生命周期与恢复 | 重复输入、空放/未命中、取消、打断、死亡、替换、结束、回滚和重连如何处理 |
| 扩展判定 | `规范内复用`、`待确认机制`、`已确认机制`或`既有违规待治理`；待确认部分不得进入实现 |
| 验收 | 正常、反向和边界场景；配置发布、真实结果及适用的表现/恢复证据 |

## 用户确认流程

先完成授权范围内的只读调查和可审阅方案，再请求决策。出现多个独立缺口时一次列清，避免边写边逐个扩大范围。

```text
机制：需求及触发场景。
当前事实：已查的规范、权威源、消费者和测试（附路径）。
缺口/冲突：现有机制在哪一点不能表达；为何不能仅改配置。
建议方案：通用语义、落点、接口/配置和数据流；可行替代与取舍。
影响范围：修改文件/层、兼容性、生命周期、回滚/重连、发布链。
验收计划：真实结果、边界和复用证明。
待确认：请求采用的具体方案和范围。
状态：待用户确认；不实施依赖此决策的修改。
```

- 等待期间可查代码、整理设计、列测试场景，继续不依赖该决策的规范内工作；不得先写依赖该方案的实现、生产配置、生成器或发布物，也不预先接入隐藏/关闭的特例。
- **用户未答复、超时、测试变绿或 Agent 自己的“推荐方案”都不是确认。** 不得以默认选项继续。
- “完成这个英雄”“继续修复”“按规范做”是任务授权，不能替代对已提出的新机制方案的明确确认；用户在当前或此前会话明确批准的相同方案与范围，无需再次确认。
- 确认后，在对应英雄设计/机制设计中记录所批准的范围和用户确认依据，先更新机制契约，再实现并验收；不得把 Agent 建议记成用户决定。仓库文档不能自行创设批准。
- 执行中如果语义、所有权或范围再次变化，对新增决策重新提案；此前确认保持在原范围内有效。
- 不将英雄特例作为推荐候选。若用户明确要求规范例外，须针对具体例外说明冲突、范围和退出安排；该次例外不自动扩展为通用规则。

## 交付检查与既有问题

评审受影响的共享代码及调用链，逐项说明行为由哪个通用机制承载，具体 ID 是否只做数据绑定，有无按英雄身份隐式分支。检查数字常量、常量符号、profile、类型判断和配置驱动分派，不能只搜索英雄名字。

新增策略要证明可用不同配置/身份执行相同语义，并验证来源撤销、错误补偿和适用的回滚恢复。优先扩充已有高价值行为场景，避免只测新类型或仅靠 trace/静态文本扫描。测试通过不等于架构合规；静态搜索没有命中也不构成合规证明。

验收报告同时给出机制映射、确认记录（如需要）、权威源/发布证据、测试结果及未覆盖项。仍有未确认机制、隐藏英雄补丁或契约冲突时，不能宣布该部分完成。

既有烬等英雄在共享 Cast/Counter/HFSM/AI 中的专属处理属于待审计、待迁移事项；旧 skill 中“runtime 零英雄 ID”不能当作当前事实。仅依据当前代码确认具体问题，不在本次规范修改中自动重构或认定所有英雄内容都违规。后续触及相关行为时，先登记问题并提出通用迁移方案，由用户确认后实施；保留原行为回归，不能用扩大旧特例解决新问题。


## 回滚数据、命令与回调边界（2026-11-17）

本节记录用户明确的实施方向：回滚恢复数据；需要逆转的命令走对应 Command 按帧回退；随后按帧处理已记录输入。清理已经确认多余的事件/回调实现，不另建一套闭包撤销或历史战斗事件回放系统。

后续用户已确认的统一契约整理于 [帧同步回滚 skill](../.claude/skills/framesync-prediction-rollback/SKILL.md) 与 [业务状态回滚契约](../.claude/skills/framesync-prediction-rollback/business-state-rollback.md)，后续接入和排查按此执行：

- **回滚是可选扩展。** 业务可在关闭回滚时正常运行，不强制回滚专用依赖或逐帧历史记录；MOBA 帧同步所用模拟机制当前均须提供恢复支持，开启后必须完整注册、捕获和预检，不能静默遗漏。能力已提供与会话已启用分别判断；正常业务/hash/同步另有用途的数据不因关闭回滚被删除。
- **准入仅约束回滚接入。** 普通业务可保留合法对象模型；显式接入回滚时定位不支持字段、依赖及副作用，要求补齐或拒绝该组合，不能通过静默跳过启用部分恢复。运行时诊断和有限静态护栏不构成任意插件正确性的证明。
- **System 统一驱动，状态保持原归属。** Entitas WorldSystem 按固定 Phase/Order 和世界帧时间调用业务推进；允许 Service/Runtime 内部确定性 TickSchedules 等算法，不允许独立时钟推进模拟。ECS 数据走组件序列化；非 ECS 的任务、游标、冻结参数等由对应 Provider 导出/导入。无需为回滚强制将 Launcher、投射物、移动或其它服务状态迁入 ECS，也不默认新增调度器。
- **结构与字段分工。** ECS 和非 ECS 对象都须明确名单/结构恢复负责人；不能用字段恢复掩盖 N 后多出的对象。既有类型化 Command 与合法名单恢复按各自契约处理，避免重复创建历史。静默重绑只恢复运行时依赖，不重跑玩法；同会话恢复不自动扩大为跨名单重建或重连基线。

2026-11-18 用户进一步确认纯 ECS 权威数据应优先统一走生成式组件恢复。当前 `Transform`、`MoveInput` 已回归组件生成序列化；托管/运行时引用组件继续由 Provider 或生命周期恢复。`[EntitasBlob]` 只标识托管风险并触发 analyzer 护栏，不隐式注册 Provider，也不证明字段覆盖；pin coverage、注册表和实际往返测试共同确定恢复归属。组件载荷 v3、Motion 状态载荷 v5、检查点 schema v2 拒绝旧版布局，详见 `plans/MobaEcsRollbackOwnershipMigrationDecision.2026-11-18.md` 与 `local/docs/Moba纯ECS数据回滚能力与职责审计.2026-11-18.md`。

上述原则已获确认；沿既定归属补齐遗漏状态不因旧方案标记待确认而重复提问。实际新增/改变机制、协议或重建范围仍遵循本页机制准入要求。本次 skill 整理不表示现存恢复缺口已经完成实现。

随后按该契约补齐物理 Provider 12001 的只读预检：先核对 Projectile/Area 包络、帧号、身份、allocator、区域组及必要依赖，再导入。直接物理导入在清理当前对象前解析/校验，Projectile 策略和过滤器先准备后写入索引；预检不执行扩展工厂或解析器。该历史批次不增加快照字段或玩法语义，不承诺跨 Provider 事务，也未包含活动发射 schedule 恢复。范围和证据见 `local/docs/Moba投射物物理恢复预检验收.2026-11-17.md`；schedule 的后续实施见下节。

### 活动 Projectile 发射任务状态恢复（2026-11-18）

沿用户已确认的 System / Provider / Command 分工补齐服务未来任务状态，方案见 `plans/MobaProjectileScheduleRollbackDecision.2026-11-17.md`。保留 System → ProjectileService.Tick 及服务所有权；服务 v2 包络恢复任务名单、原处理顺序、游标、精确 BaseSpawn、策略描述、共享过滤器和分配器，只接受 v2。四种既有冻结模式和 MOBA 动态 Modifier 模式可恢复，动态模式重绑原配置、Modifier 与唯一世界 RNG，不在 Capture/Import 取样，不回放历史通知。普通发射不以回滚描述/解析器为前提；开启回滚时不支持的活动策略明确拒绝。

服务层跨连续发射重演、正式世界同名单期间全 Provider 重演已通过，原批次范围及 97 项 Projectile / 1634 项 MOBA 验收见 `local/docs/Moba活动投射物发射任务恢复验收.2026-11-18.md`。随后用户确认 `plans/MobaLifecycleCommandPreflightDecision.2026-11-18.md` D1–D4，按下节接通有限的新增弹体名单差异；不能把原批次门禁结论当作后续未实施，也不应表述为所有发射生命周期已完成。

### 联合生命周期预检与 Actor Command 协作（2026-11-18）

完整快照内，10011 核对自身与 Entitas 目标名单和实体版本。目标 Actor 全部仍存活时，额外受追踪飞行弹体只有在既有 Command checkpoint、保留日志与完整 handler 预检可只读证明最终名单精确相等后才接受；物理对象、来源及有效 SkillRuntime retain 也须完整。投影只操作隔离值数据，不执行工厂、删除或历史通知。

Owner Blackboard 只在该证明内调整纯 Actor 所有者校验，非 Actor 来源仍保持严格同名单。战斗活动时间 10045 也校验同份生命周期目标名单。10011、10045 的协调器预检统一通过已有完整快照接口完成；公开单份 ValidateImport 及实际 Import 保持严格，不缓存跨调用放行状态。没有联合生命周期所有者的 10045 独立预检仍保持原地合同。

实际结构执行顺序不变：10000 先静默移除目标名单外 Actor，10011 再严格重验并 Commit，900001 消费既有逆命令；原 handler 对已被名单移除的对象沿既有幂等路径处理。不新建第二套执行器，不修改共享协调器、载荷版本或 Command 协议。缺失目标 Actor、已销毁 Cast／Continuous、Launcher 清理后的反向重建，以及额外召唤物／英雄仍不因本批获得新资格。正式连续发射两次重演及零导入拒绝的验收见 `local/docs/Moba生命周期预检与Actor命令协作验收.2026-11-18.md`。

### 发射结束与来源释放边界验收（2026-11-18）

正式 Console 验收进一步确认：最后一次发射前后，目标对象仍存活时可完整恢复并逐帧重演；schedule 已结束但 Launcher 等待弹体的检查点不会恢复出额外发射。自然退出及实际单位命中均在正式 Exit 消费时保留来源、帧末释放 link／retain；完全清理稳定后可恢复重演，不重复命中或退出。

较早弹体已销毁而 Launcher／来源仍存活时，仍因目标 Actor 缺失零导入拒绝。最后弹体结束当帧 Launcher Actor 清理，而对应 Launcher Continuous 到下一帧推进时移除；跨这两种销毁边界的恢复分别按既定 Actor／Continuous 门禁拒绝，不表示已支持对象重建。真实酒桶 R 到达跨清理拒绝同时核对零导入、当前完整状态及 journal 不变。证据见 `local/docs/Moba发射结束与来源释放边界验收.2026-11-18.md`。

随后用户已批准并完成 `plans/MobaDestroyedProjectileRecoveryDecision.2026-11-18.md` D1–D4：MOBA 唯一 Actor Command Provider 改用 10009，恢复顺序为 10000 → 10009 → 10011 → 字段 Provider；匹配 tombstone 与跨 Provider 证明的缺失飞行弹体获得同会话有限重建资格，旧 900001 MOBA 检查点拒绝。该批 87/87 组合测试通过，Launcher 不在当批批准范围。

### 已清理 Launcher 有限恢复实施状态（2026-11-18）

用户随后确认 `plans/MobaDestroyedLauncherRecoveryDecision.2026-11-18.md` D5–D7。实现扩展同会话且 journal 有效的目标 Launcher 骨架资格，仍按 10009 结构恢复、10011 生命周期恢复及字段 Provider 分工；完整预检闭合目标版本、组件、物理弹数、link/source、retain、父 runtime、执行节点和调度关系。内置 Repeat 发射 Continuous 保存原请求、结果及 Modifier 来源等纯数据，静默重建，不调用启动入口、扩展工厂或历史玩法通知。联合生命周期格式现为 v5，只接受 v5；Command 协议、正常清理帧及 System 驱动保持原合同。

D5–D7 首次严格验收为全量 MOBA 1683/1688 通过，5 项在全 Provider、实际弹体及通知比较后，因同帧销毁逆命令排列不同而失败，根因为 Actor 清理依赖 Entitas Group 的对象枚举顺序。用户随后明确回复“按建议的方案进行下一批推进”，确认并实施 `plans/MobaActorCleanupOrderDecision.2026-11-18.md` D8：普通运行与重演均对每轮已到确认门限的 Actor 清理候选按稳定 ActorId 升序消费；保留确认条件、正常清理帧及最多 8 轮派生，回调新增候选进入后续轮，无效身份排在有效身份后并保持原相对顺序，不原地排序 Entitas 缓存数组。消费前复核身份/请求有效性，创建序号只检测池复用，不参与业务排序或快照。未扩大其它系统排序、通知顺序或 Command 协议。

D8 复验已完成：保留逐条 journal 的 Frame、Order、类型、版本和载荷严格比较，原 5 项场景均通过两次恢复与正式 System 重演；新增边界 11/11，MOBA 全量 1699/1699，Projectile 97/97。D5–D7 的 Launcher 有限恢复至此完成本范围严格验收；旧失败证据保留，当前结果见 `local/docs/MobaActor清理确定顺序验收.2026-11-18.md`。未运行 Unity/Player，不将本结果扩展为任意复杂生命周期恢复均已支持。

缺失 Cast、任意其它 Continuous、额外未知 Actor 及跨会话恢复仍无重建资格。Launcher 清理当帧的 orphan Continuous 在之后已移除时仍拒绝；酒桶 R 跨到达产生额外 Motion Continuous 仍因不在现有名单恢复所有权内而零导入拒绝。25 项非法证据、关闭回滚正常清理及 Projectile 框架 97/97 的实际结果见 `local/docs/Moba已清理Launcher有限恢复验收.2026-11-18.md`。

- **帧状态恢复。** 检查点表示第 N 帧结算后的充分状态，重演从 N+1 开始。保存未来推进必需的阶段游标、计时、延迟任务、资格集合、随机状态和分配器；已完成 Hit/Enter/End 的效果由状态承载，不再保存事件副本来补发玩法。
- **命令回退。** 结构变化沿 `CommandRollbackLog` 的类型 ID、版本化数据和确定序号交给对应 handler 逆序回退；恢复前预检所需 handler 与数据，随后由字段 provider 恢复状态。已有 Actor 创建/销毁命令保留，不重复登记事件或 `Action` 作为第二条撤销链。该日志用于保留窗口内同会话回退，不替代重连基线。
- **静默恢复与重绑。** 数据恢复不得通过正常 Enter/Exit、Added/Removed、伤害、触发器或准入回调重新结算业务。订阅和索引按恢复状态重建，只接收重演时的新事件；已批准的被动恢复重绑定继续生效，不重新评估 `RequireOutOfCombat`。
- **失败补偿。** 一次 Import 失败时还原操作前状态的局部备份不是按帧事件历史。现有联合生命周期补偿及测试故障注入保留；不能因为以委托表达就删去原子性保障。
- **观察与输出。** Trace 的 Reconciled 和护盾 RollbackCorrection 是诊断/可见状态对账，不是历史玩法回放。保留既有合同，不能让其成为补发玩法入口。外部输出的提交/撤销与模拟状态分开评估；不能直接删除 Pending Spawn 队列恢复而不处理接收端对账。
- **残留清理与兼容。** 无业务生产或消费方的 `PassiveSkillTriggerEventRollbackLog` 及其 DTO、注册引用已删除，不保留 10020 空 provider 或兼容回放。旧检查点若包含该 key，沿现有 ProviderMissing 预检拒绝，重建会话/检查点；不静默忽略未知 provider，不将 10020 分配给其它语义。

此方向不自动确定同帧事件结算顺序、递归触发边界、跨帧消息或外部输出提交规则。直接 AreaEnter 遗留已按本页顶部批准的统一装配修复；派生链随后由用户明确确认的 `plans/MobaDerivedCombatSettlementDecision.2026-11-17.md` 第 9 节 L1–L4 实施。整轮冻结及失败门禁的当前合同见下文；活动发射 schedule 未纳入该历史实施批次，后续按 `plans/MobaProjectileScheduleRollbackDecision.2026-11-17.md` 中修订后的 Provider 补齐方向处理。不能用批量保存全部事件或无条件清空队列代替状态完整性修复。

以下两段记录 L1–L4 实施前的调查与门禁证据，其缺口结论保留历史含义。此前按非 ECS 生命周期方向完成 Area 目标名单、来源与 retain 的恢复验收，复用已有快照，不增加创建日志。
联合生命周期 10011 的整份快照预检现核对 12001 Area 物理与元数据的完整名单及 owner，检查物理帧和身份分配器；不完整关系在零导入时拒绝。
这补齐既定完整性门禁，不改变正常 Spawn／Expire 时序，也不保证 Capture 已拒绝所有未结算帧。
派生 Expire 的孤立快照现会被拒绝，派生 Spawn 的初始化残留仍存在；证据和范围见
`local/docs/MobaArea生命周期恢复与一致性预检验收.2026-11-17.md`，正常生命周期完成边界仍另行确定。

后续本轮组合验收补齐 Spawn 恢复差异及来源释放证据：合法子 Area 恢复后，下一帧 Spawn 业务丢失，
两次重演的 10011／12001 不同；Enter 中创建后立即移除还会使 Expire 越过残留 Spawn，
次帧缺失元数据的异常实际被 WorldManager 隔离，外层仍计帧。
原“访问各类别时 Drain 并循环”的候选不足以保持初始化／终结顺序；
用户于 2026-11-17 明确确认 `plans/MobaDerivedCombatSettlementDecision.2026-11-17.md` 第 9 节 L1–L4：
整轮冻结所有类别后消费、同帧完成领域收尾、每帧 16384 项预算及正式失败门禁同批实施。
失败世界停止推进、捕获和输出；两帧间待结算通知也阻断捕获。恢复仍只恢复数据及类型化 Command，不补发历史玩法。
本轮新增 6 项验收，组合 35/35，不把复现缺口的通过结果记为生产修复；
证据见 `local/docs/Moba派生Area生命周期剩余重点复核.2026-11-17.md`。

### 已实施的派生链帧完成合同

共同 `MobaProjectileSyncSystem`（PostExecute 1910）持有 Area 消费者，独立 AreaSync 不再安装。
每帧物理一次；每轮先冻结 Projectile／Area 全部通知和 delay 候选，再按
Projectile Spawn → Arrival → Tick → Hit → Exit → Area Spawn → due delay → Enter/Cross → Exit → Stay → Expire 消费。
transform 仅首轮 Tick 后、Hit 前同步一次。回调新增工作下一轮处理，不补物理、不推进未来任务。
Area 原领域服务在最后合法消费后释放来源、索引和 retain；恢复仍静默，N+1 开始输入重演。

每帧最多 16384 通知／到期 delay 项。超限、重入、必要关系缺失及后段 cleanup 派生使会话永久失败；
`IWorldFrameGuard` 阻断失败帧提交、后续推进、回滚 Capture、复制快照和权威广播。
两帧间新增待结算通知也阻断 Capture／输出，但允许进入下一正常帧完成结算；未来 schedules 不算残留。
门禁、临时批次和候选名单不入快照。失败后重建会话；不承诺撤销已发出的全部本地表现／诊断。
实现及本轮实际验收见 `local/docs/Moba派生战斗帧内结算验收.2026-11-17.md`。

## Cast 投影保留与恢复（2026-11-17）

本节落实已确认的 `plans/MobaCastRetentionRecoveryDecision.2026-11-17.md` D1–D3。
`SkillCastRetention.LastObservedFrame` 是最后消费运行中或终态 Runner 快照的模拟帧，纳入生成的 ECS 序列化；它不是技能真实结束帧，也不是最后一个派生对象结束帧。运行投影更新锚点并清除旧 cancel/destroy request，终态快照消费后不再依赖 Runner 的已消费缓冲或系统私有缓存。

`retain>0` 时，到期帧固定为 `LastObservedFrame + retain + 1`，确认门限固定为到期帧加 `gate`。本帧没有投影且达到到期帧才首次创建请求，随后 Tick 和跳帧不得移动请求字段或原因。默认 30/10；例如锚点 100 对应请求 131、确认门限 141。`retain<=0` 维持不主动申请的语义；`gate=0` 允许在请求帧被确认后销毁。最终销毁仍由原 PostExecute 清理，不改变聚合或 Projectile/Area/Summon 的生命周期。

Cast 索引每次从当前 ECS 重建，支持恢复后的终态及池复用；旧 `_lastSeenFrame` 已删除。组件 provider 10018 只导出/接受 payload v2，必须携带元数据尾部，不提供 v1、无尾部或缺锚点的迁移/猜测路径。投射物和召唤关联预检消费强制元数据；导入前在隔离实体解析全部条目，检查身份、锚点和请求关系，直接 Import 同样先完整校验并解析所有目标后再修改实体。

恢复仍要求同名单和对象可寻址，已确认销毁的 Cast 不重建；缺失在预检阶段拒绝。只有本组件 payload 的兼容路径被移除，其它协议版本不受此批准范围影响。验收结果与测试夹具边界见 `local/docs/MobaCast保留期与回滚验收.2026-11-17.md`。

2026-11-17 的后续既定契约修复补齐 HFSM 参数 provider 10044 的整份快照预检，以及参数服务先全量校验再恢复的入口。声明验证按同份 HFSM 快照实际恢复后的 profile 执行，校验过程使用隔离副本；不改变参数合成、来源所有权、空载荷语义、协议或同名单恢复范围。详细规则见 `Docs/MobaStateMachineAuthoring.md`，证据和剩余批次见 `local/docs/MobaHFSM参数恢复预检验收.2026-11-17.md`。

2026-11-17 用户确认 `plans/MobaPassiveContinuousRollbackRebindDecision.2026-11-17.md`：回滚导入产生的 `SkillLoadout` 重绑定恢复被动 listener、owner binding 与持续计划时，不重新评估已恢复 interval 的 `RequireOutOfCombat` 准入，也不结束检查点中仍有效的 interval；正常模拟内真实配置变化继续按既有规则评估和清理。正式联合生命周期注册表中的组件 provider 10018 在实际导入带 `SkillLoadout` 的 actor 后标记一次恢复重绑定；后续真实组件替换立即作废标记，即使响应式系统尚未消费。消费、actor 卸载和 teardown 清理标记与订阅，不改变战斗外门控、冷却、网络协议或被动配置。Hero 3005 到达后第 1 帧另行观测到的 `execution-contexts` / `skill-runtime` 差异未包含在该确认范围内，仍需单独调查。验收见 `local/docs/Moba被动恢复重绑定验收.2026-11-17.md`。

## Projectile／Area／Motion 组合验收（2026-11-17）

本批恢复两项既定契约：ProjectileLauncher retain 的 Child.ConfigId 指向发射器模板，不能与所发投射物模板混同；预检同时验证目标组件的两类模板及生命周期 retain。联合生命周期 10011 已按 Continuous 所有权重建技能参数投影后，字段 provider 10015 只校验载荷，不得再次清空该投影；独立 provider 的行为保持原约定。这两项不新增协议字段或版本，不修改英雄生产配置。

真实到达结算、后续径向位移、物理穿越区域的资格优先与组认领恢复证据见 `local/docs/Moba投射物区域位移组合验收.2026-11-17.md`。用户已确认 `plans/MobaManagedMotionRecoveryDecision.2026-11-17.md` D1–D4：联合生命周期 v4 对同一存活 Motion Continuous 中的 FixedDelta 来源保存并恢复精确 raw 时间和冻结值，缺失、替换、不支持或绑定不符在预检拒绝，组内失败补偿恢复原来源状态。来源身份使用现有可回滚 ModifierSourceId 分配器。不包含其它来源类型、销毁对象重建或跨名单恢复。实施证据见 `local/docs/Moba受管位移精确恢复验收.2026-11-17.md`。被动 Continuous 在恢复 SkillLoadout 重同步时误受战斗外门控结束的问题，已按用户确认的 `plans/MobaPassiveContinuousRollbackRebindDecision.2026-11-17.md` 修复并以全量 provider 字节相等验收，报告为 `local/docs/Moba被动恢复重绑定验收.2026-11-17.md`。后续已定位 Hero 3005 到达后第 1 帧的 execution-contexts / skill-runtime 差异为装配导致的未消费 AreaEnter；统一装配后 offset 1/5 两次八帧重演全 provider 一致。offset 0 仅完成原地恢复，向后八帧再恢复仍因 Local Continuous membership 超出既有所有权范围而在零导入预检拒绝，不能计作成功重演。当前证据见 `local/docs/MobaArea帧末结算与派生边界验收.2026-11-17.md`。
