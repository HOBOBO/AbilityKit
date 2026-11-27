# Triggering 入口清单与正式边界

本页是包内入口分级的权威清单，按能力和入口而不是目录名定级。它覆盖主线和已核查的旧入口，不是所有 `public` 成员的兼容承诺。未登记的入口既不自动成为正式 API，也不能按死代码删除。

## 首发稳定入口

| 能力 | 支持的成员与签名依赖 | 行为契约 |
| --- | --- | --- |
| 类型化事件 | `EventKey<TArgs>`（`AbilityKit.Core.Eventing`）、`EventBus` 的构造、`Publish`、`Subscribe`、`HasSubscribers`、`Flush`；`EventBusOptions`、`EEventDispatchMode`；`EventSchemaRegistry.Register<TArgs>`、`TryGetArgsType`、`TryGetName` | 默认立即派发；排队模式由宿主调用 `Flush`。事件 ID 与载荷类型由调用方保持一致，不把 Schema 注册视为订阅。 |
| 注册与派发 | `TriggerRunner<TCtx>` 的宿主装配构造、`Register<TArgs>`（`ITrigger<TArgs, TCtx>`）、`TriggerRunnerPlanExtensions.RegisterPlan<TArgs, TCtx>`（`TriggerPlan<TArgs>`）、返回的 `IDisposable` | 同一事件按阶段升序、优先级降序，同值按注册顺序。宿主持有并释放注册，最后一条正常释放时解除总线订阅。`RegisterPlan` 使用计划的阶段和优先级，只检查扁平动作的调度装配，不替代计划校验。运行器不替宿主推进时间；回调中的注册变更见下文。 |
| 计划与校验 | `TriggerPlanFactory.Create`、`When`、`WhenExpr`、`AddActions`，以及这些入口使用的计划数据类型（如 `ActionCallPlan`、`NumericValueRef`）；`TriggerPlanValidation.ValidateRuntimeBoundary` / `ThrowIfInvalidRuntimeBoundary` 及其 `ValidationContext<TArgs>`、`ValidationResult` | 先校验再注册；默认上下文只做可从结构确定的检查，引用核对需要宿主提供校验上下文。`PlannedTrigger<TArgs, TCtx>` 通常由注册入口装配，不要求新消费者自行构造。 |
| 显式扩展 | `ActionRegistry.Register<TDelegate>`、`FunctionRegistry.Register<TDelegate>`，及 `ActionId`、`FunctionId`、条件/动作委托；`ITriggerContextSource<TCtx>.GetContext` 与处理器读取 `ExecCtx<TCtx>.Context` | 宿主提供和管理上下文。现有构造签名所需的服务接口与选项随该签名使用；不承诺服务实现、池化策略、`ExecCtx` 所有构造方式，也不把程序集扫描作为默认接入。 |
| JSON 适配器 | `TriggerPlanJsonDatabase.Load(ITextLoader, string)` / `LoadFromJson`、`Records` / `TryGetRecordByTriggerId` / `TryGetPlanByTriggerId`、显式 `MergeFrom` | 成功加载替换存储，解析或转换失败保留原存储；正式环境允许宿主显式用规则覆盖聚合。宿主选择加载顺序、校验合并结果并持有注册句柄。JSON 格式不等于通用计划契约。 |

**按 ID 绑定或执行配置。** 调用一次 `TryGetRecordByTriggerId`，从同一条 `Record` 读取事件、`Scope`、`Plan` 和 `ExecutionRoot`。无执行根时走扁平计划路径（订阅时使用 `RegisterPlan`）；有执行根时按执行树路径装配，不回退到其他同 ID 记录的根。直接加载的 `Records` 保留原始行和顺序；同 ID 最后一条为生效记录。`TryGetExecutionRootByTriggerId` 仍保留直接加载时的历史行为，可能返回早先的非空根，不能与 `TryGetPlanByTriggerId` 拼装规则。

**按生效记录合并。** `MergeFrom` 对源库和目标库都按同 ID 的最后一条取值，但不修改源库。目标库合并后每个 ID 只有一条记录：已有 ID 保持首次出现的位置，新 ID 按在源库中首次出现的顺序追加。`replaceExisting` 只决定是否用源库记录覆盖目标库同 ID 的记录；覆盖替换整条记录，新记录无根就清除旧根。字符串和黑板初始化配置继续沿用各自的合并规则。

**可选加载诊断。** 宿主用 `TriggeringDiagnosticCollector.RecordLoadDiagnostic` 记录 `PlanLoad` 事件，按 `LoadErrors`、`LoadWarnings`、`LoadInfos` 读取累计计数；既有计数快照不受后续记录或清空影响。JSON 宿主可显式调用 `TriggerPlanJsonDiagnosticAdapter.RecordTo(collector, diagnostics)`，按原顺序保留来源、路径、消息与严重度文本；未知 JSON 严重度计为提示。收集器不是加载器或校验器，不改变严格／宽松加载的结果，也不是即时规则运行的前置依赖。

