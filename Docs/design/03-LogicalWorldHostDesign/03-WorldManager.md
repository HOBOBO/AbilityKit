# 3.3 World 管理器：IWorldManager、WorldManager 与多世界生命周期

> 本文基于 `Unity/Packages/com.abilitykit.world.di` 与 `Unity/Packages/com.abilitykit.host` 的真实源码，解释 WorldManager 如何管理多个 `IWorld`，以及 HostRuntime 如何在它外层补充 Hook、连接广播和模块扩展。

---

## 目录

- [3.3 World 管理器：IWorldManager、WorldManager 与多世界生命周期](#33-world-管理器iworldmanagerworldmanager-与多世界生命周期)
  - [目录](#目录)
  - [1. 能力定位](#1-能力定位)
  - [2. 源码入口](#2-源码入口)
  - [3. 真实接口](#3-真实接口)
  - [4. WorldManager 内部结构](#4-worldmanager-内部结构)
  - [5. Create 生命周期](#5-create-生命周期)
  - [6. Tick 生命周期](#6-tick-生命周期)
  - [7. Destroy 与 DisposeAll](#7-destroy-与-disposeall)
    - [7.1 Destroy 单个世界](#71-destroy-单个世界)
    - [7.2 DisposeAll 全部释放](#72-disposeall-全部释放)
  - [8. 与 HostRuntime 的关系](#8-与-hostruntime-的关系)
  - [9. WorldFactory 与 Blueprint](#9-worldfactory-与-blueprint)
    - [9.1 DefaultWorldFactory](#91-defaultworldfactory)
    - [9.2 WorldBlueprintWorldFactory](#92-worldblueprintworldfactory)
  - [10. 设计意图与解决的问题](#10-设计意图与解决的问题)
  - [11. 验证入口与证据状态](#11-验证入口与证据状态)
  - [12. 边界判断](#12-边界判断)
  - [13. 源码阅读路径](#13-源码阅读路径)

---

## 1. 能力定位

`WorldManager` 是 World DI 包里的多世界容器。它的职责很窄：用 `WorldId` 管理多个 `IWorld`，通过 `IWorldFactory` 创建世界，统一调用 `Tick`，并在销毁时调用 `Dispose`。

它不负责：

| 不负责的内容 | 对应位置 |
|--------------|----------|
| 连接和广播 | `HostRuntime` |
| Host 模块安装 | `WorldHostBuilder` 与 `IHostRuntimeModule` |
| 世界内部服务生命周期 | `WorldContainer`、`WorldScope` |
| 具体 ECS 或系统执行细节 | `IWorld.Tick` 内部实现 |
| 世界类型如何构造 | `IWorldFactory`、`WorldTypeRegistry`、Blueprint |

整体关系：

```mermaid
flowchart TB
    Host[HostRuntime] --> Manager[WorldManager]
    Manager --> Factory[IWorldFactory]
    Manager --> Map[Dictionary WorldId to IWorld]
    Factory --> World[IWorld]
    Manager --> Create[Create]
    Manager --> Tick[Tick all worlds]
    Manager --> Destroy[Destroy one world]
    Manager --> DisposeAll[Dispose all worlds]
```

---

## 2. 源码入口

| 源码 | 说明 |
|------|------|
| `Unity/Packages/com.abilitykit.world.di/Runtime/World/Management/IWorldManager.cs` | 多世界管理接口 |
| `Unity/Packages/com.abilitykit.world.di/Runtime/World/Management/WorldManager.cs` | `WorldManager` 默认实现 |
| `Unity/Packages/com.abilitykit.world.di/Runtime/World/Abstractions/IWorldFactory.cs` | 世界工厂接口 |
| `Unity/Packages/com.abilitykit.world.di/Runtime/World/Abstractions/IWorld.cs` | 世界最小生命周期接口 |
| `Unity/Packages/com.abilitykit.world.di/Runtime/World/Abstractions/WorldCreateOptions.cs` | 创建世界所需选项 |
| `Unity/Packages/com.abilitykit.host/Runtime/Host/Framework/HostRuntime.cs` | Host 外层调用 `WorldManager` 并补充 Hook/广播 |
| `Unity/Packages/com.abilitykit.host/Runtime/Host/Builder/DefaultWorldFactory.cs` | Host 默认工厂包装入口 |
| `Unity/Packages/com.abilitykit.host/Runtime/Host/WorldBlueprints/WorldBlueprintWorldFactory.cs` | Blueprint 介入世界创建选项的包装工厂 |
| `src/AbilityKit.Demo.Shooter.Runtime.Tests/Worlds/ShooterWorldModuleTests.cs` | Shooter Blueprint 与 Host 多世界链路的跨包集成测试 |

---

## 3. 真实接口

当前 `IWorldManager` 接口是：

```csharp
public interface IWorldManager
{
    IReadOnlyDictionary<WorldId, IWorld> Worlds { get; }

    IWorld Create(WorldCreateOptions options);
    bool TryGet(WorldId id, out IWorld world);
    bool Destroy(WorldId id);

    void Tick(float deltaTime);
    void DisposeAll();
}
```

当前接口边界包含两点：

| 边界 | 当前源码事实 |
|------|--------------|
| 世界集合访问 | 当前通过 `IReadOnlyDictionary<WorldId, IWorld> Worlds` 暴露只读字典 |
| 世界生命周期职责 | 管理器只有创建、Tick、销毁和全部释放；是否被驱动取决于 Host 或外部循环 |

---

## 4. WorldManager 内部结构

`WorldManager` 的状态非常直接：

```csharp
private readonly IWorldFactory _factory;
private readonly Dictionary<WorldId, IWorld> _worlds = new Dictionary<WorldId, IWorld>();
```

这对应两个核心约束：

| 字段 | 设计含义 |
|------|----------|
| `_factory` | `WorldManager` 不知道具体世界类型，只把创建委托给工厂 |
| `_worlds` | `WorldId` 是多世界索引 key，创建重复 id 会被拒绝 |

运行结构：

```mermaid
flowchart LR
    Request[WorldCreateOptions] --> Manager[WorldManager]
    Manager --> Validate[Validate id and type]
    Validate --> Factory[IWorldFactory]
    Factory --> World[IWorld]
    World --> Initialize[Initialize]
    Initialize --> Store[Store by WorldId]
```

---

## 5. Create 生命周期

`WorldManager.Create(options)` 的流程是：

```mermaid
sequenceDiagram
    participant Caller
    participant Manager as WorldManager
    participant Factory as IWorldFactory
    participant World as IWorld

    Caller->>Manager: Create(options)
    Manager->>Manager: validate options not null
    Manager->>Manager: validate options.Id.Value
    Manager->>Manager: validate options.WorldType
    Manager->>Manager: reject duplicate WorldId
    Manager->>Factory: Create(options)
    Factory-->>Manager: world
    Manager->>World: Initialize()
    Manager->>Manager: add world to dictionary
    Manager-->>Caller: world
```

源码里的校验顺序体现了几个关键决策：

| 校验 | 失败行为 | 解决的问题 |
|------|----------|------------|
| `options == null` | `ArgumentNullException` | 避免工厂收到空输入 |
| `options.Id.Value` 为空 | `ArgumentException` | 世界必须可索引 |
| `options.WorldType` 为空 | `ArgumentException` | 工厂必须知道创建哪类世界 |
| `_worlds.ContainsKey(options.Id)` | `InvalidOperationException` | 防止覆盖正在运行的世界 |

`world.Initialize()` 在加入字典之前执行。这样可以避免一个初始化失败的世界进入管理器；只有成功初始化后才成为可查询和可 Tick 的世界。

---

## 6. Tick 生命周期

`WorldManager.Tick(deltaTime)` 会遍历当前字典中的所有世界并调用 `IWorld.Tick`。

```mermaid
flowchart TD
    Tick[WorldManager.Tick] --> Loop[For each world]
    Loop --> Try[Try world.Tick]
    Try -->|Success| Next[Next world]
    Try -->|Exception| Log[Log worldId and exception]
    Log --> Next
    Next --> Done{All worlds processed}
```

异常隔离边界：单个世界 Tick 异常不会终止整个管理器 Tick。源码会记录：

```csharp
Log.Exception(ex, $"[WorldManager] World.Tick failed: worldId={kv.Key}");
```

该边界支撑多房间服务器的故障隔离：一个房间逻辑报错，不应该直接阻断其他房间 Tick。

并发修改边界：`WorldManager` 没有快照字典再遍历，也没有锁。如果在 Tick 遍历期间修改 `_worlds`，可能触发集合枚举问题。通常应由 Host 主线程或明确的调度点创建/销毁世界。

---

## 7. Destroy 与 DisposeAll

### 7.1 Destroy 单个世界

```mermaid
sequenceDiagram
    participant Caller
    participant Manager as WorldManager
    participant World as IWorld

    Caller->>Manager: Destroy(worldId)
    Manager->>Manager: TryGetValue(worldId)
    alt world exists
        Manager->>Manager: remove from dictionary
        Manager->>World: Dispose()
        Manager-->>Caller: true
    else missing
        Manager-->>Caller: false
    end
```

`Destroy` 先从字典移除，再调用 `world.Dispose()`。这样做的效果是：即使 Dispose 内部触发一些回调或查询，也不会再把该世界视为管理器中的活跃世界。

### 7.2 DisposeAll 全部释放

```mermaid
flowchart TD
    DisposeAll[DisposeAll] --> Loop[For each world]
    Loop --> Dispose[world.Dispose]
    Dispose --> Next[Next world]
    Next --> Clear[Clear dictionary]
```

`DisposeAll` 用于 Host 或测试退出时清理所有世界。它不像 `DestroyWorld` 那样触发 HostRuntime 的 `WorldDestroyed` Hook 或广播消息，因为它是 `WorldManager` 自身的底层释放能力。

---

## 8. 与 HostRuntime 的关系

`HostRuntime` 是 `WorldManager` 的外层门面：

```mermaid
flowchart TB
    Caller[Caller] --> Host[HostRuntime]
    Host --> Options[HostRuntimeOptions]
    Host --> Manager[WorldManager]
    Host --> Clients[Connections]

    Host --> CreateWorld[CreateWorld]
    CreateWorld --> Before[BeforeCreateWorld]
    Before --> ManagerCreate[Manager.Create]
    ManagerCreate --> Created[WorldCreated]
    Created --> BroadcastCreated[Broadcast WorldCreatedMessage]

    Host --> DestroyWorld[DestroyWorld]
    DestroyWorld --> ManagerDestroy[Manager.Destroy]
    ManagerDestroy --> Destroyed[WorldDestroyed]
    Destroyed --> BroadcastDestroyed[Broadcast WorldDestroyedMessage]
```

对比两层职责：

| 操作 | WorldManager | HostRuntime |
|------|--------------|-------------|
| 创建世界 | 校验、工厂创建、Initialize、存字典 | 创建前后触发 Hook，并广播 `WorldCreatedMessage` |
| 查找世界 | `TryGet` | `TryGetWorld` 代理到 `WorldManager.TryGet` |
| 销毁世界 | 移除字典并 Dispose | 销毁后触发 Hook，并广播 `WorldDestroyedMessage` |
| Tick | 遍历所有世界 | Tick 前后触发 Hook，外层捕获异常 |
| 连接 | 不知道连接 | 管理连接并发送消息 |

---

## 9. WorldFactory 与 Blueprint

`WorldManager` 只依赖 `IWorldFactory`，这让世界类型创建策略可以被替换。

### 9.1 DefaultWorldFactory

`DefaultWorldFactory` 支持可选的 `IWorldBlueprintRegistry`。如果存在蓝图，会先让蓝图修改 `WorldCreateOptions`，再委托 fallback factory 创建世界。

```mermaid
flowchart LR
    Options[WorldCreateOptions] --> DefaultFactory[DefaultWorldFactory]
    DefaultFactory --> HasBlueprint{Blueprint exists}
    HasBlueprint -->|Yes| Configure[blueprint.Configure]
    HasBlueprint -->|No| Fallback[Fallback factory]
    Configure --> Fallback
    Fallback --> World[IWorld]
```

默认 fallback 只注册了一个会抛异常的 `default` 类型，用于提醒使用者必须提供真实世界工厂或蓝图：

```csharp
registry.Register("default", options => throw new InvalidOperationException(
    "No world factory registered. Please use BlueprintRegistry or register a world factory."));
```

### 9.2 WorldBlueprintWorldFactory

当使用者同时提供 `IWorldFactory` 和 `IWorldBlueprintRegistry` 时，`WorldHostBuilder` 会用 `WorldBlueprintWorldFactory` 包装原工厂：

```mermaid
sequenceDiagram
    participant Builder as WorldHostBuilder
    participant Wrapper as WorldBlueprintWorldFactory
    participant Blueprint as IWorldBlueprint
    participant Inner as IWorldFactory

    Builder->>Wrapper: Create(options)
    Wrapper->>Blueprint: Configure(options)
    Wrapper->>Inner: Create(options)
    Inner-->>Wrapper: IWorld
```

这使蓝图可以统一补充模块、服务或扩展字段，而具体世界创建仍由业务工厂负责。

---

## 10. 设计意图与解决的问题

| 设计 | 解决的问题 |
|------|------------|
| `WorldManager` 只依赖 `IWorldFactory` | 多世界管理不绑定具体世界类型 |
| `WorldId` 做字典 key | 多房间、多副本、多测试世界可并存 |
| Create 时先 Initialize 后入表 | 初始化失败的世界不会进入活跃集合 |
| Destroy 时先移除再 Dispose | 防止正在销毁的世界仍被查询为活跃 |
| Tick 单世界异常隔离 | 一个世界失败不直接阻断其他世界 |
| HostRuntime 包装 WorldManager | 底层生命周期保持纯粹，Host 层再加 Hook、广播、模块 |
| Blueprint 包装工厂 | 允许配置化修改创建选项，而不侵入业务工厂 |

---

## 11. 验证入口与证据状态

当前仓库审计未发现面向 `WorldManager` 的独立测试工程或测试类。Shooter Runtime Tests 中有两个相邻用例经过真实管理器或 Host 链路，可作为跨包集成证据：

```powershell
dotnet test src/AbilityKit.Demo.Shooter.Runtime.Tests/AbilityKit.Demo.Shooter.Runtime.Tests.csproj --filter "BlueprintRegistrationCreatedWorldResolvesShooterSveltoServices|ShooterWorldHostCreatesAndDrivesBattleWorldRuntime"
```

| 用例 | 已证明 | 没有证明 |
|------|--------|----------|
| `BlueprintRegistrationCreatedWorldResolvesShooterSveltoServices` | 注册 Shooter Blueprint 后，`RegistryWorldFactory + WorldManager.Create` 能创建世界；世界类型和四类 Shooter/Svelto 服务可解析；业务运行时可启动；`DisposeAll` 调用可完成 | `DisposeAll` 的逐世界释放顺序、异常续处理、字典最终状态和服务实际释放次数 |
| `ShooterWorldHostCreatesAndDrivesBattleWorldRuntime` | Shooter Host 能创建并查回世界；一次 Host Tick 推进业务帧；销毁后无法再查到世界 | 底层 `WorldManager.Tick` 的多世界异常隔离、Host Hook/广播顺序和 `Dispose` 异常行为 |

这些测试证明真实 Shooter 装配可以经过管理器运行，但不能替代 `WorldManager` 的单元契约测试。优先补测项：

1. 空 options、空 id、空 world type 和重复 `WorldId` 的异常类型与工厂调用次数。
2. 工厂返回世界后，`Initialize` 成功才入表；初始化抛异常时不入表，并明确失败实例由谁释放。
3. 多世界 Tick 的遍历次数和单世界异常隔离，确认后续世界继续执行。
4. `Destroy` 先移除再 `Dispose`，缺失 id 返回 false，重复销毁不再调用 Dispose。
5. `DisposeAll` 的全部释放、字典清空和单个 Dispose 抛异常时的剩余世界处理语义。
6. Tick 回调中创建或销毁世界时的当前失败行为，或后续引入命令队列/快照后的目标契约。

其中第 5 项需要先做设计决策：当前源码如果某个 `Dispose` 抛异常，循环和最终 `_worlds.Clear()` 都可能被中断。文档不能把“全部释放”扩写为已经具备故障隔离。

---

## 12. 边界判断

| 容易混淆的判断 | 设计边界 |
|----------------|----------|
| `WorldManager` 是 Host | `WorldManager` 只管理世界；HostRuntime 才处理连接、Hook 和广播 |
| `WorldManager` 有 `GetAll()` | 当前接口暴露 `Worlds` 只读字典 |
| `Destroy` 会广播世界销毁消息 | 广播是 `HostRuntime.DestroyWorld` 的职责 |
| `DisposeAll` 等同于逐个 `DestroyWorld` | `DisposeAll` 不触发 HostRuntime Hook 或消息广播，也没有逐世界 Dispose 异常隔离 |
| 可以在世界 Tick 中随意创建/销毁世界 | 当前遍历没有快照和锁，创建/销毁应放在明确调度点 |
| 默认工厂能直接创建业务世界 | 默认 fallback 会抛异常，项目需要注册真实工厂或 Blueprint |
| Shooter 世界测试等于管理器契约已完整覆盖 | 当前只证明两条真实集成链路，底层校验、异常和释放顺序仍无独立断言 |

---

## 13. 源码阅读路径

1. `IWorldManager`：当前真实接口。
2. `WorldManager.Create`：校验、工厂创建和 Initialize 顺序。
3. `WorldManager.Tick`：多世界 Tick 的异常隔离。
4. `WorldManager.Destroy` 与 `DisposeAll`：释放边界和未隔离的 Dispose 异常。
5. `HostRuntime.CreateWorld` 与 `DestroyWorld`：Host 对管理器的包装。
6. `DefaultWorldFactory` 与 `WorldBlueprintWorldFactory`：Host Builder 如何注入世界创建策略。
7. `ShooterWorldModuleTests.cs`：真实 Blueprint、Host 与 Shooter 世界的集成证据。
8. [Host 运行时](./01-HostRuntime.md) 与 [Host 模块系统](./02-HostModules.md)：完整 Host 层设计。

---

*文档版本：v2.1 | 最后更新：2026-08-02*
