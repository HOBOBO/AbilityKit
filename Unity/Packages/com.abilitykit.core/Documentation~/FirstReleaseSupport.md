# Core 首发支持清单

适用于 `com.abilitykit.core@0.1.0` 候选。本清单规定**拟随首发交付的支持等级**，不代表候选已通过正式签核或已经发布。各类型的详细行为以对应契约文档为准。

## 稳定并推荐

| 能力 | 首发 API | 承诺边界 |
| --- | --- | --- |
| [数学](./Mathematics.md)与[定点桥接](./DeterministicMathBridge.md) | `Vec2`、`Vec3`、`Quat`、`Ray3`、`Transform3`、`MathUtil`、`DeterministicMathBridge` | 保持已有运算、转换、量化和失败行为；不承诺浮点锁步或 Unity 与 .NET 跨目标位一致。 |
| [集合](./CollectionsAndBuffers.md) | `StablePriorityList<T>`、`PriorityDirection`、`SortedIntSet` | 保持排序、同优先级的插入顺序和整数索引行为；不决定 World 执行阶段。 |
| [标识与单调计时](./IdentifiersAndTiming.md) | `StableHashV1`、`StableStringIdRegistry`、`IMonotonicClock`、`StopwatchMonotonicClock`、`MonotonicTime` | 保持各哈希入口的既有输出和注册表冲突行为，不承诺哈希唯一。单调计时用于耗时测量，不是模拟 Tick、回放时钟或调度器。 |
| 日志与所有权 | `ILogSink`、`Log`、`NullLogSink`、`PooledBufferOwner<T>`、`PooledBufferClearMode`、`DisposableRegistration` | 保持日志门面、托管数组归还和单次注册释放契约；日志后端、日志投递策略和业务生命周期由宿主持有。 |
| [实例对象池](./Pooling.md) | `ObjectPool<T>`、`ObjectPoolOptions<T>`、`PooledObject<T>`、`PoolScope`，及其公开签名使用的选项、键、回调与统计类型 | 新代码优先由宿主持有实例或局部作用域。复制句柄不增加归还权；释放作用域不撤销先前取出的池引用。 |
| [事件键](./Eventing.md) | `EventKey`、`EventKey<TArgs>` | 保留值身份和类型化契约；推荐新代码用泛型键。不因此统一 Triggering、World 等领域的事件传递。 |

## 随包保留：高级与共享入口

这些公开接口保持既有行为和可用性，不作为新项目的默认入口，也不扩大到领域策略。

| 接口 | 保留理由与限制 |
| --- | --- |
| `EventDispatcher`、`IEventSubscription`、`GlobalEventDispatcher` | 实例派发保持同步顺序、一次性监听、退订与参数释放行为；参数自动释放可能使用进程级 `Pools`。全局入口继续兼容，不提供世界隔离，未新增弃用标记。 |
| `Pools`、`PoolRegistry`、`PoolManager`、池配置与诊断 API | 维持现有全局池、命名作用域、配置接入和诊断行为。Pipeline、Motion 等已有生产调用；不承诺全局关闭屏障、配置登记引用计数或诊断对象深度不可变。 |
| `IBufferCapacityControl`、`IFrameIndexedBuffer<T>`、`ISequentialBuffer<T>` 及现有 Sparse、Ring、List 后端 | StateSync 的输入与快照缓冲构造器公开接受帧索引接口；Network Runtime 的远端快照缓冲公开接受顺序接口，默认使用环形实现。保留现有存储、顺序、容量和引用释放行为，不承诺回滚或插值策略。 |

## 历史兼容与待评审范围

- **既有弃用窗口**：Core 中的旧 Continuous、DebugDraw、应用配置、`ReflectionInvokeUtils` 和 `DisposeUtils` 按各自兼容契约保留，不作为新接入目标。新接入分别由 Continuous、Diagnostics、应用/引导包或资源所有者负责。Core Editor 中的旧 DebugDraw 不属于新的 Unity 接入面；已退役的 `AbilityKit.Core.Unity` 不再交付，亦不恢复 `UnityWebRequest` 依赖。
- **Marker 兼容**：现有特性、扫描器与实例注册表继续支持既有消费者；`MarkerSystem` 和三个全局引导入口保持冻结，不新增程序集扫描或静态注册副作用。
- **Observability 待评审**：`RuntimeObjectKey`、`ObservationDefinitionRef`、`ObservationTraceRef`、`IObservationSink<TEvent>`、`NullObservationSink<TEvent>` 目前会随候选源码包交付，但不纳入首发稳定面，签名继续留在 `PublicAPI.Unshipped.txt`。生产使用集中在同一条 MOBA 诊断链，迁移或移除需另行评审。三个独立使用方是新原语的准入要求，不是删除既有 API 的依据。

## 基线与制品签核

- `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` 是 .NET 公共签名检查输入，不代替本清单，也不证明发布状态。单独核对签名新增、可空性修订和兼容声明；不因记录本清单而批量移动条目。签核固定发布输入时，再按已接受的签名变更更新基线，待评审项保持原状态。
- Core 直接依赖 `com.abilitykit.deterministic@0.1.0`。必须对两包的实际候选制品与必要依赖闭包一起取证，记录输入、包锁、制品哈希和独立消费结果。
- [独立消费验证](./IndependentConsumption.md)记录本地 `.NET 10` 验证包与 Windows Unity `2022.3.62f1` Editor 本地 UPM 制品的证据。这不等于正式 NuGet 渠道或计划注册表的传递安装；同版本制品替换与回退也不等于从正式旧版升级。当前范围不含 Player、Mono、IL2CPP 或跨目标位一致性验收。
- [验证门禁与发布缺口](./ValidationAndReleaseReadiness.md)另行记录严格输入、发布渠道和签核条件；未完成正式签核不得将 `candidate` 改为 `ready`。
