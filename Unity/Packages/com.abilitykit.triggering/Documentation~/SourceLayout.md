# Triggering 源码目录

本页用于查找实现和确定维护落点，不用于判定公开 API 是否受支持。正式、兼容和待下线入口以[入口清单](FormalApiBoundary.md)为准。

## 当前落点

| 要找的代码 | 目录 | 职责 |
| --- | --- | --- |
| 事件总线与事件通道 | `Runtime/Events/` | 提供发布与订阅；不决定触发器的执行顺序。 |
| 触发器主运行器 | `Runtime/Triggering/Runner/` | 组织一次事件的注册、排序、评估、执行和通知。 |
| 计划定义、加载和执行 | `Runtime/Plans/` | `Model` 放计划模型，`Serialization` 读写计划，`Validation` 校验计划，`Execution` 执行计划。 |
| 执行控制与宿主服务 | `Runtime/Core/Execution/`、`Runtime/RuntimeServices/` | 前者承载执行上下文与控制；后者放驱动适配和仍保留的运行服务，不放诊断或治理清单。 |
| 诊断与兼容清单 | `Runtime/Diagnostics/`、`Runtime/Compatibility/` | 前者收集调用方报告的诊断；后者维护根目录占位扫描，不是兼容 API 的实现目录。 |
| 调度 | `Runtime/Scheduling/` | 调度任务、规则和时间推进；主运行器只向执行上下文提供调度管理器。 |
| 共享抽象 | `Runtime/Abstractions/` | 独立程序集持有，Unity 和 .NET 共用源码；不放主运行器实现。 |
| 编辑器、测试、示例 | `Editor/`、`Tests/Editor/`、`Samples~/` | 编辑器工具、EditMode 测试及 UPM 可选示例；旧 `Samples/` 已清理，见[样例盘点](SampleInventory.md)。 |

## 运行器内部

| 文件 | 维护范围 |
| --- | --- |
| `TriggerRunner.cs` | 构造与 `Register` 公开入口。 |
| `TriggerRunner.Subscriptions.cs` | 按参数类型和事件键定位注册列表、管理总线订阅、转发总线回调。 |
| `TriggerRunner.Dispatch.cs` | 派发循环、条件与动作调用、传播控制；直接体现关键回调与控制分支的先后。 |
| `TriggerRunner.Notifications.cs` | 条件结果、失败与短路的反馈以及 Trace 记录；不决定继续还是停止派发。 |
| `TriggerRunnerRegistration.cs`、`TriggerRunnerEntry.cs` | 单次注册的释放句柄、排序键和插入规则。 |
| `TriggerRunnerRuntimeServices.cs`、`TriggerRunnerCueDispatcher.cs` | 构建执行上下文和 Cue 上下文；后者也向计划动作提供 Cue 数据解析。 |

同一个 `TriggerRunner<TCtx>` 以部分类组织；没有新增程序集或可替换的执行层。派发遍历活动列表，本次未修改重入、回调异常、短路或调度语义。移除的内部代码仅包含无调用的短路 Cue 转发链与重复的 Cue 转发方法；有效路径继续使用先前取出的 Cue，保留原有回调顺序。

## 测试归属与旧用例处置

Unity 测试源文件放在 `Tests/Editor/` 下，由 `AbilityKit.Triggering.Tests` 程序集运行；保留的非主线入口在 `Tests/Editor/Compatibility/` 单独回归。不要把测试散放在 `Tests/` 根目录：那里没有 asmdef，Unity 不会把它们纳入门禁。

