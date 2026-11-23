# AbilityKit Core 边界

`AbilityKit.Core` 是 Unity、.NET 服务端、测试和命令行工具共享的跨运行时基础层。它的目标不是尽可能小，而是提供稳定的所有权和可移植契约。

## 程序集边界

- `AbilityKit.Core` 不引用 Unity 引擎。
- Unity 编辑器工具使用独立的 `AbilityKit.Core.Editor` 程序集；已退役的 `AbilityKit.Core.Unity` 不再交付，应用适配由各自的宿主持有。
- .NET 项目编译同一套基础层源码，不引用 Unity 编辑器或宿主程序集。
- Unity Collections、Burst、Jobs 和引擎对象类型不得进入基础程序集。

Unity/Jobs 包中应使用 `Unity.Collections.NativeArray<T>`。当缓冲区所有权跨越方法、异步操作或存储边界时，共享托管代码可以使用 `PooledBufferOwner<T>`。不要在 Core 中再创建一个名为 `NativeArray` 的类型。

## 纳入规则

新的 Core 基础类型应同时满足以下条件：

1. 至少有三个相互独立的框架使用方。
2. 不借助玩法或平台术语也能明确描述其行为。
3. 明确规定所有权、变更、顺序和失败行为。
4. 可以在 `AbilityKit.Core.Tests` 中直接测试。
5. 保持 Unity 与 .NET 的编译边界。

Core 不是通用工具包。如果某个类型只有一个子系统使用、携带玩法策略，或依赖特定调度器、序列化器、传输层、引擎或原生分配器，应优先放入领域包。

## 公共 API 兼容性

当前候选的具体 API 支持等级见[首发支持清单](./FirstReleaseSupport.md)。公开签名存在并不等同于推荐接入；现有接口的兼容要求也不由新增原语的准入数量决定。

中文多行注释与 Core 局部 XML 文档错误门禁、Core 对 Deterministic 的依赖闭包及当前发布缺口见 [验证门禁与发布缺口](./ValidationAndReleaseReadiness.md)。该门禁仅作用于 .NET Core 工程，不覆盖 Unity/Editor 编译，也不代表命名空间审计或正式发布已通过。

隔离 .NET 制品及真实 Unity Editor 的本地安装、最小调用与候选替换/回退证据见 [独立消费验证](./IndependentConsumption.md)。验证夹具不计入独立生产使用方，不新增正式 NuGet 发布承诺或扩大 Core 职责。

发布计划的状态筛选、精确依赖版本、提交内容核对和命令失败处理属于仓库发布工具，不能作为新原语放入 Core。严格检查绑定所选包的 HEAD 子树及已有标签依赖；隔离回归不赋予候选发布资格，也不改变准入或兼容边界。当前未提交输入须在正式流程中处理，实际制品、注册表和平台签核仍需分别验收。

.NET Core 构建使用 `Microsoft.CodeAnalysis.PublicApiAnalyzers` 作为机器可读的兼容性门禁：

- `src/AbilityKit.Core/PublicAPI.Shipped.txt` 是分析器的既有签名基线；文件名不证明已有正式制品或标签。
- `src/AbilityKit.Core/PublicAPI.Unshipped.txt` 记录下次发布待评审的签名变更（包括可空性修订），不自动授予首发支持状态。
- `RS0016` 阻止未被有意登记的公共符号。
- `RS0017` 阻止已登记 API 被意外删除或更改签名。

添加 API 时，在契约和使用方明确前先保持为内部实现。随后把最终公共签名加入 Unshipped，并补充所有权、顺序、失败和兼容性行为测试。发布时，把已评审的 Unshipped 条目移入 Shipped。不要为了消除兼容性失败而重新生成整个基线。

删除或更改已发布 API 时，必须提供弃用路径、使用方迁移方案，并遵循包内记录的主版本策略。如果持久化数据、生成代码或独立版本包仍可能引用旧接口，优先提供兼容垫片。

## 已拆出的基础设施

