# MOBA 技能管线发布

## 源文件

- `LubanConfig/Moba/Production/Datas/J-技能表.xlsx` 的 `技能` 页负责技能元数据及流程 ID。
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset` 是 MOBA 技能流程的权威编辑资产；选择该资产，在 Inspector 编辑流程和阶段。
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases/` 保存 P1/P2 技能流程展示的源文件和资产。
- ActionEditor 的 `Resources/moba/action_timeline/skill_*.json` 负责编辑动作轨道。时间轴阶段只能引用其导出的 `*.moba.logic.json`；表现片段保留在 `*.moba.presentation.json`。
- `Production/Archive/skill_flows.xlsx` 是迁移历史，不再登记于 `__tables__.xlsx`，也不用于发布。
- 触发效果在触发器编辑模块中维护并单独发布，见[MOBA 触发器编辑](MobaTriggerAuthoring.md)。

## 提交时采样的冷却系数

`UseCommitCooldownFactor` 是流程显式启用既有已批准叠层冷却契约的选项。准备阶段解析既有冷却输入，并将基础值存入原始 Cast 黑板。顺序路径上必须恰有一个显式 `ReserveCast`（`StartSkillCooldown`、`UseResolvedSkillCooldown`、`RequireExplicitCommit`）及一个后续必需的 `CommitPoint`。不得保留会覆盖提交结果的旧 `start_cooldown` RulePlan。

首次资源提交时，在 `CastCommit/EventSample` 解析 `Skill.CooldownCommitFactor`，乘以准备好的基础值，毫秒按 AwayFromZero 舍入，并将有效总时长冻结在该 Cast 上。重复提交不产生效果，来源变化不影响已生效的冷却。提交前取消按既有显式预留契约退款，不启动冷却。既有事务及黑板快照保存准备与提交状态，无需新增快照布局。未启用此选项的流程保留原准备契约。

可选 Modifier 定义字段 `StackStepSize`、`StackStepDelta`、`StackValueMin` 将每个来源的有效叠层数映射为 `max(min, fixedBase + floor(stacks/step)*delta)`。此映射要求实时固定值的 SkillParameter 乘数；步长为零时保留既有叠层方式，并禁止非零的仅步进字段值。Buff/Continuous 仍是叠层权威。选择器绑定内容，消费方不识别英雄或技能身份。见[维迦批准范围](../plans/VeigarSkillAudit.2026-11-12.md)及[当前实现](../plans/VeigarFidelityRepair.2026-11-15.md)。

## ActionEditor 时长与动画提前退出

对于编辑的时间轴阶段，以审阅后的 ActionEditor 导出顶层完整 `length` 为准，包括延迟开始、重叠轨道及有意保留的尾段。片段从 0.4 秒开始、占用 0.8 秒，则结束于 1.2 秒；不能仅用素材长度定义阶段。独立投射物及区域的生命周期仍各自管理。

`LogicJsonExporter` 和 `ActionTimelinePartition` 在两份运行时导出中保留相同总时长。`SkillTimelinePhaseDef.MobaLogicTimeline` 必须显式引用逻辑导出，`MobaActionTimelineCompiler` 才会据此推导 `DurationMs`（毫秒按 AwayFromZero 舍入）。空引用则使用手工编辑的时间轴；文件名相同不会自动建立关联。多阶段 Cast 时长仍按阶段树计算。

动画提前退出时，先检查真实源轨道、片段结束边界、两份导出、Flow 引用，以及可能覆盖引用的导入器。空的 500 ms 时间轴不能证明覆盖了独立 HFSM 动画。模型动作元数据提供播放事实，不自动设置阶段时长。检查此链路前，不应另加完成窗口。

按仓库 skill 的[时长检查流程](../.claude/skills/moba-skill-authoring/references/action-timeline-timing.md)执行，包括延迟开始的 HUD 验证和只读审计命令。配置一致本身不能证明完整播放。

表现时间轴消费方读取导出动画的 `playbackSpeed`、`clipOffset` 和 `clipLength`。时间轴偏移以秒计时，源归一化时间为 `(clipOffset + offset * playbackSpeed) / clipLength`；省略字段时使用速度 1、偏移 0 和编辑的片段时长。HFSM 定位仅在提供实际播放意图时设置动画速度，因此只等待的状态动作不会覆盖时间轴所管理动画的速度。

## 所有者本地子通道与独立再次施放

