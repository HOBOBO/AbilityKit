# Triggering 入口清单与正式边界

本页是包内入口分级的权威清单，按能力和入口而不是目录名定级。它覆盖主线和已核查的旧入口，不是所有 `public` 成员的兼容承诺。未登记的入口既不自动成为正式 API，也不能按死代码删除。

## 首发稳定入口

| 能力 | 支持的成员与签名依赖 | 行为契约 |
| --- | --- | --- |
| 类型化事件 | `EventKey<TArgs>`（`AbilityKit.Core.Eventing`）、`EventBus` 的构造、`Publish`、`Subscribe`、`HasSubscribers`、`Flush`；`EventBusOptions`、`EEventDispatchMode`；`EventSchemaRegistry.Register<TArgs>`、`TryGetArgsType`、`TryGetName` | 默认立即派发；排队模式由宿主调用 `Flush`。事件 ID 与载荷类型由调用方保持一致，不把 Schema 注册视为订阅。 |
| 注册与派发 | `TriggerRunner<TCtx>` 的宿主装配构造、`Register<TArgs>`（`ITrigger<TArgs, TCtx>`）、`TriggerRunnerPlanExtensions.RegisterPlan<TArgs, TCtx>`（`TriggerPlan<TArgs>`）、返回的 `IDisposable` | 同一事件按阶段升序、优先级降序，同值按注册顺序。宿主持有并释放注册，最后一条正常释放时解除总线订阅。`RegisterPlan` 使用计划的阶段和优先级，只检查扁平动作的调度装配，不替代计划校验。运行器不替宿主推进时间，不承诺回调中修改活动注册时的遍历结果。 |
| 计划与校验 | `TriggerPlanFactory.Create`、`When`、`WhenExpr`、`AddActions`，以及这些入口使用的计划数据类型（如 `ActionCallPlan`、`NumericValueRef`）；`TriggerPlanValidation.ValidateRuntimeBoundary` / `ThrowIfInvalidRuntimeBoundary` 及其 `ValidationContext<TArgs>`、`ValidationResult` | 先校验再注册；默认上下文只做可从结构确定的检查，引用核对需要宿主提供校验上下文。`PlannedTrigger<TArgs, TCtx>` 通常由注册入口装配，不要求新消费者自行构造。 |
| 显式扩展 | `ActionRegistry.Register<TDelegate>`、`FunctionRegistry.Register<TDelegate>`，及 `ActionId`、`FunctionId`、条件/动作委托；`ITriggerContextSource<TCtx>.GetContext` 与处理器读取 `ExecCtx<TCtx>.Context` | 宿主提供和管理上下文。现有构造签名所需的服务接口与选项随该签名使用；不承诺服务实现、池化策略、`ExecCtx` 所有构造方式，也不把程序集扫描作为默认接入。 |
| JSON 适配器 | `TriggerPlanJsonDatabase.Load(ITextLoader, string)` / `LoadFromJson`、`Records` / `TryGetRecordByTriggerId` / `TryGetPlanByTriggerId` / `TryGetExecutionRootByTriggerId`、显式 `MergeFrom` | 成功加载替换存储，解析或转换失败保留原存储；聚合覆盖由宿主明确决定。目录发现、加载顺序和注册生命周期归宿主。需要独立释放订阅时，读取计划、校验后调用带句柄的 `RegisterPlan`，不要用不返回句柄的 `RegisterAll`。JSON 格式不等于通用计划契约。 |

## 专项宿主入口

专项能力不是即时触发的前置依赖。纯事件通知可只用 `EventBus`；手写 `ITrigger` 或即时 `TriggerPlan` 不要求消费方装配调度器或推进时间，`TriggerRunner` 不隐式创建调度管理器。

