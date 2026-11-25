<a id="03---可选依赖tryresolve与边界用法"></a>

# 可选依赖与空实现

> 文档类型：使用示例
> 事实基线：2026-11-24 源码核对；2026-11-25 完整类按当前 .NET 镜像编译执行。

<a id="更推荐的方式null-对象--构造注入"></a>

有明确默认行为时，可在组合边界注册空实现，让业务服务仍通过构造函数声明依赖。调用 `OptionalDependencyExample.Run()` 预期返回 `disabled`。

```csharp
using AbilityKit.Ability.World.DI;

public static class OptionalDependencyExample
{
    public static string Run()
    {
        var builder = new WorldContainerBuilder();
        builder.TryRegister<IDebugStatus>(WorldLifetime.Singleton, _ => new NullDebugStatus());
        builder.RegisterType<Feature, Feature>(WorldLifetime.Scoped);
        using var root = builder.Build();
        using var scope = root.CreateScope();
        return scope.Resolve<Feature>().Status;
    }

    public interface IDebugStatus { string Status { get; } }

    public sealed class NullDebugStatus : IDebugStatus
    {
        public string Status => "disabled";
    }

    public sealed class Feature
    {
        private readonly IDebugStatus _debug;
        public Feature(IDebugStatus debug) { _debug = debug; }
        public string Status => _debug.Status;
    }
}
```

需要启用功能时，由项目模块用 Register 显式覆盖 IDebugStatus；不要在包内增加某个项目专用的调试依赖。

<a id="可选依赖推荐写法"></a>

## 何时使用 TryResolve

只有业务有降级路径时才使用 TryResolve。返回 false 不等于未注册，也不等于没有副作用：工厂或初始化可能已经部分执行。根容器与作用域的异常折叠范围不同，循环依赖会传播；作用域还会传播部分 InvalidOperationException。

排查失败时在同一解析边界用 Resolve 保留异常，不通过吞掉工厂错误伪装成“可选模块缺失”。详细行为见[注册与解析](../Documentation~/Usage.md#注册与解析)。
