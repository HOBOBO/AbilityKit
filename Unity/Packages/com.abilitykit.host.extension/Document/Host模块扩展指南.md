# Ability-Kit Host 模块扩展指南

> **阅读对象**：想要基于 Ability-Kit Host 开发自定义功能的开发者
>
> **文档目标**：让你理解 Host 模块"提供了哪些扩展点"、"为什么这样设计"、"怎么开发自己的扩展"

---

## 一、设计理念：Host 为什么是一个扩展框架？

### 1.1 传统游戏服务器的耦合问题

```
❌ 传统服务器架构的问题：

┌─────────────────────────────────────────────────────────────┐
│                      单体游戏服务器                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐       │
│   │ 网络层  │──│ 战斗逻辑│──│ 房间管理│──│ 帧同步  │       │
│   └─────────┘  └─────────┘  └─────────┘  └─────────┘       │
│       │            │            │            │              │
│       └────────────┴────────────┴────────────┘              │
│                        │                                    │
│                        ▼                                    │
│               ┌─────────────────┐                           │
│               │  紧密耦合在一起  │                           │
│               │  修改一处影响全局 │                           │
│               └─────────────────┘                           │
│                                                             │
└─────────────────────────────────────────────────────────────┘

问题：
1. 帧同步逻辑和战斗逻辑写在一起
2. 房间管理和网络传输写在一起
3. 想换一个同步算法？重写大半个服务器
4. 想复用代码？复制粘贴然后改
```

### 1.2 Host 的解决方案：关注点分离

```
✅ Host 模块的设计思路：

┌─────────────────────────────────────────────────────────────┐
│                       HostRuntime                           │
│                       (核心框架)                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   HostRuntime 只关心：                                      │
│   - 世界管理（创建、销毁、Tick）                            │
│   - 客户端连接（连接、断开、广播）                          │
│   - 扩展点提供（Hook、Feature）                            │
│                                                             │
│   HostRuntime 不关心：                                      │
│   - 帧同步怎么实现                                          │
│   - 房间怎么管理                                            │
│   - 回滚怎么做                                              │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                       扩展模块层                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐       │
│   │FrameSync   │  │   MobaRoom  │  │ ServerRoll  │       │
│   │Module      │  │   Module    │  │  backModule │       │
│   │            │  │             │  │             │       │
│   │ 帧同步实现 │  │  房间管理   │  │  回滚实现  │       │
│   └─────────────┘  └─────────────┘  └─────────────┘       │
│                                                             │
│   每个模块只做一件事，互不依赖                               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 1.3 扩展的核心思想

```
Host 扩展框架的核心思想：

┌─────────────────────────────────────────────────────────────┐
│                        三个核心机制                          │
│                                                             │
│   1️⃣ Hook（钩子）                                           │
│      ─────────────────────────────────────                  │
│      "我运行到某个时刻，通知你一声"                          │
│      - PreTick：帧开始前                                    │
│      - PostTick：帧结束后                                   │
│      - WorldCreated：世界创建后                             │
│                                                             │
│   2️⃣ Feature（功能）                                       │
│      ─────────────────────────────────────                  │
│      "我提供这个功能，你要用就来拿"                          │
│      - 注册：runtime.Features.Register<IFoo>(this)         │
│      - 获取：runtime.Features.TryGetFeature<IFoo>(out f)  │
│                                                             │
│   3️⃣ Blueprint（蓝图）                                      │
│      ─────────────────────────────────────                  │
│      "创建世界时，按这个配方装配"                            │
│      - 注册蓝图：blueprints.Register(battleBlueprint)      │
│      - 使用蓝图：CreateWorld(type="battle")                │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 二、Hook 扩展点详解

### 2.1 什么是 Hook？

**通俗解释**：Hook 就像"监控摄像头"，你告诉 Host "在某个时刻叫我一声"，然后 Host 就会在那个时刻通知你。

```
┌─────────────────────────────────────────────────────────────┐
│                        Hook 工作原理                        │
│                                                             │
│   开发者视角：                                              │
│                                                             │
│   options.PreTick.Add(myHandler, order: 0);  // 登记        │
│                                                             │
│   HostRuntime 视角：                                        │
│                                                             │
│   void Tick(float dt)                                       │
│   {                                                         │
│       // ... 世界 Tick ...                                  │
│                                                             │
│       PreTick.Invoke(dt);  // ← 通知所有登记者              │
│   }                                                         │
│                                                             │
│   好处：                                                    │
│   - Host 不需要知道你在做什么                                │
│   - 你不需要修改 Host 代码                                   │
│   - 随时可以添加/移除                                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Host 提供的所有 Hook

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         HostRuntimeOptions 的 Hook 一览                    │
│                                                                         │
│   世界生命周期 Hook：                                                    │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │                                                                  │  │
│   │  BeforeCreateWorld ──► CreateWorld() ──► WorldCreated            │  │
│   │       │                                        │                 │  │
│   │       │                                        ▼                 │  │
│   │       │                               ┌──────────────┐          │  │
│   │       │                               │ 安装世界模块  │          │  │
│   │       │                               └──────────────┘          │  │
│   │       │                                                         │  │
│   │       ▼                                                         │  │
│   │   你可以在这里修改                                               │  │
│   │   WorldCreateOptions                                            │  │
│   │                                                                  │  │
│   └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│   Tick 生命周期 Hook：                                                  │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │                                                                  │  │
│   │  PreTick ──► 世界Tick ──► PostTick                              │  │
│   │      │                    │                                     │  │
│   │      ▼                    ▼                                     │  │
│   │  适合：                  适合：                                 │  │
│   │  - 收集输入              - 帧同步广播                           │  │
│   │  - 准备数据              - 快照采集                             │  │
│   │  - 清理临时状态          - 统计/日志                            │  │
│   │                                                                  │  │
│   └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│   消息生命周期 Hook：                                                    │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │                                                                  │  │
│   │  BeforeSendMessage ──► Send ──► AfterSendMessage                │  │
│   │        │                                  │                     │  │
│   │        ▼                                  ▼                     │  │
│   │   适合：                            适合：                      │  │
│   │   - 消息加密                        - 日志记录                  │  │
│   │   - 消息过滤                        - 统计                      │  │
│   │   - 权限检查                        - 调试                      │  │
│   │                                                                  │  │
│   └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.3 Hook 的优先级机制

**通俗解释**：优先级（order）决定执行顺序，数字小的先执行。

```
┌─────────────────────────────────────────────────────────────┐
│                    Hook 优先级示例                           │
│                                                             │
│   Hook.Add(handlerA, order: 0);   // 先执行                  │
│   Hook.Add(handlerB, order: 100); // 后执行                  │
│   Hook.Add(handlerC, order: -50); // 最先执行                │
│                                                             │
│   执行顺序：                                                 │
│   ┌─────────────────────────────────────────────────────┐   │
│   │ -50: handlerC  ◄── 最先                              │   │
│   │   0: handlerA                                        │   │
│   │ 100: handlerB  ◄── 最后                              │   │
│   └─────────────────────────────────────────────────────┘   │
│                                                             │
│   典型用途：                                                │
│   - order: -100 ~ -50  → 系统级模块（最底层）               │
│   - order: 0           → 普通业务逻辑                       │
│   - order: 50 ~ 100    → 可选模块（最上层）                 │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 2.4 使用 Hook 的完整示例