配置的子通道属于原始 Cast 运行时，即使其短输入流程已经结束。独立再次施放绑定确切的原始运行时、代际、根上下文和子对象身份，并检查所有者、槽位及基础技能。有效绑定在释放校验中使用再次施放输入的零冷却；原技能装配冷却继续运行。尚未部署区域的飞行阶段不能选为区域再次施放目标。

所有者死亡时，`EndOwnerChildChannels` 按稳定 RuntimeId 顺序撤销通道能力：先锁定终态，再销毁精确关联的子对象引用，最后强制终止原 Cast。手工退出投射物不得执行到达或部署路径，已排队的过期或到达也不能再次认领终态。此清理适用于配置的子通道；没有通道的普通独立投射物保留既有生命周期。通道状态、终态锁存和再次施放绑定使用 Cast 黑板的既有回滚捕获。见[已批准的条件机制](../plans/GragasConditionalMechanismDecision.2026-11-12.md)和[生产验证](../plans/GragasProductionIntegration.2026-11-12.md)。

## 并行编辑与安全追加

英雄归属不等于文件归属。多个英雄共享 Luban 工作簿、`SkillFlowCO.asset`、触发器编辑模块和 Unity 发布产物。只有写入方不会用过期编辑器状态保存同一源文件时，并行工作才安全。

- 每个源文件指定一个活动写入方。共享 `.xlsx`、`SkillFlowCO.asset` 或触发器模块必须串行编辑；不同路径的英雄时间轴文件可独立编辑。
- 编辑前记录当前差异及新增 ID、引用。按稳定 ID 定位修改行或资产条目，不得用其他写入方修改前加载的副本替换共享工作簿、聚合资产或模块。
- 保存前检查文件自打开后是否变化；若有变化，重新加载最新版，仅重新应用目标 ID 的修改。保存后检查差异，确认无关行与条目仍保留、ID 在 Luban 命名空间内唯一、全部新引用可解析。
- 使用独立分支或工作树时，先合并编辑源并解决源冲突，再发布。不得将生成的运行时 JSON 当作编辑源合并或手工解决冲突。
- 共享 Unity 项目的发布按单写入方执行。不得从两个窗口并发运行 Luban 或 Unity 发布器。合并后的编辑差异稳定后再发布；任一步失败都应停止并修复源文件。
- 只暂存或提交本任务预期的源路径和生成产物；提交前检查最终差异中是否混入其他英雄的工作。

当前七英雄接入使用 [MobaHeroIdRegistry](../plans/MobaHeroIdRegistry.md) 作为唯一共享身份及资源映射预留清单。`规划` 行不是生产配置。仅在源资产存在且模型或特效用途已明确时添加生产 Model/Vfx 映射；仅在实际行为和引用已编辑时添加 Skill/SkillLevel/Flow/Trigger 行。不要为了占满预留槽位而创建空白或虚构行。规划、编辑、发布和游戏内验收应分别记录状态。

## 编辑器入口与写入范围

选中生产 `SkillFlowCO.asset`，在 Inspector 顶部使用「MOBA 生产技能流程」。这些命令只显示在固定的生产资产上；播放模式、正在进入播放或该资产有未保存修改时不可用。请先显式保存项目，工具不会替你保存。发布器使用 Unity 已发布的 Luban `skills.json` 检查前摇和施放流程 ID 引用。

| 交互入口 | 同一实现的批处理方法 | 当前行为 |
| --- | --- | --- |
| 校验已发布流程 | `MobaSkillPipelineExporter.Check` | 按规范化后的 JSON 内容比对全量产物，不修改正式资产。第一处内容不一致时，保存预期 JSON 至 `local/Logs/moba-pipeline-preview/<运行ID>/<Unity或Console>/<产物相对路径>`，并报告实际产物位置；缺失文件直接报错。 |
| 预检并发布全部流程 | `MobaSkillPipelineExporter.Apply` | 交互先校验并列出 Unity 与 Console 的新建和覆盖数量，有变更且确认后才写入；取消不写。批处理仍明确调用 `Apply` 直接发布，不弹窗。 |

产物为 Unity `Resources/moba/skill_flows.json`、Console `Configs/moba/skill_flows.json` 和 Unity `Resources/moba/skill_flows/skill_flows_<id>.json`。发布不清理旧分片、不发布 Luban 或 Trigger。写入不是事务，失败后核对版本控制差异再重跑；发布期间不要并行编辑或发布同一资产。