**显式校验已加载的计划。** 从候选库的同一条生效记录读取 `EventId`、`Plan` 和 `ExecutionRoot`，构造 `TriggerPlanEntry<object>`；诊断来源和位置由宿主提供。用 `TriggerPlanValidation.ValidateRuntimeBoundary(entries)` 查看结果，或用 `ThrowIfInvalidRuntimeBoundary(entries)` 拒绝错误。校验不改写数据库，也不自动回滚：若要保留当前生效库，先在独立候选库完成加载、覆盖和校验，通过后再由宿主切换。运行边界校验不隐式承接旧包装器的开发或 UGC 策略；需要更严格检查时显式装配相应校验器和上下文。

## 通用校验与宿主扩展

计划、校验和执行均不要求使用 JSON。下列入口接收已经构造的计划数据，不负责加载、注册或切换生效库。

| 入口 | 实际覆盖 | 接入定位 |
| --- | --- | --- |
| `TriggerPlanValidation.ValidateRuntimeBoundary` | 动作／条件引用检查、扁平动作结构和批量条目中的执行根结构。不启用图检查或静态限额。 | 正式运行边界；不能替代宿主准入。 |
| `CompositeTriggerValidator.CreateForDevelopment` | 在基础检查上加入循环、自触发、跨作用域检查和扁平计划限额。深度／节点／复杂度／动作数为 `50 / 500 / 200 / 100`。 | 受信开发内容排查；不代表不受信规则可以运行。 |
| `FlatTriggerPlanLimitsValidator` | 按显式构造参数检查每条计划的扁平动作、节点、谓词深度和复杂度，不读取上下文中的限额。 | 可单独调用或加入 `CompositeTriggerValidator`；不对执行根、规则集合或运行期开销计量。 |
| `CreateMinimal` / `CreateCycleDetectionOnly` | 前者组合循环和基础结构检查，后者只组装循环与自触发检查。 | 专项组合，不是正式运行边界的别名。 |

**上下文只提供引用目录。** `ValidationContext<TArgs>` 接收动作和函数 ID 集合，两类目录分别启用对应的引用检查；空集合或未提供均跳过该类 ID 核对，不跳过结构校验。集合由宿主持有，校验期间不得修改。开发诊断和扁平计划限额由显式选择的校验器决定，不由上下文切换。

这些预设遇到关键校验器返回错误时会提前停止；未报告后续问题不代表后续检查已通过。宿主可实现 `ITriggerValidator<TCtx>` 显式组合额外的计划诊断。

**首发不应承诺的能力：**

- **不是权限白名单。** 内建引用检查只核对扁平动作与谓词，不验证事件权限或执行树中的动作能力；空目录不表示拒绝全部。
- **不是完整事件环检查。** 预设中的多跳循环检测器没有接收动作到事件的映射，传入的 `eventMapper` 仅用于直接自触发检查，不能据此宣称已排除事件环或执行树发出的事件链。
- **不是确定性或运行隔离证明。** 通用校验不验证委托的确定性或副作用；使用确定性标记不等于证明委托可重演。

**UGC 属于包外扩展。** 核心包不定义 UGC 内容模型、能力目录、准入策略或运行预算。接入项目或独立扩展应在构造计划和执行树之前，对自己的内容模型限制输入体积、结构深度和允许的能力。审查后的内容经受信转换器映射为通用计划，再显式做运行边界检查和注册。宿主负责最终生效批次的校验与切换，并约束注册能力的权限、派发步数与调度资源；这些策略不进入核心包，也不是普通规则的前置依赖。

## 确定性边界

- **框架检查注册标记。** `ExecPolicy.RequireDeterministic` 启用计划动作绑定和注册条件函数求值时的 `isDeterministic` 检查。扁平动作首次成功绑定后缓存委托，不重新检查标记；执行树动作叶子每次绑定。宿主应在装配时选定策略；切换策略需重新装配实例，不把给已绑定实例传入更严格的上下文当成重新校验。
- **宿主保证可重演。** 标记是注册方声明，不是对委托实现的验证。框架不会审计自定义节点、数值函数或回调内部的时钟、I/O 和可变状态。重演需由宿主恢复会影响结果的状态，按相同次序提供输入、时间和随机取样。
- **随机分支不是例外开关。** `Random` 节点不会因启用此策略而被禁用。有可选分支并进入选择时，从 `ExecCtx.RandomSource` 取样一次；没有注入随机源则返回失败，不执行分支。宿主负责随机流状态，不由计划或调度进度代管。此契约不等于所有计划可跨平台逐位重演。

## 专项宿主入口

专项能力不是即时触发的前置依赖。纯事件通知可只用 `EventBus`；手写 `ITrigger` 或即时 `TriggerPlan` 不要求消费方装配调度器或推进时间，`TriggerRunner` 不隐式创建调度管理器。

