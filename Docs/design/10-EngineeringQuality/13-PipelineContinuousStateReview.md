# Pipeline 与 Continuous 状态边界审查

> 2026-11-19。本文应用[业务状态与执行职责规范](12-BusinessStateAndExecutionBoundaries.md)，记录现状和已完成的有界试点；其余建议不自动成为 API 或回滚承诺。

## 1. 包的职责保持独立

| 包 | 当前职责 | 不应接管的工作 |
|---|---|---|
| [Pipeline](../08-GameplayModules/08-PipelineAndAbilityRuntime.md) | 编排一次运行：选择、进入和推进阶段，处理组合、暂停、中断与收尾。外部以 `Tick(deltaTime)` 驱动；图结构和 Trace 用于观察。一次运行也可以等待外部事件或显式终止，不保证有固定时长。 | 角色长期状态、Buff 堆叠、跨实体时钟和世界级恢复。 |
| [Continuous](../08-GameplayModules/11-ContinuousFrameworkDesign.md) | 管理持续体何时进入、暂停、恢复和退出，维护注册顺序、owner 索引、准入与生命周期通知。`DefaultContinuousManager` 没有 Tick，具体持续时间、周期和效果由领域对象及项目驱动。 | 技能阶段编排、具体玩法结算和完整世界快照。 |

