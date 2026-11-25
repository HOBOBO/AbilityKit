<a id="02---模块拆分与组合composition"></a>

# 模块拆分与组合

> 文档类型：使用示例
> 事实基线：2026-11-24 源码核对；2026-11-25 完整类按当前 .NET 镜像编译执行。

<a id="推荐组合方式示意"></a>

模块只在组合边界注册服务。下例默认模块使用 TryRegister，项目模块显式覆盖，调用 `ModuleCompositionExample.Run()` 预期返回 `project`。

```csharp
using AbilityKit.Ability.World.DI;

public static class ModuleCompositionExample
{
    public static string Run()
    {
        var builder = new WorldContainerBuilder();
        builder.AddModule(new DefaultModule());
        builder.AddModule(new ProjectModule());
        using var root = builder.Build();
        return root.Resolve<Message>().Value;
    }

    public sealed class Message
    {
        public Message(string value) { Value = value; }
        public string Value { get; }
    }

    public sealed class DefaultModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            builder.TryRegister<Message>(WorldLifetime.Singleton, _ => new Message("default"));
        }
    }

    public sealed class ProjectModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            builder.Register<Message>(WorldLifetime.Singleton, _ => new Message("project"));
        }
    }
}
```

<a id="多模块重复注册同一服务容器的行为与推荐用法"></a>

## 覆盖与所有权

- `Register` 替换同一服务契约，`TryRegister` 保留已有描述符；模块顺序需要由组合入口明确指定。
- 不允许重复注册时，应选择唯一模块或显式 Reject 策略，不能误以为 Register 默认拒绝。
- 多个接口需要共享同一实现时，先注册实现类型，再通过 `RegisterAlias` 指向实现。实现与别名使用同一 Singleton 或 Scoped 缓存生命周期，并核对所有者；不要用多个工厂重复创建本应共享的实例。
- `AddModule` 在调用时执行 Configure，不构成事务；若模块在部分注册后抛错，调用方应丢弃该 builder，不能假定已有注册回滚。

<a id="模块拆分的经验规则"></a>

复杂模块的排序和依赖规划另由 WorldModulePlanner 等入口处理，不能由这个按顺序 AddModule 的示例推断会自动拓扑排序。生命周期和失败语义见[使用契约](../Documentation~/Usage.md)。