```csharp
// 场景：开发一个 FPS 监控模块

public class FramerateMonitorModule : IHostRuntimeModule
{
    private HostRuntime _runtime;
    private HostRuntimeOptions _options;

    // 缓存委托，避免每次都创建（性能优化）
    private readonly Action<IWorld> _onWorldCreated;
    private readonly Action<WorldId> _onWorldDestroyed;
    private readonly Action<float> _onPreTick;
    private readonly Action<float> _onPostTick;

    // 每个世界的 FPS 计数器
    private readonly Dictionary<WorldId, FpsCounter> _counters = new Dictionary<WorldId, FpsCounter>();

    public FramerateMonitorModule()
    {
        // 预分配委托
        _onWorldCreated = OnWorldCreated;
        _onWorldDestroyed = OnWorldDestroyed;
        _onPreTick = OnPreTick;
        _onPostTick = OnPostTick;
    }

    public void Install(HostRuntime runtime, HostRuntimeOptions options)
    {
        _runtime = runtime;
        _options = options;

        // 注册 Hook
        options.WorldCreated.Add(_onWorldCreated);
        options.WorldDestroyed.Add(_onWorldDestroyed);
        options.PreTick.Add(_onPreTick, order: -100);  // 负优先级，最先执行
        options.PostTick.Add(_onPostTick, order: 100);  // 正优先级，最后执行
    }

    public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
    {
        // 卸载时必须清理！
        options.WorldCreated.Remove(_onWorldCreated);
        options.WorldDestroyed.Remove(_onWorldDestroyed);
        options.PreTick.Remove(_onPreTick);
        options.PostTick.Remove(_onPostTick);

        _counters.Clear();
        _runtime = null;
        _options = null;
    }

    private void OnWorldCreated(IWorld world)
    {
        _counters[world.Id] = new FpsCounter();
    }

    private void OnWorldDestroyed(WorldId worldId)
    {
        _counters.Remove(worldId);
    }

    private void OnPreTick(WorldId worldId)
    {
        // PreTick 没有 WorldId 参数，这里遍历所有世界
        foreach (var counter in _counters.Values)
        {
            counter.BeginFrame();
        }
    }

    private void OnPostTick(float deltaTime)
    {
        // PostTick 按世界广播，这里我们打印总览
        foreach (var kv in _counters)
        {
            var fps = kv.Value.EndFrame();
            Console.WriteLine($"World {kv.Key}: FPS={fps:F1}");
        }
    }
}
```

---

## 三、Feature 扩展机制详解

### 3.1 什么是 Feature？

**通俗解释**：Feature 就像"服务台"，每个模块把自己的服务"挂上去"，其他模块需要时来"查询"。