调试绘制属于诊断基础设施。新使用方采用 `AbilityKit.Diagnostics.DebugDraw`，Unity 编辑器适配器采用 `AbilityKit.Diagnostics.Editor.DebugDraw`。旧的 `AbilityKit.Core.Debugging` 接口面作为过时兼容 API 保留至下一个主版本。

旧值类型、编辑器设置和 SceneView 生命周期统一见 [旧 DebugDraw 兼容与迁移契约](./DebugDrawCompatibility.md)。旧设置没有磁盘持久化，驱动不执行条目上限或碰撞层策略；旧绘制异常静默隔离，而 Diagnostics 正式驱动会记录异常。保留兼容类型不等于恢复 Core 绘制职责，.NET 通过也不能替代真实 Unity 编辑器验收。

释放策略由资源所有者负责，因为异常处理、释放顺序、可重入性和日志记录都属于生命周期决策。不要新增对 `AbilityKit.Core.Utilities.DisposeUtils` 的使用；应采用所有者局部辅助方法，或使用带明确契约的自有生命周期抽象。

广泛反射的 `ReflectionInvokeUtils` 同样仅保留到下一主版本移除窗口，新代码使用所属领域的窄接口、委托或显式注册。两个工具的实际异常边界及迁移约束统一见 [反射与释放工具兼容契约](./ReflectionAndDisposalCompatibility.md)。反射调用成功不证明返回任务成功，释放引用清空也不证明资源释放成功；这些策略继续由调用方所有者明确。

`MarkerAttribute`、`MarkerScanner` 和注册表类型在现有使用方迁移期间继续作为兼容构件。`MarkerSystem` 及三个全局引导基类已冻结：程序集级扫描和静态注册副作用必须迁移到由所有者控制的发现机制，或生成式/AOT 注册包。

既有登记、扫描及启动副作用统一见 [Marker 注册与扫描兼容契约](./Markers.md)。普通注册表、键控历史目录和命名空间扫描具有不同语义；全局初始化标记也不构成启动事务或重入屏障。本轮整理保留这些兼容限制，不新增程序集发现机制或 Core 使用方。

边界审计会拒绝这些已迁移入口的新使用方，也会拒绝在 `com.abilitykit.diagnostics` 之外声明 DebugDraw 契约。

Continuous 的正式所有权属于 `com.abilitykit.continuous`，旧 `AbilityKit.Core.Continuous` 仅保留到下一主版本移除窗口，不新增生产使用方或领域策略。旧实现的生命周期、失败与重入限制统一见 [Continuous 兼容与迁移契约](./ContinuousCompatibility.md)。独立包与旧 Core 在激活失败时存在补偿行为差异，迁移必须验证实际场景，不能假定只更换命名空间即可获得等价行为。

## 数学原语边界

六个浮点数学原语的单位、退化输入、旋转和变换顺序统一见 [数学原语契约](./Mathematics.md)。局部定点开方路径不构成完整的跨平台浮点锁步保证；碰撞形状、坐标单位、协议量化及领域精度策略继续由使用方负责。

现有 DeterministicMathBridge 的边界转换、量化、几何溢出及函数定义域统一见 [定点数学桥接契约](./DeterministicMathBridge.md)。其多个框架消费者复用已有适配，不能据此扩大为项目范围校验或完整锁步策略。底层 Fixed64 的编译目标算术差异列为独立依赖评审项，未获得跨平台结果证据前不承诺统一位级结果。

## 稳定标识符哈希

哈希编码、黄金向量、注册表冲突及默认输出统一见 [稳定标识与单调时间契约](./IdentifiersAndTiming.md)。本节规定版本治理与职责边界。

`StableHashV1` 是带版本的序列化契约，而不是可以随意替换的哈希辅助方法。其输出必须在 Unity、.NET、各平台和未来包版本之间保持一致。现有使用方不能在 UTF-16、非负 UTF-16 与 UTF-8 入口之间直接切换；未来算法或语义变更必须使用新的显式版本化类型，包含黄金向量，并为持久化或线上可见标识符提供迁移或双读规则。

`StableStringIdRegistry` 还会检测单个注册表内的冲突。仅对字符串做哈希不能证明全局唯一；不能容忍冲突的持久化协议应保留原始名称、架构命名空间或其他冲突解决契约。

