# Ability-Kit Flow 模块开发设计文档

> **阅读对象**：首次接触 Ability-Kit Flow 模块的开发者
>
> **文档目标**：让你理解 Flow 模块"是什么"、"解决了什么问题"、"为什么这样设计"、"怎么使用和扩展"

---

## 一、设计理念：为什么要做 Flow 模块？

### 1.1 传统游戏逻辑的痛点

```
❌ 传统做法的问题：

1. 回调地狱
   ┌─────────────────────────────────────────────────────────┐
   │ LoadResource("a", () => {                             │
   │     LoadResource("b", () => {                         │
   │         LoadResource("c", () => {                     │
   │             // 已经嵌套了3层...                        │
   │         });                                            │
   │     });                                                │
   │ });                                                    │
   └─────────────────────────────────────────────────────────┘
   → 代码难以阅读、难以调试、难以修改

2. 状态混乱
   ┌─────────────────────────────────────────────────────────┐
   │ // 到处都是协程，不知道执行到哪了                        │
   │ StartCoroutine(DoSomething());                         │
   │ StartCoroutine(DoAnotherThing());                     │
   │ StartCoroutine(DoMoreThings());                        │
   │ // "等等，这个到底执行完了没有？"                       │
   └─────────────────────────────────────────────────────────┘
   → 流程执行到哪了？有没有失败？怎么取消？

3. 资源泄漏
   ┌─────────────────────────────────────────────────────────┐
   │ void OnClick()                                         │
   │ {                                                     │
   │     connection = new Connection();                     │
   │     // 如果抛异常了，connection 永远不会 Close()      │
   │     DoSomething();                                     │
   │     connection.Close();                               │
   │ }                                                     │
   └─────────────────────────────────────────────────────────┘
   → 网络连接泄漏、文件句柄泄漏

4. 异步结果难以追踪
   ┌─────────────────────────────────────────────────────────┐
   │ // "这个操作成功了，然后呢？失败了怎么办？"             │
   │ Task.Run(() => DoSomething());                        │
   │ // 后续逻辑散落在各处                                    │
   └─────────────────────────────────────────────────────────┘
   → 成功/失败/取消的分支处理混乱
```

### 1.2 Flow 模块的解决方案

```
✅ Flow 的设计思路：

┌─────────────────────────────────────────────────────────────┐
│                       核心思想                              │
│                                                             │
│   把"流程"抽象成"节点"，用树形结构组织                      │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                  Sequence (顺序)                     │  │
│   │  ┌──────────┐  ┌──────────┐  ┌──────────┐         │  │
│   │  │  加载A   │→ │  加载B   │→ │  加载C   │         │  │
│   │  └──────────┘  └──────────┘  └──────────┘         │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   把"回调"变成"节点等待"                                    │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │  AwaitCallbackNode                                   │  │
│   │       │                                              │  │
│   │       ▼                                              │  │
│   │  [等待网络回调]  ──►  回调来了，继续下一步            │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 1.3 核心理念

```
┌─────────────────────────────────────────────────────────────┐
│                    Flow 模块的三大承诺                        │
│                                                             │
│   1️⃣ 可追踪                                               │
│      流程开始 → 执行中 → 成功/失败/取消                    │
│      每个状态变化都可以被监听                               │
│                                                             │
│   2️⃣ 可组合                                               │
│      小节点 → 大节点 → 完整流程                            │
│      像搭积木一样构建复杂逻辑                               │
│                                                             │
│   3️⃣ 可信任                                               │
│      资源一定被释放                                         │
│      异常一定被捕获                                         │
│      取消一定被响应                                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 二、核心概念：从零理解 Flow

### 2.1 什么是"节点（Node）"？

**通俗解释**：节点就像一个"步骤"，每个步骤知道自己要做什么，以及什么时候算完成。

```
┌─────────────────────────────────────────────────────────────┐
│                    节点的三个状态                            │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                                                     │  │
│   │  Enter()        被选中开始执行                       │  │
│   │     │                                            │  │
│   │     ▼                                            │  │
│   │  Tick()   ◄── 每帧调用，直到返回"完成"             │  │
│   │     │                                            │  │
│   │     ├─► Running  ←── "还在执行中"                   │  │
│   │     ├─► Succeeded ←── "成功了！"                   │  │
│   │     ├─► Failed    ←── "出错了！"                   │  │
│   │     └─► Canceled  ←── "被取消了！"                 │  │
│   │                                                     │  │
│   │     ▼                                            │  │
│   │  Exit()         正常结束时的清理                     │  │
│   │     │                                            │  │
│   │  Interrupt()   被中断时的清理                       │  │
│   │                                                     │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 什么是"流程（Flow）"？

**通俗解释**：流程就是一棵节点树，定义了"先做什么、再做什么、最后做什么"。

```
┌─────────────────────────────────────────────────────────────┐
│                    流程示例：登录流程                        │
│                                                             │
│   Sequence (顺序执行)                                       │
│   │                                                         │
│   ├─► WaitUntil (等待网络连接)                              │
│   │                                                         │
│   ├─► Sequence (准备阶段)                                   │
│   │   ├─► Do (显示加载界面)                                 │
│   │   └─► Do (播放加载动画)                                 │
│   │                                                         │
│   ├─► AwaitCallback (验证账号)                              │
│   │   │  回调成功 → 继续                                   │
│   │   │  回调失败 → 跳到错误处理                           │
│   │                                                         │
│   ├─► If (验证结果)                                         │
│   │   ├─► True:  Do (进入游戏)                             │
│   │   └─► False: Do (显示错误)                             │
│   │                                                         │
│   └─► Finally (清理)                                        │
│         无论成功失败，都会执行清理逻辑                       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 2.3 关键名词解释