首发优先验收两条路径：即时执行，以及宿主显式推进的执行树时间节点。包提供时间节点的登记和执行实现，不决定时间来源、世界更新阶段或回滚时机。扁平动作调度仍是独立专项入口；业务侧定时任务由宿主推进，到期可发布即时事件。

| 能力 | 支持边界 |
| --- | --- |
| 扁平计划动作 | 核心通过 `IFlatActionScheduler` 和 `IFlatActionScheduleGroup` 检查分组并登记槽位。选择默认 `ActionSchedulerManager` 时，宿主传入毫秒步长与可选 `ITriggerDispatcherContext`，并按需调用 `RemoveScheduler` 或 `Clear`；这些操作以及池均不属于核心登记接口。 |
| 执行树调度与回滚 | 时间节点通过 `ITriggerExecutionScheduler` 登记子树；宿主可持有 `TriggerExecutionScheduler`，显式调用 `Tick`、`CaptureProgress`、`ValidateProgress`、`RestoreProgress`。仅承诺在同会话、原任务定义仍在的情况下预检与静默恢复；不支持仅凭进度重建任务，不恢复扁平动作或宿主定时任务。详见[预测回滚边界](PredictionRollbackBoundary.md)。 |
| 黑板快照 | `BlackboardSnapshot` 和 `BlackboardSnapshotSet` 只承载数据；需要 JSON 的宿主显式使用 `BlackboardSnapshotJsonCodec.Serialize` 与 `DeserializeSnapshot` / `DeserializeSet`。解码不等于恢复校验；所有者成员变更仍需原有预检、授权范围和失败处理。此分离不移除包级 Newtonsoft 依赖。 |
| 加载与作者化 | `TriggerPlanDirectoryLoader` 仍有当前消费方，保留但不是默认入口；基于 `Type` 的 `RegisterPlan` 是动态绑定入口，不扩大为通用 AOT 承诺。`TriggerPlanAggregateCompiler` 归 `AbilityKit.Triggering.Authoring`，不属于运行时。 |

新接入可通过 Package Manager 导入 **Triggering Mainline**。源码位于 [`Samples~/FormalMainline/FormalTriggeringMainlineExample.cs`](../Samples~/FormalMainline/FormalTriggeringMainlineExample.cs)，不默认编入运行时程序集。

**扁平动作装配失败。** 类型化与动态 `RegisterPlan` 只检查是否已注入 `IFlatActionScheduler`；需要调度却未注入时，在新增订阅前抛出 `InvalidOperationException`，指出计划 `TriggerId`、首个需调度的动作索引及模式。经通用 `Register` 或直接调用 `PlannedTrigger.Execute` 时，运行时仍在任何扁平动作绑定与执行前检查缺失依赖。已注入的实现可在 `BindPlan` 检查整个动作列表和所有者，拒绝不支持的模式、策略或冲突分组；拒绝发生在任何动作调用或槽位登记之前，不消耗触发资格。实际使用执行根时不检查未使用的扁平列表。事件派发仍走已有失败通知；宿主负责推进所注入的实现。

**装配生命周期。** `TriggerPlan<TArgs>` 只读结构不等于深度不可变。从交给运行时起，不原地改计划、参数或替换活跃规则的注册表绑定。扁平动作首次成功绑定后复用缓存；执行树动作节点每次绑定，`RegistrationVersion` 不会让两条路径同步热更。需更新时由宿主结束旧注册及相关调度任务，再用新计划和已装配的注册表重建；这不限制启动装配时显式覆盖聚合。执行结果和通知的差异见[执行根基线](ExecutionRootOutcomeBaseline.md)。

**动作参数。** 委托元数须与计划一致。无参数使用 `new ActionCallPlan(id)` 和 `NamedAction0`；需要保留显式的位置常量零时，使用 `ActionCallPlan(ActionId, params double[])`，不要依赖以默认值推断元数的 `NumericValueRef` 可选参数构造。

**动作失败边界。** 直接调用 `PlannedTrigger.Execute` 时，动作抛出的异常在退出失败 Scope 后向调用方传播。由 `TriggerRunner` 派发时，运行器捕获该异常，不把失败计为成功执行，也不把原动作异常传回 `Publish` 调用方。每次执行只尝试一次 `Executed` Trace；动作抛出时，再分别用原异常消息通知生命周期和观察器的 `OnActionFailed`。这三处诊断各自隔离，诊断异常仅记日志，不改变动作结果、不阻断其余诊断或后续硬停止判定。这不是 `EventBus` 对任意订阅回调的异常保证。

**条件求值异常边界。** `Evaluate` 抛出异常时，`TriggerRunner` 结束当前运行器的派发，不执行当前动作或后续规则，也不因此停止 `EventBus` 的其他订阅；该异常不计入短路计数。运行器只尝试一次失败的 `Evaluated` Trace，并记录原异常，依次通知生命周期和观察器的 `OnConditionFailed`、携带原异常消息的 `OnActionFailed`（名称为 `Evaluate`），以及条件失败 Cue。诊断及 Cue 上下文构建异常各自记日志，不阻断其余诊断及派发收尾。正常返回 `false` 仍走独立的条件拒绝策略，仅 `Strict` 停止当前运行器派发。

