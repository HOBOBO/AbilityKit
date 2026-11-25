# AbilityKit 计算：Unity Jobs

此包是可选基础设施，安装后不会改变任何 AbilityKit 业务包的默认实现。

- `UnityJobPlan` 跟踪可复用的依赖图，不强制完成执行，使业务扩展能在原生内存中保留多阶段管线。业务扩展定义具体的 `[BurstCompile]` 作业结构，并将具体的 `.Schedule(...)` 调用保留在业务程序集中，以便 Unity Jobs/Burst 生成反射和 Player AOT 数据。
- `UnityComputeBuffer<T>` 为这些扩展提供明确的持久缓冲区所有权。

业务代码负责语义验证及托管回退。调整缓冲区大小或释放前，必须先完成所有使用该缓冲区的计划。

本包未提供通用 `IJobParallelFor` 或调度委托桥接。对于隐藏在泛型方法或委托分发后的作业，
Unity Collections 和 Burst 无法可靠生成 Player 反射及 AOT 数据。