旧 `Validate Published Flows`、`Publish Flows` 和 `Sync XiaoQiao Skill 1 Flow` 菜单已退役。最后一项从来不是单技能同步；其历史 `Sync` 方法仅为未登记的直接调用保留，仍会发布全部 Flow。ActionEditor 的时间轴导出仍是独立步骤。

## 发布顺序

1. 编辑 ActionEditor 源时间轴，在编辑器中保存，或以 Unity 批处理运行 `NBC.ActionEditor.LogicJsonExporter.ExportAllMobaTimelines`。不支持的权威片段在管线编译逻辑导出时失败。
2. 若修改的 ID 和跨资源引用已存在于已发布运行时数据中，运行 `python LubanConfig/Moba/export_pipeline.py --apply` 完整 Luban 管线，再在 Unity 发布 Flow 和触发器编辑资产。此路径适合常规修改，但最终仍需以下检查。
3. 新英雄、ID 迁移，或同时新增 Flow/Trigger 引用及对应 Luban 行时，使用下文分阶段顺序。编辑发布器依赖运行时配置目录校验，因此应先发布前置表，再导入及发布新编辑 ID。
4. 两个编辑发布器完成后，运行 `python LubanConfig/Moba/export_pipeline.py --apply`，执行完整跨资源检查和最终 Luban 发布。随后运行 `python LubanConfig/Moba/export_pipeline.py --check-published`、`MobaSkillPipelineExporter.Check` 和触发器编辑的 `CheckRuntimeBatch`；发布前必须全部通过。
5. 运行 `dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --filter "FullyQualifiedName~MobaLubanConfigPipelineTests|FullyQualifiedName~BattleFlowRealSkillTests"` 及 Unity EditMode 英雄验收测试。

### 新 ID 或互相引用资产的分阶段发布

表面上的循环来自两个独立校验边界。Flow 发布器通过当前已发布的 Luban 技能目录解析 Flow ID，触发器编辑通过当前 Luban 目录解析 Buff ID 等语义 ID。反向上，完整 Luban 管线又依赖已发布的 Flow 和 Trigger 产物检查 Skill-to-Flow 等跨资源引用。因此，虽然编辑源本身没有依赖环，各发布器仍需要另一侧的运行时数据保持最新。

先发布已验证的前置表，再发布编辑运行时资产，最后运行完整发布门禁，以打破此循环：

```powershell
python LubanConfig/Moba/export_pipeline.py --apply-prerequisites
# In Unity, use the canonical publishers in order:
# Trigger Authoring ImportProductionSourcesBatch (only if production Source files were edited)
# MobaSkillPipelineExporter.Apply / Check
# Trigger Authoring PublishRuntimeBatch / CheckRuntimeBatch
python LubanConfig/Moba/export_pipeline.py --apply
python LubanConfig/Moba/export_pipeline.py --check-published
```

`--apply-prerequisites` 仍执行 Luban 结构、表、业务规则和生成产物检查，仅跳过依赖尚未发布 Flow/Trigger 产物的跨资源检查，并且仅发布 Luban 前置表、二进制和代码。它不绕过最终 `--apply` 校验，也不能视为发布完成。生成的运行时 JSON 仍由发布器管理，不得手工编辑。

安妮、金克丝、维迦的专属发布和检查菜单已退役。日常修改按各自权威源处理，使用生产 `SkillFlowCO.asset` Inspector 和[触发器正式发布与检查](MobaTriggerAuthoring.md)，按需另走 ActionEditor、HFSM 和 Luban 的正式入口。旧同步方法保留给已记录的迁移复现或内部资源派生；它们可能覆盖源资产、派生 Prefab 或重新发布全量产物，不是日常单英雄发布入口，也不应作为默认门禁。

## Pipeline/Trigger 引用契约

每个触发器 ID 执行列表（RulePlan、Window 打开/步进/关闭、Buff、Projectile、Area 及相关玩法列表）中，同一触发器 ID 最多出现一次。运行时引用校验器对重复项报告阻断错误 `moba.skill.contract.duplicate_trigger_reference`，防止从管线数据组装阶段时意外重复伤害、Buff、资源或生成效果。需要多次执行时，应显式建模为 `Repeat` 阶段，或编辑具有清晰不同事件及效果意图的独立 Trigger ID，不能依赖重复 ID 隐式循环。

Flow 结构同样是阻断契约：至少包含一个支持的阶段，每个组合阶段至少有一个子阶段。未知阶段类型、空阶段、空 Flow 和空组合，在构建运行时管线前以稳定的 `moba.skill.configuration.*` 校验码拒绝，使编辑器及发布门禁与运行时构建器保持一致。