```
┌─────────────────────────────────────────────────────────────┐
│                    Feature 工作原理                         │
│                                                             │
│   模块A 提供服务：                                          │
│   ┌─────────────────────────────────────────┐              │
│   │ FrameSyncModule.Install()                │              │
│   │                                          │              │
│   │   // "我提供帧同步输入收集服务"          │              │
│   │   runtime.Features.Register<             │              │
│   │       IFrameSyncInputHub>(this);        │              │
│   │                                          │              │
│   └─────────────────────────────────────────┘              │
│                       │                                     │
│                       ▼                                     │
│              ┌────────────────┐                             │
│              │  Feature 注册表  │                             │
│              │                │                             │
│              │ IFrameSyncInputHub ──► FrameSyncModule        │
│              │ IFrameSyncDriverEvents ──► FrameSyncModule    │
│              │ IWorldStateSnapshotProvider ──► ??           │
│              └────────────────┘                             │
│                       ▲                                     │
│                       │                                     │
│   模块B 使用服务：                                          │
│   ┌─────────────────────────────────────────┐              │
│   │ ClientPredictionModule.Install()         │              │
│   │                                          │              │
│   │   // "我需要帧同步输入收集服务"          │              │
│   │   if (runtime.Features.TryGetFeature<    │              │
│   │       IFrameSyncInputHub>(out var hub)) │              │
│   │   {                                     │              │
│   │       _inputHub = hub;                 │              │
│   │   }                                     │              │
│   │                                          │              │
│   └─────────────────────────────────────────┘              │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 Feature 的优势

```
┌─────────────────────────────────────────────────────────────┐
│                    Feature 相比直接依赖的优势                │
│                                                             │
│   ❌ 直接依赖：                                              │
│   ┌─────────────┐      ┌─────────────┐                     │
│   │ ModuleA     │ ───► │ ModuleB     │                     │
│   └─────────────┘      └─────────────┘                     │
│        │                     │                              │
│        │  A 必须知道 B 的存在                                │
│        │  B 改了，A 可能也要改                               │
│        │  难以单独测试 A                                     │
│        │                                                      │
│        ▼                                                      │
│   ✅ Feature 依赖：                                          │
│   ┌─────────────┐      ┌─────────────┐                     │
│   │ ModuleA     │      │ ModuleB     │                     │
│   └──────┬──────┘      └──────┬──────┘                     │
│          │                    │                              │
│          │   ┌────────────────┘                              │
│          │   │                                                │
│          ▼   ▼                                                │
│   ┌─────────────────────────────┐                            │
│   │   IFeatureRegistry          │                            │
│   │                             │                            │
│   │   A 只知道自己需要 IFoo     │                            │
│   │   B 只知道自己提供 IFoo     │                            │
│   │   两者不直接认识             │                            │
│   └─────────────────────────────┘                            │
│                                                             │
│   好处：                                                    │
│   1. 模块解耦：A 不需要知道 B 的具体类型                     │
│   2. 按需加载：没有 B，Host 也能运行 A                      │
│   3. 易于测试：可以用 mock 替代 B                           │
│   4. 可替换：可以用 B2 替换 B，A 不需要改                    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 3.3 Feature 接口定义模式

**推荐做法**：Feature 接口和实现类分开定义

```csharp
// ========== 1. 定义 Feature 接口（给别人用的契约）==========

public interface IServerTimeProvider
{
    long GetServerTimeMs();
    float GetServerTimeSeconds();
}

// ========== 2. 定义 Feature 事件接口（可选，用于回调）==========
public interface IServerTimeEvents
{
    event Action<long> OnTimeAdvanced;
}

// ========== 3. 实现 Feature 模块 ==========
public class ServerTimeModule : IHostRuntimeModule, IServerTimeProvider, IServerTimeEvents
{
    private long _serverStartTimeMs;
    private readonly List<Action<long>> _handlers = new List<Action<long>>();

    public event Action<long> OnTimeAdvanced;

    public void Install(HostRuntime runtime, HostRuntimeOptions options)
    {
        _serverStartTimeMs = Environment.TickCount64;

        // 注册 Feature
        runtime.Features.RegisterFeature<IServerTimeProvider>(this);
        runtime.Features.RegisterFeature<IServerTimeEvents>(this);
    }

    public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
    {
        runtime.Features.UnregisterFeature<IServerTimeProvider>();
        runtime.Features.UnregisterFeature<IServerTimeEvents>();

        _handlers.Clear();
        OnTimeAdvanced = null;
    }

    public long GetServerTimeMs()
    {
        return Environment.TickCount64 - _serverStartTimeMs;
    }

    public float GetServerTimeSeconds()
    {
        return GetServerTimeMs() / 1000f;
    }
}
```

### 3.4 Feature 依赖声明

**推荐做法**：模块明确声明自己依赖哪些 Feature

```csharp
public class MyModule : IHostRuntimeModule
{
    private IServerTimeProvider _timeProvider;
    private IFrameSyncInputHub _inputHub;

    public void Install(HostRuntime runtime, HostRuntimeOptions options)
    {
        // 明确声明依赖
        if (!runtime.Features.TryGetFeature<IServerTimeProvider>(out _timeProvider))
        {
            throw new InvalidOperationException(
                "MyModule requires IServerTimeProvider. " +
                "Please install ServerTimeModule first.");
        }

        // 可选依赖：找不到也不报错
        runtime.Features.TryGetFeature<IFrameSyncInputHub>(out _inputHub);
    }
}
```

### 3.5 使用 Feature 的完整示例

```csharp
// 场景：开发一个"延迟监控"模块，依赖 FrameSync 和 ServerTime

public class LatencyMonitorModule : IHostRuntimeModule
{
    private IFrameSyncDriverEvents _frameEvents;
    private IServerTimeProvider _timeProvider;

    private readonly Action<FrameIndex, float> _onPostStep;

    public LatencyMonitorModule()
    {
        _onPostStep = OnPostStep;
    }

    public void Install(HostRuntime runtime, HostRuntimeOptions options)
    {
        // 1. 获取依赖的 Feature
        if (!runtime.Features.TryGetFeature<IFrameSyncDriverEvents>(out _frameEvents))
        {
            Console.WriteLine("Warning: FrameSync not installed, latency monitoring disabled");
            return;
        }

        if (!runtime.Features.TryGetFeature<IServerTimeProvider>(out _timeProvider))
        {
            Console.WriteLine("Warning: ServerTime not installed, using local time");
            return;
        }

        // 2. 注册自己的 Feature（可选）
        runtime.Features.RegisterFeature<ILatencyMonitor>(this);

        // 3. 使用依赖的 Feature
        _frameEvents.AddPostStep(_onPostStep);
    }

    public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
    {
        if (_frameEvents != null)
        {
            _frameEvents.RemovePostStep(_onPostStep);
        }

        runtime.Features.UnregisterFeature<ILatencyMonitor>();
    }

    private void OnPostStep(FrameIndex frame, float deltaTime)
    {
        var serverTime = _timeProvider?.GetServerTimeSeconds() ?? 0;
        Console.WriteLine($"Frame {frame.Value} at server time {serverTime:F3}s");
    }
}

// 供其他模块使用的接口
public interface ILatencyMonitor
{
    float GetAverageLatency();
    void Reset();
}
```

