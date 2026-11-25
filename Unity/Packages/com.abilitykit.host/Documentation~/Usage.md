# Host 使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：已核校本页运行时、Builder、blueprint 与模块生命周期；真实传输和玩法未验证。

## 最小本地宿主

完整示例使用无玩法、无服务的空世界，只验证创建、单次 Tick 与销毁；实际项目需提供自己的 IWorld 和服务容器。

```csharp
using System;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;

var registry = new WorldTypeRegistry().Register("empty", options => new EmptyWorld(options));
var worlds = new WorldManager(new RegistryWorldFactory(registry));
var host = new HostRuntime(worlds, new HostRuntimeOptions());
try
{
    var id = new WorldId("local");
    var world = (EmptyWorld)host.CreateWorld(new WorldCreateOptions(id, "empty"));
    host.Tick(1f / 30f);
    if (world.Ticks != 1) throw new InvalidOperationException("世界未推进");
    host.DestroyWorld(id);
}
finally
{
    worlds.DisposeAll();
}

sealed class EmptyWorld : IWorld
{
    public EmptyWorld(WorldCreateOptions options) { Id = options.Id; WorldType = options.WorldType; }
    public WorldId Id { get; }
    public string WorldType { get; }
    public IWorldResolver Services => null;
    public int Ticks { get; private set; }
    public void Initialize() { }
    public void Tick(float deltaTime) { Ticks++; }
    public void Dispose() { }
}
```

不要用 `using var host`：`HostRuntime` 本身没有 Dispose。世界关闭走管理器；模块、驱动、连接及业务订阅分别由组合根收尾。

## 执行和失败边界

| 入口 | 当前行为 | 责任 |
| --- | --- | --- |
| `CreateWorld` | 前置 Hook 和回调先执行，再由管理器创建；成功后发创建通知并广播 | 前置失败会传播；成功通知异常记日志，不能据此假设世界创建已撤销 |
| `DestroyWorld` | 管理器销毁成功后才通知和广播 | 管理器释放抛错时不会继续发成功通知；不要只靠通知维护外部缓存 |
| `Tick` | PreTick → 检查 `IHostRuntimeTickGate` → 世界 Tick → PostTick | gate 拒绝时跳过世界 Tick 与 PostTick，仍运行 PreTick；delta 单位秒，由宿主选时间来源 |
| Tick 中异常 | 整次 Tick 捕获并记日志，剩余流程可被跳过 | 无返回成功证明、不自动回滚；需要提交帧证明时使用所属驱动的确认契约 |
| `Broadcast` | 复制当前连接列表，逐连接发送并隔离异常 | Connect/Disconnect 只改变登记，不拥有、关闭或释放连接 |
| `SendTo` | 前置 Hook → connection.Send → 后置 Hook | Send 异常记录后返回；前后 Hook 异常可传播。无自动重试或发送事务 |

Hook 按优先级、同优先级按加入顺序调用；Hook 自身不隔离单个 handler 的异常。生命周期通知外层的隔离不等于所有 handler 都已完成。世界登记、模块安装和 Tick 应在同一宿主线上安排，连接字典的锁不代表整个 Host 线程安全。

## Builder、blueprint 与清理

`WorldHostBuilder.BuildWithOptions` 创建管理器、options 与 runtime，依次 Attach connection/time/input，注册 snapshot feature，再按添加顺序安装模块。它不自动启动循环；默认 fallback 不是可运行玩法世界，创建前必须有可创建目标类型的实际工厂。仅注册 blueprint 只修改 options，不能凭空产生世界实现。

`WorldBlueprintWorldFactory` 直接修改传入的 options 后调用 inner factory；未找到 blueprint 时仍调用 inner。不要在反复创建中复用会累积模块或注册的可变 options。

`HostRuntimeModuleHost.InstallAll` 按顺序安装，`UninstallAll` 逆序卸载；任一项抛错会停止当前循环，不自动撤销前面已安装项，也不保证继续卸载剩余项。Builder 的 Attach/Install 同样不是事务。组合根须记录已经安装的模块，并在失败和退出时针对性清理，不能把调用一次 UninstallAll 当作全量清理保证。不要混用两套安装入口重复安装同一模块。

## 来源与限制

源码：[HostRuntime](../Runtime/Host/Framework/HostRuntime.cs)、[模块容器](../Runtime/Host/Framework/HostRuntimeModuleHost.cs)、[Builder](../Runtime/Host/Builder/WorldHostBuilder.cs)、[blueprint 工厂](../Runtime/Host/WorldBlueprints/WorldBlueprintWorldFactory.cs)。本页不规定线上连接策略、同步模式、世界恢复范围或玩法机制；这些须由应用组合根另行声明。