RulePlan、Window 打开/步进/关闭和 Timeline `EffectId` 引用必须指向 `TriggerPlanScope.Global`。这些入口直接在技能管线内执行：RulePlan 和 Window 使用当前 `SkillPipelineContext`，Timeline 将效果 ID 传给 `MobaEffectExecutionService` 直接执行计划。它们不是所有者绑定订阅。引用 OwnerBound 计划时，校验器报告 `moba.skill.contract.invalid_trigger_scope`。

事件驱动的直接列表在运行时沿用相同划分。Buff 的 `OnAddEffects`/`OnRemoveEffects`/`OnIntervalEffects`、Continuous 的 `IntervalTriggerIds`、Projectile 和 Area 的 `On*TriggerIds` 由 `MobaTriggerExecutionGateway.ExecuteDirectTrigger` 分发，只消费 `Global` 计划；引用校验器强制此范围。关联事件的所有者绑定行为从 Passive/Buff/Continuous 绑定注册，通过 `MobaTriggerPlanSubscriptionService` 的强类型事件订阅执行。`MobaEffectExecutionService.ExecuteTrigger` 在创建执行上下文前再次检查边界；被拒绝的 OwnerBound 计划返回 `false`，网关不将其计为已执行，防止直接事件列表绕过所有者门禁或丢失来源上下文。

范围规则执行两次：启动配置校验拒绝无效引用，运行时各入口在执行前再次检查已加载的 `TriggerPlanJsonDatabase.Record.Scope`。`MobaTriggerPlanExecutor.ExecuteRulePlan` 和 `MobaEffectExecutionService.ExecuteRulePlan` 对 OwnerBound 返回 `false`；`MobaEffectExecutionService.ExecuteWithResult` 对 Timeline 效果抛出显式范围错误；`MobaEffectExecutionService.ExecuteOwnerBoundTriggerActions` 对 Global 返回 `false`。两条路线互斥：直接 Pipeline/Timeline 只接受 `Global`，所有者订阅只接受 `OwnerBound`。第二层门禁保护未执行完整启动校验的热加载或手工注入计划。OwnerBound 仅能通过 `MobaTriggerPlanSubscriptionService` 及其所有者事件路径执行，保持管线负责单次施法直接序列、订阅负责事件行为的既有架构。

持续的所有者绑定每个世界步进从活动 `OngoingTriggerPlans` 组件对账一次。缓存以列表哈希快速检查变化，同时保存并比较精确的触发器 ID 序列；即使 32 位哈希碰撞也必须刷新订阅集。空或缺失所有者会被停止并从缓存移除，防止已销毁角色保留事件订阅。

`MobaTriggerPlanSubscriptionService.OnInit` 支持重入：重复世界初始化或热重载时，先释放既有所有者注册及黑板，清除求值快照，再重建计划索引。更新所有者绑定还会移除已不存在 Trigger ID 的求值快照，使诊断仅读取当前订阅计划，不保留过期所有者及触发器条目。

Unity 批处理使用本地 Unity 2022.3 编辑器，参数为 `-batchmode -nographics -projectPath <repo>/Unity -executeMethod <method> -quit -logFile <log>`。使用 `-runTests` 时省略 `-quit`，让测试运行器完成并写出 NUnit XML；`tools/run-unity-editmode-tests.ps1` 已处理此要求。

默认运行时配置从 Luban 二进制加载其他已注册表，从 `Resources/moba/skill_flows.json` 加载 `skill_flows`；Console 使用已发布的 `Configs/moba/skill_flows.json`。必须保留流程 DTO 注册：即使 Luban 不再生成 Flow 表，它仍是运行时契约。

### 按属性缩放 Timeline 前摇

金克丝 W 的线性曲线与起手冻结已由用户明确回复“确认采用”。Timeline 可声明 `Windup`：作者时间切点、零加成时长、最短时长和达到下限的攻速百分值。首个配置只接 W，读取有效 `ATTACK_SPEED_R`（含机枪攻速贡献），在进入 Timeline 时采样；0/125/250% 对应 600/500/400 ms。负值按零，超过上限维持最短时长，结果取最近整数毫秒。500 ms 是作者资源的发射标记，不是运行时固定前摇。只重映射切点前的时间，切点后仍按原速度推进；不改经济提交、受控取消、后摇交接或命中规则。