| 能力 | 支持边界 |
| --- | --- |
| 计划内动作 | `ActionSchedulerManager.Update`、`InterruptTriggerActions`、`Clear`，以及使用宿主调度上下文的 `ActionScheduler`。宿主传入毫秒步长与 `ITriggerDispatcherContext`；不把池化构造、实例枚举或内部执行器当作接入契约。 |
| 业务时间规则 | `RuleSchedulerRegistry.Schedule`、`Update`、`Pause`、`Resume`、`Interrupt`、`Cancel`、`Clear`；`RuleSchedulePlan`、`RuleScheduleHandle`、`IRuleScheduleEffect`。默认驱动由注册表装配，宿主推进时间并管理领域生命周期；自定义驱动不属于默认主线。 |
| 执行树调度与回滚 | `TriggerExecutionScheduler.Tick`、`CaptureProgress`、`ValidateProgress`、`RestoreProgress`。仅承诺在同会话、原任务定义仍在的情况下预检与静默恢复；不支持仅凭进度重建任务，不为另外两条调度链背书。详见[预测回滚边界](PredictionRollbackBoundary.md)。 |
| 加载与作者化 | `TriggerPlanDirectoryLoader` 仍有当前消费方，保留但不是默认入口；基于 `Type` 的 `RegisterPlan` 是动态绑定入口，不扩大为通用 AOT 承诺。`TriggerPlanAggregateCompiler` 归 `AbilityKit.Triggering.Authoring`，不属于运行时。 |

新接入可通过 Package Manager 导入 **Triggering Mainline**。源码位于 [`Samples~/FormalMainline/FormalTriggeringMainlineExample.cs`](../Samples~/FormalMainline/FormalTriggeringMainlineExample.cs)，不默认编入运行时程序集。定时规则另用 **Triggering Rule Scheduling**，不需要导入才能使用主线样例。

**扁平动作装配失败。** 类型化与动态 `RegisterPlan` 遇到非即时动作且运行器未装配 `ActionSchedulerManager` 时，在新增事件订阅前抛出 `InvalidOperationException`，指出计划 `TriggerId`、首个需调度的动作索引及模式。经通用 `Register` 或直接调用 `PlannedTrigger.Execute` 时，同一检查在任何扁平动作绑定与执行前进行，不执行部分计划或消耗触发资格；事件派发仍走已有失败通知。实际使用执行根时不检查未使用的扁平列表。显式装配后，宿主推进同一实例；包不检查宿主是否漏调 `Update`。

**动作参数。** 委托元数须与计划一致。无参数使用 `new ActionCallPlan(id)` 和 `NamedAction0`；需要保留显式的位置常量零时，使用 `ActionCallPlan(ActionId, params double[])`，不要依赖以默认值推断元数的 `NumericValueRef` 可选参数构造。

**动作失败边界。** 直接调用 `PlannedTrigger.Execute` 时，动作抛出的异常在退出失败 Scope 后向调用方传播。由 `TriggerRunner` 派发时，运行器捕获该异常并通过 `OnActionFailed` 通知生命周期和观察器，不把失败计为成功执行，也不把原动作异常传回 `Publish` 调用方。这不是 `EventBus` 对任意订阅回调的异常保证。

## 兼容与内部实现：保留，不新增接入依赖