| 名词 | 通俗解释 | 类比 |
|------|----------|------|
| **Flow** | 一个完整的流程，可以包含多个步骤 | 一部电影的剧本 |
| **Node** | 流程中的一个步骤 | 剧本中的一场戏 |
| **FlowRunner** | 执行流程的引擎 | 导演 |
| **FlowSession** | 流程的一次运行实例 | 一场具体的演出 |
| **FlowContext** | 流程的上下文，存储数据和资源 | 剧组的道具箱 |
| **FlowStatus** | 流程/节点的状态 | 演出状态 |
| **WakeUp** | 唤醒机制，用于非时间驱动的推进 | "演员准备好了！" |

---

## 三、核心架构：用图说话

### 3.1 整体架构图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Flow 模块架构                                  │
│                                                                         │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                         入口层                                   │   │
│   │                                                                 │   │
│   │  ┌─────────────────────────────────────────────────────────┐   │   │
│   │  │              FlowSession (推荐使用)                       │   │   │
│   │  │                                                       │   │   │
│   │  │  - 自动管理 FlowRunner 生命周期                         │   │   │
│   │  │  - 暴露 Started/Finished/Error 事件                    │   │   │
│   │  │  - 提供 Start/Stop 接口                                 │   │   │
│   │  └─────────────────────────────────────────────────────────┘   │   │
│   │                                                                 │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                               │                                         │
│                               ▼                                         │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                         执行引擎层                               │   │
│   │                                                                 │   │
│   │  ┌─────────────────────┐    ┌─────────────────────┐           │   │
│   │  │     FlowRunner      │    │    FlowContext       │           │   │
│   │  │                     │    │                     │           │   │
│   │  │ - 管理根节点         │    │ - 存储数据          │           │   │
│   │  │ - Tick 驱动         │    │ - 作用域管理        │           │   │
│   │  │ - Wake/Pump 机制   │    │ - 依赖注入          │           │   │
│   │  │ - 异常处理         │    │                     │           │   │
│   │  └─────────────────────┘    └─────────────────────┘           │   │
│   │                                                                 │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                               │                                         │
│                               ▼                                         │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                         节点层                                   │   │
│   │                                                                 │   │
│   │  ┌──────────────────┐    ┌──────────────────┐                  │   │
│   │  │   基础节点        │    │   组合节点        │                  │   │
│   │  │                  │    │                  │                  │   │
│   │  │ - ActionNode    │    │ - SequenceNode   │                  │   │
│   │  │ - WaitSeconds   │    │ - RaceNode       │                  │   │
│   │  │ - AwaitCallback │    │ - ParallelAll    │                  │   │
│   │  │ - RepeatUntil   │    │ - IfNode         │                  │   │
│   │  │                  │    │ - TimeoutNode    │                  │   │
│   │  │                  │    │ - UsingResource  │                  │   │
│   │  └──────────────────┘    │ - FinallyNode    │                  │   │
│   │                          │                  │                  │   │
│   │                          └──────────────────┘                  │   │
│   │                                                                 │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 3.2 流程执行流程图

