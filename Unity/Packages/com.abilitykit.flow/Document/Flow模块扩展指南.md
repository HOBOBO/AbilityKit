# Ability-Kit Flow 模块扩展指南

> **阅读对象**：想要扩展 Ability-Kit Flow 模块的开发者
>
> **文档目标**：让你理解"怎么开发自定义节点"、"怎么组合现有节点"、"怎么与 Flow 协作"

---

## 一、扩展理念：Flow 为什么可以扩展？

### 1.1 Flow 的扩展点

```
┌─────────────────────────────────────────────────────────────┐
│                    Flow 模块的三大扩展点                      │
│                                                             │
│   1️⃣ 自定义节点                                            │
│      ─────────────────────────────────────                  │
│      开发一个实现 IFlowNode 的新类                           │
│      完全控制 Enter/Tick/Exit/Interrupt                    │
│                                                             │
│   2️⃣ 节点组合                                              │
│      ─────────────────────────────────────                  │
│      用现有节点组合出新功能                                  │
│      更安全、更简单                                         │
│                                                             │
│   3️⃣ 与其他模块协作                                        │
│      ─────────────────────────────────────                  │
│      FlowContext 中注入其他模块的服务                        │
│      Flow 作为其他模块的执行机制                            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 什么时候该写新节点？

```
┌─────────────────────────────────────────────────────────────┐
│                    决策树：要不要写新节点？                   │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                                                     │  │
│   │   现有节点能组合出你要的功能吗？                      │  │
│   │                                                     │  │
│   │   ├── Yes ──► 用现有节点组合                         │  │
│   │   │                                                │  │
│   │   └── No ──► 需要新的控制逻辑？                      │  │
│   │            │                                       │  │
│   │            ├── Yes ──► 考虑写新节点                  │  │
│   │            │                                       │  │
│   │            └── No ──► 可能是参数配置问题             │  │
│   │                                                     │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   ✅ 应该写新节点：                                         │
│   - 需要追踪特殊状态（如状态机）                            │
│   - 需要与其他系统深度集成（如物理引擎）                    │
│   - 需要优化性能（避免重复创建临时节点）                    │
│   - 需要暴露特殊配置接口                                   │
│                                                             │
│   ❌ 不应该写新节点：                                       │
│   - 只是想把几个节点串起来 → SequenceNode                  │
│   - 只是想加个超时 → TimeoutNode                          │
│   - 只是想处理条件分支 → IfNode                           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 二、自定义节点开发

### 2.1 节点接口详解

```
┌─────────────────────────────────────────────────────────────┐
│                    IFlowNode 生命周期                        │
│                                                             │
│   Enter(ctx) ──► Tick(ctx, dt) ──► Tick ──► ... ──► Exit  │
│                    │                                        │
│                    ├─► Running   (继续下一帧)               │
│                    ├─► Succeeded (成功完成)                 │
│                    ├─► Failed    (执行失败)                 │
│                    └─► Canceled  (被取消)                   │
│                                                             │
│   Interrupt ─────────────────────────────────────────► Exit  │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │  Enter                                              │  │
│   │  ────                                               │  │
│   │  做什么：初始化、订阅事件、开始异步操作              │  │
│   │  何时调用：节点被选中执行时                         │  │
│   │  注意：可能多次 Enter（如果节点被重新激活）          │  │
│   │                                                      │  │
│   │  Tick(ctx, dt)                                      │  │
│   │  ──────────────                                      │  │
│   │  做什么：核心逻辑、状态判断、返回状态                 │  │
│   │  何时调用：每帧一次（节点在运行中）                   │  │
│   │  注意：不要阻塞，返回 Running 表示继续                │  │
│   │                                                      │  │
│   │  Exit(ctx)                                          │  │
│   │  ───────                                             │  │
│   │  做什么：正常清理、取消订阅                          │  │
│   │  何时调用：Tick 返回非 Running 后                    │  │
│   │  注意：必须幂等，能安全重复调用                      │  │
│   │                                                      │  │
│   │  Interrupt(ctx)                                      │  │
│   │  ────────────                                        │  │
│   │  做什么：中断清理、强制结束                          │  │
│   │  何时调用：外部调用 Stop() 或父节点中断               │  │
│   │  注意：必须幂等，清理应该比 Exit 更彻底               │  │
│   │                                                      │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 基础节点开发模板

```csharp
// 场景：等待一个布尔值变为 true

