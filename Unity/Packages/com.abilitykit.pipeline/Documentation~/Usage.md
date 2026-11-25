# Pipeline 使用契约与限制

> 文档类型：包使用契约
> 核校状态：2026-11-25 对照 `PipelineRuntime`、`IAbilityPipelineContext`、生命周期接口及 `Samples~/PipelineUsage`；.NET Pipeline 测试 44 项通过，未验证 Unity Player。

从[包 README](../README.md)安装并导入 **Pipeline Usage** 样例。包提供一次能力运行的阶段编排，宿主持有 Run 并提供每步时间；阶段和领域动作不决定技能规则、逻辑时钟或持久化策略。

## 装配和推进

Unity 使用包自动初始化的 `Pipeline` 门面。跨宿主需要隔离注册表时，显式持有 `PipelineRuntime`，初始化后为 Pipeline 传入该 Runtime；省略时会沿用全局 `PipelineRegistry.Instance`。`.NET` 镜像的同名门面不含 Unity 场景加载钩子，只有该镜像提供静态 `Pipeline.Initialize()`。

以下完整方法使用导入样例的类型，按固定步长有界推进技能管线。预期重复效果执行三次并进入 `Completed`；实际宿主应每逻辑步调用一次 `Tick`：

```csharp
using System;
using AbilityKit.Pipeline;
using AbilityKit.Pipeline.Examples;

public static class PipelineQuickStart
{
    public static int Run()
    {
        var count = 0;
        var pipeline = AbilityPipelineUsageExample.BuildSkillPipeline((ctx, index) => count++);
        var context = new ExampleAbilityPipelineContext();
        var config = new DefaultAbilityPipelineConfig();
        var run = pipeline.Start(config, context);
        try
        {
            for (var step = 0; step < 100 && run.State == EAbilityPipelineState.Executing; step++)
                run.Tick(0.1f);
            if (run.State != EAbilityPipelineState.Completed || count != 3)
                throw new InvalidOperationException("Pipeline did not complete as expected.");
            return count;
        }
        finally
        {
            run.Release();
        }
    }
}
```

`State` 来自 `IPipelineRunControl`，执行中为 `Executing`，终态为 `Completed` 或 `Failed`；取消标志另见 `IsCancelled`。宿主负责步长来源、暂停和中断时机、领域结算及跨帧状态捕获。

## 运行与阶段状态

- 可能被重叠 Run 共享、且含可变执行状态的阶段，应通过 `IAbilityPipelinePhaseInstanceFactory<TCtx>` 为每个 Run 创建独立实例。仅继承阶段基类不会自动复制或隔离其状态。
- 阶段工厂、重置或启动期间失败时，运行时归还其已租借的阶段列表；它不因失败而释放业务 Context。
- 旧阶段没有实例工厂时会复用原对象。若它含运行状态，调用方必须确保同一时间不会并发/重叠运行，或先实现实例工厂。
- `SetRegistry` 仅影响随后注册的运行。已有 Run 向创建时记录的原注册表注销。
- `Release()` 结束运行生命周期和登记，不会重放业务回调，不负责 Context 中资源释放。

## 所有权、恢复与时间

管线实例、Run、Context、阶段实例和业务资源是不同所有者。确认 `ReleaseContext`、实例工厂和宿主注册表的责任后再交付运行。`SharedData` 是实时兼容字典，不是可校验快照；不要放入订阅、委托或服务引用并假定能恢复。

恢复业务字段需由调用方提供 `PipelineExecutionProgress` 捕获、校验和写回。先校验全树，再应用恢复；写回回调失败没有事务回滚。写回应直接设置已捕获状态，不要重放阶段进入动作。

阶段推进以宿主传入 `Tick(deltaTime)` 为准。样例中的 `AAbilityPipelineContext.ElapsedTime` 读取 `TimeProvider.Instance.RealtimeSinceStartup`，不代表系统自动采用逻辑帧时钟；参与确定性模拟时，应使用宿主权威时间并在恢复时一并处理。

## 源码与证据

- [样例入口](../Samples~/PipelineUsage/AbilityPipelineUsageExample.cs)：阶段组合和上下文归属。
- `Runtime/PipelineRuntime.cs`、`Runtime/Interface/IAbilityPipelinePhaseInstanceFactory.cs`：隔离注册表和阶段实例工厂。

本轮 `core-stability` Pipeline 单步 44/44 通过。未执行 Unity Editor、Player 或完整技能闭环。