## 单调时间边界

时钟来源、单位、取整及溢出统一见 [稳定标识与单调时间契约](./IdentifiersAndTiming.md)。Core 仅提供本地计时抽象与换算，不接管宿主调度、模拟帧推进、回放或跨端时间同步。不同进程或设备的时间戳不能因频率相同就直接比较。

## 稳定优先级集合

集合排序、匹配回调、缓冲容量与数组所有权的详细契约统一见 [集合、缓冲区与数组所有权契约](./CollectionsAndBuffers.md)。本节保留 Core 的准入理由与领域边界。

`StablePriorityList<T>` 用于小型注册管线，保证：

- 按整数优先级升序或降序排列；
- 优先级相同时保持原始注册顺序；
- 使用二分查找插入，而不是每次添加后对整个列表排序；
- 明确定义首次匹配删除和优先级更新行为。

该集合不是线程安全的。枚举列表或直接调用列表项期间，调用方不得添加、删除、更新或清空内容。

它不能替代领域专用的 Top-K 选择、快照缓冲区或并行键/值排序。这些算法保留各自领域实现和同值决胜规则。

## 有序整数索引

`SortedIntSet` 是面向小型整数域、关注分配行为的窄职责索引。它保证值唯一且升序，支持二分插入和查找、明确的上下界以及连续区间删除。它有意采用索引访问而不是可枚举接口，使热路径调用方可以使用不产生分配的 `for` 循环。

该类型只负责整数成员关系和顺序。领域缓冲区仍负责具体值、锁、重复值替换、保留窗口、防御性副本以及区间端点是否包含的策略。当前使用方包括 StateSync 快照和输入缓冲区、StateManager 回滚对齐、实体预测快照、FrameSync 命令缓冲区、远端帧缓冲区及 RemoteFrameAggregator。它们会把索引和对应值映射一起同步。

`core-collections` 运行时基准保留了之前的列表排序/临时裁剪列表工作负载作为基线。在 2026-08-14 的本地 Release 冒烟运行中，输入 128 个乱序帧时，基线每次索引变更耗时 367.57 ns、分配 3.375 字节；`SortedIntSet` 耗时 38.80 ns 且无分配。这些测量是准入依据，不是性能预算；只有工作负载和环境数据一致时才可比较后续结果。

## 有界运行时缓冲区

`IFrameIndexedBuffer<T>` 和 `ISequentialBuffer<T>` 统一跨包需要的最小保留语义。Frame 缓冲区按完整 `int` 键升序存储，重复帧替换值，超过容量时只保留数值最大的最新帧；`RemoveBefore` 和 `RemoveAfter` 均采用严格边界，不删除边界帧本身。顺序缓冲区按最旧到最新读取，满容量追加和缩容均淘汰最旧项。构造容量及成功应用的容量必须为正，容量变化保留最新项及其原顺序。

Ring 后端用于单调追加和频繁淘汰的热路径；帧 Ring 的查询和存储定位仍采用二分查找，乱序插入可能搬移元素。Sparse/List 后端用于乱序帧或低频参考实现。它们共享行为契约，但不承诺相同复杂度。淘汰、范围删除、清空和缩容必须断开内部存储对已删除引用的持有，不自动释放值。所有后端均非线程安全；读取、写入和容量调整需要单一所有者或外部同步。

`IBufferCapacityControl` 只暴露单次容量请求，不保证多个缓冲区之间的原子调整。策略评估、上下界、重试、平滑、调度和并发协调均由 Network 等策略所有者负责，不上提到 Core。

## 托管池化缓冲区

`PooledBufferOwner<T>` 是从 `System.Buffers.ArrayPool<T>` 租用数组时的可移植所有权边界。它提供：

- 即使实际租用数组更大，也能保持准确的逻辑 `Length`；
- 逻辑 `Memory`、`Span` 和 `ArraySegment` 视图；
- 明确的 `OnRent` 和 `OnReturn` 整数组清理策略；
- 幂等且线程安全的释放，确保最多尝试归还一次；自定义池的异常直接传播且不重试；
- 获取新视图时检测释放后使用。