public sealed class WaitForConditionNode : IFlowNode
{
    private readonly Func<FlowContext, bool> _condition;
    private bool _entered;

    public WaitForConditionNode(Func<FlowContext, bool> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public void Enter(FlowContext ctx)
    {
        _entered = true;
    }

    public FlowStatus Tick(FlowContext ctx, float dt)
    {
        if (!_entered) return FlowStatus.Failed;  // 防御性检查

        if (_condition(ctx))
        {
            return FlowStatus.Succeeded;
        }

        return FlowStatus.Running;
    }

    public void Exit(FlowContext ctx)
    {
        // 清理（如果有订阅）
        _entered = false;
    }

    public void Interrupt(FlowContext ctx)
    {
        // 中断清理
        _entered = false;
        // 这里应该做比 Exit 更彻底的清理
    }
}
```

### 2.3 带事件的节点开发模板

```csharp
// 场景：等待 GameEvent 触发

public sealed class WaitForEventNode : IFlowNode
{
    private readonly GameEvent _event;
    private readonly Func<FlowContext, GameEvent, bool> _condition;
    private bool _triggered;
    private bool _subscribed;

    public WaitForEventNode(GameEvent gameEvent, Func<FlowContext, GameEvent, bool> condition = null)
    {
        _event = gameEvent ?? throw new ArgumentNullException(nameof(gameEvent));
        _condition = condition;
    }

    public void Enter(FlowContext ctx)
    {
        _triggered = false;
        _event.OnTriggered += OnEventTriggered;
        _subscribed = true;
    }

    public FlowStatus Tick(FlowContext ctx, float dt)
    {
        if (_triggered)
        {
            return FlowStatus.Succeeded;
        }

        return FlowStatus.Running;
    }

    public void Exit(FlowContext ctx)
    {
        // 取消订阅（防止内存泄漏）
        if (_subscribed)
        {
            _event.OnTriggered -= OnEventTriggered;
            _subscribed = false;
        }
    }

    public void Interrupt(FlowContext ctx)
    {
        Exit(ctx);  // 中断时也取消订阅
    }

    private void OnEventTriggered(GameEvent evt)
    {
        _triggered = true;
        // 通知 Flow 继续执行
        // （通过 FlowWakeUp.Wake()）
    }
}
```

### 2.4 带资源的节点开发模板

```csharp
// 场景：管理一个 HttpClient 连接

public sealed class HttpConnectionNode : IFlowNode
{
    private readonly Func<FlowContext, string> _urlProvider;
    private readonly IFlowNode _body;

    private HttpClient _client;
    private bool _disposed;

    public HttpConnectionNode(string url, IFlowNode body)
        : this(ctx => url, body)
    {
    }

    public HttpConnectionNode(Func<FlowContext, string> urlProvider, IFlowNode body)
    {
        _urlProvider = urlProvider ?? throw new ArgumentNullException(nameof(urlProvider));
        _body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public void Enter(FlowContext ctx)
    {
        var url = _urlProvider(ctx);
        _client = new HttpClient { BaseAddress = new Uri(url) };
        _disposed = false;

        // 将 client 注入 context，供子节点使用
        ctx.Set(_client);

        // 进入子节点
        _body.Enter(ctx);
    }

    public FlowStatus Tick(FlowContext ctx, float dt)
    {
        return _body.Tick(ctx, dt);
    }

    public void Exit(FlowContext ctx)
    {
        // 退出子节点
        _body.Exit(ctx);

        // 释放资源
        Cleanup();
    }

    public void Interrupt(FlowContext ctx)
    {
        // 中断子节点
        _body.Interrupt(ctx);

        // 强制释放资源
        Cleanup();
    }

    private void Cleanup()
    {
        if (_disposed) return;

        _client?.Dispose();
        _client = null;
        _disposed = true;
    }
}
```

---

## 三、高级节点开发

### 3.1 组合节点开发模板

组合节点是包含子节点并控制其执行顺序的节点。

```csharp
// 场景：重复执行 N 次的节点

public sealed class RepeatNode : IFlowNode
{
    private readonly IFlowNode _child;
    private readonly int _count;
    private int _currentIndex;
    private bool _childEntered;

    public RepeatNode(IFlowNode child, int count)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _count = count > 0 ? count : 1;
    }