**Trace 作用域。** `TriggerRunner` 的每次派发独立持有 `BeginTrace` 返回的作用域，嵌套派发不覆盖外层作用域。开启失败仅记日志，本次派发不再记录或结束 Trace；成功返回默认 `TraceScope` 仍视为已开启。已开启时，单条 `RecordTrigger` 失败不阻断派发；`EndTrace` 始终尝试一次，其异常不会阻断正常收尾或覆盖原派发异常。这不改变 Runner 的线程使用约束，也不为非 Trace 回调提供额外兜底。

**短路通知边界。** `TriggerRunner` 在决定跳过或停止后，依次通知生命周期、观察器、Trace 和 Cue。短路通知及其 Cue 上下文构建失败时通过日志报告，仍尝试其余短路通知。优先级软过滤继续派发未被过滤的触发器，硬停止和严格条件短路仍结束当前运行器派发；不因通知抛出异常而改变上述决策。这些隔离约定不覆盖未列明的回调，也不撤销回调已经产生的副作用。

**正常派发回调边界。** `OnEventDispatched` 在 Trace 收尾后报告已确定的计数；该通知抛异常只记日志，不会阻断原本应执行的订阅，也不会解除已经生效的硬停止。派发若因其他未隔离异常退出，不补发完成通知。除已列明的诊断点外，Lifecycle 仍是同步扩展钩子，Observer 可接触 `ExecCtx.Control`，Cue 可以调用宿主表现链；不把它们一概视为无副作用的诊断，不改变其既有异常传播。以上约定仅适用于普通 `TriggerRunner`。

**注册通知边界。** 普通 `TriggerRunner` 在插入注册条目后同步通知 `OnRegistered`，回调失败只记日志，调用方仍取得可释放的句柄。释放句柄先移除本条规则，再通知 `OnUnregistered`；即使通知抛异常，也会检查当前事件是否仍有活动规则。没有活动规则才解除订阅。重复释放不重复通知。这不隔离总线自身订阅或退订的失败。

**派发中的注册变更。** 普通 `TriggerRunner` 在 `OnEventDispatching` 之前确定本次派发的候选项及其顺序。之后新增的注册不加入本次派发；在轮到之前释放的注册直接跳过，不计作规则短路。当前已进入的规则按原有求值、执行和短路契约继续，释放句柄不撤销本次执行。每次派发独立获取候选项，嵌套派发可见发起时已经生效的增删，不会改变外层候选顺序。同一触发器实例的多条注册独立生效。本约定不改变事件总线的订阅遍历、线程使用约束或兼容运行器行为。

## 兼容与内部实现：保留，不新增接入依赖

| 入口 | 现有证据 | 迁移边界 |
| --- | --- | --- |
| `.NET` 抽象类型转发（`src/AbilityKit.Triggering/AbstractionTypeForwarders.cs`） | `AbstractionAssemblyOwnershipTests` 检查唯一类型定义及旧程序集限定名解析。 | 新代码引用抽象所属程序集；转发不是空壳，不随目录整理移除。 |
| `ITriggerContext`、`Runtime.Context.TriggerContext`、`ActionContext`（`Runtime/Context`）；内部 `ExecCtxAdapter` | `ActionContext`、`ExecCtxAdapter` 仍连接包内旧执行链；独立样例包的同名 `ActionContext` 不是本包类型。 | 新接入使用宿主上下文与 `ExecCtx<TCtx>`。服务聚合上下文与旧执行链单独复核。 |
| `Runtime.TriggerContext`（`Runtime/Core/Context`） | 帧、序号值类型，与 `Runtime.Context.TriggerContext` 服务聚合类不是同一类型。`SkillCore`、`BattleRuntime` 已把本类型的用法迁移到各自的私有上下文；两个完整示例输出与迁移前一致。 | 新接入不必引用此帧数据结构。保留公开类型身份，待核实已发布消费者后再决定下线。 |
| `ITriggerDispatcherContext`、`TriggerPredicate<TArgs>`（`RuntimeServices/Dispatcher`）及 `Runtime/Plans/Execution` 中的执行助手 | 动作调度、计划注册和执行链仍使用共享上下文与条件委托；主线测试覆盖立即、延迟和周期计划。 | 正式调度契约与旧派发器分文件，保留公开类型身份。 |
| `Runtime/RuntimeServices/Continuous`、Blackboard 恢复接口 | `.NET` 的 `ContinuousExecutorRegistryTests`、`OwnerBlackboardMembershipRestoreTests` 覆盖状态和生命周期。 | 不纳入首轮下线；没有替代及恢复验证之前，不标为临时实现。 |
| `Runtime/Compatibility` 清单与 `Runtime/Validation/Compatibility` 门面 | 编辑器诊断直接读取 `RootRuntimeCompatibilityCatalog`；`RuntimeCompatibilityCatalogTests` 验证旧门面委托到同一份清单。未找到旧门面的包外源码调用。 | 根目录扫描需保留；旧门面可单独核对外部兼容承诺。空条目不代表整个包没有兼容代码。 |

