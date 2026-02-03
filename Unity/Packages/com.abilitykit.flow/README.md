# AbilityKit Flow（com.abilitykit.flow）

`AbilityKit Flow` 是一套轻量、可组合的流程（workflow）运行时，用于在游戏业务中组织 **可中断、可取消、可超时、可并行** 的“任务流程”。

它的外形看起来像 tick 行为树（Behavior Tree），但定位更偏向 **异步流程编排 / 任务树（Task Tree）**：

- 支持 `Tick(deltaTime)`：与 Unity `Update` 对齐的推进方式。
- 支持 `Wake/Pump`：事件回调触发的立即推进（避免每帧空轮询）。
- 通过 `FlowContext` 提供运行时上下文与注入能力，并支持 `Scope`（作用域）隔离与自动回收。
- `FlowRunner` 内置异常捕获与可插拔上报（事件/委托），并保证异常时可靠收尾。

## 1. 核心概念

### 1.1 IFlowNode：可组合的流程节点

所有流程节点实现 `IFlowNode`：

- `Enter(ctx)`：进入节点（通常用于订阅事件/初始化）。
- `Tick(ctx, deltaTime)`：推进节点，返回 `FlowStatus`。
- `Exit(ctx)`：节点正常完成时的清理。
- `Interrupt(ctx)`：节点被取消/中断时的清理。

> 约定：`Exit/Interrupt` 应该是幂等/可重复调用安全（至少不应导致二次异常）。

### 1.2 FlowStatus：节点/流程的状态

- `Running`：未完成
- `Succeeded`：成功完成
- `Failed`：失败完成
- `Canceled`：被取消
- `NotStarted`：未开始（仅用于 runner/session 状态）

### 1.3 FlowRunner：驱动流程执行

`FlowRunner` 负责：

- 驱动 `Enter/Tick/Exit/Interrupt`
- 持有 `FlowContext`
- 提供 `Wake/Pump` 机制（通过 `FlowWakeUp`）
- 异常捕获与上报：
  - `event Action<Exception> UnhandledException`
  - `Action<Exception> ExceptionHandler { get; set; }`
- 防死循环：`MaxPumpIterationsPerWake`（默认 128）

### 1.4 Tick + Wake/Pump：两种推进方式共存

- `Step(deltaTime)`：外部主动 tick
- `FlowWakeUp.Wake()`：由节点在回调中触发“尽快推进”

这两种方式共存是合理的：

- Tick 适合时间驱动/每帧逻辑
- Wake 适合事件驱动（网络回包、UI点击、资源加载完成等）

**关键约束（非常重要）：**

- 回调触发 `Wake()` 必须在与 `FlowRunner` 相同线程执行（通常是 Unity 主线程）。
- 节点作者需假设：同一帧内可能出现多次 `Tick(0)`（因为 Pump）。

### 1.5 FlowContext：运行时上下文（带 Scope）

`FlowContext` 用 `Type -> object` 的方式存储数据，类似“强类型 blackboard / 轻量 DI 容器”。

并且支持作用域栈：

- `BeginScope()`：推入一个新的局部 scope，并返回 `IDisposable` 句柄
- scope 内 `Set<T>()` 只写入当前 scope；`TryGet<T>()` 优先查 scope 再查全局
- scope 释放后，局部注入自动回收

`FlowRunner.Start()` 会创建一个根 scope，并将 `FlowWakeUp` 注入到 scope 中；流程结束/Stop/异常都会自动释放根 scope。

## 2. 快速上手

### 2.1 最小示例：启动一个 FlowSession

建议从 `FlowSession` 开始使用：

- 管理 runner 生命周期
- 暴露 `Started/StatusChanged/Finished/UnhandledException` 事件

示例见：`Samples~/FlowExamples`。

### 2.2 处理异常（不依赖任何 Log facade）

你可以通过以下方式接入项目自己的日志/告警系统：

- 订阅 `FlowSession.UnhandledException`
- 或在 `FlowRunner.ExceptionHandler` 注入上报函数

### 2.3 配置 Pump 防死循环

当节点在回调里反复 `Wake()`，可能触发短时间内大量推进；
`FlowRunner.MaxPumpIterationsPerWake` 用于防止无限循环。

> 建议：线上保持默认值或根据业务调整；调试时可适当调小以尽早暴露死循环。

## 3. 常用节点（Blocks / Nodes）

> 以下仅说明语义，完整代码示例请看 Samples。

- `DoNode`：用委托快速构造节点
- `TimeoutNode(seconds, child)`：对子节点加超时，超时返回 `Failed`
- `ParallelAllNode(nodes...)`：并行执行所有子节点，全部完成后成功；任何失败则整体失败
- `RaceNode(nodes...)`：并行执行，首个完成的节点决定结果，并中断其他节点
- `FinallyNode(tryNode, finallyNode)`：try 完成后执行 finally；最终返回 try 的状态
- `UsingResourceNode<T>(create, dispose, body)`：进入时创建资源并注入到 context，离开/中断时自动移除并释放
- `AwaitCallbackNode(subscribe)`：订阅某个回调，回调完成后 `Wake` 推进并返回成功/失败

## 4. 设计理念与边界

- 本模块更偏向 **workflow / 任务树**：强调流程编排、组合结构、异常与收尾一致性。
- 它与通用行为树在结构层高度相似，但运行时语义更偏业务流程（超时、资源、回调唤醒、上下文注入）。
- 如果未来要做 AI 决策树，建议单独建立 `BehaviorTree` 模块或拆分出“纯树执行内核”，避免语义漂移。

## 5. Samples（示例）

在 Unity Package Manager 中选中 `com.abilitykit.flow`，点击 `Samples` 里的 `Flow Examples` 导入。

导入后可在 `Samples/FlowExamples` 查看示例代码。
