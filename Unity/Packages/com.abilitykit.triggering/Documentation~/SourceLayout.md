# Triggering 源码目录

本页用于查找实现和确定维护落点，不用于判定公开 API 是否受支持。正式、兼容和待下线入口以[入口清单](FormalApiBoundary.md)为准。

## 当前落点

| 要找的代码 | 目录 | 职责 |
| --- | --- | --- |
| 事件总线与事件通道 | `Runtime/Events/` | 提供发布与订阅；不决定触发器的执行顺序。 |
| 触发器主运行器 | `Runtime/Triggering/Runner/` | 组织一次事件的注册、排序、评估、执行和通知。 |
| 计划定义、加载和执行 | `Runtime/Plans/` | `Model` 放计划模型，`Serialization` 读写计划，`Validation` 校验计划，`Execution` 执行计划。 |
| 黑板快照与恢复 | `Runtime/Blackboard/{Snapshot,Initialization,Resolvers}/` | 快照模型不读写 JSON；`BlackboardSnapshotJsonCodec` 单独处理格式。所有者恢复在内存中复制快照并核对初始化模板，不依赖编解码器。 |
| 执行控制与宿主服务 | `Runtime/Core/Execution/`、`Runtime/RuntimeServices/` | 前者承载执行上下文与控制；后者放驱动适配和仍保留的运行服务，不放诊断或治理清单。 |
| 诊断与兼容清单 | `Runtime/Diagnostics/`、`Runtime/Compatibility/` | 前者收集调用方报告的诊断；后者维护根目录占位扫描，不是兼容 API 的实现目录。 |
| 可选时间能力 | `Runtime/Scheduling/ExecutionTree/`、`Runtime/Core/Execution/IFlatActionScheduler.cs` | 执行树时间节点保留实现；扁平计划只定义登记接口，具体实现由宿主装配。 |
| 共享抽象 | `Runtime/Abstractions/` | 独立程序集持有，Unity 和 .NET 共用源码；不放主运行器实现。 |
| 编辑器、测试、示例 | `Editor/`、`Tests/Editor/`、`Samples~/` | 编辑器工具、EditMode 测试及 UPM 可选示例；旧 `Samples/` 已清理，见[样例盘点](SampleInventory.md)。 |

## 运行器内部

| 文件 | 维护范围 |
| --- | --- |
| `TriggerRunner.cs` | 构造与 `Register` 公开入口。 |
| `TriggerRunner.Subscriptions.cs` | 按参数类型与事件键定位事件槽；每个槽统一管理注册列表、总线订阅、回调转发及最后一条释放后的退订。 |
| `TriggerRunner.Dispatch.cs` | 派发循环、条件与动作调用、传播控制；直接体现关键回调与控制分支的先后。 |
| `TriggerRunner.Notifications.cs` | 注册与注销通知、条件结果、失败与短路的反馈以及 Trace 记录；不决定继续还是停止派发。 |
| `TriggerRunnerRegistration.cs`、`TriggerRunnerEntry.cs` | 单次注册的释放句柄、排序键和插入规则。 |
| `TriggerRunnerRuntimeServices.cs`、`TriggerRunnerCueDispatcher.cs` | 构建执行上下文和 Cue 上下文；后者也向计划动作提供 Cue 数据解析。 |

同一个 `TriggerRunner<TCtx>` 以部分类组织；没有新增程序集或可替换的执行层。退订后复用同一事件槽，使总线已捕获但尚未进入运行器的回调仍能读取当前注册；运行器进入派发后，外层候选顺序保持不变。短路、回调异常和调度语义不由事件槽处理。

## 测试归属与旧用例处置

Unity 测试源文件放在 `Tests/Editor/` 下，由 `AbilityKit.Triggering.Tests` 程序集运行；保留的非主线入口在 `Tests/Editor/Compatibility/` 单独回归。不要把测试散放在 `Tests/` 根目录：那里没有 asmdef，Unity 不会把它们纳入门禁。

| 原根目录用例 | 处置 | 依据 |
| --- | --- | --- |
| `TriggerConfigTests` | 删除失效源码，原版留在 Git 历史。无条件和函数条件的断言由 `TriggerPlanPredicateTests` 接续。 | `TriggerConfig<TArgs>` 已不存在；正式路径用 `TriggerPlan<TArgs>`。配置可变状态和 `FromPlan` 转换没有直接等价入口，不计为当前覆盖。 |
| `TriggerInstanceTests`、`TriggerRegistryTests` | 随旧实例接口退役，不移植到正式主线。 | 旧实例快照与按 ID 查找不是计划订阅或执行树恢复的契约，保留历史证据而不重写成等价测试。 |

## 条件与动作执行

