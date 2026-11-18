# AbilityKit Core

> 2026-08-14 边界更新：DebugDraw 的正式所有权已迁至 `com.abilitykit.diagnostics`；
> `DisposeUtils` 已由资源 owner 的生命周期策略替代；`MarkerSystem` 与三个全局
> Bootstrapper 入口已冻结。Core 中的旧类型仅用于下一个主版本前的兼容。

`com.abilitykit.core` 是 AbilityKit 的基础设施包。它不表达具体玩法规则，而是提供上层战斗、技能、同步、工具和示例都可以复用的通用运行时能力。

## 定位

Core 是第一批内部推广的 P0 基础底座包，适合被 `world.di`、`triggering`、`pipeline`、`combat.*`、`record`、`diagnostics` 等模块依赖。

它主要负责：

- 纯 C# 数学类型：`Vec2`、`Vec3`、`Quat`、`Transform3`。
- 日志抽象：`Log`、`ILogSink`、`NullLogSink`。
- 稳定集合和标识：`StablePriorityList<T>`、`SortedIntSet`、`StableHashV1`。
- 有界运行时存储：按整数帧键保留的 `IFrameIndexedBuffer<T>` 和按插入顺序保留的 `ISequentialBuffer<T>`。
- 所有权与时间契约：`PooledBufferOwner<T>`、`DisposableRegistration`、`IMonotonicClock`。
- 当前兼容的事件与对象池基础设施。

以下目录属于历史兼容面，不再接收新的领域能力：

- `Continuous`：已迁往零依赖的 `com.abilitykit.continuous`；旧命名空间仅保留到下一次主版本移除窗口。
- `Config`、`Reflection`：MOBA 消费方已迁至 `demo.moba.runtime` / `demo.moba.view.runtime`，Core 仅保留下一主版本前的兼容 API。
- `DisposeUtils`：释放策略由资源所有者负责，旧辅助入口仅保留到下一主版本移除窗口。
- `Markers`：保留既有注册和发现构件；`MarkerSystem` 及全局启动入口已冻结，程序集发现与宿主策略继续由所属包承接。
- `DebugDraw`：正式能力已迁至 `com.abilitykit.diagnostics`；Core 中的旧接口仅保留到下一次主版本移除窗口。

数学原语源码位于 `Runtime/Math`，与玩法数值修饰能力分开维护。应用设置和反射兼容入口不属于 Core 新能力的纳入方向，具体规则见包内边界文档。

## 不负责什么

- 不承载项目业务技能、Buff、角色或怪物逻辑。
- 不直接依赖 `demo.moba.*`、`demo.shooter.*` 或服务端 Demo。
- 不决定具体网络、帧同步或表现层方案。
- 不提供通用玩法数值修饰、持续行为、程序集发现或项目配置策略。

## 推荐接入

最小推广组合从 `Foundation` 开始：

```text
com.abilitykit.core
com.abilitykit.world.di
```

在这个组合中，Core 提供数学、日志、集合、标识、所有权和兼容中的事件/对象池能力；`world.di` 负责战斗世界或关卡作用域的服务装配。

## 验收要求

Core 进入内部 Starter 前至少满足：

- 包根目录有 README，说明定位、边界和推荐接入方式。
- `Documentation~/README.md` 能索引 Core 的主要子能力。
- Foundation 示例能展示日志、集合、所有权、事件或对象池中的至少两个能力。
- 不引入 Demo 包反向依赖。
- `package.json` 合法且版本与内部依赖策略一致。

## 相关文档

- [`Documentation~/README.md`](./Documentation~/README.md)：Core 文档索引。
- [`Documentation~/CoreBoundary.md`](./Documentation~/CoreBoundary.md)：准入规则、所有权和兼容迁移边界。
- [`Documentation~/ContinuousCompatibility.md`](./Documentation~/ContinuousCompatibility.md)：旧持续体的登记、回调、查询、重入限制与独立包迁移差异。
- [`Documentation~/ReflectionAndDisposalCompatibility.md`](./Documentation~/ReflectionAndDisposalCompatibility.md)：旧反射调用的异常、返回值及异步边界，以及释放回调、引用清空与重入契约。
- [`Documentation~/ConfigurationFileCompatibility.md`](./Documentation~/ConfigurationFileCompatibility.md)：旧设置文件读写、对象缓存失效、模块描述及应用迁移边界。
- [`Documentation~/Markers.md`](./Documentation~/Markers.md)：标记登记、实时查询、反射扫描及已冻结全局引导的兼容契约。
- [`Documentation~/DebugDrawCompatibility.md`](./Documentation~/DebugDrawCompatibility.md)：旧绘制值、编辑器设置、SceneView 回调及 Diagnostics 迁移限制。
- [`Documentation~/Mathematics.md`](./Documentation~/Mathematics.md)：数学单位、退化输入、旋转顺序及浮点边界。
- [`Documentation~/DeterministicMathBridge.md`](./Documentation~/DeterministicMathBridge.md)：定点桥接的转换差异、量化、几何溢出、函数定义域与跨目标限制。
- [`Documentation~/IdentifiersAndTiming.md`](./Documentation~/IdentifiersAndTiming.md)：版本化哈希、注册冲突、默认输出、时钟来源与换算边界。
- [`Documentation~/CollectionsAndBuffers.md`](./Documentation~/CollectionsAndBuffers.md)：集合顺序、缓冲容量、引用释放与租用数组所有权契约。
- [`Documentation~/Pooling.md`](./Documentation~/Pooling.md)：对象池回调、异常、归还责任、容量裁剪、作用域与配置契约。
- [`../README.md`](../README.md)：AbilityKit 包分级、推荐组合和 Starter 推进顺序。