## 早期通用调度：已移除

`Runtime.Schedule` 的 `Simple` / `Grouped` 管理器、策略、数据、效果执行器和 Behavior 适配器构成独立调用链。仓库源码核对未发现正式调用方，独立样例原先只展示旧接口文本。首发前一并移除该组及其专用预热、适配器和临时集合池，动作池保持原有配置与用法。未核对发布文件。

旧实现同时管理计时、分组生命周期和效果终结，不再作为宿主的第二套调度入口。按用途选用扁平计划的 `ActionScheduler` 或执行树的 `ITriggerExecutionScheduler`；它们不是旧分组、效果或终结语义的一对一替代。保留正式的 `Runtime.Config.EScheduleMode`，以及不依赖已退役适配器的 Behavior 实现。

## 旧独立业务调度：已移除

`Runtime.RuleScheduler` 下的 `RuleSchedulerRegistry`、默认驱动和专用模型，以及 `RuleSchedulePlanValidator` 在首发前移除。源码核对仅发现两处示例直接装配，未发现 MOBA 等业务运行时使用。延迟示例收口到 `com.abilitykit.samples` 的 `SchedulerExample`，展示宿主定时器到期后发布即时事件；Triggering 不依赖 Timer。

宿主定时器仅替代示例的一次性延迟，不保证旧驱动的逐条暂停相位、成功效果计数、补跑策略或同步中断通知。若业务需要这些语义，应由宿主或包外扩展明确建模；不将 `WhileActive` 视为业务对象的生命周期，也不把旧进度交给执行树恢复。

## 旧实例注册与同步：已移除

`TriggerRegistry` 管理旧配置及实例 ID；`TriggerInstanceManager` 和 `SyncedTriggerExecutor` 各自维护独立的实例索引。仓库源码仅找到该链自身和专属兼容测试，受检非发布 Unity 源资产未发现相关 GUID 引用。首发前同时移除旧实例、快照、同步服务、注册接口、专属测试和实例键集合池。

旧恢复必须先有已注册实例，只恢复实例进度和浅拷贝的数据映射，不重建 Behavior 执行状态或外部依赖；旧同步服务的通知和快照记录也不构成可重演契约。`TriggerRunner.RegisterPlan` 只管理事件订阅，并非旧实例查找或恢复接口的替代。保留执行树的进度与重绑契约、黑板快照、`ActionRegistry` 和 `FunctionRegistry`，不改变 MOBA 装配。

## 旧数据库入口：已移除

`ExecutableTriggerDatabase` 独立反序列化执行器配置并维护触发器及字符串索引，重复承担了正式计划加载的职责。它直接修改现有存储，记录构造失败可能留下部分数据，输入没有字符串表时也可能留下旧字符串；不宜作为另一条正式加载入口。

受检的 MOBA、其他示例、服务端、工具和测试源码未发现消费旧数据库及其配套的 `TriggerRecord`、`ITextLoader`、`ICueFactory`、`NullCueFactory`、`TriggerDatabaseDto` 和 `TriggerPlanDto`。这些类型随旧库一并移除，不保留别名。`TriggerPlanJsonDatabase` 及其同名嵌套类型不受影响；新接入使用正式计划加载或直接构造计划，不承诺旧 DTO 与正式 JSON 格式互换。

`ValidatingTriggerPlanJsonDatabase` 和 `TriggerValidationOptions` 也已移除。受检源码仅有包内测试使用；执行根诊断已迁到正式的显式校验入口。旧包装器先写入内部数据库，后校验并按选项报告，失败不回滚；不保留该隐式编排行为。正式 JSON 库和宿主启动校验不变。

## 旧装饰器与空调度控制器：已移除

受检仓库源码中，`DecoratorBuilder` 调用 `DecoratorRegistry`，旧 `ScheduledExecutor` 是装饰器契约唯一的组外代码引用；未发现业务注册、配置构建、Behavior 工厂或正式计划使用该链。装饰器脚本和目录的六个 GUID 仅见于各自的 `.meta` 声明。

旧注册表使用进程级可变映射与反射构造；`ScheduledExecutor` 的控制器工厂固定返回已完成的空控制器，不提供实际的定时推进。首发前移除装饰器组、旧调度执行器和空控制器契约。保留 `ExecutableExecutor`、配置转换仍引用的 `ScheduledExecutableFactory.Wrap*` 和相应数据包装器；正式执行树调度与持续对象生命周期均不变。

## 其他非主线入口与下线候选

