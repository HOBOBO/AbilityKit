# 01 - 基础注册与生命周期（WorldLifetime）

本示例演示：

- 如何在模块里注册服务
- `Singleton / Scoped / Transient` 的常见选择
- 为什么推荐构造函数注入

## 典型模块结构（只负责“注册/接线”）

```csharp
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

public sealed class TimeModule : IWorldModule
{
    public void Configure(IWorldContainerBuilder builder)
    {
        // 中文说明：
        // 1) Singleton：对每个 WorldContainer 只会创建一次。
        // 2) 适用于“无对局态”的基础设施能力，例如：时间源、随机数、日志适配等。
        builder.Register<IWorldClock, WorldClock>(WorldLifetime.Singleton);

        // 中文说明：
        // Scoped：每个 WorldScope（通常每个逻辑 world 一个 scope）一份实例。
        // 适用于：一局内缓存、对局态 manager、事件聚合器（如果 event 绑定在对局内）。
        builder.Register<IFrameTime, FrameTimeImpl>(WorldLifetime.Scoped);

        // 中文说明：
        // Transient：每次 Resolve 都是新对象。
        // 适用于：轻量、无状态、短生命周期的 helper。
        builder.Register<IPathFinder, AStarPathFinder>(WorldLifetime.Transient);
    }
}
```

## 为什么推荐构造函数注入

```csharp
using AbilityKit.Ability.World.Services;

public sealed class CombatService : IService
{
    private readonly IWorldClock _clock;
    private readonly IFrameTime _frameTime;

    // 中文说明：
    // 依赖显式（更易读/更易测试/更容易定位依赖图）
    // 而不是在方法里到处 Resolve。
    public CombatService(IWorldClock clock, IFrameTime frameTime)
    {
        _clock = clock;
        _frameTime = frameTime;
    }
}
```