    public void Enter(FlowContext ctx)
    {
        _currentIndex = 0;
        _childEntered = false;
        EnterChild(ctx);
    }

    public FlowStatus Tick(FlowContext ctx, float dt)
    {
        if (!_childEntered)
        {
            EnterChild(ctx);
        }

        var status = _child.Tick(ctx, dt);

        switch (status)
        {
            case FlowStatus.Running:
                return FlowStatus.Running;

            case FlowStatus.Succeeded:
                _currentIndex++;
                if (_currentIndex >= _count)
                {
                    return FlowStatus.Succeeded;
                }
                // 退出当前子节点，进入下一次
                _child.Exit(ctx);
                EnterChild(ctx);
                return FlowStatus.Running;

            case FlowStatus.Failed:
            case FlowStatus.Canceled:
                // 失败/取消直接传播
                return status;

            default:
                return FlowStatus.Failed;
        }
    }

    public void Exit(FlowContext ctx)
    {
        if (_childEntered)
        {
            _child.Exit(ctx);
        }
    }

    public void Interrupt(FlowContext ctx)
    {
        if (_childEntered)
        {
            _child.Interrupt(ctx);
        }
    }

    private void EnterChild(FlowContext ctx)
    {
        _child.Enter(ctx);
        _childEntered = true;
    }
}
```

### 3.2 条件循环节点

```csharp
// 场景：每帧执行直到条件满足（类似 while 循环）

public sealed class WhileNode : IFlowNode
{
    private readonly IFlowNode _body;
    private readonly Func<FlowContext, bool> _condition;
    private bool _firstTick;

    public WhileNode(Func<FlowContext, bool> condition, IFlowNode body)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public void Enter(FlowContext ctx)
    {
        _firstTick = true;
        _body.Enter(ctx);
    }

    public FlowStatus Tick(FlowContext ctx, float dt)
    {
        // 第一帧先检查条件
        if (_firstTick)
        {
            _firstTick = false;
            if (!_condition(ctx))
            {
                return FlowStatus.Succeeded;
            }
        }

        // 执行循环体
        var status = _body.Tick(ctx, dt);

        if (status == FlowStatus.Running)
        {
            return FlowStatus.Running;
        }

        // 循环体完成，检查是否继续
        if (status == FlowStatus.Succeeded && _condition(ctx))
        {
            // 继续下一次循环
            _body.Exit(ctx);
            _body.Enter(ctx);
            return FlowStatus.Running;
        }

        // 条件不满足或循环体失败
        return status;
    }

    public void Exit(FlowContext ctx)
    {
        _body.Exit(ctx);
    }

    public void Interrupt(FlowContext ctx)
    {
        _body.Interrupt(ctx);
    }
}
```

### 3.3 带进度追踪的节点

```csharp
// 场景：带进度百分比的加载节点

public sealed class ProgressLoadingNode : IFlowNode
{
    private readonly string[] _assetPaths;
    private readonly Action<float> _onProgress;

    private int _loadedCount;
    private bool _loading;

    public ProgressLoadingNode(string[] assetPaths, Action<float> onProgress = null)
    {
        _assetPaths = assetPaths ?? throw new ArgumentNullException(nameof(assetPaths));
        _onProgress = onProgress;
    }

    public void Enter(FlowContext ctx)
    {
        _loadedCount = 0;
        _loading = true;

        // 开始加载第一个
        LoadNext(ctx);
    }

    public FlowStatus Tick(FlowContext ctx, float dt)
    {
        // 检查当前加载是否完成
        if (IsCurrentLoaded(ctx))
        {
            _loadedCount++;
            ReportProgress();

            if (_loadedCount >= _assetPaths.Length)
            {
                return FlowStatus.Succeeded;
            }

            // 加载下一个
            LoadNext(ctx);
        }

        return FlowStatus.Running;
    }

    public void Exit(FlowContext ctx)
    {
        _loading = false;
    }

    public void Interrupt(FlowContext ctx)
    {
        // 中断所有加载
        for (int i = _loadedCount; i < _assetPaths.Length; i++)
        {
            CancelLoad(_assetPaths[i]);
        }
        _loading = false;
    }

    private void LoadNext(FlowContext ctx)
    {
        var path = _assetPaths[_loadedCount];
        ctx.Set($"loading_{path}", true);

        // 异步加载（示例）
        Resource.LoadAsync(path, obj =>
        {
            ctx.Set($"loading_{path}", false);
            ctx.Set($"loaded_{path}", obj);
        });
    }