时序只允许接在同步 RulePlan 之后的末尾 Timeline，不与单发普攻节奏组合。每次 Cast 的 Timeline phase 保存冻结时长、起始帧、累计原始时间及下个事件游标；属原 SkillRunner phase 快照，恢复原 Cast 时校验并静默导入，不重新评估属性，也不放宽结构重建范围。状态同步通过角色动作快照投影冻结时序，Animator 和表现时间线消费该值，不从面板属性重算。未配置技能保留原行为和原角色动作快照布局。

## 运行时回滚契约

Pipeline 包公开运行阶段实例、执行位置和生命周期调用，不负责回滚格式或策略。MOBA 技能层保存版本化阶段树及各活动运行器的阶段、上下文、策略、施法序号游标和运行字节。恢复从 `IMobaSkillPipelineLibrary` 重建阶段实例，重新绑定事件订阅和管线持续状态，不重复执行管线开始动作。技能运行时 Provider 在运行器 Provider 导入前，恢复其黑板及技能阶段执行上下文生命周期。

这些快照支持**进程内预测回滚**。Triggering 包公开调度项进度和生命周期操作；MOBA 触发器运行时负责载荷验证、条目选择、ID 游标恢复及可执行定义保留窗口。新快照仅包含等待项。已完成定义保持可用，直到 MOBA 收到超出配置回滚历史范围的确认帧后才释放。预检要求保存的可执行闭包仍可用；执行上下文生命周期恢复也要求原上下文身份。新进程不能将这些本地回滚载荷作为重连或完整世界恢复快照。

联合生命周期世界中，结构 Provider 10011 撤销旧 Continuous 贡献，并从其所有者重建目标技能参数投影。后续字段 Provider 10015 验证载荷，但不得再次清空或导入投影。独立 Provider 行为不变。这是所有权顺序修复，不改变载荷版本，见 `local/docs/Moba投射物区域位移组合验收.2026-11-17.md`。

角色变换恢复不等于完整 Motion 来源恢复。已批准的[受管位移恢复方案](../plans/MobaManagedMotionRecoveryDecision.2026-11-17.md) 将 FixedDelta 来源精确值（原始时间、活动性、速度、仲裁和碰撞策略）归入联合生命周期的 continuous-local。生命周期包络 v4 拒绝 v3，其他 Provider 版本不变。恢复要求同一存活受管实例及绑定到其所有者 Pipeline 的原来源，校验冻结配置并静默恢复，不调用激活或完成回调。缺失、替换或不支持来源在预检失败；来源值参与组补偿。其他来源、外部 Pipeline 来源和已销毁实例重建不在此契约内。

受管 Motion 即使没有 Modifier，也从既有回滚管理的 ModifierSourceId 分配器预留身份，用于识别配置完全相同的替换对象，不引入非确定性进程标识。较早的到达后第 1 帧检查点仍有未解决的执行上下文及技能运行时差异，记录于 `local/docs/Moba受管位移精确恢复验收.2026-11-17.md`。

已批准的[被动回滚重绑定方案](../plans/MobaPassiveContinuousRollbackRebindDecision.2026-11-17.md) 区分恢复绑定与正常装配变化。正式联合生命周期注册表中，组件 Provider 10018 为实际导入的 SkillLoadout 标记一次响应式重绑定。该次操作更新被动监听器、所有者绑定和持续计划，不对检查点间隔实例执行插入更新、准入、对账或结束；Provider 10011 已恢复其名单和索引。检查点不存在的间隔即使当前可准入，也保持不存在。后续真实 SkillLoadout 替换会立即作废标记，包括响应式消费前的替换；正常同步仍评估 RequireOutOfCombat 并清理已移除被动。消费、角色注销及拆卸时清除标记。不涉及冷却、战斗活动、协议或生产内容变化。中途位移检查点的正式 R 重演要求含战斗外门控被动目标时所有 Provider 字节相等，证据见 `local/docs/Moba被动恢复重绑定验收.2026-11-17.md`。

## 运行时生命周期钩子契约

`MobaSkillRuntimeLifecycleHookService` 是技能运行时聚合的观察与集成接口，不负责技能状态、子对象保留、管线完成或回滚决策。运行时服务仍是生命周期状态的唯一写入方；对应转换记录完成后，钩子接收生成的 `MobaSkillRuntimeLifecycleEvent`。

每次 `Notify` 都基于调用开始时已注册钩子的稳定快照分发。钩子处理事件时可以注销自己、注销其他钩子或注册后续钩子；变化仅影响下一事件，不会跳过既有钩子、重入调用新钩子或改变当前顺序。注册保持幂等，`Clear` 移除全部后续通知。