---

## 四、World Blueprint 扩展机制

### 4.1 什么是 Blueprint？

**通俗解释**：Blueprint 就像"装修方案"，定义了"创建 X 类型的世界需要安装什么模块"。

```
┌─────────────────────────────────────────────────────────────┐
│                    Blueprint 工作原理                        │
│                                                             │
│   定义蓝图（一次性配置）：                                   │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ blueprints.Register(new DelegateWorldBlueprint(      │  │
│   │     "battle",  // 世界类型                             │  │
│   │     options => {                                     │  │
│   │         // 战斗世界的配方：                           │  │
│   │         options.Modules.Add(new BattleModule());    │  │
│   │         options.Modules.Add(new FrameSyncModule());│  │
│   │         options.Modules.Add(new MobaRoomModule()); │  │
│   │     }                                                │  │
│   │ ));                                                  │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   使用蓝图（每次创建世界）：                                 │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ runtime.CreateWorld(new WorldCreateOptions {        │  │
│   │     WorldType = "battle",  // 指定蓝图               │  │
│   │     WorldId = new WorldId("match_001")              │  │
│   │ });                                                  │  │
│   │                                                        │  │
│   │ 结果：自动装配 BattleModule + FrameSyncModule + ...   │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   好处：                                                    │
│   - 创建世界时不需要记着一堆模块名                           │
│   - 蓝图可以复用、继承、组合                               │
│   - 不同项目可以用不同蓝图                                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 Blueprint 适用场景

```
┌─────────────────────────────────────────────────────────────┐
│                    何时使用 Blueprint                        │
│                                                             │
│   ✅ 适合使用 Blueprint：                                    │
│   - 创建特定类型的游戏世界（MOBA房间、RPG副本）              │
│   - 需要预定义一组模块组合                                   │
│   - 不同环境用不同配置（开发/测试/生产）                      │
│                                                             │
│   ❌ 不适合使用 Blueprint：                                  │
│   - 动态添加单个模块                                         │
│   - 模块间有复杂依赖关系                                     │
│   - 需要在运行时修改模块组合                                  │
│                                                             │
│   最佳实践：                                                │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ Blueprint 定义模块组合                               │  │
│   │ Module 在运行时通过 Feature 协作                     │  │
│   │                                                      │  │
│   │   Blueprint ──► "安装哪些模块"                        │  │
│   │   Feature ────► "模块间怎么通信"                     │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.3 Blueprint 开发示例

```csharp
// 场景：为一个 MOBA 游戏创建世界蓝图

// 1. 定义蓝图接口
public interface IMobaWorldBlueprint : IWorldBlueprint
{
    // 可以添加蓝图特有的配置方法
    void SetMapId(int mapId);
    void SetPlayerCount(int min, int max);
}

// 2. 实现蓝图
public class MobaWorldBlueprint : IMobaWorldBlueprint
{
    private int _mapId = 1001;
    private int _minPlayers = 2;
    private int _maxPlayers = 10;

    public string WorldType => "moba_battle";

    public void SetMapId(int mapId)
    {
        _mapId = mapId;
    }

    public void SetPlayerCount(int min, int max)
    {
        _minPlayers = min;
        _maxPlayers = max;
    }

    public void Configure(WorldCreateOptions options)
    {
        // 1. 设置服务容器
        options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();

        // 2. 添加世界模块（按顺序，影响执行优先级）
        options.Modules.Add(new MobaWorldBootstrapModule());  // 基础引导
        options.Modules.Add(new FrameSyncDriverModule());     // 帧同步
        options.Modules.Add(new ServerRollbackModule());       // 回滚
        options.Modules.Add(new MobaRoomModule());           // 房间管理

        // 3. 添加自定义配置
        options.SetExtension(new MobaWorldConfig
        {
            MapId = _mapId,
            MinPlayers = _minPlayers,
            MaxPlayers = _maxPlayers,
            TickRate = 30,
            InputDelayFrames = 3
        });
    }
}

// 3. 注册蓝图
var registry = new WorldBlueprintRegistry();
registry.Register(new MobaWorldBlueprint());

// 4. 使用蓝图创建世界
var worldId = runtime.CreateWorld(new WorldCreateOptions
{
    WorldType = "moba_battle",
    WorldId = new WorldId("match_001"),
    Extensions = new[] { new MobaWorldConfigExtension { MapId = 1001 } }
});
```

---

## 五、模块开发完整流程

### 5.1 开发模块的标准步骤