    private bool IsCurrentLoaded(FlowContext ctx)
    {
        var path = _assetPaths[_loadedCount];
        return ctx.TryGet<bool>($"loading_{path}", out var loading) && !loading
               && ctx.TryGet($"loaded_{path}", out var _);
    }

    private void CancelLoad(string path)
    {
        // 取消加载逻辑
    }

    private void ReportProgress()
    {
        var progress = (float)_loadedCount / _assetPaths.Length;
        _onProgress?.Invoke(progress);
    }
}
```

---

## 四、Flow 与其他模块协作

### 4.1 在 Flow 中使用其他模块服务

```
┌─────────────────────────────────────────────────────────────┐
│                    FlowContext 作为 DI 容器                  │
│                                                             │
│   Flow 模块本身不依赖其他模块                                 │
│   但你可以在 FlowContext 中注入其他服务                      │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                                                       │  │
│   │   // 在启动 Flow 前注入服务                          │  │
│   │   var ctx = new FlowContext();                       │  │
│   │   ctx.Set(new FlowWakeUp());                        │  │
│   │   ctx.Set(_networkService);                         │  │
│   │   ctx.Set(_audioService);                           │  │
│   │   ctx.Set(_gameState);                              │  │
│   │                                                       │  │
│   │   // 在节点中使用                                    │  │
│   │   new AwaitCallbackNode((ctx, complete) => {         │  │
│   │       var network = ctx.Get<INetworkService>();     │  │
│   │       network.Request(url, result => complete(...)); │  │
│   │   });                                                │  │
│   │                                                       │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 Flow 作为其他模块的执行机制

```
┌─────────────────────────────────────────────────────────────┐
│                    Flow 作为执行机制                          │
│                                                             │
│   其他模块可以创建 Flow 来执行复杂的异步逻辑                   │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                                                       │  │
│   │   // GameManager 使用 Flow 执行关卡加载               │  │
│   │   public class GameManager {                         │  │
│   │       private FlowSession _levelLoadSession;        │  │
│   │                                                       │  │
│   │       public void LoadLevel(string levelId) {       │  │
│   │           var ctx = CreateContext();                 │  │
│   │           var flow = BuildLevelFlow(levelId);       │  │
│   │                                                           │  │
│   │           _levelLoadSession = new FlowSession();    │  │
│   │           _levelLoadSession.Finished += OnLevelLoad;  │  │
│   │           _levelLoadSession.Start(flow, ctx);       │  │
│   │       }                                              │  │
│   │                                                           │  │
│   │       private FlowContext CreateContext() {           │  │
│   │           var ctx = new FlowContext();                │  │
│   │           ctx.Set(new FlowWakeUp());                 │  │
│   │           ctx.Set(_networkService);                  │  │
│   │           ctx.Set(_resourceService);                 │  │
│   │           ctx.Set(_audioService);                   │  │
│   │           return ctx;                                │  │
│   │       }                                              │  │
│   │                                                           │  │
│   │       private IFlowNode BuildLevelFlow(string id) {   │  │
│   │           return new SequenceNode(                     │  │
│   │               new AwaitCallbackNode(...),  // 加载资源 │  │
│   │               new AwaitCallbackNode(...),  // 初始化   │  │
│   │               new DoNode(...)           // 完成       │  │
│   │           );                                         │  │
│   │       }                                              │  │
│   │   }                                                  │  │
│   │                                                       │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.3 完整示例：带网络请求的流程

```csharp
// 场景：实现一个"登录流程"，涉及网络请求、错误处理、重试

public class LoginFlowBuilder
{
    private readonly INetworkService _network;
    private readonly IAuthService _auth;
    private readonly int _maxRetries = 3;

    public LoginFlowBuilder(INetworkService network, IAuthService auth)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
    }