```
┌─────────────────────────────────────────────────────────────────────┐
│                      Flow 执行完整流程                                │
│                                                                     │
│   1️⃣ 创建会话                                                       │
│   ┌─────────────────────────────────────────────────────────────┐  │
│   │ var session = new FlowSession();                            │  │
│   │ var ctx = new FlowContext();                                │  │
│   │ ctx.Set(new FlowWakeUp());  // 注入唤醒器                   │  │
│   │                                                             │  │
│   │ var root = new SequenceNode(...);  // 定义流程树             │  │
│   └─────────────────────────────────────────────────────────────┘  │
│                               │                                    │
│                               ▼                                    │
│   2️⃣ 启动流程                                                       │
│   ┌─────────────────────────────────────────────────────────────┐  │
│   │ session.Start(root, ctx);                                  │  │
│   │                                                             │  │
│   │ // 内部执行：                                                │  │
│   │ // 1. ctx.BeginScope()                                      │  │
│   │ // 2. root.Enter(ctx)                                       │  │
│   │ // 3. root.Tick(ctx, 0)  // 初始化一次                       │  │
│   │ // 4. Status → Running                                      │  │
│   └─────────────────────────────────────────────────────────────┘  │
│                               │                                    │
│                               ▼                                    │
│   3️⃣ 每帧驱动                                                       │
│   ┌─────────────────────────────────────────────────────────────┐  │
│   │ void Update() {                                             │  │
│   │     var status = session.Step(deltaTime);                  │  │
│   │     if (status != FlowStatus.Running) {                     │  │
│   │         // 流程结束了！                                      │  │
│   │     }                                                       │  │
│   │ }                                                           │  │
│   │                                                             │  │
│   │ // Step 内部：                                               │  │
│   │ // root.Tick(ctx, deltaTime)                                │  │
│   │ //   - 如果节点返回 Running，继续                           │  │
│   │ //   - 如果节点返回 Succeeded/Failed/Canceled，结束        │  │
│   └─────────────────────────────────────────────────────────────┘  │
│                               │                                    │
│                               ▼                                    │
│   4️⃣ 非时间驱动唤醒                                                 │
│   ┌─────────────────────────────────────────────────────────────┐  │
│   │ // 比如网络请求完成                                          │  │
│   │ void OnNetworkComplete() {                                 │  │
│   │     ctx.Get<FlowWakeUp>().Wake();  // 唤醒流程              │  │
│   │ }                                                          │  │
│   │                                                             │  │
│   │ // Wake 内部：                                               │  │
│   │ // - 设置 _wakeRequested = true                             │  │
│   │ // - Pump: 循环调用 Step(0) 直到不需要 Wake                  │  │
│   │ // - 这样网络回调可以"无缝衔接"继续执行                      │  │
│   └─────────────────────────────────────────────────────────────┘  │
│                               │                                    │
│                               ▼                                    │
│   5️⃣ 流程结束                                                       │
│   ┌─────────────────────────────────────────────────────────────┐  │
│   │ // 流程完成或被中断                                         │  │
│   │ session.Finished += (status) => {                         │  │
│   │     if (status == FlowStatus.Succeeded) { ... }            │  │
│   │     if (status == FlowStatus.Failed) { ... }              │  │
│   │ };                                                          │  │
│   │                                                             │  │
│   │ // 清理：                                                    │  │
│   │ // root.Exit(ctx) / root.Interrupt(ctx)                    │  │
│   │ // ctx.EndScope()                                          │  │
│   └─────────────────────────────────────────────────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 3.3 节点树结构

```
┌─────────────────────────────────────────────────────────────────────┐
│                        节点树示例                                    │
│                                                                     │
│                      SequenceNode                                    │
│                      (顺序执行)                                       │
│                      ┌────┴────┐                                    │
│                      │         │                                    │
│                      ▼         ▼                                    │
│               ActionNode    AwaitCallbackNode                        │
│               (做一件事)    (等待回调)                                │
│                      │         │                                    │
│                      │         ▼                                    │
│                      │     [等待中...]                               │
│                      │         │                                    │
│                      │         ▼ (回调触发)                          │
│                      │         ▼                                    │
│                      │     RaceNode (竞速)                          │
│                      │     ┌────┴────┐                              │
│                      │     │         │                              │
│                      │     ▼         ▼                              │
│                      │  Timeout   等待玩家输入                        │
│                      │   (5秒)                                       │
│                      │                                              │
│                      ▼                                              │
│               FinallyNode                                            │
│               (保证清理)                                             │
│               ┌────┴────┐                                           │
│               │         │                                           │
│               ▼         ▼                                           │
│           正常退出    中断退出                                        │
│           清理        清理                                           │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 四、核心组件详解

### 4.1 IFlowNode - 节点接口

**文件位置**：`Runtime/Flow/IFlowNode.cs`

**通俗解释**：这是所有节点的"契约"，定义了节点必须实现的方法。

```
┌─────────────────────────────────────────────────────────────┐
│                    IFlowNode 接口                           │
│                                                             │
│   public interface IFlowNode                                │
│   {                                                         │
│       // 1. 进入节点（初始化）                               │
│       void Enter(FlowContext ctx);                        │
│                                                             │
│       // 2. 每帧推进（核心逻辑）                             │
│       FlowStatus Tick(FlowContext ctx, float dt);          │
│                                                             │
│       // 3. 正常退出（清理）                                 │
│       void Exit(FlowContext ctx);                          │
│                                                             │
│       // 4. 被中断退出（清理）                                │
│       void Interrupt(FlowContext ctx);                      │
│   }                                                         │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │  Enter ──► Tick ──► Tick ──► ... ──► Exit           │  │
│   │           │                              ▲          │  │
│   │           │                              │          │  │
│   │           └───── 返回 Running ───────────┘          │  │
│   │                                                      │  │
│   │   Interrupt 可以从任何时候插入                          │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 FlowRunner - 执行引擎

**文件位置**：`Runtime/Flow/FlowRunner.cs`

**通俗解释**：FlowRunner 是"导演"，负责按正确顺序调用每个节点的每个方法。

```csharp
public sealed class FlowRunner
{
    private FlowContext _ctx;
    private IFlowNode _root;
    private FlowStatus _status;
    private FlowWakeUp _wakeUp;
    private bool _wakeRequested;
    private bool _pumping;

    // 最大 Pump 次数，防止死循环
    public int MaxPumpIterationsPerWake { get; set; } = 128;
}
```

**核心方法**：

```
┌─────────────────────────────────────────────────────────────┐
│                    FlowRunner 核心方法                      │
│                                                             │
│   Start(root, ctx)                                         │
│   ─────────────────────                                     │
│   1. _ctx = ctx                                            │
│   2. _root = root                                          │
│   3. _status = Running                                     │
│   4. root.Enter(ctx)                                       │
│   5. root.Tick(ctx, 0)  // 初始化一次                       │
│                                                             │
│   Step(dt)                                                 │
│   ──────────────                                           │
│   1. if (status != Running) return status;                  │
│   2. var s = _root.Tick(ctx, dt);                          │
│   3. if (s != Running) {                                    │
│          _root.Exit(ctx);                                  │
│          _status = s;                                      │
│      }                                                      │
│   4. return _status;                                        │
│                                                             │
│   Wake()                                                   │
│   ──────────                                               │
│   1. _wakeRequested = true                                 │
│   2. if (_pumping) return;  // 防止重入                     │
│   3. Pump()                                                │
│                                                             │
│   Pump()                                                   │
│   ─────────                                                │
│   while (_wakeRequested && Running) {                      │
│       _pumpIterations++;                                   │
│       if (_pumpIterations > MaxPumpIterations)              │
│           throw "可能死循环";                               │
│       _wakeRequested = false;                              │
│       Step(0);                                             │
│   }                                                        │
│   _pumpIterations = 0;                                     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.3 FlowSession - 会话封装

