# AbilityKit World DI

> 文档类型：包入口与快速开始
> 核校状态：2026-11-24 核对包声明与容器源码；下例在 .NET 镜像工程编译运行，未替代 Unity 独立安装验证。

`com.abilitykit.world.di` 提供运行时依赖注入容器，用于注册服务、创建作用域和装配依赖。不负责宿主调度、世界推进或表现层接线。

## 安装与接入

从项目配置的包来源安装本包；Unity 版本与依赖以 [`package.json`](package.json) 为准。

当前声明 Unity 2022.3，直接依赖 Core、Deterministic、MemoryPack 与 Unsafe。使用自定义 asmdef 的代码需引用 `AbilityKit.World.DI`；依赖包也应由安装来源完整提供。本轮使用仓库 .NET 镜像验证代码，不据此承诺特定注册表的可用性、裁剪或 IL2CPP 兼容性。

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

- [使用文档索引](Documentation~/README.md)：按接入任务选择阅读入口。
- [使用契约与限制](Documentation~/Usage.md)：生命周期、作用域所有权、解析失败语义。
- [示例](Examples/README.md)：基础注册、模块组合和可选依赖。
- [变更记录](CHANGELOG.md)；[历史设计入口](Document/README.md)。