| 入口 | 现状 | 删除前需要的证据 |
| --- | --- | --- |
| `ITriggerDispatcher` / `TriggerDispatcherBase`、`DispatcherRegistration<TArgs>`、`EDispatcherType`、`TriggerExecutor<TArgs>`、`EventBusDispatcherContext` | 已从包源码移除。MOBA、示例及其他受检源码未发现实际调用。 | 使用类型化 `EventBus` + `TriggerRunner` 注册和派发，宿主持有订阅句柄。`ITriggerDispatcherContext` 和 `TriggerPredicate<TArgs>` 仍是主线依赖；`ActionSchedulerManager.Update` 只推进动作调度，不承担事件派发。 |
| `TriggerPlanJsonDatabase.RegisterAll` 的两个重载 | 已从包源码移除。相关调用仅来自专测旧入口的测试；数据库加载、查询、合并均保留。 | 旧入口不返回句柄，中途失败不撤销前项，并忽略执行根。改用宿主显式读取、校验：扁平计划用 `RegisterPlan`，带执行根的计划按执行树路径装配再注册；宿主持有注册句柄。 |
| `RpnNumericExprParserExtensions` / `AccessorRegistry` | 直接调用只在包内测试及扫描链内；扩展首次使用扫描已加载程序集，`RescanAccessors` 可重扫。`RpnNumericExprParserExtensionsTests` 覆盖稳定 ID、已声明域通过和未知域拒绝。 | `SkillCore` 已示范在宿主注册 `combat` 域，并用 `RpnNumericExprParser.Parse` 的显式回调拒绝未知域；`.NET` 的 `RpnNumericExprParserTests` 验证已声明域 ID 一致及未知域拒绝。扫描入口保留，不能用默认解析替换严格解析。 |
| `HierarchicalTriggerRunner<TCtx>` / `HierarchicalOptions` | 已在首发前成组移除。受检自有源码、文本反射入口和 Unity 源资产中未发现实际调用，脚本与目录 GUID 只命中原有声明。原示例已归档。 | 普通 `TriggerRunner` 不提供父子级联派发。接入方如需要父子作用域，应在宿主定义顺序、传播与短路策略，再复用通用事件和规则执行入口。 |
| `Runtime/Triggering/Contracts/ILegacyTriggerExecutor.cs` | **已清理**：文件仅含空命名空间，不定义类型；未发现源码或 Unity GUID 引用。 | 与原 `.meta` 一并删除，不建立替代占位。 |

## 扁平动作调度的推进边界

- 同一个管理器内，用不同的 `TriggerId` 隔离不同计划实例。进入混合动作执行时，计划先取得分组所有权；分组已归另一计划实例时拒绝本次执行，不执行即时动作、不替换旧槽，也不消耗本次触发资格。分组仍按 `TriggerId` 管理，不要对计划使用的分组混用底层动作登记 API。
- 事件注册句柄只管理订阅。宿主卸载或替换计划时，先退订，再在推进之外用 `RemoveScheduler(triggerId)` 清理其分组；整实例卸载时可用 `Clear`。分组移除后，其他计划实例才可接管该 ID。仅需让现有动作执行完毕时，可以只退订，由管理器在分组变空后回收。
- 宿主通过管理器串行推进，不直接获取或推进分组。管理器推进期间，不能重入同一管理器的 `Update`，也不能 `Clear`、`RemoveScheduler` 或新建分组。
- 分组推进期间，该分组拒绝登记、替换和销毁。分组内部的直接推进路径继续受重入检查约束，不是宿主入口。
- 上述拒绝在调用点抛出 `InvalidOperationException`，发生在结构变更、旧槽中断和登记路径租借执行器之前，不会自动延后执行。若异常从默认动作委托抛出，仍按既有异常隔离将当前动作置为失败，不等于外层 `Update` 会向宿主抛出。需要结构变更时，由宿主在推进返回后执行。

**推进失败隔离。** 单个动作推进抛出异常时，当前实例标为 `Failed`，原始原因保存在 `InterruptReason`。若失败清理调用执行器的 `Cancel` 时也抛出异常，追加清理原因，不覆盖原始原因，也不中断同帧中尚未推进的动作和分组。失败实例仍在下一次正常推进时回收。这不改变宿主上下文读取、显式中断或同槽替换的异常传播规则。

**延迟上下文。** 扁平计划的延迟、周期动作及其条件使用同一次成功登记的事件参数和 `ExecCtx<TCtx>`（包括该次的 `Policy` 和 `Control`）；被执行资格拒绝的后续触发不会改写已登记实例。宿主 `Update` 的毫秒步长和本次 `ActionExecutionContext` 仍用于调度层，不替换计划回调的登记上下文。登记只保存值和引用，不复制其中的可变对象，也不是回滚快照。

**同槽替换。** 新实例准备失败时，保留旧实例、槽位索引和已累计的进度；失败路径归还登记过程中租借的执行器。新实例登记后才中断和回收旧实例；如果旧实例清理抛出异常，仍向调用方报告异常，但不撤销已登记的新实例。替换期间拒绝对该分组的结构重入。登记按槽生效，后续槽位失败不撤销之前已成功登记的槽位。