**文件位置**：`Runtime/Flow/FlowSession.cs`

**通俗解释**：Session 是推荐的入口类，自动处理 Runner 的生命周期。

```csharp
public sealed class FlowSession : IDisposable
{
    private readonly FlowRunner _runner;

    // 事件通知
    public event Action Started;
    public event Action<FlowStatus, FlowStatus> StatusChanged;  // 旧状态 → 新状态
    public event Action<FlowStatus> Finished;
    public event Action<Exception> UnhandledException;

    // 启动流程
    public void Start(IFlowNode root, FlowContext ctx);

    // 停止流程
    public void Stop();

    // 获取当前状态
    public FlowStatus Status { get; }
}
```

**使用模式**：

```
┌─────────────────────────────────────────────────────────────┐
│                    FlowSession 使用模式                      │
│                                                             │
│   // 推荐：使用事件                                           │
│   var session = new FlowSession();                          │
│   session.Finished += (status) => {                        │
│       Debug.Log($"流程结束: {status}");                    │
│   };                                                        │
│   session.Start(myFlow, ctx);                               │
│                                                             │
│   // 每帧调用                                               │
│   void Update() {                                          │
│       session.Step(Time.deltaTime);                        │
│   }                                                        │
│                                                             │
│   // 或者自动管理（using 块）                                │
│   using (var session = FlowSession.Start(myFlow, ctx))     │
│   {                                                        │
│       session.Finished += OnFinished;                      │
│       // session 会自动 Stop 和 Dispose                     │
│   }                                                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.4 FlowContext - 上下文容器

**文件位置**：`Runtime/Flow/FlowContext.cs`

**通俗解释**：Context 就像一个"道具箱"，可以在里面放东西（Set），也可以拿出来（Get）。

```
┌─────────────────────────────────────────────────────────────┐
│                    FlowContext 工作原理                      │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                    道具箱                            │  │
│   │                                                     │  │
│   │   Set<T>(value)   ──►  放进去                       │  │
│   │   Get<T>()        ──►  拿出来                       │  │
│   │   TryGet<T>()     ──►  试试拿                        │  │
│   │   BeginScope()    ──►  创建子箱子                   │  │
│   │                                                     │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │  作用域层级                                          │  │
│   │                                                     │  │
│   │   Global Scope                                      │  │
│   │   ┌───────────────────────────┐                    │  │
│   │   │  Child Scope (if any)     │                    │  │
│   │   │  ┌───────────────────┐   │                    │  │
│   │   │  │  Grandchild Scope  │   │                    │  │
│   │   │  └───────────────────┘   │                    │  │
│   │   └───────────────────────────┘                    │  │
│   │                                                     │  │
│   │   Get<T>() 会查找当前Scope，没找到就查父Scope        │  │
│   │   BeginScope() 返回 IDisposable，用于 using         │  │
│   │                                                     │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**使用示例**：

```csharp
// 父节点注入数据
ctx.Set(new Connection(connection));
ctx.Set(userId);

// 子节点获取数据
var connection = ctx.Get<Connection>();
var user = ctx.Get<string>();

// 创建局部作用域
using (ctx.BeginScope())
{
    var localVar = "临时变量";
    ctx.Set(localVar);
}  // 作用域结束，临时变量自动清理

// TryGet 安全获取
if (ctx.TryGet<IResourceLoader>(out var loader))
{
    loader.Load("asset");
}
```

### 4.5 FlowWakeUp - 唤醒机制

**文件位置**：`Runtime/Flow/FlowWakeUp.cs`

**通俗解释**：WakeUp 就像"对讲机"，让非时间驱动的操作可以"插队"推进流程。

```
┌─────────────────────────────────────────────────────────────┐
│                    WakeUp 工作原理                          │
│                                                             │
│   问题：                                                    │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                                                       │  │
│   │   WaitSecondsNode(5f)                               │  │
│   │         │                                           │  │
│   │         ▼                                           │  │
│   │   // 要等5秒...                                     │  │
│   │   // 但是网络请求已经完成了，能不能快点？             │  │
│   │                                                       │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   解决：                                                    │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                                                       │  │
│   │   var wakeUp = ctx.Get<FlowWakeUp>();               │  │
│   │   SomeAsyncOperation(() => {                         │  │
│   │       wakeUp.Wake();  // "我完成了，流程继续！"      │  │
│   │   });                                                │  │
│   │                                                       │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │  Wake() 做了什么：                                   │  │
│   │                                                       │  │
│   │   _wakeRequested = true                             │  │
│   │   FlowRunner.Pump()                                 │  │
│   │       │                                              │  │
│   │       ├─► Step(0)  // 无时间前进                    │  │
│   │       ├─► Step(0)  // 再检查                         │  │
│   │       └─► ... 直到不需要 Wake                        │  │
│   │                                                       │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 五、节点详解

### 5.1 基础节点（Nodes/）

基础节点是最小的构建单元：

```
┌─────────────────────────────────────────────────────────────┐
│                    基础节点一览                              │
│                                                             │
│   ┌───────────────────┬─────────────────────────────────┐ │
│   │ 节点               │ 用途                             │ │
│   ├───────────────────┼─────────────────────────────────┤ │
│   │ ActionNode        │ 执行自定义逻辑                   │ │
│   │ SequenceNode      │ 顺序执行多个节点                 │ │
│   │ WaitUntilNode     │ 等待条件满足                     │ │
│   │ WaitSecondsNode   │ 等待指定秒数                     │ │
│   │ AwaitCallbackNode │ 等待外部回调                     │ │
│   │ RepeatUntilNode   │ 重复执行直到条件满足             │ │
│   └───────────────────┴─────────────────────────────────┘ │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