    public IFlowNode Build()
    {
        return new SequenceNode(
            // 步骤1：连接服务器
            new AwaitCallbackNode<bool>((ctx, complete) =>
            {
                ctx.Set(new FlowWakeUp());  // 确保有 WakeUp

                _network.Connect("server.example.com", result =>
                {
                    if (result.Success)
                    {
                        ctx.Set(new Connection(result.Connection));
                        complete(FlowStatus.Succeeded);
                    }
                    else
                    {
                        ctx.Set(new ErrorInfo(result.Error));
                        complete(FlowStatus.Failed);
                    }
                });
            }),

            // 步骤2：带重试的登录
            new RetryNode(
                maxRetries: _maxRetries,
                body: new SequenceNode(
                    // 收集凭据
                    new DoNode(onEnter: ctx =>
                    {
                        var credentials = _auth.GetCurrentCredentials();
                        ctx.Set(credentials);
                    }),

                    // 发送登录请求
                    new AwaitCallbackNode<LoginResult>((ctx, complete) =>
                    {
                        var credentials = ctx.Get<Credentials>("credentials");
                        _network.Request<LoginResponse>(
                            "auth/login",
                            credentials.ToDictionary(),
                            response =>
                            {
                                if (response.Success)
                                {
                                    ctx.Set(new Token(response.Token));
                                    ctx.Set(new UserInfo(response.User));
                                    complete(FlowStatus.Succeeded);
                                }
                                else
                                {
                                    ctx.Set(new ErrorInfo(response.Error));
                                    complete(FlowStatus.Failed);
                                }
                            }
                        );
                    })
                ),
                onRetry: (ctx, attempt, maxRetries) =>
                {
                    var error = ctx.Get<ErrorInfo>();
                    Debug.Log($"登录失败，第 {attempt} 次重试: {error.Message}");
                }
            ),

            // 步骤3：处理登录结果
            new IfNode(
                condition: ctx => ctx.TryGet<ErrorInfo>(out var err) && err != null,
                thenBranch: new DoNode(onTick: ctx =>
                {
                    var error = ctx.Get<ErrorInfo>();
                    Debug.LogError($"登录失败: {error.Message}");
                    return FlowStatus.Failed;
                }),
                elseBranch: new SequenceNode(
                    // 存储用户信息
                    new DoNode(onTick: ctx =>
                    {
                        var token = ctx.Get<Token>();
                        var user = ctx.Get<UserInfo>();
                        _auth.SetSession(token, user);
                        return FlowStatus.Succeeded;
                    }),

                    // 加载用户数据
                    new AwaitCallbackNode<bool>((ctx, complete) =>
                    {
                        var userId = ctx.Get<UserInfo>().Id;
                        _network.Request<UserData>(
                            $"users/{userId}/data",
                            null,
                            response =>
                            {
                                if (response.Success)
                                {
                                    ctx.Set(response.Data);
                                    complete(FlowStatus.Succeeded);
                                }
                                else
                                {
                                    complete(FlowStatus.Failed);
                                }
                            }
                        );
                    }),

                    // 完成
                    new DoNode(onTick: ctx =>
                    {
                        Debug.Log("登录成功！");
                        return FlowStatus.Succeeded;
                    })
                )
            ),

            // 步骤4：清理（Finally）
            new FinallyNode(
                tryNode: new DoNode(onEnter: _ => { /* 占位 */ }),
                finallyNode: new DoNode(onExit: ctx =>
                {
                    // 无论成功失败，都断开连接
                    if (ctx.TryGet<Connection>(out var conn))
                    {
                        conn.Disconnect();
                    }
                    Debug.Log("登录流程结束");
                })
            )
        );
    }
}

// 重试节点
public sealed class RetryNode : IFlowNode
{
    private readonly IFlowNode _body;
    private readonly int _maxRetries;
    private readonly Action<FlowContext, int, int> _onRetry;

    private int _currentAttempt;
    private bool _childEntered;

