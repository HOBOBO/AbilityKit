# AbilityKit Threading

服务端与高性能场景的并发工具集。不依赖任何其他 AbilityKit 包，可独立选装。

## 为什么需要它

战斗服、帧同步服等逻辑密集型服务需要低开销的并发原语：标准库的 `System.Threading.Channels`
和 `ConcurrentQueue` 在高频小对象场景下分配和竞争开销偏高，且缺少纤维级调度、工作窃取和
线程亲和控制。本包提供一组为逻辑帧循环设计的替代品。

## 能力清单

| 分类 | 类型 | 说明 |
|---|---|---|
| 调度 | `FiberScheduler` | 纤维调度器，轻量协作式任务调度 |
| 调度 | `DynamicThreadPool` / `ThreadPoolManager` | 动态线程池与线程池管理 |
| 调度 | `PriorityWorkQueue` / `ThreadAffinity` | 优先级工作队列、线程亲和绑定 |
| 集合 | `MpscQueue` / `WorkStealingQueue` | 多生产者单消费者队列、工作窃取队列 |
| 集合 | `ConcurrentCollections` | 无锁/低锁集合 |
| 同步 | `SpinLock` / `ReaderWriterLock` / `Events` | 自旋锁、读写锁、事件 |
| 原子 | `AtomicTypes` | 原子类型 |
| 通道 | `Channel` | 低分配通道 |
| 内存 | `Allocators` / `ArrayPool` / `ObjectPool` | 分配器、数组池、对象池 |
| 并行 | `Partitioner` | 数据分区 |
| 观测 | `LoadMetrics` | 负载指标 |

## 选装建议

- 逻辑跑在单线程帧循环里 → 通常只需要 `MpscQueue`（主循环排空跨线程输入）。
- 自研 job 化或服务器 worker → `DynamicThreadPool` + `WorkStealingQueue`。
- 只想要对象池/数组池 → 优先考虑 `com.abilitykit.core` 的 Pooling（与 Unity 侧共享实现语义）。

## 依赖

无 AbilityKit 内部依赖。Unity 2022.3+。
