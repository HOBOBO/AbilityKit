# SubFeature 与 SessionHooks

源文件：`Runtime/SubFeatures/ISessionSubFeature.cs` + `ISessionHost.cs` + `SessionEventsSubFeature.cs` + `SessionTickLoopSubFeature.cs` + `SessionSnapshotRoutingSubFeature.cs` + `Runtime/Core/SessionHooks.cs`

## ISessionSubFeature 接口族

```csharp
public interface ISessionSubFeature {
    int Priority { get; }   // 大者先调用
    void OnAttach(ISessionHost session);
    void OnDetach(ISessionHost session);
}

// 细粒度接口（按需实现）
public interface ISessionLifecycleSubFeature : ISessionSubFeature {
    void OnSessionStarting(ISessionHost session);
    void OnSessionStarted(ISessionHost session);
    void OnSessionStopping(ISessionHost session);
    void OnSessionStopped(ISessionHost session);
    void OnSessionFailed(ISessionHost session);
}

public interface ISessionEventsSubFeature : ISessionSubFeature {
    void OnPreTick(ISessionHost session);
    void OnPostTick(ISessionHost session);
    void OnFirstFrameReceived(ISessionHost session);
}

public interface ISessionTickLoopSubFeature : ISessionSubFeature { ... }
public interface ISessionSnapshotRoutingSubFeature : ISessionSubFeature { ... }
```

## ISessionHost（SubFeature 访问会话的入口）

```csharp
public interface ISessionHost {
    SessionId SessionId { get; }
    SessionConfig Config { get; }
    SessionState State { get; }
    IWorld World { get; }
    IWorldResolver WorldResolver { get; }
    ISyncAdapter SyncAdapter { get; }
    ILogicWorldDriverBridge? LogicWorldDriver { get; }
    T Resolve<T>() where T : class;
    bool TryResolve<T>(out T service) where T : class;
}
```

## 内置 3 个 SubFeature（按 Priority）

| 类 | Priority | 职责 |
|----|---------|------|
| `SessionEventsSubFeature` | **1000** | 触发生命周期事件钩子（OnSessionStarting/Started/...） |
| `SessionTickLoopSubFeature` | **500** | 帧循环钩子（OnPreTick/OnPostTick/OnFirstFrameReceived） |
| `SessionSnapshotRoutingSubFeature` | **300** | 快照路由钩子（OnEnterGameSnapshot/OnActorTransformSnapshot/...） |

## SessionCoordinator 与 SubFeature 的交互

- `AddSubFeature(ISessionSubFeature)` / `RemoveSubFeature(ISessionSubFeature)`
- **注意**：SubFeature 的 `OnTick` **不被** `SessionCoordinator.Tick` 自动驱动（`Tick` 循环里只调 `OnPreTick` / `OnPostTick`）
- 调用顺序按 `Priority` 降序（平手按注册顺序）

## SessionHooks（含 README 漏掉的视图钩子）

```csharp
public sealed class SessionHooks {
    // 生命周期
    public Action? OnSessionStarting;
    public Action? OnSessionStarted;
    public Action? OnSessionStopping;
    public Action? OnSessionStopped;
    public Action? OnSessionFailed;

    // 帧循环
    public Action<float>? OnPreTick;       // float deltaTime
    public Action<float>? OnPostTick;
    public Action? OnFirstFrameReceived;

    // 视图（README 漏掉）
    public Action? OnViewBinderReady;
    public Action? OnViewsRebound;
    public Action? OnViewFrameAligned;

    // 内部触发器（SubFeature 调用）
    public void InvokeSessionStarting();
    public void InvokeSessionStarted();
    // ...
    public void Clear();
}
```

`Clear()` 在 `Destroy` 时被调用，避免回调悬空。

## 自定义 SubFeature 步骤

1. 实现 `ISessionSubFeature`（或更具体的子接口）
2. 决定 `Priority`（数值大者先调用）
3. 在 `OnAttach` 中订阅 `session.Hooks.*` 或注册服务
4. 在 `OnDetach` 中清理（取消订阅、释放资源）
5. 通过 `coordinator.AddSubFeature(yourSubFeature)` 添加

或者通过 `SessionConfig.SubFeatures`（`SubFeatureConfigItem[]`）声明，让 coordinator 自动构造（需配合应用层的工厂机制）。