| 职责 | 落点 | 边界 |
| --- | --- | --- |
| 条件求值 | `Runtime/Plans/Execution/PlannedTriggerPredicateEvaluator.cs` | 扁平计划和执行树节点复用表达式求值；条件节点不构造触发器实例。 |
| 单动作绑定与调用 | `Runtime/Plans/Execution/` | `PlannedTriggerActionBindingResolver` 从注册表绑定一个动作；`TriggerPlanActionInvoker` 使用给定的上下文解析参数、调用委托并通知动作 Scope 与行为 Cue，不保存执行状态。 |
| 扁平计划与执行树装配 | `Runtime/Plans/Execution/PlannedTriggerActionExecutor.cs`、`Runtime/Plans/Executables/ActionCallTriggerPlanExecutable.cs` | 扁平计划首次执行前绑定全部动作，立即与调度路径复用缓存；执行树叶子每次执行独立绑定，保留原始 Scope 索引，不派发行为 Cue。两条路径不为复用而统一绑定时机或调度上下文。 |

### 扁平计划动作调度

`Runtime/Core/Execution/IFlatActionScheduler.cs` 承载包外登记接口：计划先绑定分组，再按索引登记或替换槽位。`PlannedTrigger` 不引用具体调度类型。原内置管理器、动作池和 `Queued` 兼容包装器均归 `com.abilitykit.triggering.actionscheduling` 的 `Runtime/`；该包单向依赖核心。

### 执行树调度

执行树调度的登记接口、运行实现和进度数据一同归 `Runtime/Scheduling/ExecutionTree/`；`Runtime/Core/Execution/` 保留通用执行上下文和控制。目录归类不改变命名空间、程序集或运行时依赖。

| 文件（`Runtime/Scheduling/ExecutionTree/`） | 职责 |
| --- | --- |
| `ITriggerExecutionScheduler.cs`、`TriggerScheduledExecutionHandle.cs` | 节点登记入口和本地句柄，不定义帧驱动或恢复协议。 |
| `TriggerExecutionProgress.cs`、`TriggerScheduledExecutionInfo.cs`、`TriggerScheduledExecutionState.cs` | 进度值与 ID 游标，不携带任务定义或宿主绑定。 |
| `TriggerExecutionProgressValidator.cs` | 只读核对进度、游标和定义是否存在，不缓存结果。 |
| `TriggerExecutionScheduler.cs` | 管理有序定义、推进和帧边界；恢复时重新预检，校验成功后才写入进度及游标。 |

## 计划加载与校验

| 职责 | 落点 | 边界 |
| --- | --- | --- |
| JSON 格式识别与转换 | `Runtime/Plans/Serialization/Json/{Parsing,Source,Conversion}/` | 解析器识别源格式和运行时格式，DTO 留在格式层；`TriggerPlanJsonReader` 负责展开模块、解析 Cue 并转换为运行时记录。解析成功不代表领域引用有效。 |
| 单条 JSON 记录装配 | `Runtime/Plans/Serialization/Json/Conversion/` | `TriggerPlanConverter` 只装配扁平计划与执行根；动作选项、条件表达式和值/模板分别由 `TriggerPlanJsonActionConverter`、`TriggerPlanJsonPredicateConverter` 和 `TriggerPlanJsonValueConverter` 转换。同一条记录的两种产物共用记录级模板查询，不在共享转换器上保存临时状态。执行节点转换器负责引用展开及树形构造，不负责单个参数的解释。 |
| 执行树引用 | `TriggerPlanExecutionNodeReferenceCatalog`、`TriggerPlanExecutionNodeReferenceResolver` | 模块展开后的每份文档在首棵显式执行树转换时建立一次行为与节点索引；纯扁平记录不读取索引。每棵树独立维护活动引用路径，只检测当前路径上的循环，不把兄弟分支使用同一引用判为循环，也不共享运行时节点。 |
| 记录与索引 | `Runtime/Plans/Storage/` | `TriggerPlanRecordStore<TRecord>` 保留直接加载的原始行，并按 ID 索引生效记录；跨库合并后遍历与查询使用同一条记录。存储还管理字符串和黑板初始化配置，不依赖 JSON、文件读取或宿主策略。 |
| 兼容查询与文件读取 | `Runtime/Plans/Serialization/Json/{Database,Loading}/` | `TriggerPlanJsonDatabase` 保留现有公开加载、查询和合并入口；`TriggerPlanDirectoryLoader` 按输入与选项读取文件。不替宿主决定目录优先级或启动校验。 |
| JSON 诊断映射 | `Runtime/Plans/Serialization/Json/Loading/` | `TriggerPlanJsonDiagnosticAdapter` 按输入次序把 JSON 诊断映射为通用加载诊断，不重做解析、校验或失败决策。 |
| 发布聚合编译 | `Editor/Authoring/` | `TriggerPlanAggregateCompiler` 编译发布源；不是宿主的目录覆盖入口。 |
| 计划语义校验 | `Runtime/Plans/Validation/`、`Runtime/Validation/Core/` | 前者放计划规则及其类型化输入 `TriggerPlanDatabase<TCtx>`，后者放基础契约、上下文和校验入口。规则不发现文件；引用检查依赖调用方提供的上下文。 |
| 扁平计划限额诊断 | `Runtime/Plans/Validation/FlatTriggerPlanLimitsValidator.cs` | 按显式上限逐条检查扁平计划；不计执行树、集合总量或运行开销，也不决定内容准入。 |
| 宿主编排 | 接入项目 | 选择资源、输入顺序、失败策略和启动校验时机；在合并后的库上检查事件注册、动作 Schema 与领域引用。 |