| 入口 | 现有证据 | 迁移边界 |
| --- | --- | --- |
| `.NET` 抽象类型转发（`src/AbilityKit.Triggering/AbstractionTypeForwarders.cs`） | `AbstractionAssemblyOwnershipTests` 检查唯一类型定义及旧程序集限定名解析。 | 新代码引用抽象所属程序集；转发不是空壳，不随目录整理移除。 |
| `ITriggerContext`、`Runtime.Context.TriggerContext`、`ActionContext`（`Runtime/Context`）；内部 `ExecCtxAdapter` | `ActionContext`、`ExecCtxAdapter` 仍连接包内旧执行链；独立样例包的同名 `ActionContext` 不是本包类型。 | 新接入使用宿主上下文与 `ExecCtx<TCtx>`。服务聚合上下文与旧执行链单独复核。 |
| `Runtime.TriggerContext`（`Runtime/Core/Context`） | 帧、序号值类型，与 `Runtime.Context.TriggerContext` 服务聚合类不是同一类型。`SkillCore`、`BattleRuntime` 已把本类型的用法迁移到各自的私有上下文；两个完整示例输出与迁移前一致。 | 新接入不必引用此帧数据结构。保留公开类型身份，待核实已发布消费者后再决定下线。 |
| `Runtime.Schedule` 的早期调度与 Behavior 适配（`Runtime/Scheduling` 的 `Simple`、`Grouped`、`Strategies` 等） | 未发现 `SimpleScheduleManager` 或 `GroupedScheduleManager` 的包外源码调用；独立样例包中的相关名称是展示字符串。`TriggerRunnerMainlineTests` 的计划调度用例创建的是 `ActionSchedulerManager`，不证明旧管理器的推进、终结和取消行为。池化、旧执行器与调度数据仍有包内关联。 | 独立核查旧管理器和 Behavior；计划内动作比较 `ActionScheduler`，业务定时规则比较 `RuleScheduler`，两者均不是分组、效果和终结语义的直接替换。不把 `Runtime.Schedule` 当作已删除的 `Runtime.Scheduler`。 |
| `ITriggerDispatcherContext`、`TriggerPredicate<TArgs>`（`RuntimeServices/Dispatcher`）及 `Runtime/Plans/Execution` 中的执行助手 | 动作调度、计划注册和执行链仍使用共享上下文与条件委托；主线测试覆盖立即、延迟和周期计划。 | 正式调度契约与旧派发器分文件，保留公开类型身份。 |
| `Runtime/RuntimeServices/Continuous`、Blackboard 恢复接口 | `.NET` 的 `ContinuousExecutorRegistryTests`、`OwnerBlackboardMembershipRestoreTests` 覆盖状态和生命周期。 | 不纳入首轮下线；没有替代及恢复验证之前，不标为临时实现。 |
| `Runtime/Compatibility` 清单与 `Runtime/Validation/Compatibility` 门面 | 编辑器诊断直接读取 `RootRuntimeCompatibilityCatalog`；`RuntimeCompatibilityCatalogTests` 验证旧门面委托到同一份清单。未找到旧门面的包外源码调用。 | 根目录扫描需保留；旧门面可单独核对外部兼容承诺。空条目不代表整个包没有兼容代码。 |

## 非主线与下线候选：尚未批准删除