| 原根目录用例 | 处置 | 依据 |
| --- | --- | --- |
| `TriggerConfigTests` | 删除失效源码，原版留在 Git 历史。无条件和函数条件的断言由 `TriggerPlanPredicateTests` 接续。 | `TriggerConfig<TArgs>` 已不存在；正式路径用 `TriggerPlan<TArgs>`。配置可变状态和 `FromPlan` 转换没有直接等价入口，不计为当前覆盖。 |
| `TriggerInstanceTests` | 删除失效源码，原版留在 Git 历史，不移植到现有实例。 | `TriggerInstance<TArgs>` 和 `TriggerInstanceState` 已不存在。现有非泛型实例使用 `TriggerSnapshot` 与独立状态字段，不能将原先的执行计数、复制更新等断言机械套用。 |
| `TriggerRegistryTests` | 用现有 `RegisterConfig` 和 `TryGet` 迁入兼容回归，保留原测试脚本 GUID。 | 注册、自动编号、查找、注销、枚举、清空与句柄释放仍有可验证行为；不依赖已删除的 `Register(TriggerPlan<TArgs>)`，也不作为主线注册示例。 |

## 条件与动作执行

| 职责 | 落点 | 边界 |
| --- | --- | --- |
| 条件求值 | `Runtime/Plans/Execution/PlannedTriggerPredicateEvaluator.cs` | 扁平计划和执行树节点复用表达式求值；条件节点不构造触发器实例。 |
| 单动作绑定与调用 | `Runtime/Plans/Execution/` | `PlannedTriggerActionBindingResolver` 从注册表绑定一个动作；`TriggerPlanActionInvoker` 使用给定的上下文解析参数、调用委托并通知动作 Scope 与行为 Cue，不保存执行状态。 |
| 扁平计划与执行树装配 | `Runtime/Plans/Execution/PlannedTriggerActionExecutor.cs`、`Runtime/Plans/Executables/ActionCallTriggerPlanExecutable.cs` | 扁平计划首次执行前绑定全部动作，立即与调度路径复用缓存；执行树叶子每次执行独立绑定，保留原始 Scope 索引，不派发行为 Cue。两条路径不为复用而统一绑定时机或调度上下文。 |

## 计划加载与校验

| 职责 | 落点 | 边界 |
| --- | --- | --- |
| JSON 格式识别与转换 | `Runtime/Plans/Serialization/Json/{Parsing,Source,Conversion}/` | 解析器识别源格式和运行时格式，DTO 留在格式层；`TriggerPlanJsonReader` 负责展开模块、解析 Cue 并转换为运行时记录。解析成功不代表领域引用有效。 |
| 记录与索引 | `Runtime/Plans/Storage/` | `TriggerPlanRecordStore<TRecord>` 保存运行时记录、执行根索引、字符串与黑板初始化配置，不依赖 JSON、文件读取或宿主。 |
| 兼容查询与文件读取 | `Runtime/Plans/Serialization/Json/{Database,Loading}/` | `TriggerPlanJsonDatabase` 保留现有公开加载、查询和合并入口；`TriggerPlanDirectoryLoader` 按输入与选项读取文件。不替宿主决定目录优先级或启动校验。 |
| 发布聚合编译 | `Editor/Authoring/` | `TriggerPlanAggregateCompiler` 编译发布源；不是宿主的目录覆盖入口。 |
| 计划语义校验 | `Runtime/Plans/Validation/`、`Runtime/Validation/Core/` | 前者放计划规则及其类型化输入 `TriggerPlanDatabase<TCtx>`，后者放基础契约、上下文和校验入口。规则不发现文件；引用检查依赖调用方提供的上下文。 |
| 兼容包装 | `Runtime/Plans/Validation/Adapters/` | `ValidatingTriggerPlanJsonDatabase` 将 JSON 记录投影为校验输入，保留执行根。自动校验在内部加载后执行，报错不回滚；`TriggerValidationOptions` 只配置此包装器。 |
| 宿主编排 | 接入项目 | 选择资源、输入顺序、失败策略和启动校验时机；在合并后的库上检查事件注册、动作 Schema 与领域引用。 |

不要把宿主失败或覆盖策略加入计划模型、JSON 转换器或执行器。MOBA 两端的实际装配和已确认规则见仓库的 `Docs/TriggerPlanLoadingBaseline.md`。

