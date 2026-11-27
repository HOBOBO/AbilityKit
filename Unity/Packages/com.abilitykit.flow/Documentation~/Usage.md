# Flow 使用契约与限制

> 文档类型：包使用契约
> 核校状态：2026-11-25 对照运行时实现和样例；记录工作区验证，不代表发布候选签核。

从[包 README](../README.md)导入 Flow Examples。Flow 负责组合和推进节点树；宿主持有 Session/Host，提供 Tick 节奏和外部事件路由。流程不是后台任务调度器。

## 启动和推进

Unity 使用 `FlowPools.RegisterDefaultConfig()` 注册默认池配置，再创建 `FlowSession`，订阅事件并调用 `Start(root)`。Start 会同步执行一次 `Step(0)`，使根节点立即进入并可订阅事件。之后宿主按逻辑步调用 `Step(deltaTime)`。正常完成或 `Stop()` 只结束当前运行，Session 仍可再次启动；不再使用时才释放。

`FlowWakeUp` 只属于取得它的那次运行。运行收尾时句柄失效，不再持有 Runner；之后即使同一 Runner 重新启动或归池后重新租借，旧句柄的 `Wake()` 也是无操作。有效的 `Wake()` 可能在调用线程同步推进节点树，不提供跨线程同步或调度保证；Unity 回调应在主线程唤醒。重复 Wake 会以 `Step(0)` 再推进，默认每次 Wake 最多 Pump 128 次；超过上限报告异常、将运行标为 Failed，并尝试中断根节点。

## Context 和资源

`FlowContext` 按类型存储值。`BeginScope()` 创建槽位作用域，须按后进先出顺序释放，不会对槽内对象调用 `Dispose()`。Runner 的根作用域承载 `FlowWakeUp` 与诊断对象；结束运行时一并释放该次运行内未关闭的子作用域，保留外层数据。旧的子作用域句柄此后失效，不会移除新运行的槽位。`FlowRunner.Dispose()` 还会清空整个 Context。

`UsingResourceNode<T>` 调用 create 并将值写入当前 Context 层。收尾时先清理仍活动的子节点，再移除资源槽并调用 dispose 委托；即使子节点清理抛错，也会尝试释放资源。dispose 抛错后不会再次释放同一个值。这不是跨节点事务，不会回滚委托已执行的副作用。

## 失败与收尾

提供器或 `FlowSession.Started` 抛错时，异常直接传给调用方，Runner 不启动此次运行。Runner 自身的启动通知抛错时，先以 `Failed` 收尾，再传播原异常；尚未进入的根节点不会收到 `Interrupt()`。`Enter()` / `Tick()` 异常会上报，Runner 尝试中断根节点，以 `Failed` 收尾。显式 `Stop()` 使用 `Interrupt()`，并在完成作用域清理和完成通知后传播中断阶段的异常。

终态通知抛错会上报，但不会阻断运行作用域清理；退出或中断时，诊断抛错也不会跳过该节点的清理。

`ParallelAllNode` 和 `RaceNode` 在进入途中失败时，只清理已经尝试进入的分支。退出或中断时按输入顺序尝试清理每个仍活动的分支，不因前一项抛错而跳过后续分支；`RaceNode` 仍先退出胜出分支，再中断其余分支。这两个组合节点和 `UsingResourceNode<T>` 在同一次收尾中遇到单个异常时保留原异常，多个异常时按清理顺序放进 `AggregateException`。这不改变 `FinallyNode` 的阶段调度，也不保证自定义节点内部的其他清理会继续执行。

`SequenceNode`、`IfNode`、`SwitchNode<TKey>`、`TimeoutNode` 和 `FinallyNode` 在调用子节点的 `Enter()` 前登记活动状态。若子节点的 `Enter()` 抛错，Runner 会尝试中断该子节点；若进入诊断先抛错、未调用子节点的 `Enter()`，则不清理该子节点。`Exit()` 或 `Interrupt()` 开始前会清除活动状态，收尾抛错后不会再调用同一子节点的清理回调。`FinallyNode` 的后续阶段仍只在主阶段返回终态且退出成功后启动，不因异常或主动取消而启动。

节点、诊断和通知回调不得重入同一流程的启动、停止、释放或归池；这也适用于 `FlowSession.Started` 和 `FlowHost` 的根节点提供器。重入释放以 `InvalidOperationException` 拒绝，不解绑回调，也不归还仍在执行的对象。Runner 回调中的 `Step()` / `Wake()` 不进行嵌套遍历；Runner 启动期间的请求由首次 `Step(0)` 承接，结束时丢弃。

Unity 宿主如需在 `Finished` 后释放 Flow，应在回调中仅登记释放请求，等 `Start()`、`Step()` 或 `Wake()` 的调用栈退出，再由宿主执行释放。不要在 `Finished` 里同步调用 `Dispose()` 或归池。

直接构造的 Session/Host 由调用方 `Dispose()`；从 `FlowPools` 租借的对象使用对应的 `ReleaseRunner`、`ReleaseSession` 或 `ReleaseHost`。Session/Host 释放前解绑订阅，不派发释放导致的终态通知；如果中断抛错，先完成归还，再抛出原异常。收到这类清理异常后不要重试归还；归还后也不得继续使用原租借引用。

节点可能因 Pump 在同一帧被多次以 `deltaTime = 0` 推进。时间节点应正确处理零步长；Enter/Exit/Interrupt 应保持幂等。Flow Runner 和 Context 没有并发保证。

## 源码和验证范围

- `Runtime/Flow/FlowRunner.cs`、`FlowSession.cs`、`FlowContext.cs`：驱动、所有权和 Scope。
- `Runtime/Flow/Blocks/UsingResourceNode.cs`：资源回调顺序。
- [Flow Examples](../Samples~/FlowExamples/README.md)：Session、Wake、组合、Scope 和异常样例。

- .NET：`foundation-units` 的 `Flow engine tests` 单步 286 项通过，覆盖旧唤醒句柄、异常归池、回调重入和组合节点异常收尾。
- Unity 2022.3.62f1：`instance2` 共享源码工程通过 Editor 批处理编译。另在干净工程只直接声明 Flow，从本地注册表安装候选归档；验证 Flow 与 HFSM Core 的程序集来源并执行 `StateMachineFlowRunner`，未解析完整 HFSM、Editor Platform、AI 或 Animation。
- 同一隔离工程仅额外引入 Unity Test Framework，Starter PlayMode 2 项通过，覆盖启动、完成后释放、取消、重启及旧唤醒句柄失效。未验证正式注册表安装、独立 Player 或跨线程派发。