所有者有意设计为密封引用类型。可释放的值类型会被复制，每个副本都可能归还同一数组，因而无法提供可靠的单一所有者行为。之前取得的视图仍引用租用数组，并会在所有者释放后立即失效；绝不能在所有权作用域结束后存储或使用这些视图。

连接级暂存缓冲区、排队工作、保留环形槽位以及其他需要显式表达所有权的生命周期应使用此所有者。对于已有局部 `try/finally` 的短同步热路径，直接调用 `ArrayPool<T>.Rent/Return` 可以避免分配所有者对象，仍是首选方式。不建议在没有分析数据的情况下池化小数组。

## 单调计时

`IMonotonicClock` 是测量经过时间和检查截止时间的可移植契约。`StopwatchMonotonicClock` 是进程内默认实现，`MonotonicTime` 则集中处理时间戳读取和避免溢出的转换：

- 对同一时钟实例，时间戳绝不会倒退；
- 时间戳只有和同一时钟的 `Frequency` 配合时才有意义；
- `DurationToTimestampTicks` 对正持续时间向上取整，确保截止时间不会早于请求时间到期；
- `GetMilliseconds` 和 `ToMilliseconds` 表示单调递增的经过时间单位，不代表 UTC、本地时间、Unix 时间，也不适合持久化。

除非不同时钟实例明确记录了共享起点和频率，否则不要比较它们生成的时间戳。持久化墙上时钟时刻应使用领域自有的 UTC 契约。

Core 不负责调度、定时器、重试/退避策略、帧推进、模拟时间、回放时间或 Unity 播放循环集成。因此，`IWorldClock`、`IFrameClock`、`IReplayClock` 和管线时间源仍留在各自领域包中。Network Host 保留原有 `IMonotonicClock` 和 `StopwatchMonotonicClock` 名称作为源码兼容适配器；新的跨包 API 应依赖 `AbilityKit.Core.Timing.IMonotonicClock`。

异步取消边界应使用 `System.Threading.CancellationToken`。只有多个包需要相同的附加状态转换、所有权和回调语义时，Core 包装才有价值；仅重命名平台令牌只会增加间接层，不会增加契约。

## Guard、Result 与诊断边界

Core 不提供通用 `Guard` 辅助类。除非至少三个包需要完全相同的验证规则、异常类型、参数命名和消息契约，否则在调用处使用标准参数检查和标准异常类型更清晰。仅包装 `ArgumentNullException` 或 `ArgumentOutOfRangeException` 的辅助方法增加了间接层，却没有统一行为。

Core 也不提供通用 `Result<T>`。触发器执行、流程状态、回滚、网络协商、恢复和线上协议结果具有不同的状态机、错误码、合并规则和序列化约束，因此仍应作为领域类型。只有独立包共享相同的失败状态、错误码所有权以及持久化或线上兼容规则时，结果契约才可进入 Core。

诊断和验证记录遵循相同规则。错误、警告和信息等严重级别名称不足以构成共享契约：有些诊断是可变的合并输出，有些是不可变的启动门禁，还有些携带导航、修订、健康状态或领域载荷。在生产方和使用方共享标识、顺序、本地化、传输及失败门禁语义前，不要添加通用诊断严重级别或问题类型。

`RuntimeObjectKey`、`ObservationDefinitionRef`、`ObservationTraceRef` 和同步 sink 的详细契约统一见 [观测关联原语契约与准入复核](./Observability.md)。标识分配、复用和生命周期仍归领域所有，同步接口不提供采集调度或事件所有权管理。

本轮生产引用盘点集中在 MOBA 诊断链，不能将同项目多个程序集当作三个独立框架使用方。现有 API 在 Unshipped 中保留，尚缺正式纳入 Core 的独立需求证据；若后续仍不能满足，应按实际领域或 Diagnostics 所有权单独评审迁移与兼容窗口。本批不迁移、不新增弃用标记，也不扩大事件内容、目录或调度职责。

## 可释放注册项

`DisposableRegistration` 把释放回调适配为 `IDisposable`。释放操作会先以原子方式取得回调再调用，因此并发或重复调用最多执行一次。回调异常会传播给调用方，但注册项仍保持已释放状态。当使用方已有小型状态持有者时，带状态的重载可避免额外的捕获闭包分配。