**销毁与归池。** `Dispose`、`RemoveScheduler` 和 `Clear` 只对仍活跃且允许中断的动作调用 `Cancel`，已终态或不可中断的动作只移除，不再发取消回调。有池、无池采用同一业务行为，归池只清理字段和归还资源。销毁的分组不允许重入推进或修改实例结构；管理器移除分组期间也拒绝重入推进或修改分组结构。某项清理失败时继续清理其余实例及目标分组，清除索引后再报告异常：单个异常原样抛出，多个异常使用 `AggregateException`。主动中断及正常推进的终态回收时机不变。

## 包外调度扩展

依赖只从扩展指向 Triggering。业务定时、暂停策略、计数和生命周期可由包外组件维护，到期通过 `EventBus.Publish` 驱动即时规则。需要调度执行树子节点时，扩展可实现 `ITriggerExecutionScheduler`，由宿主通过 `ExecCtx<TCtx>` 装配；宿主仍负责推进、清理和恢复。该接口不要求使用包内默认调度器，也不承诺各驱动的快照可互换。

扁平计划可由独立程序集实现 `IFlatActionScheduler`，通过 `TriggerRunner<TCtx>` 或 `ExecCtx<TCtx>` 的 `flatActionScheduler:` 参数注入；上下文转发使用 `ExecCtx<TCtx>.FlatActionScheduler`。这是第一稳定版前的源码签名调整，原来传入的内置 `ActionSchedulerManager` 仍可作为该接口注入。核心不要求扩展引用具体管理器或池，也不为扁平动作与执行树统一时钟。

扩展按 `TriggerId` 和计划所有者绑定分组，按计划索引登记或替换槽位；`BindPlan` 只检查并取得分组，`RegisterOrReplace` 只登记，不同步执行动作。由宿主推进，到期用登记的载荷及调度上下文先求条件、再调用动作。释放注册句柄只停止订阅，不清空已登记的动作，清理由宿主负责。当前已验证包外实现可补回默认执行策略的一次性 `Delayed`；不能据此承诺周期、持续、重试或调度快照可互换。默认实现及其专用 `ActionSchedulerPools` 位于可选包 `com.abilitykit.triggering.actionscheduling`，使用方显式引用 `AbilityKit.Triggering.ActionScheduling`；核心不依赖该包，核心接口也不包含池。

## 调度计数核对

默认适配器公开 `SchedulerCount` 和 `TotalActionCount`，分别用于观察当前分组数和实例持有量。后者包含尚未回收的终态实例，不代表正在执行的数量。两种计数均不是 `IFlatActionScheduler` 的要求。

## `Queued` 执行策略核对

**旧动作同步已退役。** `TriggerWaitHandle` 未置位时不等待而是抛错；`SynchronizedActionExecutor` 未置位时跳过，不登记后续唤醒；`TriggerMutex` 只在单个句柄内记录所有权。源码核对未发现这组三个类型的实际消费者，首发源码已成组移除。保留的 `Queued` 不提供信号等待或互斥所有权，也不会把动作积入队列；事件总线的队列派发是另一项独立能力。

- **兼容保留，不建议新计划使用。** `EActionExecutionPolicy.Queued` 的值仍为 `1`，JSON 仍接受名称和数字，现有校验和执行行为不变。既有仓库核对未找到业务侧显式配置；当前测试覆盖计划校验和调度防重入。本批未解析发布文件。
- **实现只有单实例防重入。** 调度路径为每个动作实例创建独立的 `QueuedActionExecutor`；标记在同步调用内部执行器期间置位，返回后复位。没有跨实例队列或排序逻辑，`QueuePriority` 不参与调度。`Immediate` 动作仍走立即执行路径，不会装配这个包装器。
- **核对证据。** 有池和无池的 .NET 探针均确认：同一触发器连续发布两次时，延迟动作的同计划槽位会替换而非积压；同一调度器两条动作同帧到期时，后登记槽位先运行；同一包装器重入会被跳过，另一包装器仍可执行。该顺序是现有遍历结果，不是 `Queued` 的 FIFO 或优先级承诺。
- **不隐式迁移。** 既有计划无需改写，不把该策略自动替换为 `Immediate`。也不要把事件总线的排队派发视为计划内动作排序的替代方案。

## ExecutableRegistry 下线记录

核查日期：2026-11-21。Git 基线为 `bcc9cea4a`，源码与 DLL 核查使用当时可见的工作区和本地构建产物。