#### ActionNode - 快速构造节点

```csharp
// 简单写法
var node = new ActionNode(
    onEnter: ctx => Debug.Log("开始"),
    onTick: ctx => FlowStatus.Succeeded,  // Tick一次就完成
    onExit: ctx => Debug.Log("结束")
);

// 等价于
public class MyAction : IFlowNode
{
    public void Enter(FlowContext ctx) { ... }
    public FlowStatus Tick(FlowContext ctx, float dt) { return FlowStatus.Succeeded; }
    public void Exit(FlowContext ctx) { ... }
    public void Interrupt(FlowContext ctx) { ... }
}
```

#### AwaitCallbackNode - 等待回调

```csharp
// 等待网络请求
var node = new AwaitCallbackNode((ctx, complete) =>
{
    Network.Request("api/login", (success, data) =>
    {
        if (success)
        {
            ctx.Set(new UserData(data));
            complete(FlowStatus.Succeeded);
        }
        else
        {
            ctx.Set(new ErrorInfo("网络错误"));
            complete(FlowStatus.Failed);
        }
    });
    return null;  // 返回取消回调（可选）
});
```

#### WaitSecondsNode - 等待时间

```csharp
// 等待3秒
var wait = new WaitSecondsNode(3f);

// 等待可变时间（通过 Context 传入）
var wait2 = new WaitSecondsNode(ctx.Get<float>("delaySeconds"));
```

### 5.2 组合节点（Blocks/）

组合节点包含子节点，实现复杂的流程控制：

```
┌─────────────────────────────────────────────────────────────────────┐
│                        组合节点一览                                  │
│                                                                     │
│   ┌───────────────────┬─────────────────────────────────────────┐  │
│   │ 节点               │ 行为                                      │  │
│   ├───────────────────┼─────────────────────────────────────────┤  │
│   │ DoNode            │ 执行自定义逻辑（简单版 ActionNode）        │  │
│   │ IfNode            │ 条件分支                                   │  │
│   │ SwitchNode<T>     │ 多路分支                                   │  │
│   │ RaceNode          │ 首个完成决定结果（竞速）                   │  │
│   │ ParallelAllNode   │ 全部成功才成功（并行）                     │  │
│   │ TimeoutNode       │ 超时控制                                   │  │
│   │ FinallyNode       │ try-finally 语义                           │  │
│   │ UsingResourceNode │ RAII 资源管理模式                         │  │
│   │ CreateResourceNode│ 创建资源                                   │  │
│   │ UseResourceNode   │ 使用资源                                   │  │
│   │ DisposeResourceNode│ 释放资源                                  │  │
│   │ TickWhileNode     │ 每帧 Tick 直到条件满足                    │  │
│   │ RunUntilCompletionNode│ 运行直到节点完成                        │  │
│   │ AwaitCompletionNode│ 等待节点完成                              │  │
│   └───────────────────┴─────────────────────────────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

#### SequenceNode - 顺序执行

```csharp
// 顺序：加载 → 解压 → 初始化
var flow = new SequenceNode(
    new AwaitCallbackNode((ctx, complete) => LoadAssets(complete)),
    new AwaitCallbackNode((ctx, complete) => ExtractAssets(complete)),
    new DoNode(onTick: ctx =>
    {
        Debug.Log("完成！");
        return FlowStatus.Succeeded;
    })
);
```

#### RaceNode - 竞速（超时场景）

```csharp
// 等5秒，或者等网络响应，先到先得
var race = new RaceNode(
    new AwaitCallbackNode((ctx, complete) =>
    {
        Network.Request("api/data", (_, __) => complete(FlowStatus.Succeeded));
        return () => { /* 取消回调 */ };
    }),
    new WaitSecondsNode(5f)
);

// 如果超时，Race 会返回 WaitSecondsNode 的 Succeeded
```

#### ParallelAllNode - 并行执行

```csharp
// 同时加载多个资源，全部完成才算成功
var parallel = new ParallelAllNode(
    new AwaitCallbackNode((ctx, complete) => LoadTextureA(complete)),
    new AwaitCallbackNode((ctx, complete) => LoadTextureB(complete)),
    new AwaitCallbackNode((ctx, complete) => LoadAudio(complete))
);

// 其中任意一个失败，整个 ParallelAll 失败
```

#### TimeoutNode - 超时控制

```csharp
// 给任何节点加超时
var withTimeout = new TimeoutNode(
    child: new AwaitCallbackNode((ctx, complete) => SlowOperation(complete)),
    timeoutSeconds: 10f
);

