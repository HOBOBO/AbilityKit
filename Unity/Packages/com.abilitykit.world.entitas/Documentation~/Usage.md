# Entitas 世界适配使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖 contexts factory、组合与生命周期；无独立适配器回归 suite，未跑 Unity。

安装与第三方依赖见[包入口](../README.md)。本包提供 `EntitasWorld : IWorld` 及系统安装桥。Unity 使用随包声明的 Entitas 依赖；仓库 .NET 镜像声明 NuGet Entitas 最低 1.5.0，实际解析版本见下文，两者验证目标不同，不能相互替代。

## 完整本地空 contexts 示例

```csharp
using System;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

var options = new WorldCreateOptions(new WorldId("local"), "entitas");
options.ServiceBuilder = new WorldContainerBuilder()
    .RegisterInstance<IWorldLogger>(new NullWorldLogger())
    .RegisterInstance<IWorldClock>(new WorldClock());
options.SetEntitasContextsFactory(new EmptyContextsFactory());
options.Modules.Add(new TickCounterWorldModule());
using var world = new EntitasWorld(options);
world.Initialize();
world.Tick(1f / 30f);
if (world.Services.Resolve<WorldTickCounter>().TickCount != 1)
    throw new InvalidOperationException("Tick 计数不正确");

sealed class EmptyContexts : Entitas.IContexts
{
    public Entitas.IContext[] allContexts => Array.Empty<Entitas.IContext>();
}

sealed class EmptyContextsFactory : IEntitasContextsFactory
{
    public Entitas.IContexts Create() => new EmptyContexts();
    public void Release(Entitas.IContexts contexts) { }
}
```

此例验证空 contexts 装配及一次系统执行，不覆盖生成的业务 contexts、实体生命周期、确定性或世界恢复。

## 创建、安装与推进

构造 `EntitasWorld` 前必须通过 `SetEntitasContextsFactory` 提供工厂；构造函数立即创建 contexts，不能在构造后补注入。工厂返回 null 会失败。`RegisterEntitasWorld` 只注册世界类型的创建方式，不能替代 options 中的 contexts factory。

Composer 还会从最终 world scope 解析 `IWorldLogger`，Tick 依赖 `IWorldClock`。示例显式注册两项；实际宿主按所需模块补齐服务。默认服务工厂通过可选程序集反射发现模块，独立适配器镜像不保证自动取得这些服务。

`Initialize` 通过 composer 规划模块、注册内建服务和模块服务、创建 container/scope，调用 `IEntitasSystemsInstaller.Install`，再初始化 systems。业务模块负责生成 contexts 的实际类型与系统安装；适配层不会自动把其他 ECS 的实体数据转换过来。

`Tick` 先推进 world clock，再执行 `Systems.Execute()` 与 `Cleanup()`，并报告 frame guard 完成或失败。异常向调用方传播；已执行系统的修改不会自动回滚。宿主负责单一执行线、固定步长与同步模式，不能从这个适配器推导帧同步或状态同步恢复能力。

`AutoSystemInstaller` 只扫描显式程序集和命名空间前缀，选择 `[WorldSystem]` 类型及要求的 `(Entitas.IContexts, IWorldResolver)` 构造函数。排序作用于本次发现的集合，按 phase/order/类型全名排列，不对其他安装器已加入的系统全局重排；重复安装可能重复添加系统，也不是帧末调度器。

## 失败与资源所有权

world 通过工厂取得 contexts，并持有 systems、scope/container；正常释放依次 TearDown、释放 DI、调用 factory.Release。业务工厂负责实现 contexts 的实际回收。

初始化失败会尝试清理 DI 并重置初始化标志，但不会统一拆除所有部分安装的 systems 或释放 contexts；直接重试可能重复副作用。应由宿主控制失败实例的处理，不把它当成事务重试接口。

`Dispose` 会记录 systems TearDown 的错误，但后续 scope/container/factory 释放没有逐项 finally 隔离；中途异常可能跳过后续清理。当前无完整终态 guard，释放后不得继续 Tick 或重新初始化。不要依赖自动聚合所有释放异常。

## 核校范围与源码

本轮 .NET Entitas 镜像构建通过；上例另行本地编译执行。工程声明最低 Entitas 1.5.0，但本机缓存实际解析为 1.13.0，产生 NU1603/NU1701 警告；本轮不是 1.5.0 精确版本或跨框架兼容验收。没有独立适配器 suite、Unity 生成 contexts、业务场景或双同步链回归证据。旧设计保留在[历史路径](../Document/WorldEntitas世界适配模块开发设计文档.md)，当前使用契约以本页为准。

- [EntitasWorld](../Runtime/World/Core/EntitasWorld.cs)、[Composer](../Runtime/World/Core/EntitasWorldComposer.cs)：生命周期与组合。
- [contexts factory](../Runtime/World/Interfaces/IEntitasContextsFactory.cs)、[options 扩展](../Runtime/World/Extensions/WorldCreateOptionsEntitasExtensions.cs)：创建前置条件。
- [AutoSystemInstaller](../Runtime/World/Modules/AutoSystemInstaller.cs)：发现与局部排序。
- [TickCounterWorldModule](../Runtime/World/Modules/TickCounter/TickCounterWorldModule.cs)：最小消费者。