| 证据 | 结果 |
| --- | --- |
| 公开身份 | 三个类型均位于 `AbilityKit.Triggering.Runtime.Executable` 命名空间，由 `AbilityKit.Triggering` 公开导出。检查的 Unity DLL 为 `0.0.0.0`，.NET DLL 为 `1.0.0.0`；程序集版本与 UPM 声明版本 `0.1.0` 都不是发行凭证。 |
| 发布记录 | 当前及本地可达历史的发布清单均未列入 Triggering；本地与 `origin` 均无 Git 标签，GitHub Releases 列表为空。OpenUPM 精确包名 `com.abilitykit.triggering` 与 NuGet.org 精确 ID `AbilityKit.Triggering` 的注册表查询均返回 HTTP 404。只能确认这些渠道未查到发行，不能排除 Git 源码安装、私有源和离线制品。 |
| 源码与 Unity 资产 | 在 `Unity/Packages`、`Unity/Assets`、`src`、`tools`、`Server` 未发现三个类型的包外引用或该脚本 GUID 的资产引用。源码中的登记逻辑和框架通用扫描接口不等同于已发现的消费方。 |
| 本地二进制 | 读取上述目录、Unity 主工程及隔离检查工程的 `Library/ScriptAssemblies` 中可见托管程序集的 PE 元数据，共检查 22,309 个程序集文件（含构建、引用及输出副本）。只发现类型定义，未发现其他程序集的 `TypeRef` 引用。此检查不覆盖动态拼接反射名、序列化字符串或仓库外产物。 |
| Git 源码分发 | `origin` 是公开仓库，GitHub API 当日列出 58 个公开 fork。仓库内受检的安装配置和文档未发现该仓库的 UPM Git 路径依赖；fork 数量既不能证明有人调用旧注册表，也不能排除源码消费者。 |
| 私有源与离线包 | 当前 Unity 项目的 scoped registry 仅有 OpenUPM；本机启用的 NuGet 源为 NuGet.org 和 SDK 离线源。Unity UPM 缓存、NuGet 全局缓存和 SDK 离线源未找到对应包；工作区受检目录也未找到以 Triggering 命名的发布归档。不覆盖其他机器、未登记的分发渠道或整套应用归档。 |

**消费链复查。** MOBA 的 `WorldModulesStage` 装配 `ActionRegistry`、`FunctionRegistry` 和 `TriggerRunner<IWorldResolver>`，`TriggerPlansStage` 加载配置，`PlanTriggeringStage` 校验并绑定计划；Console 提供配置输入，不经旧注册表。`SkillCore`、`BattleRuntime`、包内主线及队列样例同样使用显式注册。旧配置转换由独立的 `ExecutableConverterStrategyRegistry` 承担，也不通过此单例。

**设计判断。** 用 ID 查找工厂本身没有问题；这里把可变的全局单例、动作和条件工厂、元数据与反射构造绑在一起，无法按宿主隔离。重复 ID 或名称会静默覆盖，ID 改名时旧名称映射仍可能指向新工厂；公开的可变描述符也能绕过登记逻辑。由于正式链路已经使用宿主装配的注册表，旧单例没有独立消费者，不值得再修出一套平行入口。

**处置。** 按仓库内无消费者的批准条件，首发前移除 `ExecutableRegistry`、`ExecutableDescriptor`、`ConditionDescriptor` 及对应脚本 GUID。对直接调用这三个类型的仓库外代码，这是破坏性变更；正式入口不是旧数字 ID 工厂的一对一替代。保留现有执行器、条件、类型 ID、特性、转换策略及计划注册链。正式环境目录覆盖聚合的策略不变。

**验收。** Unity 与 .NET 运行时程序集的公开类型集合均只减少上述三个类型。Triggering 的 .NET 测试 41 项、MOBA Triggering 专项测试 67 项通过；`SkillCore` 与 `BattleRuntime` 示例运行完成。隔离 Unity 编辑器的 `CheckRuntimeBatch` 通过，正式 Authoring Modules 的重建结果与现有 Unity、Console 聚合文件一致，未重新发布制品。

## 核查范围与迁移顺序

- 搜索范围为当前工作区的 `Unity/Packages`、`Unity/Assets`、`src`、`tools`、`Server`。核对类型名、部分方法名与特性名、文本注册及配置，并在包外文本资产中检索候选及关联脚本的 24 个 GUID，未发现 GUID 引用。静态搜索不能排除动态拼接反射名、预编译程序集或仓库外消费者；本次未确认外部发布制品和版本承诺，不能据此批准删除公开 API。下线流程见 [`LegacyMigrationPolicy.md`](LegacyMigrationPolicy.md)。
- 两个 `.NET` 示例的上下文迁移和显式 RPN 域校验回归已落地。`TriggeringTestHarness<TCtx>` 和 `TriggerPlanAggregateCompiler` 已移出运行时，引用迁移见[源码导航](SourceLayout.md#测试支持与工具归属)。`ExecutableRegistry` 已按单独范围下线；有快照、层级作用域或校验时序差异的入口仍后置。
- 早期核查记录（非当前验收）：Triggering `.NET` 测试通过 38 项；Unity 2022.3.62f1 隔离工程中，兼容清单与旧入口扫描、注册表、Attribute 解析和数据库校验等 5 个测试类共 40 项通过；主线测试另有 10 项通过、1 项失败。单独复跑 `PlannedTrigger_ActionScopeObserver_ExitsOnceAsFailedWhenActionThrows` 仍失败：用例期望 `Publish` 抛出 `InvalidOperationException`，实际未向调用端抛出。当时未调整异常策略或测试断言。