// 超时会强制结束节点，返回 Failed
```

#### FinallyNode - try-finally 语义

```csharp
// 无论成功失败，都执行清理
var flow = new FinallyNode(
    tryNode: new SequenceNode(
        new DoNode(onEnter: ctx => OpenFile()),
        new AwaitCallbackNode((ctx, complete) => ProcessFile(complete)),
        new DoNode(onExit: ctx => CloseFile())  // 这里也可能失败
    ),
    finallyNode: new DoNode(onExit: ctx =>
    {
        // 一定会执行，即使上面失败了
        Debug.Log("清理完成");
    })
);
```

#### UsingResourceNode - RAII 资源管理

```csharp
// 自动获取和释放资源
var flow = new UsingResourceNode<DatabaseConnection>(
    create: ctx => Database.Connect(connectionString),
    dispose: db => db.Disconnect(),
    body: new SequenceNode(
        new DoNode(onTick: ctx =>
        {
            var db = ctx.Get<DatabaseConnection>();
            var result = db.Query("SELECT ...");
            return FlowStatus.Succeeded;
        })
    )
);

// 无论 body 成功、失败还是被中断，
// dispose 都会被调用
```

---

## 六、使用指南

### 6.1 基础使用模式

```csharp
// 1. 创建 Context
var ctx = new FlowContext();
ctx.Set(new FlowWakeUp());

// 2. 构建流程树
var flow = new SequenceNode(
    new AwaitCallbackNode((ctx, complete) =>
    {
        Debug.Log("步骤1: 加载资源");
        Resource.LoadAsync("player_prefab", complete);
    }),
    new AwaitCallbackNode((ctx, complete) =>
    {
        Debug.Log("步骤2: 初始化角色");
        var prefab = ctx.Get<Object>("player_prefab");
        Instantiate(prefab);
        complete(FlowStatus.Succeeded);
    })
);

// 3. 创建 Session 并启动
var session = new FlowSession();
session.Finished += status => Debug.Log($"完成: {status}");
session.Start(flow, ctx);

// 4. 每帧驱动
void Update()
{
    session.Step(Time.deltaTime);
}

// 5. 记得清理
void OnDestroy()
{
    session.Dispose();
}
```

### 6.2 完整示例：资源加载流程

```csharp
public class ResourceLoaderFlow
{
    private FlowSession _session;

    public void LoadResources(string[] paths, Action onComplete, Action<string> onError)
    {
        var ctx = new FlowContext();
        ctx.Set(new FlowWakeUp());

        // 构建加载节点列表
        var loadNodes = paths.Select(path =>
            new AwaitCallbackNode<bool>((c, complete) =>
            {
                Resource.LoadAsync(path, obj =>
                {
                    c.Set(path, obj);  // 存储加载结果
                    complete(true);
                });
            }) as IFlowNode
        ).ToList();

        var flow = new SequenceNode(
            // 准备阶段
            new DoNode(onEnter: _ => Debug.Log("开始加载")),

            // 并行加载所有资源
            new ParallelAllNode(loadNodes.ToArray()),

            // 验证阶段
            new SequenceNode(
                new DoNode(onTick: ctx =>
                {
                    // 验证所有资源
                    foreach (var path in paths)
                    {
                        if (!ctx.TryGet(path, out var _))
                        {
                            ctx.Set(new ErrorInfo($"缺少资源: {path}"));
                            return FlowStatus.Failed;
                        }
                    }
                    return FlowStatus.Succeeded;
                })
            ),

            // 完成阶段
            new DoNode(onTick: _ =>
            {
                onComplete?.Invoke();
                return FlowStatus.Succeeded;
            })
        );

        _session = new FlowSession();
        _session.Finished += status =>
        {
            if (status == FlowStatus.Failed)
            {
                var error = ctx.Get<ErrorInfo>();
                onError?.Invoke(error.Message);
            }
        };
        _session.Start(flow, ctx);
    }

    public void Cancel()
    {
        _session?.Stop();
    }
}
```

### 6.3 完整示例：带超时的网络请求

```csharp
public IFlowNode CreateNetworkRequestFlow(string url, Action<NetworkResponse> onResponse)
{
    var ctx = new FlowContext();

    return new TimeoutNode(
        timeoutSeconds: 10f,
        child: new RaceNode(
            // 网络请求
            new AwaitCallbackNode<FlowStatus>((c, complete) =>
            {
                Network.Request(url, response =>
                {
                    c.Set(response);
                    complete(FlowStatus.Succeeded);
                });
                return null;  // 暂无取消回调
            }),

            // 或者等待玩家取消
            new AwaitCallbackNode<FlowStatus>((c, complete) =>
            {
                c.Set(new CancelToken());
                // 等待取消信号
                return new Action(() => c.Set(new CancelToken()));
            })
        )
    );
}
```

---

## 七、设计模式总结

### 7.1 Flow 模块使用的设计模式

```
┌─────────────────────────────────────────────────────────────┐
│                    设计模式应用                              │
│                                                             │
│   1️⃣ Composite Pattern（组合模式）                          │
│      ─────────────────────────────────────                  │
│      组合节点（Sequence, Race, Parallel）包含子节点          │
│      统一接口，不同实现                                       │
│                                                             │
│      ┌───────────┐                                          │
│      │ IFlowNode │                                          │
│      └─────┬─────┘                                          │
│            │                                                │
│     ┌──────┴──────┐                                        │
│     │             │                                        │
│  ┌──┴──┐      ┌──┴──┐                                     │
│  │Leaf │      │Composite│                                  │
│  │     │      │        │                                  │
│  │Action│      │Sequence│                                  │
│  │Wait  │      │ Race   │                                  │
│  └─────┘      │Parallel│                                  │
│               └────────┘                                  │
│                                                             │
│   2️⃣ Strategy Pattern（策略模式）                          │
│      ─────────────────────────────────────                  │
│      IFlowNode 是策略接口                                    │
│      不同节点实现不同的 Tick 行为                            │
│                                                             │
│   3️⃣ Template Method Pattern（模板方法）                    │
│      ─────────────────────────────────────                  │
│      Enter → Tick → Exit/Interrupt 固定流程                │
│      子类只实现具体逻辑                                       │
│                                                             │
│   4️⃣ Scope/DI Pattern（作用域/依赖注入）                   │
│      ─────────────────────────────────────                  │
│      FlowContext 存储和传递数据                              │
│      父节点注入，子节点获取                                   │
│                                                             │
│   5️⃣ RAII Pattern（资源获取即初始化）                       │
│      ─────────────────────────────────────                  │
│      UsingResourceNode 保证资源释放                         │
│      自动调用 dispose，即使异常                               │
│                                                             │
│   6️⃣ Try-Finally Pattern（保证清理）                        │
│      ─────────────────────────────────────                  │
│      FinallyNode 确保清理逻辑执行                            │
│      不依赖 return 语句                                      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 7.2 各模式解决的问题

