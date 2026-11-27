# Dataflow 使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.dataflow` 0.1.0，2026-11-25 Runtime 与 .NET 10 镜像
> 核校状态：部分核校；同步执行、槽位、结果和实例所有权已对照源码；局部 E3 见第 6 节
> 事实基线：2026-11-25

## 1. 目标与前置条件

用于同步处理链：一次请求经过若干 `IDataflowProcessor<TInput,TOutput>`，返回 `DataflowResult<TOutput>`。宿主提供输入、处理器、Context 与后续副作用；本包不控制线程、时钟、对象池或业务回滚。版本、Unity 和依赖条件见 [package.json](../package.json)，安装路径见[包 README](../README.md)。

## 2. 完整示例：同形处理与中断

本例不接世界或网络。两个阶段把正数乘二再加偏移量，负数由校验阶段中断。预期输出 `dataflow execution contracts passed`。

```csharp
using System;
using AbilityKit.Dataflow;

internal static class Program
{
    private static readonly DataflowSlot<int> Offset = new DataflowSlot<int>("Example.Offset", 5);

    private static void Main()
    {
        var pipeline = new DataflowPipeline<int, int>();
        pipeline.AddProcessor(new PositiveValidator());
        pipeline.AddProcessor(new Multiply());
        pipeline.AddProcessor(new AddOffset());
        var context = new DataflowContext();
        context.SetData(Offset, 5);
        var success = pipeline.Execute(10, context);
        Require(success.IsSuccess && success.Output == 25 && success.ProcessedCount == 3);

        context.Reset();
        var stopped = pipeline.Execute(-1, context);
        Require(stopped.IsAborted && !stopped.HasError && stopped.ProcessedCount == 1);
        // Execute 不会自动清除 Abort；每次复用由所有者重置。
        context.Reset();
        Require(pipeline.Execute(10, context).Output == 25);
        Console.WriteLine("dataflow execution contracts passed");
    }

    private static void Require(bool value)
    {
        if (!value) throw new InvalidOperationException("Dataflow example failed.");
    }
    private sealed class PositiveValidator : ValidatorProcessor<int>
    {
        protected override bool Validate(int input, IDataflowContext context) => input > 0;
    }
    private sealed class Multiply : DataflowProcessor<int, int>
    {
        protected override int OnProcess(int input, IDataflowContext context) => input * 2;
    }
    private sealed class AddOffset : DataflowProcessor<int, int>
    {
        protected override int OnProcess(int input, IDataflowContext context) => input + context.GetData(Offset);
    }
}
```

## 3. 阶段输入与结果判读

每个阶段共享同一组泛型。仅当某次输出 `is TInput` 时才把它作为下一阶段输入；`Request -> Result` 这样的异形管线通常每阶段仍读原 Request，需要领域 Context 或 typed slot 显式传递中间 Result。本包不是每阶段改变泛型的类型链。

| 执行结果 | 含义与调用者责任 |
| --- | --- |
| `IsSuccess` | 未中断且无错误；空管线也成功，Output 为 default、ProcessedCount 为 0 |
| `IsAborted` | Context 请求中断；末阶段中断同样返回 Aborted，保留该阶段输出并计入完成数量 |
| `HasError` | 阶段抛异常；`Error` 和 `FailedProcessorIndex` 定位失败；若该阶段的 `Name` 无法取得，`FailedProcessorName` 为 null。保留最后一个完整阶段的输出和已完成数量 |
| 预先已 Abort | 不执行阶段，返回 Aborted(default, 0) |

先检查状态，再消费 Output；中断和错误结果可能包含有用的部分输出，但不代表业务已经成功。基础 `DataflowProcessor` 的 before/process/after 钩子若抛错，该阶段不算完成；已发生的输入对象或 Context 副作用不会撤销。`ProcessedCount` 统计顶层阶段，组合处理器内部执行数不单独展开。

传 null Context 时创建基础 `DataflowContext`；需要领域状态的管线应显式传对应类型，不能依赖此兜底。配置 API 的参数校验可直接抛出；`Execute` 的阶段异常封装不代表所有 API 都不抛异常。

## 4. 槽位、清理与共享

- `DataflowSlot<T>` 使用非空名称；存储键是名称的 ordinal 值与 `typeof(T)`。同名同类型的不同 Slot 对象共享值；同名不同类型隔离。这与按实例身份隔离的其他包 ContextKey 不同。
- 未写入时 GetData 调用当前 Slot 的默认工厂，每次读取可重新创建值，不会自动缓存；TryGetData 返回 false。显式写入 null 是已存在值，不回退默认值。
- Context 用 `Dictionary<SlotKey,object>` 存储，值类型会装箱；不深拷贝值。槽位尽量静态复用，对可变引用值明确所有权。
- `Reset()` / `Clear()` 清除槽位、Source 与 Abort；派生 Context 应 override Reset 并先调用 base.Reset，再清领域字段。Execute 不自动重置，也不自动调用 SetSource。
- 每笔并发或重入执行独占一个 Context。调用者拥有它及其载荷，管线不 Dispose 输入、Context、Processor 或 Output。

## 5. 配置、扩展与故障定位

Execute 开始时 `_processors.ToArray()` 固定本次顶层列表。`AddProcessors` 会先验证全部参数再追加；Clone 和 Builder.Build 复制列表结构，但共享 Processor 实例。Composite 构造时复制输入数组，内部按相同的兼容回灌原则执行，观察 Abort 后停止；它不提供嵌套失败诊断树。

配置列表无并发同步，不应一边 Execute 一边修改配置。执行快照不等于安全热更；共享运行必须保证 Processor 无跨执行可变状态，并为每次执行提供独立 Context/输入。需要发布新配置时由宿主构建新管线并明确切换边界。

常见问题：第二次执行直接中断，先检查是否 Reset；异形阶段丢中间值，检查是否通过 Context 传递；默认值重复创建，检查是否需要先显式 SetData；失败输出为空，确认失败前是否有完整阶段。没有自动事务回滚、阶段耗时统计或跨平台数值确定性承诺。

## 6. 验证范围与关联文档

2026-11-25 对照 [Pipeline](../Runtime/Core/Pipeline/DataflowPipeline.cs)、[Result](../Runtime/Core/Pipeline/DataflowResult.cs)、[Context](../Runtime/Core/Context/DataflowContext.cs)、[Slots](../Runtime/Core/Context/DataflowSlots.cs)和[通用处理器](../Runtime/Core/Processor/CommonProcessors.cs)。同源 .NET suite 与本页完整示例的本次结果由仓库 D4 记录持有，证据限 E0 与局部 E3，不等于完整 gate、Unity、性能预算或独立发布通过。

内部分析见[局部设计](../Document/Dataflow数据流处理模块开发设计文档.md)，新接入以本页操作契约为入口；返回[文档导航](README.md)。
