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

解析失败不一定是可选模块未安装。注册覆盖、异常和作用域边界见[使用契约](../Documentation~/Usage.md#注册与解析)。

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