| 模式 | 解决的问题 |
|------|------------|
| Composite | 如何用小节点构建大流程 |
| Strategy | 如何让不同行为使用相同接口 |
| Template Method | 如何定义固定的执行骨架 |
| Scope/DI | 如何在节点间传递数据 |
| RAII | 如何保证资源一定被释放 |
| Try-Finally | 如何保证清理代码一定执行 |

---

## 八、最佳实践

### 8.1 推荐做法

```
┌─────────────────────────────────────────────────────────────┐
│                    Flow 使用最佳实践                         │
│                                                             │
│   ✅ 推荐                                                   │
│                                                             │
│   1. 使用 FlowSession 管理生命周期                         │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ using (var session = FlowSession.Start(flow, ctx)) │  │
│   │ {                                                    │  │
│   │     session.Finished += OnFinished;                  │  │
│   │     // 自动清理                                       │  │
│   │ }                                                    │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   2. 使用 UsingResourceNode 管理资源                        │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new UsingResourceNode<Connection>(                  │  │
│   │     create: _ => Connect(),                         │  │
│   │     dispose: c => c.Close(),                        │  │
│   │     body: ...                                       │  │
│   │ )                                                   │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   3. 使用 FinallyNode 保证清理                              │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new FinallyNode(                                    │  │
│   │     tryNode: ...,                                   │  │
│   │     finallyNode: new DoNode(onExit: ctx => ...)    │  │
│   │ )                                                   │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   4. 设置 MaxPumpIterationsPerWake 防止死循环              │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ runner.MaxPumpIterationsPerWake = 128;             │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   5. 回调完成后调用 Wake()                                  │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new AwaitCallbackNode((ctx, complete) => {          │  │
│   │     DoAsync(result => {                            │  │
│   │         complete(FlowStatus.Succeeded);             │  │
│   │         ctx.Get<FlowWakeUp>().Wake();             │  │
│   │     });                                              │  │
│   │ })                                                   │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 8.2 避免做法

```
┌─────────────────────────────────────────────────────────────┐
│                    应避免的做法                              │
│                                                             │
│   ❌ 避免在 Tick 中阻塞                                    │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ // 错误：阻塞主线程                                   │  │
│   │ new DoNode(onTick: ctx => {                          │  │
│   │     Thread.Sleep(5000);  // 不要这样！               │  │
│   │     return FlowStatus.Succeeded;                     │  │
│   │ });                                                  │  │
│   │                                                       │  │
│   │ // 正确：使用 AwaitCallbackNode                      │  │
│   │ new AwaitCallbackNode((ctx, complete) => {           │  │
│   │     DoAsync(result => complete(...));               │  │
│   │ });                                                  │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   ❌ 避免忘记处理中断                                       │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ // 错误：订阅事件但不取消订阅                        │  │
│   │ public void Enter(FlowContext ctx) {                 │  │
│   │     SomeEvent += OnEvent;  // 订阅了                  │  │
│   │ }                                                    │  │
│   │                                                       │  │
│   │ // 正确：同时实现 Interrupt                         │  │
│   │ public void Interrupt(FlowContext ctx) {             │  │
│   │     SomeEvent -= OnEvent;  // 取消订阅               │  │
│   │ }                                                    │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   ❌ 避免直接在节点中抛出异常                               │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ // 错误                                              │  │
│   │ new DoNode(onTick: ctx => {                         │  │
│   │     throw new Exception("错误！");                  │  │
│   │ });                                                  │  │
│   │                                                       │  │
│   │ // 正确：返回 Failed                                 │  │
│   │ new DoNode(onTick: ctx => {                         │  │
│   │     try { ... }                                      │  │
│   │     catch { return FlowStatus.Failed; }             │  │
│   │ });                                                  │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 九、与其他模块的关系

### 9.1 Flow 模块的定位