逐回调隔离钩子失败：继续分发快照其余部分，将首个异常保留为本次失败，记录后续异常，在 `finally` 归还池化快照后重新抛出首个异常。钩子代码必须仅观察或具备明确恢复能力，不得假定后续钩子已执行，也不得将回调顺序作为玩法规则。玩法效果、状态转换和回滚数据归上述运行时、Pipeline、Trigger 或 Modifier 所有者负责。

快照列表使用对象池，避免每事件产生长期分配。池化仅为实现细节；调用方不得保留生命周期事件，也不得从其他线程修改钩子注册。运行时生命周期通知在主模拟线程执行，应在下一世界步进前同步消费。

## 技能子对象保留契约

`MobaSkillCastRuntimeService` 保留的每个子对象由一个 `MobaSkillRuntimeRetainHandle` 表示，记录运行时身份、代际、子对象身份和单调保留 ID。旧 `runtimeId + child` API 与句柄 API 走同一注册路径；旧释放重载先消费匹配句柄，防止释放子对象后在注册表留下不可见能力。

每个句柄仅释放一次。过期句柄、运行时代际不匹配、子对象身份不同或重复释放均拒绝。`ForceTerminate` 和 `Clear` 在终结前撤销全部剩余子对象能力。`ValidateRetentionState` 检查诊断一致性：每个活动子对象应有一个保留句柄，每个已注册句柄应指向活动运行时子对象。孤立或未跟踪数非零属于生命周期缺陷，必须在所属 Buff、Projectile、Area、Summon 或派生技能清理路径修复，不能强删注册项掩盖。

回滚历史窗口内，技能 Flow、时间轴及触发器定义必须保持不变。MOBA 运行编解码器检查版本、配置 ID/名称、阶段 ID/类型和边界，但不对所有编辑时长及效果参数计算指纹。`SkillPipelineContext.SharedData` 仅接受编解码器支持的基本及值类型；遇到不支持对象时捕获失败，禁止将部分快照记录为有效状态。

## 临时战斗对象清理

失败或回滚的技能施放按创建逆序释放 Actor、Projectile/Area 运行时关联、执行上下文和技能运行时保留。区域清理使用 `IAreaRollbackService.CancelArea`；`DespawnArea` 仍仅用于会发布 `AreaExpireEvent` 的玩法移除，防止取消及预测回滚被误解释为过期触发，并保持后续生成失败时多区域技能的确定性。

## 表现快照消费契约

`BattleClientSnapshotStream` 是网关接收与表现投影间的所有权边界。网络回调将按约定不可变的 `GatewayStateSyncSnapshot` 入队；表现线程排空队列并同步应用已接受快照。流拥有角色字典，绝不将该字典或可复用角色缓冲区暴露给投影。

`AttachProjection` 及完整快照恢复从流的角色缓存构建完整基线。角色 ID 维护缓存排序，仅在角色集合变化时重建；基线角色数组则为每次挂载或恢复重新分配。这样可从常规 `Present`/`Drain` 路径移除排序及临时列表创建，同时保留确定的角色顺序。

投影 `Apply` 回调必须同步消费传入快照，不得用作跨线程或长期可变缓存。需要插值的投影自行拥有副本或环形缓冲。`DetachProjection`、会话停止及流重置会调用 `Reset`；卸载或重置后不再向该投影交付快照。恢复时丢弃增量，直到接受完整快照，再通过相同缓存复制边界重建投影基线。

表现提示对账有独立历史契约。控制器容量限制历史量，不代表可以丢弃活动提示。活动键保留预测及权威历史，直到停用或代际重置；仅非活动条目可淘汰。接收记录携带条目身份，防止旧队列记录删除移除后复用的同名键。预测技能特效可能在后续权威确认、纠偏或拒绝到达前持续活动，因此需要此边界。

会话功能拆卸对完整步骤列表尽力清理。某步失败不短路后续清理；生命周期控制器仅记录失败步骤，下次卸载时重试。订阅、快照缓冲、网络绑定及表现资源因此可独立清理，同时保留确定的失败报告。

## 可靠事件基线契约

可靠战斗事件交付将每次 `RequireAuthoritativeBaseline` 视为新恢复窗口开始。新窗口接收事件前，丢弃等待上一基线时保留的批次；否则旧乱序批次可能在新水位后重放，触发第二次序列缺口恢复。本窗口批次仅在接纳匹配时间轴基线后重放，游标仍移除权威水位已覆盖的事件。