注册项只负责执行回调。它不会在回调周围加锁、让目标变为线程安全、吞掉失败、控制释放顺序或维护组合集合。Snapshot routing、World ECS 和 Triggering 在共享一次性机制的同时，仍保留各自领域策略。多个注册项需要逆序释放、错误聚合、移除或生命周期状态转换时，应使用领域自有的组合类型。

现有 `IEventSubscription` 接口仍归各领域所有。有些接口提供 `Unsubscribe` 而非 `Dispose`，有些还会释放已注册处理器或通知生命周期观察者。共享一次性机制并不能证明这些公共契约拥有相同的所有权语义。

## 应用设置与反射所有权

配置来源、持久化路径、序列化器选择和可选模块安装属于应用程序/引导策略。它们不会仅仅因为实现小或可复用就满足 Core 纳入规则。

旧文件工具、静态对象缓存及模块描述的实际契约统一见 [配置文件、缓存与模块描述兼容契约](./ConfigurationFileCompatibility.md)。写入不提供原子替换，缓存不提供内容版本或在途加载失效屏障；这些应用策略应由所有者明确。Core 已退役 `UnityJsonSettingsBootstrap`，见 [应用层迁移说明](./UnitySettingsBootstrapCompatibility.md)。MOBA 的同名类型是独立实现，不受影响。

当前 MOBA 的所有权已明确：

- `AbilityKit.Demo.Moba.View.Settings` 归 `com.abilitykit.demo.moba.view.runtime` 所有。纯分层设置位于嵌套的游戏流程程序集中；文件和 Unity 持久化适配器位于外层视图运行时中。
- `AbilityKit.Demo.Moba.Bootstrap` 归 `com.abilitykit.demo.moba.runtime` 所有。反射只通过窄接口 `ModuleInstallerInvoker.TryInvoke(ModuleInstallerConfig)` 暴露。

生产包不得使用 `AbilityKit.Core.Configuration` 或 `AbilityKit.Core.Reflection`，非 Core 的 .NET 项目也不得直接链接这些源码目录。旧 Core 类型作为过时兼容 API 保留至下一个主版本移除窗口。兼容性测试仍可覆盖它们，以防公开签名在此期间漂移。

## 包命名空间所有权

`AbilityKit.Core.*` 命名空间归 `com.abilitykit.core` 所有。即使依赖 Core 值类型，领域包也必须使用包自有命名空间。边界审计会拒绝 Core 包之外的新声明。

五个历史区域按精确目录和文件数上限暂时加入允许列表：碰撞数学、导航数学、录像、Unity 池化适配器和快照路由。该允许项是逐步缩减的迁移基线，并不允许增加文件。其目标命名空间依次为 `AbilityKit.Combat.Collision`、`AbilityKit.Combat.Navigation`、`AbilityKit.Record`、`AbilityKit.Unity.Pooling` 和 `AbilityKit.World.Snapshot.Routing`。

## 线程安全契约

线程安全必须显式声明和选择加入。除非类型 API 或本文另有说明，否则类型均不保证线程安全。