```
┌─────────────────────────────────────────────────────────────┐
│                    Flow 模块在框架中的位置                    │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │              ability-kit 框架                        │  │
│   │                                                     │  │
│   │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐│  │
│   │  │   World     │  │   Flow      │  │   Network   ││  │
│   │  │  (世界管理) │  │ (流程控制)  │  │  (网络通信) ││  │
│   │  └─────────────┘  └──────┬──────┘  └──────┬──────┘│  │
│   │                          │                   │        │  │
│   │                          ▼                   │        │  │
│   │                   ┌─────────────┐           │        │  │
│   │                   │   Host      │◄──────────┘        │  │
│   │                   │  (运行时)   │                    │  │
│   │                   └─────────────┘                    │  │
│   │                                                     │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   Flow 的职责：流程编排（做什么、怎么做）                     │
│   Host 的职责：运行时管理（何时做、谁来做）                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 9.2 典型使用场景

```
┌─────────────────────────────────────────────────────────────┐
│                    Flow 典型使用场景                         │
│                                                             │
│   1️⃣ 游戏流程                                              │
│      - 登录流程：连接服务器 → 验证账号 → 进入大厅            │
│      - 战斗流程：加载资源 → 等待玩家 → 开始战斗 → 结束结算  │
│      - 背包流程：打开界面 → 加载数据 → 显示物品              │
│                                                             │
│   2️⃣ 资源管理                                              │
│      - 批量加载：并行加载所有资源 → 验证完整性 → 完成        │
│      - 资源替换：卸载旧资源 → 加载新资源 → 切换场景          │
│                                                             │
│   3️⃣ 网络请求                                              │
│      - 请求超时：发送请求 → 等待响应或超时                    │
│      - 重试机制：尝试请求 → 失败则重试 → 达到上限则失败      │
│                                                             │
│   4️⃣ UI 交互                                              │
│      - 弹窗流程：显示弹窗 → 等待玩家选择 → 处理结果          │
│      - 引导流程：显示提示 → 等待完成 → 进入下一步            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 十、快速参考

### 10.1 常用节点速查表

```
┌─────────────────────────────────────────────────────────────┐
│                    常用节点速查                              │
│                                                             │
│   执行一个动作：                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new DoNode(onTick: ctx => { ... })                 │  │
│   │ new ActionNode(onEnter: ..., onTick: ..., ...)      │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   顺序执行：                                                 │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new SequenceNode(node1, node2, node3)              │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   等待时间：                                                 │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new WaitSecondsNode(3f)                             │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   等待回调：                                                 │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new AwaitCallbackNode((ctx, complete) => {         │  │
│   │     asyncOp(result => {                            │  │
│   │         complete(FlowStatus.Succeeded);            │  │
│   │         ctx.Get<FlowWakeUp>().Wake();             │  │
│   │     });                                             │  │
│   │ })                                                   │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   条件分支：                                                 │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new IfNode(                                         │  │
│   │     condition: ctx => ctx.Get<bool>("condition"),  │  │
│   │     thenBranch: node1,                              │  │
│   │     elseBranch: node2                               │  │
│   │ )                                                   │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   超时控制：                                                 │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new TimeoutNode(child, 5f)                         │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   资源管理：                                                 │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new UsingResourceNode<T>(                          │  │
│   │     create: ctx => new T(),                         │  │
│   │     dispose: t => t.Dispose(),                      │  │
│   │     body: node                                      │  │
│   │ )                                                   │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   保证清理：                                                 │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ new FinallyNode(tryNode, finallyNode)               │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 10.2 状态流转速查

```
FlowStatus 状态机：

              ┌──────────────────────────────────────────┐
              │                                          │
              │              Enter()                     │
              │                  │                        │
              │                  ▼                        │
              │  ┌───────────────────────────────────┐   │
              │  │                                   │   │
              └─►│           Running                  │◄──┘
                 │              │                      │
                 │    Tick()    │ Tick()              │
                 │              │                      │
                 │     ▼        │        ▼             │
                 │  Succeeded   │    Failed           │
                 │              │                      │
                 │              │        ▼             │
                 │              │     Canceled         │
                 │              │                      │
                 │              ▼                      │
                 │  ┌───────────────────────────────────┐│
                 │  │                                   ││
                 └──│            Exit()                 │┘
                    └───────────────────────────────────┘

       Interrupt() 可以从任何 Running 状态跳转到 Exit()
```

---

## 十一、下一步

```
┌─────────────────────────────────────────────────────────────┐
│                    继续学习                                  │
│                                                             │
│   📖 阅读示例代码                                           │
│      - Samples~/FlowExamples/Runtime/                      │
│      - 01_BasicSessionExample.cs    ← 从这里开始            │
│      - 02_WakePumpExample.cs        ← Wake 机制             │
│      - 03_TimeoutAndRaceExample.cs  ← 超时和竞速            │
│      - 04_ParallelAllExample.cs     ← 并行执行              │
│      - 05_UsingResourceExample.cs   ← 资源管理              │
│      - 06_ExceptionHandlingExample.cs ← 异常处理            │
│                                                             │
│   📖 查看完整节点实现                                       │
│      - Runtime/Flow/Nodes/*.cs      ← 基础节点             │
│      - Runtime/Flow/Blocks/*.cs      ← 组合节点             │
│                                                             │
│   📖 理解框架集成                                           │
│      - Flow 在 Host 模块中的使用                            │
│      - Flow 与 World 的交互                                  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

*文档版本：1.0*
*最后更新：2026-03-19*