不要把宿主失败或覆盖策略加入计划模型、JSON 转换器或执行器。UGC 内容模型、准入和运行预算也在包外实现，由宿主复用通用计划、校验和执行入口。MOBA 两端的实际装配和已确认规则见仓库的 `Docs/TriggerPlanLoadingBaseline.md`。

新增输入格式时，可在独立的格式目录完成读取、转换和诊断映射，再复用运行时记录存储与通用诊断收集器；不要求实现 JSON DTO，也不预先承诺公共编解码接口或隐式格式探测。当前仍只接收现有 JSON 输入。

`Runtime/Executables/` 中已移除旧的独立数据库和数据库专属 DTO。执行器、条件和仍有引用的配置转换类型保留，不再扩展为第二套计划加载入口。旧 JSON 校验包装器也已移除；由宿主把候选记录连同执行根投影为校验条目，再显式调用计划校验。下线依据见[正式入口边界](FormalApiBoundary.md#旧数据库入口已移除)。

原 `Runtime/Executables/Decorators/` 与旧 `ScheduledExecutor` 同批退役。`Executables/Core/` 仍承载普通执行契约与执行器，`Executables/Composition/` 保留配置转换使用的包装器；实际执行树时间推进归 `Runtime/Scheduling/ExecutionTree/`。

## 宿主服务与兼容边界

| 目录 | 当前用途与处置 |
| --- | --- |
| `RuntimeServices/Dispatcher/` | `ITriggerDispatcherContext` 与 `TriggerPredicate<TArgs>` 分文件保存，仍被计划注册与动作调度链引用。 |
| `RuntimeServices/Time/`、`RuntimeServices/Random/` | 时间和随机源适配，仍被上下文接口引用；宿主决定如何供给，不在本批更换时钟或随机流。 |
| `RuntimeServices/Continuous/` | 与持续执行契约的桥接；已有注册表回归，不改状态及生命周期。 |
| `Diagnostics/`、`Editor/Diagnostics/` | 运行时收集器由调用方显式记录，不引用具体输入格式；编辑器目录只负责报告菜单，不能作为运行时依赖。 |
| `Compatibility/`、`Validation/Compatibility/` | 前者持有根目录占位清单；后者保留校验门面。同一清单只维护一份。 |

原 `Runtime/Scheduling/` 下的 `Simple`、`Grouped`、`Common`、`Data`、`Strategies`、`Effects` 已随 `Runtime.Schedule` 一并退役。

原 `RuntimeServices/Instance/`、`RuntimeServices/Sync/` 与旧 `TriggerRegistry` 成组退役。`Runtime/Registries/` 继续存放动作和函数注册表。

原 `LegacyCompatibility/Optional/` 的三个兼容组均已退役，不保留空目录或裁剪入口。兼容入口的具体状态与测试范围见[下线流程](LegacyMigrationPolicy.md)。

## 后续整理

共享运行时保留类型化事件、计划、调度、通用解析和扩展接口。上下文内容、载荷读法、黑板域和校验策略由宿主提供。`SkillCore` 与 `BattleRuntime` 使用各自的上下文；`SkillCore` 在示例里声明黑板域、注册数据并以解析回调做严格域校验，框架无需认识 `combat`。

### 测试支持与工具归属

| 实现 | 目标归属与前置条件 |
| --- | --- |
| `TriggeringTestHarness<TCtx>` | 位于 `Tests/Support/` 的 `AbilityKit.Triggering.TestSupport`，仅供测试引用。默认不装配调度，接受可选的 `flatActionScheduler:`；测试宿主持有、推进并清理该实现，夹具只管理自己的订阅。Unity 测试 asmdef 显式引用该夹具；.NET 测试引用 `src/AbilityKit.Triggering.TestSupport/AbilityKit.Triggering.TestSupport.csproj`。MOBA 的 `MobaRuntimeTestEnvironment<TCtx>` 留在其包内 `Tests/Support/`。 |
| `TriggerPlanAggregateCompiler` | `AbilityKit.Triggering.Authoring` 的编辑器与 .NET 工具程序集共用同一份源码。Unity 调用方显式引用 Editor-only asmdef，CLI 引用 `src/AbilityKit.Triggering.Authoring/AbilityKit.Triggering.Authoring.csproj`。仅此工具程序集获得额外的内部访问权限，以复用解析器与 DTO；不将它们升级为公开 API。 |

测试支持和发布工具程序集均不由运行时引用。迁移保留夹具与聚合编译器的命名空间、源码签名及 Unity 脚本 GUID；旧程序集限定名不再解析，预编译调用方需更新引用并重新编译。发布编译仍拒绝重复触发器 ID；宿主的目录覆盖规则不变。

兼容入口的调用证据、回归缺口和迁移顺序见[入口清单](FormalApiBoundary.md#核查范围与迁移顺序)。旧样例的清理不等于对应 API 已下线。
