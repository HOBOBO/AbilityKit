<a id="01---基础注册与生命周期worldlifetime"></a>

# 基础注册与生命周期

> 文档类型：使用示例
> 事实基线：2026-11-24 源码核对；2026-11-25 完整类按当前 .NET 镜像编译执行。

<a id="典型模块结构只负责注册接线"></a>

下面的完整类比较同一根容器的 singleton、不同作用域的 scoped 和每次创建的 transient，预期返回 `True:True:True`。

```csharp
using AbilityKit.Ability.World.DI;

public static class BasicRegistrationExample
{
    public static string Run()
    {
        var builder = new WorldContainerBuilder();
        builder.Register<SharedService>(WorldLifetime.Singleton, _ => new SharedService());
        builder.RegisterType<SessionService, SessionService>(WorldLifetime.Scoped);
        builder.Register<WorkItem>(WorldLifetime.Transient, _ => new WorkItem());
        using var root = builder.Build();
        using var first = root.CreateScope();
        using var second = root.CreateScope();

        bool shared = ReferenceEquals(first.Resolve<SharedService>(), second.Resolve<SharedService>());
        bool isolated = !ReferenceEquals(first.Resolve<SessionService>(), second.Resolve<SessionService>());
        bool fresh = !ReferenceEquals(first.Resolve<WorkItem>(), first.Resolve<WorkItem>());
        return $"{shared}:{isolated}:{fresh}";
    }

    public sealed class SharedService { }
    public sealed class SessionService
    {
        public SessionService(SharedService shared) { Shared = shared; }
        public SharedService Shared { get; }
    }
    public sealed class WorkItem { }
}
```

`using` 按作用域逆序退出，先释放两个作用域，再释放根容器。示例服务不持有资源；实际 transient 若需清理，由取得实例的调用方负责，容器不会替它执行退出清理。

<a id="为什么推荐构造函数注入"></a>

`RegisterType` 根据可解析的公开构造函数创建 `SessionService`，注入已注册的 `SharedService`；业务服务通过构造参数声明依赖，容器查询留在组合边界。

跨世界共享根容器时，singleton 也跨世界共享。不要默认将有状态随机流、帧计时器或对局态注册成 singleton；生命周期由真实所有权决定，详细规则见[使用契约](../Documentation~/Usage.md)。
