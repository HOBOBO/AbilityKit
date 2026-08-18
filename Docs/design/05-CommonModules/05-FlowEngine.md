# 5.5 Flow 流程引擎

> 本文基于 `Unity/Packages/com.abilitykit.flow/Runtime` 源码说明 AbilityKit Flow 的运行模型。Flow 不是 Console Demo 里的 `BattleFlow` 命名约定，而是一套通用流程节点引擎：用 `IFlowNode` 表达可进入、可推进、可退出、可中断的流程节点，用 `FlowRunner` 承载运行状态，用 `FlowContext` 传播作用域数据，用 block 和 stage provider 组合出顺序、并行、竞速、超时和 finally 等控制结构。

---

## 目录

- [5.5 Flow 流程引擎](#55-flow-流程引擎)
  - [目录](#目录)
  - [1. 能力定位](#1-能力定位)
  - [2. 源码入口](#2-源码入口)
  - [3. 总体结构](#3-总体结构)
  - [4. 节点生命周期](#4-节点生命周期)
  - [5. Runner、Session 与 Host](#5-runnersession-与-host)
    - [5.1 同步执行](#51-同步执行)
    - [5.2 池化与所有权](#52-池化与所有权)
  - [6. FlowContext 与作用域数据](#6-flowcontext-与作用域数据)
  - [7. 控制结构与组合节点](#7-控制结构与组合节点)
  - [8. 阶段贡献模型](#8-阶段贡献模型)
  - [9. 唤醒、泵循环与诊断](#9-唤醒泵循环与诊断)
    - [9.1 HFSM 适配边界](#91-hfsm-适配边界)
  - [10. 和 Pipeline、Triggering、Demo Flow 的边界](#10-和-pipelinetriggeringdemo-flow-的边界)
  - [11. 扩展边界与验证](#11-扩展边界与验证)
  - [12. 和其他文档的关系](#12-和其他文档的关系)

---

## 1. 能力定位

Flow 解决的是“一个可被外部 Tick 或事件唤醒推进的流程，如何用小节点组合、隔离上下文、统一中断和诊断”的问题。它适合表达：

| 场景 | Flow 负责 | 不负责 |
|------|-----------|--------|
| 会话入场 | 按顺序连接、加载、创建上下文、进入运行态 | 网络协议、房间状态本身 |
| 战斗启动 | 把准备、校验、资源创建、finally 清理组合成流程 | ECS System 的每帧业务遍历 |
| 长流程等待 | 等回调、等条件、等秒数、超时失败 | 具体条件语义和业务动作 |
| 工具或测试脚本 | 用节点复用顺序、并行、竞速、重复逻辑 | 断言框架或外部测试报告 |

设计上，Flow 的核心不是“状态机”，而是“可组合流程树”。每个节点只需要实现四段生命周期，组合节点负责把多个子节点编排成控制流。

| 层级 | 应负责 | 不应由该层统一规定 |
|------|--------|--------------------|
| Flow 包 | 节点生命周期、Runner/Session、组合节点、唤醒和 pump 限制 | 登录、选角、技能、结算等项目阶段目录 |
| Host / World 接入 | 驱动时机、session 所有权、停止/销毁和诊断出口 | 替项目选择流程树或失败恢复策略 |
| 项目应用层 | 组装业务节点、上下文数据、外部事件订阅和补偿逻辑 | 将项目流程名称下沉为通用节点 |
| Samples / Starter | 给出 Sequence、Race、等待和启动示例 | 作为统一 Battle Flow 应用运行时 |

Flow 的开箱即用价值来自控制结构和生命周期契约，而不是来自一套固定战斗流程。项目专用编排可以大量复用节点机制，同时继续拥有自己的阶段、上下文和失败策略。

---

## 2. 源码入口

| 类型 | 源码 | 职责 |
|------|------|------|
| `IFlowNode` | [IFlowNode.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/IFlowNode.cs) | 节点四段生命周期接口 |
| `FlowRunner` | [FlowRunner.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/FlowRunner.cs) | 单次流程运行器，维护根节点、状态、上下文、唤醒和诊断 |
| `FlowExecutionOptions` | [FlowExecutionOptions.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/FlowExecutionOptions.cs) | 同步执行的步长、步数上限、异常重抛和诊断选项 |
| `FlowNodeExtensions` | [FlowNodeExtensions.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/FlowNodeExtensions.cs) | 提供池化 runner 驱动的同步 `Execute` 入口 |
| `FlowSession` | [FlowSession.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/FlowSession.cs) | 对外会话包装，复用 runner 并暴露 Started/Finished 事件 |
| `FlowHost<TArgs>` | [FlowHost.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/FlowHost.cs) | 将参数化 root provider 和 session 组合成可启动宿主 |
| `FlowPools` | [FlowPools.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/Pooling/FlowPools.cs) | 管理 Context、Runner、Session、Host、scope map 和完成对象的租借与释放 |
| `FlowContext` | [FlowContext.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/FlowContext.cs) | 按类型存取运行时对象，支持作用域栈 |
| `FlowWakeUp` | [FlowWakeUp.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/FlowWakeUp.cs) | 事件回调唤醒 runner 的轻量句柄 |
| `FlowDiagnostics` | [FlowExecutionOptions.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/FlowExecutionOptions.cs) | 包装 Enter/Tick/Exit/Interrupt 并记录观察事件 |
| `ParallelAllNode` | [ParallelAllNode.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/Blocks/ParallelAllNode.cs) | 等待全部子节点终态，再把任一非成功折叠为失败 |
| `RaceNode` | [RaceNode.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/Blocks/RaceNode.cs) | 按数组推进，第一个终态子节点决定结果并中断其余节点 |
| `FinallyNode` | [FinallyNode.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/Blocks/FinallyNode.cs) | 执行 finally 节点，但保留 try 节点的最终状态 |
| `UsingResourceNode<T>` | [UsingResourceNode.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/Blocks/UsingResourceNode.cs) | 将资源放入 Context，并在退出或中断时移除和释放 |
| `OrderedStagedFlowRootProvider<TArgs>` | [OrderedStagedFlowRootProvider.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/Stages/OrderedStagedFlowRootProvider.cs) | 将阶段、贡献者和核心 provider 编译成根节点 |
| `HfsmFlowRunner` | [HfsmFlowRunner.cs](../../../Unity/Packages/com.abilitykit.flow/Runtime/Flow/HfsmFlowRunner.cs) | 用事件队列驱动 HFSM 的独立适配器 |

---

## 3. 总体结构

```mermaid
flowchart TB
    Args["启动参数 TArgs"] --> Provider["IFlowRootProvider<TArgs>"]
    Provider --> Root["IFlowNode root"]
    Host["FlowHost<TArgs>"] --> Session["FlowSession"]
    Session --> Runner["FlowRunner"]
    Runner --> Context["FlowContext"]
    Runner --> Root
    Runner --> Wake["FlowWakeUp"]
    Runner --> Diag["FlowRuntimeDiagnostics"]
    Root --> Blocks["Sequence / Parallel / Race / Timeout / Finally"]
    Blocks --> Nodes["Action / Wait / AwaitCallback / Repeat"]
    Diag --> Observer["IFlowObserver"]
    Diag --> Trace["IFlowTraceRecorder"]
```

这条链路的关键边界是：

1. `FlowHost<TArgs>` 只知道如何从参数创建 root，不关心 root 内部结构。
2. `FlowSession` 只包装一次运行会话和外部事件，不持有业务状态。
3. `FlowRunner` 是唯一推进者，负责状态转换、异常处理、唤醒泵循环和 finally 清理。
4. `IFlowNode` 是组合单位，普通节点和 block 节点使用同一生命周期。
5. `FlowContext` 是运行时数据总线，避免把 runner、diagnostics、wake handle 等依赖塞进每个节点构造函数。

---

## 4. 节点生命周期

`IFlowNode` 的接口非常小：

| 方法 | 调用时机 | 语义 |
|------|----------|------|
| `Enter(ctx)` | 节点开始运行前 | 初始化节点本次运行状态、订阅事件、创建资源 |
| `Tick(ctx, deltaTime)` | runner 或父节点推进时 | 返回 `Running`、`Succeeded`、`Failed` 或 `Canceled` |
| `Exit(ctx)` | 节点正常结束后 | 清理正常路径资源，交给父节点或 runner 记录完成状态 |
| `Interrupt(ctx)` | 父节点、runner、超时或竞速失败时 | 撤销订阅、取消等待、释放未完成资源 |

```mermaid
stateDiagram-v2
    [*] --> Entered: Enter
    Entered --> Running: Tick returns Running
    Running --> Running: Tick returns Running
    Running --> Exited: Tick returns Succeeded/Failed/Canceled
    Exited --> [*]: Exit
    Running --> Interrupted: Interrupt
    Entered --> Interrupted: Interrupt
    Interrupted --> [*]
```

组合节点通过同一套接口嵌套子节点。例如 `ParallelAllNode` 在 `Enter` 时进入所有子节点，在每次 `Tick` 时推进仍处于 `Running` 的子节点；`RaceNode` 在第一个子节点完成后中断其他仍在运行的子节点；`TimeoutNode` 在超时时调用子节点的 `Interrupt` 并返回失败。

---

## 5. Runner、Session 与 Host

`FlowRunner` 的 `Start` 会先清理旧流程，再创建根作用域、注入 `FlowWakeUp` 和 `FlowRuntimeDiagnostics`，把状态置为 `Running`，最后立即 `Step(0f)` 一次。

立即 prime 的意义是让根节点和子节点尽早执行 `Enter`，这样等待回调、订阅事件、注册唤醒句柄等逻辑不需要等到下一帧才生效。

```mermaid
sequenceDiagram
    participant Host as FlowHost
    participant Provider as IFlowRootProvider
    participant Session as FlowSession
    participant Runner as FlowRunner
    participant Root as IFlowNode

    Host->>Provider: CreateRoot(args)
    Provider-->>Host: root
    Host->>Session: Start(root)
    Session->>Runner: Start(root, callbacks)
    Runner->>Runner: BeginScope + Set FlowWakeUp + diagnostics
    Runner->>Root: Enter(ctx) via Step(0)
    loop Tick or Wake
        Host->>Session: Step(deltaTime)
        Session->>Runner: Step(deltaTime)
        Runner->>Root: Tick(ctx, deltaTime)
    end
    Runner->>Root: Exit(ctx, status)
    Runner->>Session: Finished(status)
```

`FlowSession` 和 `FlowHost<TArgs>` 都是 thin wrapper。它们的价值在于把外部 API 稳定下来：调用方只需要订阅 Started、StatusChanged、Finished、UnhandledException，再选择显式 `Step` 或由事件唤醒推进。`FlowHost<TArgs>.Step` 当前是程序集内入口，外部程序集不能把 Host 当作任意公开 Tick 接口；跨程序集宿主应通过已提供的接入层推进，或显式持有公开的 runner/session API。

### 5.1 同步执行

`IFlowNode.Execute(options)` 适合工具、样例和测试中的有限流程。实现会从 `FlowPools` 租借 runner，启动后用固定 `DeltaTime` 循环 `Step`，并在 `finally` 中归还 runner。默认 `MaxSteps` 为 1024，默认 `DeltaTime` 为 0。

| 结果路径 | 返回状态 | 异常位置 |
|----------|----------|----------|
| 节点正常完成 | 节点终态 | `Exception` 为空 |
| 节点生命周期抛异常 | `Failed` | runner 捕获到的异常进入执行结果 |
| 达到 `MaxSteps` | `Canceled` | 执行结果包含步数上限异常 |
| `RethrowExceptions = true` | 不返回结果 | 捕获异常在释放 runner 前后按实现边界重新抛出 |

步数上限不是业务失败状态。实现先创建 `InvalidOperationException`，再调用 `runner.Stop()`，所以结果组合是 `Canceled + captured exception`。调用方不能只检查 `Status == Failed`，应同时检查结果中的异常。

### 5.2 池化与所有权

默认构造函数形成递归所有权：`FlowHost<TArgs>` 租借 `FlowSession`，Session 再租借 `FlowRunner`；Host 的 `Dispose` 释放 Session，Session 的 `Dispose` 释放 Runner。归还时会清理 Context、事件订阅、observer、trace recorder 和异常回调，调用方不得在释放后继续持有这些运行态引用。

`FlowPools` 默认覆盖 Context、Runner、Session、scope map、stage node list 和 Completion。泛型 Host 与泛型事件队列按闭合泛型类型分别维护；使用 `RentHost<TArgs>` 前，应为实际 `TArgs` 配置对应的 `PoolHost<TArgs>`，不能把一个参数类型的池配置视为所有 Host 的全局配置。

---

## 6. FlowContext 与作用域数据

`FlowContext` 是按 `Type` 存取的运行时对象表，并带有 scope 栈。`Set<T>` 在有 scope 时写入栈顶 scope，没有 scope 时写入全局 map；`TryGet<T>` 从当前 scope 栈向下查找，再回退到全局 map。

```mermaid
flowchart TB
    Ctx["FlowContext"] --> ScopeTop["scope top Dictionary<Type, object>"]
    Ctx --> ScopeParent["parent scope Dictionary<Type, object>"]
    Ctx --> Global["global map Dictionary<Type, object>"]
    TryGet["TryGet<T>"] --> ScopeTop
    ScopeTop --> ScopeParent
    ScopeParent --> Global
    Begin["BeginScope"] --> Pool["FlowPools.RentContextScopeMap"]
    Dispose["ScopeHandle.Dispose"] --> Release["FlowPools.ReleaseContextScopeMap"]
```

这个模型适合三类数据：

| 数据 | 生命周期 | 例子 |
|------|----------|------|
| Runner 注入的基础能力 | 一次 flow run | `FlowWakeUp`、`FlowRuntimeDiagnostics` |
| 外部业务上下文 | 由 host 或 root provider 决定 | 会话对象、启动参数、服务定位器、测试上下文 |
| 局部节点数据 | 某个 composite 或 stage 内部 | 临时资源、finally 清理所需的句柄 |

需要注意的是，`FlowContext` 是按类型索引，不适合同时放入多个同类型但语义不同的对象。多个同类型数据应包装成语义化上下文类型，或由调用方显式组织为一个 aggregate 对象。

---

## 7. 控制结构与组合节点

Flow 的 block 节点把常见控制结构做成可复用节点，而不是让业务流程到处手写状态字段。

| 节点 | 控制语义 | 关键行为 |
|------|----------|----------|
| `SequenceNode` | 顺序执行 | 当前子节点成功后进入下一个，非成功终态停止后续节点 |
| `ParallelAllNode` | 全部完成 | 不是 fail-fast；继续等待全部子节点终态，再将任一非成功折叠为 `Failed` |
| `RaceNode` | 竞速 | 按数组顺序 Tick，本轮第一个终态子节点决定整体状态，其余运行中节点被中断 |
| `TimeoutNode` | 超时保护 | 超过指定秒数后中断子节点并返回失败 |
| `FinallyNode` | 清理保障 | try 结束后运行 finally；finally 的 `Succeeded`、`Failed` 或 `Canceled` 不覆盖 try 状态 |
| `IfNode` | 二选一 | 只在 `Enter` 时计算条件并固定分支；没有 else 时直接成功 |
| `SwitchNode` | 多分支 | 只在 `Enter` 时确定 key 和分支；没有 default 时直接成功 |
| `UsingResourceNode<T>` | 资源作用域 | 创建资源后按类型写入 Context，正常退出或中断时移除并释放 |
| `RepeatUntilNode` | 条件重复 | 适合轮询等待或脚本化重试 |
| `AwaitCallbackNode` | 回调等待 | 外部回调通过 wake handle 推进流程 |
| `WaitSecondsNode` / `WaitUntilNode` | 时间和条件等待 | 每帧 Tick 或被唤醒时检查完成条件 |

```mermaid
flowchart LR
    Root["root"] --> Try["try sequence"]
    Root --> Finally["finally sequence"]
    Try --> A["ActionNode"]
    Try --> P["ParallelAllNode"]
    P --> W1["WaitUntilNode"]
    P --> W2["AwaitCallbackNode"]
    Try --> R["RaceNode"]
    R --> T["TimeoutNode"]
    R --> C["Cancel signal"]
```

这些结构让 Flow 更像“流程控制语法树”，而不是单个长方法。节点的进入、中断和退出都经过 `FlowDiagnostics`，所以组合后仍能保持一致的 trace 和 observer 语义。

两个终态边界需要显式测试。第一，`FinallyNode` 只保留 try 状态，因此不能用 finally 节点的返回状态表达清理失败；需要让清理代码抛异常或把失败写入显式业务上下文。第二，`UsingResourceNode<T>` 只有在资源创建完成并标记为已创建后，才会在自身退出路径释放资源；创建委托在交付资源前抛异常时，委托内部已分配的部分资源仍由委托负责回收。

---

## 8. 阶段贡献模型

`OrderedStagedFlowRootProvider<TArgs>` 把阶段列表、核心 provider 和外部 contributor 合成根节点。它的构建顺序是：

1. 按 `IFlowStageContributor<TArgs>.Order` 排序 contributor。
2. 对每个 `FlowStageKey` 依次询问 contributor 是否能贡献节点。
3. 追加核心 provider 对该阶段创建的节点。
4. 多个节点包装成 `SequenceNode`，没有节点则用空 `DoNode`。
5. try stages 和 finally stages 最终包装为 `FinallyNode`。

```mermaid
flowchart TB
    Stages["try/finally FlowStageKey list"] --> Builder["OrderedStagedFlowRootProvider"]
    Contributors["IFlowStageContributor<TArgs>[] ordered"] --> Builder
    Core["IStagedFlowProvider<TArgs>"] --> Builder
    Builder --> TryNodes["try stage nodes"]
    Builder --> FinallyNodes["finally stage nodes"]
    TryNodes --> SeqTry["SequenceNode"]
    FinallyNodes --> SeqFinally["SequenceNode"]
    SeqTry --> Root["FinallyNode"]
    SeqFinally --> Root
```

这个模型适合做模块化启动流程。例如核心流程声明固定阶段，业务包或测试包通过 contributor 在某些阶段插入校验、日志、资源创建、mock 替换或清理节点，而不需要修改核心 provider。

---

## 9. 唤醒、泵循环与诊断

Flow 支持两种推进方式：

| 推进方式 | 入口 | 适用场景 |
|----------|------|----------|
| 外部 Tick | `FlowRunner.Step(deltaTime)` | 定时等待、每帧条件检查、宿主主循环 |
| 事件唤醒 | `FlowWakeUp.Wake()` | 回调完成、异步事件到达、不希望持续 Tick 的流程 |

`FlowRunner` 的 wake 模型有两个保护点：

1. wake 只在 `Running` 状态有效，已完成或已释放 runner 不会继续泵循环。
2. `MaxPumpIterationsPerWake` 默认限制为 128，防止一个 wake 链条不断同步触发新的 wake 导致死循环。

```mermaid
sequenceDiagram
    participant Callback as Event Callback
    participant Wake as FlowWakeUp
    participant Runner as FlowRunner
    participant Root as IFlowNode
    participant Diag as FlowDiagnostics

    Callback->>Wake: Wake()
    Wake->>Runner: Wake callback
    Runner->>Runner: mark wakeRequested
    Runner->>Runner: Pump()
    loop while wakeRequested and Running
        Runner->>Root: Step(0)
        Root->>Diag: Tick/Exit/Interrupt events
    end
```

诊断由 `FlowRuntimeDiagnostics`、`IFlowObserver` 和 `IFlowTraceRecorder` 组成。Runner 在 run started、status changed、node enter/tick/exit/interrupt、pump limit exceeded 等关键点记录事件。这样 Flow 可以用于测试和工具，不只是运行时业务流程。

节点生命周期抛出的异常由 runner 捕获：先通知 `ExceptionHandler` 和 `UnhandledException`，再以 `Failed` 中止根流程；异常回调自身抛出的二次异常会被吞掉，避免遮蔽原始中止流程。外部 `Stop` 则按 `Canceled` 中断当前根节点。泵循环超过默认 128 次时记录 `PumpLimitExceeded`，构造异常并按失败路径中止。

Runner 对“推进时失败”和“主动停止时失败”的处理强度并不相同。`Step/Pump` 中的生命周期异常会进入统一失败与中断路径；`Stop()` 调用根节点 `Interrupt` 时，异常会在 `finally` 清除 runner 运行状态后继续向外传播。再次 `Start()` 会先 Stop 旧流程，因此旧 root 的 Interrupt 抛错会阻止新 root 启动。

`Dispose()` 不是 best-effort 清理器：它先调用 `Stop()`，只有 Stop 正常返回后才清 callback、context 并写 `_disposed=true`。Interrupt 抛错时 runner 虽已停止，但 Dispose 后半段不会执行；`FlowSession.Dispose()` 经 `FlowPools.ReleaseRunner` 归还 runner 时也会继承这一风险。宿主应把 Stop/Dispose 放在自己的异常隔离边界内，并把节点 `Interrupt` 设计为幂等、无异常的资源回收路径。

组合节点同样存在局部提交点。`FinallyNode` 会保存 try 分支终态再执行 finally；`UsingResourceNode` 若资源创建委托已经产生外部副作用、随后抛异常，则节点拿不到完整资源引用，无法自动补偿。Flow 能统一“已取得所有权的节点资源”退出流程，但不能替业务撤销创建委托内部的半完成副作用。

Flow 不提供跨线程同步。`FlowWakeUp` 和 `AwaitCallbackNode` 的回调唤醒必须与 runner 的使用线程协调；若外部异步 API 在工作线程回调，应先投递回宿主线程，再调用 wake，不能把 wake 视为线程安全调度器。

### 9.1 HFSM 适配边界

`HfsmFlowRunner<TOwnId,TStateId,TEvent>` 复用了 Flow 的事件队列和启动/停止外观，但不是流程树 runner。`Start` 调用状态机 `OnEnter`；`Step` 先耗尽 `FlowEventQueue<TEvent>` 并逐个 `Trigger`，再调用一次 `OnLogic`；`Stop` 调用 `OnExit` 并清空队列。当前 `Step(deltaTime)` 不消费 `deltaTime`，时间语义由 HFSM 或外部状态自行提供。

---

## 10. 和 Pipeline、Triggering、Demo Flow 的边界

| 能力 | 主要职责 | 典型使用 |
|------|----------|----------|
| Flow | 通用流程树、等待、并行、竞速、finally、事件唤醒 | 会话流程、启动流程、工具流程、脚本化测试 |
| Pipeline | 业务阶段管线，强调 context、phase、运行实例、pause/interruption/registry | 技能释放、效果阶段、持续运行时 |
| Triggering | 事件-条件-动作执行，强调事件订阅、排序、条件、动作计划和调度 | 技能规则、Buff 阶段效果、被动触发 |
| Console `BattleFlow` | 示例层阶段机，组织 Prepare/Connect/InMatch 等 Demo 阶段 | Console Demo 启动和自动测试 |

Flow 可以被业务层使用，但它本身不规定“技能阶段”或“触发器计划”的语义。若流程需要强业务上下文、运行实例查询、暂停恢复和 registry，Pipeline 更合适；若流程本质是事件触发和动作执行，Triggering 更合适；若只是示例中几个固定战斗阶段，Demo 自己的 `BattleFlow` 已经足够。

---

## 11. 扩展边界与验证

- 新增基础流程能力时，优先实现新的 `IFlowNode`，保持节点只关心自身生命周期。
- 需要组合多个节点时，优先做 block 节点，不要把大量子节点状态散落在业务 service 中。
- 需要给标准阶段插入能力时，优先实现 `IFlowStageContributor<TArgs>`，避免修改核心 staged provider。
- 需要等待外部事件时，应在 `Enter` 中订阅，在 `Interrupt` 和 `Exit` 中解除订阅，并通过 `FlowWakeUp` 唤醒 runner。
- 长时间运行节点要明确中断语义，避免 `RaceNode`、`TimeoutNode` 或外部 `Stop` 后遗留回调。
- 放入 `FlowContext` 的对象应是语义化类型，避免同类型对象互相覆盖。
- 对可能同步连锁唤醒的节点，应保留 runner 的 pump iteration 限制，并让诊断输出足够定位是哪类节点反复 wake。

| 证据 | 入口 | 能证明什么 | 不能证明什么 |
|------|------|------------|--------------|
| .NET 编译 | `dotnet build src/AbilityKit.Flow/AbilityKit.Flow.csproj -c Release` | package runtime 可在 `net10.0` 镜像中编译，并能解析 Core/HFSM 依赖 | 不覆盖 Unity player 生命周期或线程行为 |
| 纯逻辑样例 | `src/AbilityKit.Samples.Logic/Samples/Flow` | Sequence、Race、等待和技能计时等组合可被真实调用 | 不是完整契约测试套件 |
| package sample | `Unity/Packages/com.abilitykit.flow/Samples~/FlowExamples` | Unity package 的接入形态与典型节点组合 | 不等于自动回归门禁 |
| Starter 调用 | `Unity/Assets/Scripts/Starter/StarterFlowManager.cs` | Unity 宿主存在生产式启动入口 | 不覆盖所有异常和池化分支 |
| 专项测试 | `src/AbilityKit.Flow.Tests/` | 2026-08-17 扩至 **236/236**（Runner/Session/Host/Context 作用域/全部节点与块/pump 上限/池化往返/诊断/Stages/HfsmFlowRunner/Execute 扩展），连续多轮全绿，接入 `foundation-units` 门禁 | 不覆盖 Unity player 生命周期、线程行为与 Editor 场景 |

### 2026-08-17 缺陷修复记录（236 项测试扩容时定位并修复）

| 缺陷 | 修复 |
|------|------|
| `FlowPools.RentHost` 把 provider 放进池 onGet 闭包，Core 池只登记首次 options，**第一个 provider 被永久固化**（新 provider 永不生效） | Get 之后显式绑定当次 provider |
| Runner 根节点 `Exit` 抛异常时收尾被跳过（onFinished 不触发、ctx/rootScope 不清理） | Exit 异常按二级通道上报，终态不变、收尾照常完成 |
| `Step` 无重入守卫：Enter 内同步 Wake 重入 Pump→Step，对未完成 Enter 的节点树嵌套 Tick/重复 Enter（假 NRE、同步完成丢失卡 Running） | `_stepping` 守卫 + 步进结束后统一推进积压唤醒 |
| `ParallelAllNode` 终态只看最后一轮 Tick 的局部失败标记，**早先轮次的失败被遗忘**（末子成功时整体误报 Succeeded） | 终态改为遍历全部子节点最终状态 |
| 空 `RaceNode` 永远 Running（与空 Sequence/ParallelAll 不一致，只能靠 MaxSteps 兜底） | 空组立即 Succeeded |
| `FlowContext` 存入的 null 对引用类型槽不可见（`is T` 对 null 恒 false；scope 内 Set(null) 不遮蔽外层） | null 视为"存在但为 null"，TryGet 命中返回 null |
| `CreateResourceNode._created` 跨运行存留，同实例第二次运行不重写资源 | Enter 每次运行重新创建写入 |
| `FlowSession.Started` 在 `runner.Start` 之后触发，立即完成流程的 Finished 早于 Started（时序倒置） | Started 先触发 |

保留的已知语义（测试钉住、未改）：`FinallyNode` 在 try 被 Interrupt 时不执行 finally 分支（Interrupt 协议为同步 void，无法多帧推进，属设计边界）；finally 自身 Failed 状态被 tryStatus 覆盖（与 C# try/finally 语义一致）；`TimeoutNode` 用 `>`（保护 dt=0 预热步）而 `WaitSecondsNode` 用 `>=`（到点即完成）的边界差异；`FlowContext` scope 句柄 Dispose 恒弹栈顶（不绑定自己创建的 scope）；`FlowSession.Dispose` 不触发 Finished；`FlowRunner.Status` Dispose 后仍可读。

---

## 12. 和其他文档的关系

| 文档 | 关系 |
|------|------|
| [核心概念](../01-OverviewAndGettingStarted/02-CoreConcepts.md) | 该文解释 Session、Flow、Phase 等入门术语，本文补充通用 Flow 引擎源码模型 |
| [技能系统架构](../08-GameplayModules/01-SkillSystemArchitecture.md) | 该文解释技能 Pipeline，本文说明 Flow 与 Pipeline 的边界 |
| [触发器系统](../08-GameplayModules/02-TriggeringSystem.md) | Triggering 负责事件条件动作，Flow 负责流程树和等待控制 |
| [Console Demo 解析](../09-ImplementationExamples/01-ConsoleDemoAnalysis.md) | Console `BattleFlow` 是示例阶段流，不等于通用 Flow 引擎 |

---

文档类型：Canonical 设计 | 事实基线：2026-08-17 | 证据等级：E0 源码、E1 .NET 构建、E2 Samples/Starter、E3 契约测试（`AbilityKit.Flow.Tests` 236 项 + `foundation-units` 门禁）；未达到 E4/E5

*文档版本：v3.3 | 最后更新：2026-08-17*