| 区域 | 契约 |
| --- | --- |
| 稳定哈希和不可变数学值类型 | 无共享可变状态，可以并发调用。 |
| `PooledBufferOwner<T>` | `Dispose` 是幂等的，最多归还一次。缓冲区访问不得与释放并发；已取得的视图在释放后失效。 |
| `StablePriorityList<T>` | 非线程安全；变更和枚举需要单一所有者或外部同步。 |
| `SortedIntSet` | 非线程安全；必须与它索引的领域值处于同一把锁或同一所有者下。 |
| Frame/Sequential 缓冲区与 `IBufferCapacityControl` | 非线程安全；存取、范围删除、清空及容量调整不得并发，复合容量策略需由调用方协调。 |
| 观测值类型与 `NullObservationSink<TEvent>` | 值类型没有共享可变状态，Null Sink 可并发复用；其他 Sink 的线程安全、重入和事件保留行为由具体实现声明。 |
| `DisposableRegistration` | 支持并发调用 `Dispose`，释放回调最多执行一次。回调及其目标仍遵循各自的线程安全要求。 |
| `EventDispatcher` 和 `StableStringIdRegistry` | 非线程安全；应在同一执行上下文中订阅/注册/发布/退订，或在整个操作外加保护。 |
| MOBA View 扁平及分层设置存储 | 非线程安全；在一个所有者线程中构建或更新，再向读取方发布不可变快照。 |
| `ObjectPool<T>` | 栈、计数器、容量预留和重复归还跟踪已同步。工厂与生命周期回调在池锁外执行，可能并发或重入；调用方必须保证回调及池化对象满足自身的线程安全要求。启用 `CollectionCheck` 时，同一对象在 Get、Release 或销毁过渡期间不能重复归还。 |
| `PoolManager` | 注册表访问已同步，池构造、注册回调以及 Clear/Trim/ForceTrim 均在管理器锁外执行。批量操作基于锁内取得的注册表快照；需要与并发创建或移除形成更强生命周期边界时，调用方仍须在管理器操作外协调。 |
| 全局日志、标记和分发器状态 | 把注册/配置视为启动工作。运行时变更需要外部同步和有文档记录的生命周期边界。 |

对象池的生命周期、失败补偿、容量与计数、值类型归还句柄、作用域及配置契约统一见 [对象池生命周期与配置契约](./Pooling.md)。池配置只描述池创建参数，应用配置文件、资源绑定与热更新策略继续由所有者包承担；当前不扩展为新的应用配置框架。

同步包装应靠近并发所有者，而不是放入 Core；只有至少三个使用方需要完全相同的原子性和生命周期语义时才例外。这样可以避免一个局部加锁的基础类型让人误以为更大的多步工作流也具有原子性。

## 后续基础层候选项

只有在存在具体使用方和基准数据后，才按以下顺序评估未来新增项：

1. 其他小型所有权和生命周期基础类型，前提是至少三个包重复了相同的租约或状态转换契约。不要仅因为组合内每个条目都可释放，就把领域组合类型提升到 Core。
2. 与序列化无关的版本范围或令牌，前提是线上、持久化和回放使用方共享比较、来源及兼容性语义。仅有相似的 `Version`、`Revision` 或 `Generation` 名称还不够。
3. 只有未来使用方满足上文更严格的共享行为规则时，才考虑 Guard、Result 和诊断契约。

并发集合、作业化/原生存储、序列化框架、依赖注入、ECS、网络及玩法生命周期策略均不属于 Core 职责。它们应通过窄契约依赖 Core。

## 迁移候选项

以下现有区域目前仍保持源码兼容，但应通过弃用周期评审，而不是继续原地扩展：

- `Continuous`：已拆到无依赖的 `com.abilitykit.continuous` 包和 `AbilityKit.Continuous` 命名空间。Core 只保留过时兼容实现至下一个主版本移除窗口；边界审计会阻止生产使用方重新依赖它。
- `Numerics`：**已移除（2026-08，首次发布前）**。过时的玩法修改器兼容接口面（`NumberValue` 等）没有生产使用方；唯一的框架使用方已迁移到 MOBA 自有的定点数 `CombatNumberValue`（原名 DamageNumberValue）。领域自有数值管线应遵循定点数集成指南。
- 配置与反射：MOBA 使用方现已采用所有者包内的设置和引导 API。Core 保留过时兼容类型至下一个主版本移除窗口；边界审计会阻止新使用方。
- 标记扫描：候选的宿主/发现服务。

公共命名空间迁移需要兼容垫片、下游迁移和主版本移除窗口。它们有意与基础层正确性修复分开进行。

碰撞形状、查询和边界读取已经由 `com.abilitykit.combat.collision.abstractions` 持有；录制、回放和规范状态差异分析已经由 `com.abilitykit.record` 持有。两个领域仍有历史 `AbilityKit.Core.*` 命名空间，但这些类型并不由 Core 程序集交付。Core 保留几何数学与稳定哈希等原语，不迁入领域实现，也不存在可直接删除的 Collision 或 Record 副本。