新增输入格式时，可先在独立的格式目录完成读取和转换，再复用运行时记录存储；不要求实现 JSON DTO，也不预先承诺公共编解码接口或隐式格式探测。当前仍只接收现有 JSON 输入。

## 宿主服务与兼容边界

| 目录 | 当前用途与处置 |
| --- | --- |
| `RuntimeServices/Dispatcher/` | `ITriggerDispatcherContext` 与 `TriggerPredicate<TArgs>` 分文件保存，仍被计划注册与动作调度链引用。 |
| `LegacyCompatibility/Optional/Dispatcher/` | 保留旧派发器接口、基类、注册类型和原上下文适配器。不作为新接入入口，仍随包编译。 |
| `LegacyCompatibility/Optional/ActionSynchronization/` | 保留旧动作信号句柄、互斥句柄和信号门控执行器。不作为新接入入口，仍随包编译。 |
| `LegacyCompatibility/Required/` | 存放主线仍依赖的兼容实现。`QueuedActionExecutor` 仍服务既有计划策略，不列入 `Optional/` 排除验证。 |
| `RuntimeServices/Time/`、`RuntimeServices/Random/` | 时间和随机源适配，仍被上下文接口引用；宿主决定如何供给，不在本批更换时钟或随机流。 |
| `RuntimeServices/Continuous/` | 与持续执行契约的桥接；已有注册表回归，不改状态及生命周期。 |
| `RuntimeServices/Instance/`、`RuntimeServices/Sync/` | 保留的实例与快照相关入口，不是主线计划订阅，也不等同于 MOBA 帧同步；不以缺少包外源码调用作为删除依据。 |
| `Diagnostics/`、`Editor/Diagnostics/` | 运行时收集器由调用方显式记录；编辑器目录只负责报告菜单，不能作为运行时依赖。 |
| `Compatibility/`、`Validation/Compatibility/` | 前者持有根目录占位清单；后者保留校验门面。同一清单只维护一份。 |

## 后续整理

共享运行时保留类型化事件、计划、调度、通用解析和扩展接口。上下文内容、载荷读法、黑板域和校验策略由宿主提供。`SkillCore` 与 `BattleRuntime` 使用各自的上下文；`SkillCore` 在示例里声明黑板域、注册数据并以解析回调做严格域校验，框架无需认识 `combat`。

### 测试支持与工具归属

| 实现 | 目标归属与前置条件 |
| --- | --- |
| `TriggeringTestHarness<TCtx>` | 已迁到 `Tests/Support/` 的 `AbilityKit.Triggering.TestSupport`，仅供测试引用。Unity 测试 asmdef 显式引用它；.NET 测试引用 `src/AbilityKit.Triggering.TestSupport/AbilityKit.Triggering.TestSupport.csproj`。MOBA 的 `MobaRuntimeTestEnvironment<TCtx>` 同步迁到其包内 `Tests/Support/`。 |
| `TriggerPlanAggregateCompiler` | `AbilityKit.Triggering.Authoring` 的编辑器与 .NET 工具程序集共用同一份源码。Unity 调用方显式引用 Editor-only asmdef，CLI 引用 `src/AbilityKit.Triggering.Authoring/AbilityKit.Triggering.Authoring.csproj`。仅此工具程序集获得额外的内部访问权限，以复用解析器与 DTO；不将它们升级为公开 API。 |

测试支持和发布工具程序集均不由运行时引用。迁移保留夹具与聚合编译器的命名空间、源码签名及 Unity 脚本 GUID；旧程序集限定名不再解析，预编译调用方需更新引用并重新编译。发布编译仍拒绝重复触发器 ID；宿主的目录覆盖规则不变。

兼容入口的调用证据、回归缺口和迁移顺序见[入口清单](FormalApiBoundary.md#核查范围与迁移顺序)。旧样例的清理不等于对应 API 已下线。
