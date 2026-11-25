# Flow 使用契约与限制

> 文档类型：包使用契约
> 核校状态：2026-11-25 对照 `FlowRunner`、`FlowSession`、`FlowContext`、`UsingResourceNode<T>` 和 Samples；.NET Flow 单步 236 项通过，未运行 Unity Player。

从[包 README](../README.md)导入 Flow Examples。Flow 负责组合和推进节点树；宿主持有 Session/Host，提供 Tick 节奏和外部事件路由。流程不是后台任务调度器。

## 启动和推进

Unity 使用 `FlowPools.RegisterDefaultConfig()` 注册默认池配置，再创建 `FlowSession`，订阅事件并调用 `Start(root)`。Start 会同步执行一次 `Step(0)`，使根节点立即进入并可订阅事件。之后宿主按逻辑步调用 `Step(deltaTime)`；正常完成、Stop 或 Dispose 时释放 Session。

`FlowWakeUp.Wake()` 可能同步调用 Pump，并在调用线程推进节点树。它没有跨线程同步或调度保证；Unity 回调应在主线程唤醒。重复 Wake 会以 `Step(0)` 再推进，默认每次 Wake 最多 Pump 128 次；超过上限报告异常、将运行标为 Failed，并尝试中断根节点。

## Context 和资源

`FlowContext` 按类型存储值。`BeginScope()` 创建槽位作用域；释放只清除该层字典并归还池化字典，不会对槽内对象调用 `Dispose`。Runner 的根 Scope 承载 `FlowWakeUp` 与诊断对象；Runner Dispose 还会 Clear 整个 Context。

`UsingResourceNode<T>` 负责调用 create、将值写入当前 Context 层，在 Exit/Interrupt 时先移除槽，再调用传入的 dispose 委托。该委托抛错时，节点不会完成后续字段复位，且父组合节点后续清理可能被中断。清理动作应幂等、短小并自行捕获或汇总异常；不要把它当成跨节点事务。

## 失败与收尾

Enter/Tick 异常会经 `UnhandledException` / `ExceptionHandler` 上报，随后 Runner 尝试 Interrupt 根节点、置为 Failed、清理内部 Context 项并通知 Finished。Exit 或 Interrupt、Observer、Finished 等用户回调也可能失败；部分通知和清理顺序是 best-effort，其他清理不保证完成，也不回滚已经执行的副作用。Stop 使用 Interrupt 并将运行标为 Canceled；清理回调异常可能向调用者传播。

节点可能因 Pump 在同一帧被多次以 `deltaTime = 0` 推进。时间节点应正确处理零步长；Enter/Exit/Interrupt 应保持幂等。Flow Runner 和 Context 没有并发保证。

## 源码和验证范围

- `Runtime/Flow/FlowRunner.cs`、`FlowSession.cs`、`FlowContext.cs`：驱动、所有权和 Scope。
- `Runtime/Flow/Blocks/UsingResourceNode.cs`：资源回调顺序。
- [Flow Examples](../Samples~/FlowExamples/README.md)：Session、Wake、组合、Scope 和异常样例。

本轮通过现有 `core-stability` Flow 单步 236 项；未执行 Unity Editor/Player，也未在真实宿主验证跨线程派发或异常清理策略。
