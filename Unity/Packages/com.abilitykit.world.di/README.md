# AbilityKit World DI

`com.abilitykit.world.di` 提供运行时依赖注入容器，用于注册服务、创建作用域和装配依赖。不负责宿主调度、世界推进或表现层接线。

## 安装与接入

从项目配置的包来源安装本包；Unity 版本与依赖以 [`package.json`](package.json) 为准。

在组合入口完成注册，再构建根容器并创建作用域：

```csharp
using System.Text;
using AbilityKit.Ability.World.DI;

public static class WorldDiQuickStart
{
    public static string BuildMessage()
    {
        var builder = new WorldContainerBuilder();
        builder.Register<StringBuilder>(WorldLifetime.Scoped, _ => new StringBuilder());
        using var container = builder.Build();
        using var scope = container.CreateScope();
        return scope.Resolve<StringBuilder>().Append("Hello").ToString();
    }
}
```

## 文档

- [使用契约与限制](Documentation~/Usage.md)：生命周期、作用域所有权、解析失败语义。
- [示例](Examples/README.md)：基础注册、模块组合和可选依赖。
- [变更记录](CHANGELOG.md)；[历史设计入口](Document/README.md)。