| 入口 | 现状 | 删除前需要的证据 |
| --- | --- | --- |
| `DecoratorRegistry` / `DecoratorBuilder`、`SyncedTriggerExecutor` | 未发现包外源码调用。`DecoratorBuilder` 使用装饰器注册表；同步执行器连接 Behavior、`TriggerInstance` 与 `ITriggerSyncService`。未找到针对这两个入口的专项运行回归。 | 分别评估，不整目录删除。计划执行节点和 MOBA 帧同步都不能视为同步实例及其状态捕获的直接替代。 |
| `ITriggerDispatcher` / `TriggerDispatcherBase`、`DispatcherRegistration<TArgs>`、`EDispatcherType`、`TriggerExecutor<TArgs>`、`EventBusDispatcherContext` | 已归入 `Runtime/LegacyCompatibility/Optional/Dispatcher`。仓库源码未发现该旧派发器链的调用，亦未找到旧上下文适配器的使用；保留公开类型、成员和原脚本 GUID。 | 兼容归档不等于下线批准。后续单独核对二进制消费者；不把 `ActionSchedulerManager.Update` 当成旧派发器的完整替代。 |
| `TriggerWaitHandle` / `TriggerMutex` / `SynchronizedActionExecutor` | 已归入 `Runtime/LegacyCompatibility/Optional/ActionSynchronization`。MOBA、示例及其他受检源码未发现调用；`SynchronizedActionExecutor` 的公开构造签名仍依赖 `TriggerWaitHandle`。主线执行器工厂不创建这些类型。`WaitOne` 不阻塞，未置位时抛出异常；同步执行器只检查信号，未置位即跳过。 | 不作为新接入的等待或互斥机制。保留程序集、命名空间、公开成员和现有行为；评估下线前仍须检查发布和二进制消费面。 |
| `RpnNumericExprParserExtensions` / `AccessorRegistry` | 直接调用只在包内测试及扫描链内；扩展首次使用扫描已加载程序集，`RescanAccessors` 可重扫。`RpnNumericExprParserExtensionsTests` 覆盖稳定 ID、已声明域通过和未知域拒绝。 | `SkillCore` 已示范在宿主注册 `combat` 域，并用 `RpnNumericExprParser.Parse` 的显式回调拒绝未知域；`.NET` 的 `RpnNumericExprParserTests` 验证已声明域 ID 一致及未知域拒绝。扫描入口保留，不能用默认解析替换严格解析。 |
| `ValidatingTriggerPlanJsonDatabase` / `TriggerValidationOptions` | 未发现包外调用。`ValidatingTriggerPlanJsonDatabaseExecutionRootTests` 有直接回归；MOBA 启动使用 `TriggerPlanJsonDatabase`，不经包装器。 | 替代方向为 `TriggerPlanJsonDatabase` 加显式投影与校验。必须对齐执行根、诊断和失败时机；原包装器先加载后验证，报错不回滚，不能宣称两条路径等价。 |
| `TriggerRegistry` 与 `Runtime/RuntimeServices/Instance` 的实例入口 | 未发现包外源码调用；[`TriggerRegistryTests`](../Tests/Editor/Compatibility/TriggerRegistryTests.cs) 覆盖配置注册、查找和句柄释放，不覆盖实例快照和恢复。同步执行器仍使用 `TriggerInstance`。 | `TriggerRunner.RegisterPlan` 是事件订阅，不提供按 ID 查找、恢复或原句柄语义；先单独验证这些契约，不作机械替换。 |
| `HierarchicalTriggerRunner<TCtx>` | 仓库源码只命中定义，未发现直接调用或专项回归。原层级样例已归档，详见[样例盘点](SampleInventory.md)。 | 普通 `TriggerRunner` 不直接等价于父子作用域；迁移前验证父子顺序、传播、短路和订阅释放。 |
| `Runtime/Triggering/Contracts/ILegacyTriggerExecutor.cs` | **已清理**：文件仅含空命名空间，不定义类型；未发现源码或 Unity GUID 引用。 | 与原 `.meta` 一并删除，不建立替代占位。 |

## 调度计数核对

`ActionSchedulerManager` 保持正式调度入口。MOBA、其他示例、服务端及工具的受检源码均未发现读取下列计数属性；这不排除仓库外调用。

- `TotalActionCount` 表示当前由管理器持有的动作实例数。读取时汇总各子调度器的 `ActionCount`，包括等待、暂停和仍未回收的终态实例；注册、替换、移除调度器和清空后的结果无需等待下一次 `Update`。旧实现始终返回 `0`；此次仅修复计数，不改变调度生命周期。
- `ActiveActionCount` 保持现有行为：在 `Update` 时汇总子调度器的 `ActiveCount`，在 `Clear` 时归零。它会计入暂停时仍持有的实例及尚未回收的终态实例，显式移除调度器后也要到下一次 `Update` 才刷新，不能视为实时运行实例数。不建议把它作为新接入的判断依据。

## `Queued` 执行策略核对

- **兼容保留，不建议新计划使用。** `EActionExecutionPolicy.Queued` 的值仍为 `1`，JSON 仍接受名称和数字，现有校验和执行行为不变。受检 MOBA 作者化文本、Unity 与 Console 发布配置、示例及服务端未发现显式配置；仓库源码中，显式选用仅见包内计划校验测试。此结论不覆盖外部输入、动态构造或已发布的二进制调用方。
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