两个包没有互相依赖：Pipeline 清单与 Runtime asmdef 依赖 Core；Continuous 不依赖 Core，Core 内同名旧 API 是兼容面。MOBA 在项目层组合两者：[SkillPipelineRunner](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/SkillPipelineRunner.cs#L768) 持有并推进 Run，同时注册 [`SkillPipelineContinuousRuntime`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/SkillPipelineContinuousRuntime.cs#L7)，让施法成为标签、准入和生命周期管理可观察的持续体；该包装不推进阶段。[MobaContinuousManager](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Continuous/Runtime/MobaContinuousManager.cs#L24) 继承默认管理器，领域 Tick 仍由世界 System 驱动。这条组合链足以支撑两包分别演进。

## 2. 源码里的状态及恢复边界

| 对象 | 当前数据与执行边界 | 对备份和回滚的影响 |
|---|---|---|
| Pipeline 的定义及启动 | [`AbilityPipeline.Start`](../../../Unity/Packages/com.abilitykit.pipeline/Runtime/Core/Pipeline/AbilityPipeline.cs#L28) 接收配置和 Context，从阶段模板创建 Run；[`AbilityPipelinePhaseRuntime`](../../../Unity/Packages/com.abilitykit.pipeline/Runtime/Core/Pipeline/AbilityPipelinePhaseRuntime.cs#L16) 对实现工厂的节点创建实例，否则兼容复用原对象，再调用 `Reset`。配置、谓词和委托可以共享；工厂不自动隔离闭包和引用成员。 | 新 Run 不等于独立快照；自定义可变阶段没有工厂时，第二次启动可能重置第一次正在使用的阶段。定义身份、节点运行隔离和状态捕获须分别验证。 |
| Run 与阶段 | Run 持有根阶段游标、当前节点关联、取消与暂停；Context 也保存阶段、暂停等对外状态。Delay 推进累计时间，Sequence 有子阶段位置，Parallel/Race 保留有序活动子节点，Conditional 保存选中分支，Repeat 保存次数、间隔与子阶段进度。见 [`Run.SetExecutionPosition`](../../../Unity/Packages/com.abilitykit.pipeline/Runtime/Core/Pipeline/AbilityPipeline.Run.cs#L86)、[`AbilityRepeatPhase`](../../../Unity/Packages/com.abilitykit.pipeline/Runtime/Phase/Timing/AbilityRepeatPhase.cs#L31) 和 [MOBA 阶段捕获](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/SkillPipelineRollback.cs#L81)。 | 仅存 `CurrentPhaseId` 或图快照不足。应区分推进所需的游标、活跃顺序与仅供观察的结果；同一权威值的对外镜像可重建时无需另存。已选分支不能靠恢复时重跑条件来猜测。 |
| Pipeline Context 与绑定 | [`IAbilityPipelineContext`](../../../Unity/Packages/com.abilitykit.pipeline/Runtime/Interface/IAbilityPipelineContext.cs#L6) 允许实例引用和 `Dictionary<string, object?>`；[`AAbilityPipelineContext.ElapsedTime`](../../../Unity/Packages/com.abilitykit.pipeline/Runtime/Impl/AAbilityPipelineContext.cs#L44) 源自全局实时时钟。MOBA 用自己的逻辑累计时间，并为可恢复 `SharedData` 定义有限类型；[等待事件阶段](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/Phases/SkillAwaitEventPhase.cs#L78) 捕获累计时间、接收标记和是否需订阅，恢复后重绑实际订阅。 | 任意 Context 无法通用复制。只有真正接受逻辑时间的路径才能声明帧重演；系统/事件总线、订阅令牌、委托和池租约是依赖。事件载荷若已写入后续会读的黑板，则按该黑板的 owner 捕获。 |
| Continuous 管理层 | [`DefaultContinuousManager`](../../../Unity/Packages/com.abilitykit.continuous/Runtime/DefaultContinuousManager.cs#L10) 持有注册顺序、注册/活跃集合、owner 索引和 `OnEnded` 订阅；`IContinuous` 持有实例状态、计时与领域操作。`GetAllContinuous()` 复制列表，但列表仍引用原实例。 | 注册名单和有意义的顺序属于管理层；owner/active 索引可从已验证名单、owner、实例状态重建。逐项实现若绕过管理器直接改活动状态，不能假定索引同步。`IContinuousConfig` 的 owner 关联也不等于稳定的实例身份。 |
| Continuous 领域层 | MOBA 把计时、周期、层数、修改器捕获、来源及投影等留给具体领域；联合生命周期恢复保存有序名单、恢复计时和投影依赖。见 [`MobaSkillLifecycleRollbackCommit`](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Rollback/MobaSkillLifecycleRollbackCommit.cs#L190)。 | 只取生命周期枚举和 `ElapsedSeconds` 无法恢复 Buff 或周期行为。需恢复约定的精确时间单位、下次到期及稳定顺序；涉及身份分配器时由其原 owner 恢复，不能用新分配代替。普通 Binder 可以提交领域副作用，不等价于恢复重绑。 |

**当前恢复入口的实际保证：**

- Pipeline 已有 `AttachRun`、`SetExecutionPosition` 和阶段进度读写。`AttachRun` 跳过普通 `OnPipelineStart` 和开始 Trace，仍创建进程内 Registry ID、注册 Run 并通知调试 hook。这个 ID 不应作为可重演的业务身份；`CaptureDebugGraph` 的结构 ID 也不是含全部玩法参数的定义版本。MOBA Pipeline 适配会递归捕获/校验阶段、另存 Context，再附着或恢复 Run 并重绑。既有回退分支接受阶段基类，只覆盖它已知的字段；继承基类并通过形状校验，不证明自定义节点的新私有字段已覆盖。当前证据不能支持“任意插件节点可恢复”。
- Continuous 已有受保护的 [`RestoreRegistrationForRollback`](../../../Unity/Packages/com.abilitykit.continuous/Runtime/DefaultContinuousManager.cs#L247)：接收已准备的实例，重建索引与管理器自己的结束订阅，跳过正常准入、生命周期方法、Binder 和公开事件。它不捕获实例数据、不负责恢复投影或补偿任意钩子故障。普通注册、注销和激活中的异常清理也不是按帧回滚。
- MOBA 的 [Runner 恢复](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/SkillRunnerRollback.cs#L475) 持有施法序列、业务身份、Context 和 Run 载荷；联合生命周期适配对 Pipeline 包装与 Runner 状态做一致性核对，将配置归 Runner，而非另存一份。现有恢复可按各自已批准的范围准备骨架或重新挂载 Run；这不扩大 Cast、持续体或其它结构的重建资格。帧末捕获、恢复后从下一帧重演、失败处理及结构准入仍按[业务状态回滚契约](../../../.claude/skills/framesync-prediction-rollback/business-state-rollback.md)与 [MOBA 机制治理](../../MobaMechanismGovernance.md)执行。

## 3. GAS 可以借鉴的边界

| Unreal GAS 5.6 | 对本框架的启发 |
|---|---|
| [`FGameplayAbilitySpec`](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/GameplayAbilities/FGameplayAbilitySpec?application_version=5.6) 是被授予的技能条目，保存等级、`InputID`、来源等，亦有 `ActiveCount` 等运行字段；[`FGameplayAbilityActivationInfo`](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/GameplayAbilities/FGameplayAbilityActivationInfo?application_version=5.6) 另表达激活模式和预测信息。 | 授予/装载资格与一次施法的冻结参数不是同一个生命周期。Pipeline 无需承接角色技能栏或授予逻辑。 |
| [`FGameplayEffectSpec`](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/GameplayAbilities/FGameplayEffectSpec?application_version=5.6) 引用效果定义，携带该次应用的上下文、`SetByCaller`、属性捕获等；[`FActiveGameplayEffect`](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/GameplayAbilities/FActiveGameplayEffect?application_version=5.6) 表达已应用的效果及其持续状态，例如开始时间和周期/到期计时器。 | 一次应用的参数和应用后的进度应分别建模。GAS 自己的 Spec 和 Active 对象含可变值与引擎关联，不能直接视为独立的帧回滚快照。 |

本仓已有的 [`GameplayEffectSpec`](../../../Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/GameplayEffectSpec.cs#L9) 持有持续策略、周期、标签、组件和 Cue，当前用法更接近可复用的效果定义与组合；计时和层数在 [`EffectInstance`](../../../Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EffectInstance.cs#L11)。MOBA [Cast 准备](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Cast/SkillCastPreparationService.cs#L237) 已有目标、瞄准和指定参数采样；`SkillCastPreparationResult` 同时携带 Context、服务和阶段引用，是装配结果。若未来需要显式的施法 Spec，应从已冻结的领域数据抽取，保留动态查询或按事件重新采样的既有时点。

## 4. 建议统一语义，按领域定义形状

| 层 | 职责 | Pipeline / Continuous 例子 |
|---|---|---|
| Definition | 可共享的规则与拓扑。若需由备份定位，同一恢复范围内规则须保持稳定，或能凭身份与版本精确寻址。 | 阶段图和规则参数 / 持续效果的配置与规则。 |
| Spec（可选） | 在业务规定的时点冻结本次启动或应用的值；应独立于可变来源。不发生冻结且无需保存的值不必装进 Spec。 | 本次施法准备已采样的参数 / 一次效果应用已确认的来源、目标与捕获值。 |
| Runtime State | 唯一权威的可变状态，保存决定后续结果的计时、游标、结构身份、资格或顺序。跨 owner 关系用稳定 ID/handle 或已有同实例合同。 | Run 和阶段进度 / 持续体状态、计时、叠层及实际处理顺序。 |
| 执行与依赖 | 计算读取显式的定义、Spec、当前状态、时间和已声明输入。业务 owner 提交动作；世界查询、委托、订阅和索引由原 owner 管理，按现有恢复合同重绑。 | Repeat 的计时推进与执行子节点分开 / 周期到期计算与伤害或属性投影分开。 |

备份应保存充分状态和本次冻结的数据，按定义身份重用规则。恢复遵循 `Capture → Validate → Restore → Bind` 的职责，帧同步 Provider 仍可选；视图、服务和执行器无需为捕获而复制。可保留有字段的 Runtime、不可变策略对象和临时工作区，执行器不得用未声明的跨帧权威状态改变结果。无需引入 `ISpec`、公共字典、统一 Context、通用对象序列化或 Core 管理器。

## 5. 推荐下一批与确认范围

**先试 Pipeline 的 `AbilityRepeatPhase<TCtx>`。** 其已有领域消费者：[MOBA 表驱动 Repeat](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/TableDrivenMobaSkillPipelineLibrary.cs#L674) 建立通用阶段，已有适配负责导出和恢复索引、间隔、是否等待、子节点占用和通用累计时间。选它能同时验证显式计时推进和嵌套状态，先不碰 Cast Spec 或 Continuous 通用快照 API。

- **方案：**内部归拢 Repeat 自有的计数、间隔进度、等待及子节点存在性，给间隔和重入分支明确的显式输入；子节点的运行数据仍归子节点，父节点不复制子节点状态。子对象和 `repeatAction` 仍由执行层调用；延用已有的紧接执行、间隔到达后余量处理以及结束和错误顺序。对外现有属性和 `SetRepeatProgress` 由同一份状态读写，保留阶段工厂、签名、配置和快照字段/顺序；不顺带更换时钟或加入次数上限。
- **替代及取舍：**Delay 只验证一个计时字段，实施成本更低，但不能验证重绑子节点和回放顺序；一次迁移全部组合阶段或先立共用 Spec 接口，会把尚未验证的假设固化为长期兼容负担。第二个试点可选现有 MOBA 周期 Buff，仅复核生命周期、精确余量、到期次数和 Runtime/Manager 结构所有权；两次都出现相同且稳定的状态操作后才考虑抽工具。
- **验收：**两次并行运行互不污染；分别在等间隔和子阶段进行中捕获，推进、恢复再以相同 delta 逐帧比较次数、结束帧、事件顺序和业务结果；恢复不触发动作或重启子阶段；非法输入在写入前拒绝，未知可变子阶段不得借用已知结构宣称充分恢复。用旧载荷和现有 MOBA 消费路径验证兼容，比较原有 Pipeline 热路径的分配变化。既有快照准入如果暴露独立缺口，记录并单审其范围。
- **本次需确认的决策：**是否采用上述 Repeat 小试点作为现有规范在 Pipeline 的首次落地，限定内部状态归属与显式推进；MOBA 只验证既有消费和恢复，不改变生命周期、配置、同步协议或结构准入。按 [MOBA 技能创作指引](../../../.claude/skills/moba-skill-authoring/SKILL.md)及[机制确认流程](../../MobaMechanismGovernance.md#用户确认流程)，实现共享执行逻辑的状态归属方案前等待这一具体决策；本文不授予后续 Continuous 试点或新的恢复范围。

## 6. 工程清理及验证

- Pipeline 包说明补齐单次阶段推进的定位，并移除没有类型定义的停用 Timeline 占位。真实领域时间线保留在 MOBA。运行基础代码的目录、中文多行注释和公开契约按[框架包工程规范](10-FrameworkPackageEngineeringStandards.md)推进；函数文档明确“对象引用列表”和“独立状态备份”的区别。
- 包级 .NET 测试基线：Pipeline 16 项、Continuous 3 项通过。前者包括诊断、失败和部分 Run 访问，后者覆盖基本生命周期、拒绝中断和激活异常清理；本次结果不证明完整帧回滚、MOBA 全世界联测或 Unity Player 发布能力。Core 的发布判定仍以 [Core 稳定化记录](11-CorePackageStabilization.md)为准。

## 7. Context 专项复核（2026-11-19）

[Pipeline Context 新增用法](../08-GameplayModules/08-PipelineAndAbilityRuntime.md#42-context-的数据与备份边界)沿用现有状态规范。统一的是 Context 的**可备份部分与恢复步骤**；每个领域仍定义自己的 Context 数据。只导出 Run 游标不能复原阶段间输入和提交结果，只导出 Context 也不能重建活动 Run。

| 审查点 | 当前证据与结论 |
|---|---|
| 任意对象入口 | [`IAbilityPipelineContext.SharedData`](../../../Unity/Packages/com.abilitykit.pipeline/Runtime/Interface/IAbilityPipelineContext.cs#L19) 公开可写字典，限制 `SetData<T>` 仍无法覆盖直接写入。框架还有 `FailReason` 键，[Behavior 阶段](../../../Unity/Packages/com.abilitykit.behavior/Runtime/Pipeline/AbilityBehaviorPhase.cs#L40) 也读通用键；立即换字典类型会影响真实消费者。 |
| 已有回滚检查 | [MOBA 捕获](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/SkillPipelineContext.cs#L621) 按序导出键，值转换只接受明确的有限类型，遇到委托或服务会包含键名报错；[载荷读取](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/SkillRunnerRollback.cs#L232) 校验数量、键序和部分值范围。施法预采样保留键另校验业务类型；其他共享键没有统一的类型或必填合同。编码顺序也不等于玩法执行顺序。 |
| 实例引用和委托 | 实例、单位、事件总线属于绑定。`SkillPipelineContext` 持有运行时清理列表；HFSM 参数和模式清理可由已保存的所有者/模式重绑，Pipeline 持续包装由 Runner 重新关联。见 [MOBA 清理入口](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/SkillPipelineContext.cs#L418)、[Runner 的拆卸与附着](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/SkillRunnerRollback.cs#L384)。这不能证明任意新增 `RegisterCleanup(Action)` 都能恢复；不得用新的任意闭包承载跨帧待办或释放资格，不直接删除已用的清理路径。 |
| 捕获与绑定尚未完全分开 | 当前 `CaptureRollbackState()` 会刷新 `_characterModeInstanceId`，而 `RestoreRollbackState()` 在恢复字段时同时重新登记清理。评审时记录这些依赖与顺序；先确认如何从 HFSM 的唯一权威源取得同帧值，再决定是否分离。不能只搬字段后宣称捕获已纯读或恢复已可单独使用。 |

例如“若下一帧到期则执行一次”应保存到期帧、待执行标记、稳定规则/目标身份与参数，由 Step 使用这些值。若规则本来由确定的阶段定义给出，无需再在快照重复存一份。`SharedData["onDue"] = () => DoWork(...)` 无法表达这些条件，给闭包外面再套一个对象也不能使它可备份。订阅及资源释放按已有契约重绑，不能重新播放历史通知。

**已完成的类型化试点。** `SkillPipelineContext` 现以私有排序数组和可空提交标记持有施法预采样；`SharedData` 不再承载这两类运行时权威值。Runner v4 快照继续用原 `cast.prepared.parameter.*` / `cast.prepared.committed` 键、值类型及 Ordinal 排序单向导入/导出，版本不变；正式解码与本地恢复都会验证保留键的规范格式和类型，恢复先准备完整共享字典再修改目标。其他 Context 数据、参数采样时点、HFSM 来源与清理行为未扩展。Runner 还会在恢复于 PreCast 的条目上把已冻结样本带到后续 Cast 启动请求，修复阶段上下文重建时的值丢失。

验收现已覆盖 v4 往返及重复提交、输入与备份独立、非法类型/键预检和本地恢复零写入、直接写入保留键不能篡改权威状态，以及真实 Runner 在 PreCast 恢复后只用冻结值完成 Cast 提交。.NET 定向回归 292 项全部通过，未运行 Unity Player。此试点未清理 Context 中其它委托/清理依赖；将来若提供通用的受约束写入口，还要覆盖嵌套值，不能以反射扫描代替状态充分性验证。

### 7.1 本批具体决策项

用户回复“按推荐的下一批继续”，确认上文推荐的 MOBA Context 单项试点。范围只有 `SkillPipelineContext` 的预采样值和提交标记，不包含 Repeat 或通用 Pipeline 状态 API：

1. 增加私有的类型化预采样集合和 `ParametersCommitted` 标记，由 `Initialize`、`CommitPreparedParameters`、`CaptureRollbackState` 与 `RestoreRollbackState` 使用；不增加 Pipeline 公共接口，也不把它们上移到 Core。
2. `SharedData` 中的 `cast.prepared.parameter.*` 与 `cast.prepared.committed` 不再作为运行时权威写入。旧回滚载荷仍按现有 v4 的共享条目布局读写这两类值，读入时转换到类型化状态，写出时使用原键和原顺序规则，避免升级协议；迁移前后不得同时存在两份可写权威值。
3. 现有通用键 API、其他共享键、参数采样时点、HFSM 参数所有权、提交成功后的同步与清理顺序均保持不变。其他键继续按现有支持类型编解码。保留键在读取时校验键名和匹配的类型；错误载荷在预检拒绝。
4. 仅当确认不存在生产消费者依赖这两个旧键的通用读取后，才移除运行时对它们的字典投影；诊断和兼容读写若仍需要，必须是单向适配，不得把改写后的字典值再次作为权威输入。

仓内旧键写入者只有原 Context 初始化和提交入口；Runner 通过 `GetPreparedParameterSnapshots()` 读取显式状态，无其他按这两个旧键读值的生产调用方。普通运行仍允许访问字典，但直接放入保留键不会影响参数提交，显式捕获则报告键冲突。这样保留通用字典行为，同时避免歧义进入检查点。测试覆盖旧载荷、提交前和提交后恢复、重复提交、独立备份与非法输入零写入。