    public RetryNode(IFlowNode body, int maxRetries, Action<FlowContext, int, int> onRetry = null)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _maxRetries = maxRetries > 0 ? maxRetries : 1;
        _onRetry = onRetry;
    }

    public void Enter(FlowContext ctx)
    {
        _currentAttempt = 0;
        _childEntered = false;
        EnterChild(ctx);
    }

    public FlowStatus Tick(FlowContext ctx, float dt)
    {
        if (!_childEntered)
        {
            EnterChild(ctx);
        }

        var status = _body.Tick(ctx, dt);

        if (status == FlowStatus.Running)
        {
            return FlowStatus.Running;
        }

        if (status == FlowStatus.Succeeded)
        {
            return FlowStatus.Succeeded;
        }

        // 失败，尝试重试
        _currentAttempt++;
        _body.Exit(ctx);

        if (_currentAttempt >= _maxRetries)
        {
            return FlowStatus.Failed;
        }

        // 触发重试回调
        _onRetry?.Invoke(ctx, _currentAttempt + 1, _maxRetries);

        // 重试
        EnterChild(ctx);
        return FlowStatus.Running;
    }

    public void Exit(FlowContext ctx)
    {
        if (_childEntered)
        {
            _body.Exit(ctx);
        }
    }

    public void Interrupt(FlowContext ctx)
    {
        if (_childEntered)
        {
            _body.Interrupt(ctx);
        }
    }

    private void EnterChild(FlowContext ctx)
    {
        _body.Enter(ctx);
        _childEntered = true;
    }
}
```

---

## 五、最佳实践

### 5.1 节点设计原则

```
┌─────────────────────────────────────────────────────────────┐
│                    节点设计原则                              │
│                                                             │
│   1. 单一职责                                              │
│      ─────────────────────────────────────                  │
│      每个节点只做一件事                                      │
│      复杂逻辑通过组合实现                                    │
│                                                             │
│   2. 幂等性                                                │
│      ─────────────────────────────────────                  │
│      Exit() 和 Interrupt() 必须幂等                          │
│      能安全重复调用                                         │
│                                                             │
│   3. 状态隔离                                              │
│      ─────────────────────────────────────                  │
│      节点状态存储在节点内部                                  │
│      不要依赖 FlowContext 存储临时状态                       │
│                                                             │
│   4. 资源管理                                              │
│      ─────────────────────────────────────                  │
│      获取的资源必须释放                                      │
│      使用 try-finally 或 UsingResourceNode                  │
│                                                             │
│   5. 事件订阅                                              │
│      ─────────────────────────────────────                  │
│      Enter() 中订阅                                        │
│      Exit() 或 Interrupt() 中取消订阅                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 常见错误与避免

```
┌─────────────────────────────────────────────────────────────┐
│                    常见错误与解决方案                         │
│                                                             │
│   ❌ 错误1：忘记取消事件订阅                                │
│   ───────────────────────────────────────────────────────  │
│   public void Enter(FlowContext ctx) {                    │
│       SomeEvent += OnEvent;  // 订阅了                      │
│   }                                                        │
│   public void Exit(FlowContext ctx) {                      │
│       // 忘记取消订阅！                                     │
│   }                                                        │
│                                                             │
│   ✅ 正确做法：                                             │
│   public void Exit(FlowContext ctx) {                      │
│       SomeEvent -= OnEvent;  // 取消订阅                    │
│   }                                                        │
│   public void Interrupt(FlowContext ctx) {                │
│       SomeEvent -= OnEvent;  // 中断也要取消                │
│   }                                                        │
│                                                             │
│   ───────────────────────────────────────────────────────  │
│                                                             │
│   ❌ 错误2：在 Tick 中阻塞                                 │
│   ───────────────────────────────────────────────────────  │
│   public FlowStatus Tick(FlowContext ctx, float dt) {      │
│       while (!done) { Thread.Sleep(100); }  // 阻塞！      │
│       return FlowStatus.Succeeded;                         │
│   }                                                        │
│                                                             │
│   ✅ 正确做法：                                             │
│   public FlowStatus Tick(FlowContext ctx, float dt) {      │
│       if (!done) return FlowStatus.Running;               │
│       return FlowStatus.Succeeded;                         │
│   }                                                        │
│                                                             │
│   ───────────────────────────────────────────────────────  │
│                                                             │
│   ❌ 错误3：Enter 中调用 Tick                              │
│   ───────────────────────────────────────────────────────  │
│   public void Enter(FlowContext ctx) {                     │
│       Tick(ctx, 0);  // 错误！                             │
│   }                                                        │
│                                                             │
│   ✅ 正确做法：                                             │
│   public void Enter(FlowContext ctx) {                    │
│       // 只做初始化，不执行逻辑                             │
│   }                                                        │
│   // Tick 会被框架在下一帧调用                             │
│                                                             │
│   ───────────────────────────────────────────────────────  │
│                                                             │
│   ❌ 错误4：不处理取消状态                                  │
│   ───────────────────────────────────────────────────────  │
│   switch (status) {                                        │
│       case FlowStatus.Succeeded: return FlowStatus.Succeeded;│
│       case FlowStatus.Running: return FlowStatus.Running;   │
│       default: return FlowStatus.Failed;  // 丢失 Canceled  │
│   }                                                        │
│                                                             │
│   ✅ 正确做法：                                             │
│   if (status == FlowStatus.Running) return FlowStatus.Running;│
│   if (status == FlowStatus.Succeeded) return FlowStatus.Succeeded;│
│   return status;  // 传播 Failed 和 Canceled              │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.3 调试技巧

```csharp
// 技巧1：添加日志
public FlowStatus Tick(FlowContext ctx, float dt)
{
    Debug.Log($"[{GetType().Name}] Tick at frame {Time.frameCount}");

    if (_condition(ctx))
    {
        Debug.Log($"[{GetType().Name}] Condition met, succeeding");
        return FlowStatus.Succeeded;
    }

    return FlowStatus.Running;
}