待处理批次跨越传输回调边界，交付运行时保留前先复制事件列表。网络解码缓冲和调用方列表可在 `Handle` 后复用或修改，但重放必须观察接收时的批次。队列上限按批次计，是恢复保护；事件顺序和水位覆盖仍由游标负责。

交付队列除批次数外，还限制事件总数（`MaxPendingEvents` 默认 512），防止单个超大推送无界消耗恢复内存。溢出清空待处理窗口，并为本恢复窗口报告一次 `PendingQueueOverflow` 失效；后续推送直接丢弃，直到新基线或代际重置信号后才重新入队。成功基线开启新窗口，可独立报告后续溢出。`RecoveryWindowId` 标识当前窗口，`PendingOverflowCount` 累计诊断溢出压力；二者均不参与玩法决策。

等待权威基线时，流标识与活动游标不同的批次视为其他战斗的过期数据，在入队前丢弃。运行时增加 `PendingInvalidBatchCount`，发出 `BatchRejected/InvalidStream` 观察诊断，但不使当前恢复窗口失效，也不再次请求同步。恢复期间仍保留时间轴不匹配批次，因为新权威基线可能建立新纪元；接纳基线后仍仅重放匹配时间轴。

MOBA 权威恢复协调器将 `ReliableEventResyncRequired` 关联到该窗口 ID。同窗口重复可靠事件失效仅首次上报，避免等价失败突发时重复入队完整状态请求。新代际会重置关联。执行恢复动作会调用 `RequireAuthoritativeBaseline` 并推进 `RecoveryWindowId`，因此后续失败仍可重试。此抑制仅用于可靠事件重同步信号，是诊断协调状态，不是 Pipeline、Trigger 或玩法决策。

### 已批准的持续施放与辅助再次施放契约

2026-11-14 已批准的 D2 正常恢复段契约以 `Timeline.RecoveryStartMs` 编辑（`-1` 禁用，`0` 允许即时效果后交接）。首批仅支持最后一个顶层时间轴：入口必须早于完整编辑时长，其全部玩法事件必须位于入口或之前，后续及嵌套阶段不得声明恢复段。运行时配置加载及编辑器发布均验证此边界。ActionEditor 编译保留完整时长，仅复制 Flow 中显式设置的恢复段配置。

恢复段资格来自当前终端阶段、确定性已过时间、已提交事件游标及执行和控制有效性。既有 Pipeline 快照已保存这些事实；恢复不重放效果，也不需要第二张生命周期表。新施法（包括普攻槽）成功时，通过正常完成路径结束冲突的恢复段运行；拒绝输入则保留旧运行。合法非零移动输入仅在战斗规则允许移动时结束恢复段。无新输入则继续完整编辑动作。交接不取消原 Cast：`MarkPipelineEnded(PipelineCompleted)` 保留已提交子对象，按来源限定的清理不能退还已提交消耗。普通死亡、中断及未配置流程保持原契约。

首批内容：拉克丝 W 入口 250 ms，对应既有投射物提交；安妮 E 入口 0 ms，对应即时护盾提交。完整时长仍为 5042 ms/4167 ms。Meraki 补充说明分别为 0.25 秒/无，但属于最新资料，不能独立证明全部 16.22.1 时序。其他技能需逐项审阅时长。

2026-11-26 确认的提伯斯连续攻击修复：技能管线同步 HFSM 时沿用当前 Cast 选择规则，较早的并行 Cast 不覆盖已选中的 Cast。同槽位普通攻击成功后，已声明且满足正常恢复条件的旧 Cast 走完成交接，不因新攻击结束而重新占用动作。普通和狂暴攻击的恢复入口均在 0 ms 的既有命中提交后，保留完整流程时长和原冷却。

狂暴攻击的基础层模型动作由 HFSM 的技能变体独占；Timeline 不再向同一层发送动画请求。仲裁复用现有 Runner 运行记录、阶段游标和 HFSM 投影，不增加权威状态、同步字段或回滚载荷。

[拉克丝接入准备](../plans/LuxImplementationReadiness.2026-11-13.md) 批准的有界扩展均为显式 Flow 字段，默认 false：