```
┌─────────────────────────────────────────────────────────────┐
│                 开发 Host 模块的标准流程                      │
│                                                             │
│   Step 1: 定义接口（契约）                                   │
│   ┌─────────────────────────────────────────────────────┐   │
│   │ public interface IMyFeature                        │   │
│   │ {                                                  │   │
│   │     void DoSomething();                           │   │
│   │ }                                                  │   │
│   └─────────────────────────────────────────────────────┘   │
│                                                             │
│   Step 2: 实现模块                                          │
│   ┌─────────────────────────────────────────────────────┐   │
│   │ public class MyModule : IHostRuntimeModule,        │   │
│   │                           IMyFeature                │   │
│   │ {                                                  │   │
│   │     public void Install(...) { ... }               │   │
│   │     public void Uninstall(...) { ... }             │   │
│   │     public void DoSomething() { ... }              │   │
│   │ }                                                  │   │
│   └─────────────────────────────────────────────────────┘   │
│                                                             │
│   Step 3: 注册到 Host                                       │
│   ┌─────────────────────────────────────────────────────┐   │
│   │ var module = new MyModule();                       │   │
│   │ module.Install(runtime, options);                  │   │
│   └─────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 模块生命周期详解

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         模块完整生命周期                                 │
│                                                                         │
│   1️⃣ 创建模块实例                                                       │
│      ┌───────────────────────────────────────────────────────────┐       │
│      │ new MyModule()                                          │       │
│      │   - 构造函数：初始化配置                                 │       │
│      │   - 不要在构造函数里访问 runtime/options               │       │
│      └───────────────────────────────────────────────────────────┘       │
│                               │                                         │
│                               ▼                                         │
│   2️⃣ 安装模块                                                           │
│      ┌───────────────────────────────────────────────────────────┐       │
│      │ module.Install(runtime, options)                          │       │
│      │                                                            │       │
│      │ // 这里可以做：                                            │       │
│      │ // - 获取依赖的 Feature                                    │       │
│      │ // - 注册自己的 Feature                                    │       │
│      │ // - 注册 Hook                                             │       │
│      │ // - 初始化状态                                            │       │
│      │                                                            │       │
│      │ // ⚠️ 注意：                                               │       │
│      │ // - Install 可能被多次调用（模块复用）                     │       │
│      │ // - 要做好幂等处理                                         │       │
│      └───────────────────────────────────────────────────────────┘       │
│                               │                                         │
│                               ▼                                         │
│   3️⃣ 模块运行中                                                         │
│      ┌───────────────────────────────────────────────────────────┐       │
│      │ HostRuntime.Tick() 循环                                  │       │
│      │                                                            │       │
│      │   PreTick ──► [你的Hook] ──► 世界Tick ──► PostTick      │       │
│      │                               [你的Hook]                  │       │
│      │                                                            │       │
│      └───────────────────────────────────────────────────────────┘       │
│                               │                                         │
│                               ▼                                         │
│   4️⃣ 卸载模块                                                           │
│      ┌───────────────────────────────────────────────────────────┐       │
│      │ module.Uninstall(runtime, options)                        │       │
│      │                                                            │       │
│      │ // 必须做：                                                 │       │
│      │ // - 取消注册的 Hook                                       │       │
│      │ // - 注销自己的 Feature                                    │       │
│      │ // - 清理状态                                               │       │
│      │ // - 释放资源                                               │       │
│      └───────────────────────────────────────────────────────────┘       │
│                               │                                         │
│                               ▼                                         │
│   5️⃣ 模块销毁                                                           │
│      ┌───────────────────────────────────────────────────────────┐       │
│      │ // 模块实例被 GC 回收                                     │       │
│      │ // ⚠️ 如果有 IDisposable，要在这里释放                    │       │
│      └───────────────────────────────────────────────────────────┘       │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 5.3 最佳实践清单

```
┌─────────────────────────────────────────────────────────────┐
│                    模块开发最佳实践                            │
│                                                             │
│   ✅ 必须做的事情：                                          │
│                                                             │
│   1. 实现 IHostRuntimeModule 接口                           │
│      - Install() 和 Uninstall() 必须成对                      │
│      - Uninstall() 必须清理 Install() 中注册的所有内容       │
│                                                             │
│   2. 正确处理依赖                                            │
│      - 明确声明依赖的 Feature                                 │
│      - 找不到依赖时给出明确提示                               │
│      - 可选依赖要做好空检查                                   │
│                                                             │
│   3. Hook 优先级规划                                        │
│      - 系统模块：order < 0                                    │
│      - 业务模块：order = 0                                   │
│      - 可选模块：order > 0                                    │
│                                                             │
│   4. 内存管理                                                │
│      - 缓存委托引用（避免 GC）                               │
│      - 及时清理集合                                           │
│      - 实现 IDisposable（如果有原生资源）                      │
│                                                             │
│   ❌ 不要做的事情：                                          │
│                                                             │
│   1. 不要在构造函数访问 runtime/options                      │
│   2. 不要在 Install 之外注册 Hook                            │
│   3. 不要忘记在 Uninstall 中注销                             │
│   4. 不要假设 Feature 一定存在                               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 六、实际扩展案例

### 6.1 案例一：开发一个"游戏统计"模块

**需求**：收集每个世界的游戏数据（玩家数、帧率、在线时长）

```
┌─────────────────────────────────────────────────────────────┐
│                    游戏统计模块设计                           │
│                                                             │
│   提供的能力：                                              │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ IGameStatisticsProvider                             │  │
│   │   - GetWorldStats(worldId) → WorldStats             │  │
│   │   - GetGlobalStats() → GlobalStats                  │  │
│   │   - Reset()                                         │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   收集的数据：                                              │
│   - 世界创建时间                                            │
│   - 累计 Tick 次数                                         │
│   - 每帧耗时（计算 FPS）                                    │
│   - 玩家进出事件                                            │
│                                                             │
│   依赖：                                                   │
│   - 无（独立模块）                                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

```csharp
public class GameStatisticsModule : IHostRuntimeModule, IGameStatisticsProvider
{
    private readonly Dictionary<WorldId, WorldStats> _worldStats = new Dictionary<WorldId, WorldStats>();
    private readonly GlobalStats _globalStats = new GlobalStats();

    private readonly Action<IWorld> _onWorldCreated;
    private readonly Action<WorldId> _onWorldDestroyed;
    private readonly Action<float> _onPostTick;

    public GameStatisticsModule()
    {
        _onWorldCreated = OnWorldCreated;
        _onWorldDestroyed = OnWorldDestroyed;
        _onPostTick = OnPostTick;
    }

    public void Install(HostRuntime runtime, HostRuntimeOptions options)
    {
        // 注册 Hook
        options.WorldCreated.Add(_onWorldCreated);
        options.WorldDestroyed.Add(_onWorldDestroyed);
        options.PostTick.Add(_onPostTick);

        // 注册 Feature
        runtime.Features.RegisterFeature<IGameStatisticsProvider>(this);
    }