// 技巧2：验证状态
public void Enter(FlowContext ctx)
{
    if (_entered)
    {
        Debug.LogWarning($"[{GetType().Name}] Enter called but already entered!");
    }
    _entered = true;
}

// 技巧3：记录执行次数（防死循环）
public FlowStatus Tick(FlowContext ctx, float dt)
{
    _tickCount++;
    if (_tickCount > 10000)
    {
        throw new InvalidOperationException($"Tick count exceeded limit in {GetType().Name}");
    }
    // ...
}

// 技巧4：使用节点名便于调试
public override string ToString()
{
    return $"{GetType().Name}[{_id}]";
}
```

---

## 六、扩展模式总结

### 6.1 节点类型选择

```
┌─────────────────────────────────────────────────────────────┐
│                    何时使用哪种节点？                         │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐  │
│   │                                                       │  │
│   │   只需要执行一次逻辑                                 │  │
│   │   └─► DoNode / ActionNode                           │  │
│   │                                                       │  │
│   │   需要等待一段时间                                   │  │
│   │   └─► WaitSecondsNode                               │  │
│   │                                                       │  │
│   │   需要等待外部回调                                   │  │
│   │   └─► AwaitCallbackNode                              │  │
│   │                                                       │  │
│   │   需要按顺序执行多个步骤                             │  │
│   │   └─► SequenceNode                                   │  │
│   │                                                       │  │
│   │   需要先完成的决定结果                               │  │
│   │   └─► RaceNode                                       │  │
│   │                                                       │  │
│   │   需要全部完成才算成功                               │  │
│   │   └─► ParallelAllNode                                 │  │
│   │                                                       │  │
│   │   需要条件分支                                       │  │
│   │   └─► IfNode / SwitchNode                            │  │
│   │                                                       │  │
│   │   需要限制时间                                       │  │
│   │   └─► TimeoutNode                                    │  │
│   │                                                       │  │
│   │   需要保证清理                                       │  │
│   │   └─► FinallyNode / UsingResourceNode                │  │
│   │                                                       │  │
│   │   现有节点无法满足需求                                │  │
│   │   └─► 自定义 IFlowNode 实现                          │  │
│   │                                                       │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 6.2 扩展策略对比

```
┌─────────────────────────────────────────────────────────────┐
│                    扩展策略对比                              │
│                                                             │
│   ┌───────────────┬─────────────────┬─────────────────┐  │
│   │ 策略          │ 优点              │ 缺点              │  │
│   ├───────────────┼─────────────────┼─────────────────┤  │
│   │ 节点组合      │ 安全、可复用     │ 受限于现有节点   │  │
│   ├───────────────┼─────────────────┼─────────────────┤  │
│   │ 自定义节点    │ 完全可控         │ 需要更多代码     │  │
│   ├───────────────┼─────────────────┼─────────────────┤  │
│   │ 节点工厂      │ 动态生成          │ 复杂度增加      │  │
│   ├───────────────┼─────────────────┼─────────────────┤  │
│   │ 节点装饰器    │ 透明扩展          │ 需要包装        │  │
│   └───────────────┴─────────────────┴─────────────────┘  │
│                                                             │
│   推荐顺序：                                                │
│   1. 先尝试用现有节点组合                                   │
│   2. 组合做不到，再考虑自定义节点                          │
│   3. 如果需要动态生成，考虑节点工厂                        │
│   4. 如果需要透明增强，考虑装饰器模式                       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 七、完整示例：技能释放流程

```csharp
// 场景：实现一个 MOBA 技能释放流程