- `ContinueThroughControl` 分离准入与持续执行。新施法仍通过普通战斗门禁；既有施法在逐 Cast 检查和 Pipeline Continuous 执行门禁中忽略该技能控制结果掩码，显式 Continuous 模板要求仍适用。死亡及移除终止施法。角色 HFSM 在普通控制下保留施法动作，除非绑定设置 `RespectActionControl`，允许控制接管表现但不暂停施法时钟。
- `PreserveCharacterCastContext` 让辅助施法保留主角色 Cast、瞄准及动作游标。HFSM 选择和 `TryGetLatestRunning` 排除该辅助上下文；其自身运行时、效果及清理仍独立。
- `QueueRecastInFlight` 让再次施放绑定精确的原通道、槽位、基础技能、Cast 代际和唯一投射物。重复请求仅置一个待处理标志。落地先保留区域再释放投射物，并通过与已部署再次施放相同的终态认领消费请求。原 Cast 黑板在回滚中保存待处理身份；死亡清除自有通道。

`resolve_cast_area` 支持用于落地交接的 `pending_only`，以及解析当前 Cast 唯一区域的 `owned_area`。区域回调携带原中心和前向量；查询读取该空间基准，不使用施法者后来的变换。契约不包含英雄身份判断。

向前快照落后于表现时间轴已采样帧时，角色表现绑定必须保留活动动作进度。同一动作及 Cast 绑定在较晚帧采样，不重播一次性片段；显式向后定位仍按既有规则停止并重建表现。仅放置（`Follow=false`）的时间轴粒子不得以移动的隐式自身 Transform 为父对象。这些是消费方契约修复，不构成玩法时序或瞄准权威。

使用可调用动作 `start_auxiliary_skill`，从 HFSM 状态入口启动独立、无槽位施法。它走正常准入，拒绝同技能第二个存活实例，不参与前台施法的运行器冲突竞争。角色绑定可将施法标为 `Auxiliary`，提供独立状态及条件，同时让前台动作优先。单个不中断时间轴的表现快照携带原施法起点和编辑时长；切换表现状态不重启动画。额外字段仅为视图投影数据，不是输入或回滚权威。

烬第四次成功发射投射物后安装弹匣锁（`CannotAttack`）；角色可进入装填时，HFSM 启动装填技能 `30010605`。由 2500 ms 时间轴移除锁并重置弹匣，不以状态退出或 Animator 事件为依据。原非循环装填片段按时间轴进度采样。

### 已批准的 Cast 表现空间基准

[CastFrame 方案](../plans/LuxCastPresentationFrameDecision.2026-11-13.md) 已明确批准。`spawn_area.capture_cast_frame=true` 在原 Cast 黑板中捕获偏移前的单一解析位置及形状偏航前的归一化水平瞄准方向（模块 1002，Vec3 键 140/141）。拒绝多目标捕获。首次捕获生效，辅助施法及后续角色移动不得覆盖。既有黑板回滚恢复纯值，不保存世界引用。

`CharacterActionData.CastFrame` 按原 Cast 上下文和施法者身份投影该可选值。CharacterAction 编解码 v3 写入可选空间基准，并校验有限原点和单位水平前向；v1/v2 仍可读取但不带此数据。该基准仅用于表现，不改变角色朝向或命中查询。模板 `PlacementSpace=0` 保留既有附着行为；`1` 要求匹配 Cast 基准，在其局部空间应用模板偏移，生成固定世界请求。缺少基准时拒绝播放，不从视觉 Transform 推断瞄准。异步加载请求携带值副本；既有 Timeline/Cast 生命周期负责取消与清理。

模型动作采样优先使用解析后动作目录的显式时长。Unity 在零增量定位后可能立即返回默认或过期的 `AnimatorStateInfo.length`，即使当前 Clip 已有正确的较长时长；此回退不能覆盖已知编辑时序。未声明时长的动作保留 Animator 长度回退；模拟 Cast 时序及角色移动仍为权威。


## 显式地面目标点范围策略（2026-11-15）

技能按钮 `PointRangePolicy` 默认 `Legacy=0`。`ClampToResolvedRange=1` 仅适用于 `AimMode=Point` 且 `UsePointMode=TargetPoint`。CastPrepare 解析既有 CastRange Modifier，拒绝非有限坐标或范围及负范围，并限制相对当前施法者的 XZ 距离，保留输入地面 Y。最终 AimPos 存入原 Cast、请求及既有快照；全部 Area、触发器和特效消费同一点。范围为零时选择施法者 XZ。不新增快照字段或英雄分发。

启用该选项的按钮，HUD 使用正的静态 `SelectRange`，否则回退到 `Skill.Range`；TargetCircle 的 `IndicatorWorldWidth` 为世界半径。运行时解析范围仍为权威。外部动态范围同步到 HUD 不在已批准的最小范围内；未启用内容保留既有行为。