    public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
    {
        // 清理 Hook
        options.WorldCreated.Remove(_onWorldCreated);
        options.WorldDestroyed.Remove(_onWorldDestroyed);
        options.PostTick.Remove(_onPostTick);

        // 注销 Feature
        runtime.Features.UnregisterFeature<IGameStatisticsProvider>();
    }

    private void OnWorldCreated(IWorld world)
    {
        _worldStats[world.Id] = new WorldStats
        {
            WorldId = world.Id,
            CreatedAt = DateTime.UtcNow,
            TotalTicks = 0,
            FpsSamples = new List<float>()
        };

        _globalStats.TotalWorldsCreated++;
    }

    private void OnWorldDestroyed(WorldId worldId)
    {
        if (_worldStats.TryGetValue(worldId, out var stats))
        {
            stats.Duration = DateTime.UtcNow - stats.CreatedAt;
            _globalStats.TotalWorldsDestroyed++;
        }
        _worldStats.Remove(worldId);
    }

    private void OnPostTick(float deltaTime)
    {
        var fps = 1f / deltaTime;

        foreach (var world in _worldStats.Values)
        {
            world.TotalTicks++;
            world.FpsSamples.Add(fps);

            // 保持最近 100 帧样本
            if (world.FpsSamples.Count > 100)
                world.FpsSamples.RemoveAt(0);
        }

        _globalStats.TotalTicks++;
    }

    public WorldStats GetWorldStats(WorldId worldId)
    {
        if (_worldStats.TryGetValue(worldId, out var stats))
        {
            stats.AverageFps = stats.FpsSamples.Count > 0
                ? stats.FpsSamples.Average()
                : 0;
            return stats;
        }
        return null;
    }

    public GlobalStats GetGlobalStats()
    {
        _globalStats.CurrentWorldCount = _worldStats.Count;
        return _globalStats;
    }

    public void Reset()
    {
        _worldStats.Clear();
        _globalStats.Reset();
    }
}
```

### 6.2 案例二：开发一个"调试工具"模块

**需求**：在开发环境提供命令控制台和状态查询

```
┌─────────────────────────────────────────────────────────────┐
│                    调试工具模块设计                          │
│                                                             │
│   提供的能力：                                              │
│   ┌─────────────────────────────────────────────────────┐  │
│   │ IDebugConsole                                      │  │
│   │   - ExecuteCommand(command) → string               │  │
│   │   - RegisterCommand(name, handler)                 │  │
│   └─────────────────────────────────────────────────────┘  │
│                                                             │
│   内置命令：                                                │
│   - /help              - 显示帮助                          │
│   - /worlds            - 列出所有世界                      │
│   - /world <id>        - 查看世界详情                       │
│   - /stats             - 显示统计                          │
│   - /fps               - 显示帧率                          │
│                                                             │
│   依赖：                                                   │
│   - IGameStatisticsProvider（可选）                        │
│   - IFrameSyncDriverEvents（可选）                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

```csharp
public class DebugConsoleModule : IHostRuntimeModule, IDebugConsole
{
    private readonly Dictionary<string, CommandHandler> _commands = new Dictionary<string, CommandHandler>();

    private IGameStatisticsProvider _statsProvider;
    private IFrameSyncDriverEvents _frameEvents;
    private HostRuntime _runtime;

    public DebugConsoleModule()
    {
        // 注册内置命令
        RegisterCommand("help", HandleHelp);
        RegisterCommand("worlds", HandleWorlds);
        RegisterCommand("world", HandleWorld);
        RegisterCommand("stats", HandleStats);
    }

    public void Install(HostRuntime runtime, HostRuntimeOptions options)
    {
        _runtime = runtime;

        // 尝试获取可选依赖
        runtime.Features.TryGetFeature(out _statsProvider);
        runtime.Features.TryGetFeature(out _frameEvents);

        // 注册 Feature
        runtime.Features.RegisterFeature<IDebugConsole>(this);

        // 可选：注册消息处理（如果有网络命令通道）
        options.BeforeSendMessage.Add(OnBeforeSendMessage);
    }

    public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
    {
        options.BeforeSendMessage.Remove(OnBeforeSendMessage);
        runtime.Features.UnregisterFeature<IDebugConsole>();

        _commands.Clear();
        _runtime = null;
    }

    public void RegisterCommand(string name, CommandHandler handler)
    {
        _commands[name.ToLower()] = handler;
    }

    public string ExecuteCommand(string command)
    {
        var parts = command.Trim().Split(' ', 2);
        var name = parts[0].ToLower();
        var args = parts.Length > 1 ? parts[1] : "";

        if (_commands.TryGetValue(name, out var handler))
        {
            try
            {
                return handler(args);
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        return $"Unknown command: {name}. Type /help for available commands.";
    }

    // 命令实现
    private string HandleHelp(string args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Available commands:");
        foreach (var cmd in _commands.Keys)
        {
            sb.AppendLine($"  /{cmd}");
        }
        return sb.ToString();
    }

    private string HandleWorlds(string args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Active Worlds:");

        foreach (var world in _runtime.Worlds.GetAll())
        {
            sb.AppendLine($"  {world.Id}");
        }

        return sb.ToString();
    }

    private string HandleWorld(string args)
    {
        if (string.IsNullOrEmpty(args))
            return "Usage: /world <world_id>";

        var worldId = new WorldId(args);
        if (_runtime.Worlds.TryGet(worldId, out var world))
        {
            var sb = new StringBuilder();
            sb.AppendLine($"World: {world.Id}");
            sb.AppendLine($"Type: {world.GetType().Name}");

            if (_statsProvider != null)
            {
                var stats = _statsProvider.GetWorldStats(worldId);
                if (stats != null)
                {
                    sb.AppendLine($"Ticks: {stats.TotalTicks}");
                    sb.AppendLine($"Avg FPS: {stats.AverageFps:F1}");
                    sb.AppendLine($"Duration: {stats.Duration}");
                }
            }

            return sb.ToString();
        }

        return $"World not found: {args}";
    }

    private string HandleStats(string args)
    {
        if (_statsProvider == null)
            return "Statistics module not available";

        var stats = _statsProvider.GetGlobalStats();
        var sb = new StringBuilder();
        sb.AppendLine("Global Statistics:");
        sb.AppendLine($"Current Worlds: {stats.CurrentWorldCount}");
        sb.AppendLine($"Total Created: {stats.TotalWorldsCreated}");
        sb.AppendLine($"Total Destroyed: {stats.TotalWorldsDestroyed}");
        sb.AppendLine($"Total Ticks: {stats.TotalTicks}");

        return sb.ToString();
    }

    private void OnBeforeSendMessage(ServerClientId clientId, ServerMessage message)
    {
        // 调试：记录发送的消息
        // Console.WriteLine($"[Debug] Sending {message.Kind} to {clientId}");
    }

    private delegate string CommandHandler(string args);
}
```