public class SkillCastFlowBuilder
{
    private readonly ISkillSystem _skillSystem;
    private readonly ICooldownService _cooldown;
    private readonly IEffectService _effect;

    public SkillCastFlowBuilder(ISkillSystem skillSystem, ICooldownService cooldown, IEffectService effect)
    {
        _skillSystem = skillSystem;
        _cooldown = cooldown;
        _effect = effect;
    }

    public IFlowNode BuildCastFlow(Skill skill, Entity caster, Entity target)
    {
        // 上下文初始化
        var init = new DoNode(onEnter: ctx =>
        {
            ctx.Set(skill);
            ctx.Set(caster);
            ctx.Set(target);
            ctx.Set(new SkillCastContext());
        });

        // 技能引导
        var channel = new SequenceNode(
            new DoNode(onEnter: ctx =>
            {
                var cast = ctx.Get<SkillCastContext>();
                cast.StartChannelTime = Time.time;
                _skillSystem.StartChanneling(skill, caster);
            }),

            new WaitSecondsNode(skill.ChannelDuration),

            new DoNode(onTick: ctx =>
            {
                var cast = ctx.Get<SkillCastContext>();
                cast.ChannelComplete = true;
                return FlowStatus.Succeeded;
            })
        );

        // 带中断的引导
        var interruptibleChannel = new IfNode(
            condition: ctx => ctx.Get<SkillCastContext>().ChannelComplete,
            thenBranch: new DoNode(onTick: FlowStatus.Succeeded),
            elseBranch: new RaceNode(
                channel,
                // 等待玩家中断
                new WaitUntilNode(ctx => ctx.Get<SkillCastContext>().Interrupted)
            )
        );

        // 效果应用
        var applyEffects = new ParallelAllNode(
            skill.Effects.Select(effectId =>
                new AwaitCallbackNode<bool>((ctx, complete) =>
                {
                    var c = ctx.Get<SkillCastContext>();
                    var sk = ctx.Get<Skill>();
                    var t = ctx.Get<Entity>();

                    _effect.Apply(effectId, c.Caster, t, result =>
                    {
                        complete(result ? FlowStatus.Succeeded : FlowStatus.Failed);
                    });
                })
            ).ToArray()
        );

        // 冷却开始
        var startCooldown = new DoNode(onEnter: ctx =>
        {
            var sk = ctx.Get<Skill>();
            var c = ctx.Get<Entity>();
            _cooldown.StartCooldown(sk.Id, c.Id, sk.Cooldown);
        });

        // 清理
        var cleanup = new DoNode(onExit: ctx =>
        {
            var cast = ctx.Get<SkillCastContext>();
            if (!cast.ChannelComplete && !cast.Interrupted)
            {
                // 技能被打断
                _skillSystem.CancelChanneling(skill, caster);
            }
        });

        // 完整流程
        return new SequenceNode(
            init,

            // 检查冷却
            new IfNode(
                condition: ctx =>
                {
                    var sk = ctx.Get<Skill>();
                    var c = ctx.Get<Entity>();
                    return !_cooldown.IsOnCooldown(sk.Id, c.Id);
                },
                thenBranch: new SequenceNode(
                    interruptibleChannel,
                    applyEffects,
                    startCooldown
                ),
                elseBranch: new DoNode(onTick: ctx =>
                {
                    Debug.Log("技能还在冷却中");
                    return FlowStatus.Failed;
                })
            ),

            // Finally
            new FinallyNode(
                tryNode: new DoNode(onEnter: _ => { }),
                finallyNode: cleanup
            )
        );
    }
}
```

---

## 八、下一步

```
┌─────────────────────────────────────────────────────────────┐
│                    继续学习                                  │
│                                                             │
│   📖 阅读示例代码                                           │
│      - Samples~/FlowExamples/Runtime/                      │
│      - 从简单的开始，逐步理解复杂组合                        │
│                                                             │
│   📖 学习现有节点实现                                       │
│      - Blocks/ 下的组合节点                                 │
│      - 理解组合节点如何管理子节点                           │
│                                                             │
│   📖 与其他模块结合                                         │
│      - Flow + Host：作为世界的引导逻辑                      │
│      - Flow + Network：处理网络请求流程                     │
│      - Flow + UI：处理界面交互流程                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

*文档版本：1.0*
*最后更新：2026-03-19*
