# Flow — 流程编排引擎

> 历史设计说明入口：正文保留旧术语和示意 API，不能作为当前调用契约。请从[包使用文档](../Documentation~/README.md)开始；本文仍作为历史概念材料阅读。

## 设计理念

Flow 回答的问题是："如何组织异步/时间驱动的复杂逻辑？"

Flow 基于 IFlowNode 节点树组织逻辑，每个节点有 `Enter`/`Tick`/`Exit`/`Interrupt` 四个生命周期方法。FlowContext 作为作用域 Type→object 字典，在节点树间传递数据。WAKE/PUMP 机制让 Flow 可以等待外部信号（异步完成、事件）再继续执行，而无需阻塞线程。

**与 Pipeline 的关系**：Pipeline 面向技能执行的结构化阶段，Flow 面向通用异步协调逻辑。Pipeline 的阶段是预定义的，Flow 的节点是动态组合的。

**与 HFSM 的关系**：`HfsmFlowRunner` 将 HFSM 作为 Flow 节点嵌入，使状态机可以被 Flow 的 Sequence/Parallel/Timeout 等组合器管理。

## 核心抽象

```
IFlowNode
├── Enter(context)        → 初始化
├── Tick(dt, context)     → 每帧推进
├── Exit(context)         → 清理
└── Interrupt(context)    → 中断

FlowRunner
├── Step(dt)             → 推进一步
├── Wake()               → 唤醒等待中的节点
└── Reset()              → 重置

FlowContext                → Scoped DI (Set/Get/TryGet<T>)
```

## 内置节点

| 节点 | 说明 |
|------|------|
| `SequenceNode` | 顺序执行子节点，任一失败则整体失败 |
| `RaceNode` | 并行执行，首个完成的子节点决定结果 |
| `ParallelAllNode` | 并行执行，所有子节点成功才整体成功 |
| `IfNode` | 条件分支，根据谓词选择分支 |
| `SwitchNode` | 多路分支 |
| `TimeoutNode` | 超时装饰器 |
| `AwaitCompletionNode` | 等待外部 FlowCompletion.Set() 信号 |
| `RunUntilCompletionNode` | Tick 一个异步任务直到完成 |
| `TickWhileNode` | 循环执行，回调每帧触发 |
| `UsingResourceNode` | RAII 模式（创建→使用→释放） |
| `DoNode` | 叶子节点，执行回调 |
| `WaitSecondsNode` | 等待指定秒数 |

## 快速示例

```csharp
// 顺序执行：等待 0.5s，然后执行条件分支
Sequence(
    WaitSeconds(0.5f),
    If(
        () => hasTarget,
        Do(() => ApplyEffect()),
        Do(() => ShowMiss())
    ),
    Finally(() => ClearState())
)

// 使用 FlowContext 传递数据
Sequence(
    CreateResource<Target>("Target", () => FindTarget()),
    UsingResource<Target>("Target", target => {
        Do(() => target.ApplyDamage(damage));
        WaitSeconds(1.0f);
    }),
    DisposeResource<Target>("Target")
)
```

## WAKE/PUMP 机制

旧示意中的 `FlowCompletion.Set()` 已不是当前 API；当前方法名是 `Complete(bool succeeded)`。`Complete` 会直接调用绑定的 `FlowWakeUp.Wake()`，实现没有跨线程同步或调度保证，因此应在 Runner 所在线程调用。后台操作需先把完成通知投递到宿主线程，再调用 `Complete`：

```csharp
var completion = new FlowCompletion();
// 在 FlowRunner 所在线程处理已投递的完成通知。
completion.Complete(succeeded: true);
```