---

## 七、模块依赖关系图

### 7.1 Host Extension 现有模块依赖

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      Host Extension 模块依赖图                           │
│                                                                         │
│                          ┌─────────────────┐                            │
│                          │  HostRuntime    │                            │
│                          │    (核心)       │                            │
│                          └────────┬────────┘                            │
│                                   │                                      │
│           ┌──────────────────────┼──────────────────────┐               │
│           │                      │                      │               │
│           ▼                      ▼                      ▼               │
│   ┌───────────────┐    ┌───────────────┐    ┌───────────────┐        │
│   │FrameSyncDriver│    │ServerFrameTime│    │  (其他基础模块) │        │
│   │   Module      │    │    Module     │    │               │        │
│   └───────┬───────┘    └───────┬───────┘    └───────────────┘        │
│           │                    │                                      │
│           │                    │                                      │
│           ▼                    │                                      │
│   ┌───────────────┐            │                                      │
│   │ServerRollback │            │                                      │
│   │    Module     │◄────────────┘                                      │
│   │               │  依赖 IFrameSyncDriverEvents                       │
│   └───────┬───────┘                                                   │
│           │                                                            │
│           ▼                                                            │
│   ┌───────────────┐                                                   │
│   │ClientPrediction│                                                   │
│   │    Module     │                                                   │
│   └───────┬───────┘                                                   │
│           │                                                            │
│           │ 依赖                                                       │
│           ▼                                                            │
│   ┌───────────────┐    ┌───────────────┐                              │
│   │  MobaRoom     │    │  MobaRoomSync │                              │
│   │  Orchestrator │◄───│   Server      │                              │
│   └───────┬───────┘    └───────────────┘                              │
│           │                                                            │
│           ▼                                                            │
│   ┌───────────────┐                                                   │
│   │  MobaGameStart │                                                   │
│   │ Orchestrator  │                                                   │
│   └───────────────┘                                                   │
│                                                                         │
│   依赖规则：                                                           │
│   - 实线箭头表示"依赖"                                                 │
│   - 依赖只能指向下层（单向依赖，无环）                                   │
│   - 同层模块间不应有依赖                                                │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 7.2 Feature 依赖关系

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Feature 接口依赖图                                │
│                                                                         │
│   由 HostRuntime 提供：                                                 │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │ IHostRuntimeFeatures                                            │  │
│   └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│   由 FrameSyncDriverModule 提供：                                       │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │ IFrameSyncInputHub          - 输入提交                          │  │
│   │ IFrameSyncDriverEvents      - 帧事件回调                        │  │
│   │ IWorldStateSnapshotProvider - 世界状态快照                      │  │
│   └─────────────────────────────────────────────────────────────────┘  │
│                               │                                         │
│                               ▼                                         │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │ 被以下模块使用：                                                 │  │
│   │ - ServerRollbackModule（依赖 InputHub, Events）                 │  │
│   │ - ClientPredictionModule（依赖 InputHub, Events）               │  │
│   │ - ServerFrameTimeModule（依赖 Events）                          │  │
│   └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│   由 ClientPredictionModule 提供：                                      │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │ IClientPredictionDriverStats       - 预测统计                   │  │
│   │ IClientPredictionTuningControl     - 预测调优                   │  │
│   │ IClientPredictionReconcileTarget   - 对齐目标                   │  │
│   │ IClientPredictionReconcileControl  - 对齐控制                   │  │
│   └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│   由 GameStatisticsModule 提供（示例）：                                 │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │ IGameStatisticsProvider        - 游戏统计                      │  │
│   │ IDebugConsole                   - 调试控制台                    │  │
│   └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 八、故障排查指南

### 8.1 常见问题与解决方案

