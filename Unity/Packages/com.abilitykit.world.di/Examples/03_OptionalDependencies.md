# 03 - 可选依赖（TryResolve）与边界用法

本示例强调：

- `TryResolve` 适用于“确实可选且有合理 fallback”的依赖
- `IWorldResolver` 推荐只在边界使用

## 可选依赖：推荐写法

```csharp
using AbilityKit.Ability.World.DI;

public sealed class SomeService
{
    private readonly IDebugRegistry _debug;

    // 中文说明：
    // 如果 DebugRegistry 是可选模块提供的服务：
    // - 有则增强能力
    // - 无则功能正常
    public SomeService(IWorldResolver services)
    {
        // 注意：这属于边界层/过渡层的写法。
        // 理想情况下，更推荐把 IDebugRegistry 作为构造参数，
        // 并在 composition 时决定是否注入一个 Null 实现。
        services.TryResolve(out _debug);
    }
}
```

## 是否需要考虑“注册顺序”？

一般不需要。

在 `world.di` 中，模块 `Configure(...)` 阶段只是把 `serviceType -> factory` 写入注册表，并不会立刻创建实例。
真正会创建实例的是 `Resolve(...)`（通常发生在某个服务/系统第一次被解析、或者某个服务构造函数里触发解析）。

因此你真正要关注的不是“模块先后顺序”，而是：

- **解析时机（Resolve time）**
  - 如果你在 compose 过程中就提前 `Resolve` 某个服务，而它依赖的服务尚未注册完成，就会失败。
- **循环依赖（A -> B -> A）**
  - 服务 A 的构造函数里解析 B，同时 B 的构造函数里又解析 A，会在创建时直接抛异常。
- **生命周期穿透（Singleton -> Scoped）**
  - 如果 singleton 服务在构造时解析了 scoped 服务，可能导致 scoped 实例被“提升”并泄漏到 singleton 生命周期中。
  - 建议：singleton 不直接依赖 scoped；如有需要，改为依赖工厂/提供器（延迟到 scope 内解析）。

## 更推荐的方式：Null 对象 + 构造注入

```csharp
public sealed class NullDebugRegistry : IDebugRegistry
{
    // 中文说明：
    // 用空实现保证依赖始终存在，业务层不需要 if/try。
}

// 模块里：
// - 不安装 DebugModule 时，注册 NullDebugRegistry
// - 安装 DebugModule 时，注册真实 DebugRegistry
```