```
┌─────────────────────────────────────────────────────────────┐
│                    常见问题与解决方案                         │
│                                                             │
│   问题 1: Hook 没有被调用                                   │
│   ────────────────────────────────────────────────────────  │
│   可能原因：                                                 │
│   - Install() 没有被调用                                    │
│   - Uninstall() 提前被调用                                  │
│   - 添加的是 lambda，每次都是新实例                          │
│                                                             │
│   解决方案：                                                │
│   ✅ 缓存委托引用                                            │
│   ❌ _options.PreTick.Add((dt) => { ... });                │
│   ✅ _options.PreTick.Add(_onPreTick);                      │
│   ✅ _options.PreTick.Add(MyHandler, order: 0);             │
│                                                             │
│   ────────────────────────────────────────────────────────  │
│                                                             │
│   问题 2: Feature 获取为 null                                │
│   ────────────────────────────────────────────────────────  │
│   可能原因：                                                 │
│   - 依赖的模块没有安装                                       │
│   - 安装顺序错误（依赖的模块还没安装）                        │
│                                                             │
│   解决方案：                                                │
│   ✅ 明确声明依赖并给出提示                                   │
│   if (!runtime.Features.TryGetFeature<IFoo>(out _foo))     │
│   {                                                         │
│       throw new InvalidOperationException(                  │
│           "MyModule requires FooModule to be installed.");  │
│   }                                                         │
│                                                             │
│   ────────────────────────────────────────────────────────  │
│                                                             │
│   问题 3: 模块卸载后行为异常                                 │
│   ────────────────────────────────────────────────────────  │
│   可能原因：                                                 │
│   - Uninstall() 没有清理干净                                │
│   - 其他模块还持有模块的引用                                  │
│                                                             │
│   解决方案：                                                │
│   ✅ Uninstall 必须清理所有注册                              │
│   ✅ 使用弱引用持有可选依赖                                   │
│   ✅ 提供 IsInstalled 属性检查                               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 8.2 调试技巧

```csharp
// 技巧 1: 添加模块加载日志
public void Install(HostRuntime runtime, HostRuntimeOptions options)
{
    Console.WriteLine($"[MyModule] Installing...");
    Console.WriteLine($"[MyModule] Runtime: {runtime.GetHashCode()}");
    Console.WriteLine($"[MyModule] Feature count: {CountFeatures(runtime)}");

    // ...
}

// 技巧 2: 验证 Hook 注册
public void Install(...)
{
    var before = _options.PreTick.GetHandlerCount();

    _options.PreTick.Add(_onPreTick);

    var after = _options.PreTick.GetHandlerCount();

    if (after != before + 1)
    {
        Console.WriteLine($"[Warning] Hook registration may have failed");
    }
}

// 技巧 3: Feature 查找调试
public static void DumpFeatures(HostRuntime runtime)
{
    var features = runtime.Features.GetType()
        .GetField("_map", BindingFlags.NonPublic | BindingFlags.Instance)
        .GetValue(runtime.Features) as Dictionary<Type, object>;

    foreach (var kv in features)
    {
        Console.WriteLine($"  {kv.Key.Name} -> {kv.Value?.GetType().Name}");
    }
}
```

---

## 九、总结

### 9.1 扩展机制对比

```
┌─────────────────────────────────────────────────────────────┐
│                    三种扩展机制对比                           │
│                                                             │
│   ┌───────────┬─────────────────┬─────────────────────────┐ │
│   │  机制     │    适用场景      │         示例           │ │
│   ├───────────┼─────────────────┼─────────────────────────┤ │
│   │  Hook     │  在某个时刻做某事 │  帧开始前收集输入      │ │
│   │           │                 │  帧结束后广播数据        │ │
│   │           │                 │  世界创建时初始化       │ │
│   ├───────────┼─────────────────┼─────────────────────────┤ │
│   │  Feature  │  提供/使用服务   │  提供帧同步能力         │ │
│   │           │                 │  使用时间服务           │ │
│   │           │                 │  提供统计接口           │ │
│   ├───────────┼─────────────────┼─────────────────────────┤ │
│   │  Blueprint │  定义世界类型   │  MOBA战斗世界蓝图       │ │
│   │           │                 │  RPG副本世界蓝图        │ │
│   │           │                 │  测试世界蓝图           │ │
│   └───────────┴─────────────────┴─────────────────────────┘ │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 9.2 设计原则

```
┌─────────────────────────────────────────────────────────────┐
│                    模块设计原则                              │
│                                                             │
│   1. 单一职责                                              │
│      ─────────────────────────────────────                  │
│      每个模块只做一件事                                      │
│      帧同步就做帧同步，回滚就做回滚                          │
│                                                             │
│   2. 依赖倒置                                              │
│      ─────────────────────────────────────                  │
│      模块依赖接口，不依赖具体实现                            │
│      使用 Feature 接口，而不是具体类                          │
│                                                             │
│   3. 生命周期对称                                          │
│      ─────────────────────────────────────                  │
│      Install() 和 Uninstall() 必须成对                      │
│      注册了什么就要注销什么                                  │
│                                                             │
│   4. 明确依赖                                              │
│      ─────────────────────────────────────                  │
│      必须的依赖要明确声明并检查                              │
│      可选的依赖要做好空检查                                  │
│                                                             │
│   5. 幂等设计                                              │
│      ─────────────────────────────────────                  │
│      Install() 可能被多次调用                               │
│      做好幂等处理                                            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 9.3 下一步

```
┌─────────────────────────────────────────────────────────────┐
│                    继续学习                                  │
│                                                             │
│   📖 Host模块开发设计文档.md                                 │
│      - 核心概念和架构                                        │
│      - 数据流程图                                            │
│      - 设计模式总结                                          │
│                                                             │
│   📖 FrameSync 模块源码                                      │
│      - Runtime/FrameSync/FrameSyncDriverModule.cs           │
│      - 完整展示了模块开发模式                                │
│                                                             │
│   📖 ClientPrediction 模块源码                               │
│      - Runtime/FrameSync/ClientPredictionDriverModule.cs   │
│      - 复杂的 Feature 协作示例                               │
│                                                             │
│   📖 MobaRoom 模块源码                                       │
│      - Runtime/Moba/Server/Room/MobaRoomOrchestrator.cs    │
│      - 业务逻辑模块示例                                      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

*文档版本：1.0*
*最后更新：2026-03-19*
