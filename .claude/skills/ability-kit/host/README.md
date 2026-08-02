# Host / WorldBlueprints 接入与排查
> v0.1.0 Beta -- AbilityKitStable=true, has direct src tests, zero hard errors.

> WorldBlueprint 机制**存在且健康**，但拆分到 4 个包，注册流程代码已与旧 skill 描述完全不同。旧 `LogicWorldServer` 实际只是示例 `LogicWorldServerExample`。

## 4 包分工

| 包 | 职责 |
|----|------|
| `com.abilitykit.host` | WorldBlueprints 装配、HostRuntime、WorldHostBuilder、Transport |
| `com.abilitykit.world.di` | World 抽象（IWorld/WorldId/WorldCreateOptions）+ DI 容器（WorldContainerBuilder/WorldContainer）+ WorldManager + WorldTypeRegistry + RegistryWorldFactory |
| `com.abilitykit.world.entitas` | Entitas 适配（EntitasWorld/EntitasWorldComposer/IEntitasContextsFactory） |
| `com.abilitykit.host.extension` | FrameSync/BattleHost/RoomSync/CatchUp/GameStartSource/Session 等扩展模块 |

命名空间注意：包名是 `com.abilitykit.host`，但命名空间带 `Ability`（`AbilityKit.Ability.Host.*`）。

## 关键对象速查

### WorldBlueprints（`com.abilitykit.host/Runtime/Host/WorldBlueprints/`，ns `AbilityKit.Ability.Host.WorldBlueprints`）

- `IWorldBlueprint` — `interface { string WorldType { get; } void Configure(WorldCreateOptions options); }` + `sealed class DelegateWorldBlueprint`
- `IWorldBlueprintRegistry` — `interface { bool TryGet(string worldType, out IWorldBlueprint blueprint); }`
- `WorldBlueprintRegistry` — `sealed class`，`Register(IWorldBlueprint)` / `TryGet` / `Configure(WorldCreateOptions)`
- `WorldBlueprintWorldFactory` — `sealed class : IWorldFactory`，装饰器模式

### World 抽象与 DI（`com.abilitykit.world.di/Runtime/World/`，ns `AbilityKit.Ability.World.*`）

- `WorldId`（`[MemoryPackable] readonly partial struct`，单字段 `string Value`）
- `IWorld` / `IWorldFactory` / `WorldCreateOptions`（字段：`Id` / `WorldType` / `ServiceBuilder` / `Extensions` / `Modules`）
- `WorldTypeRegistry` — `Register(string worldType, Func<WorldCreateOptions, IWorld> factory)` / `Create(options)`
- `RegistryWorldFactory` — `sealed class : IWorldFactory`，包一层 `WorldTypeRegistry`
- `WorldManager` — `sealed class : IWorldManager`，`Create / TryGet / Destroy / Tick / DisposeAll`，内部 `Dictionary<WorldId, IWorld>`
- `WorldContainerBuilder` / `WorldContainer` / `WorldScope` / `WorldActivator` / `WorldServiceDescriptor` / `IWorldModule`
- `WorldServiceAttribute`（`[WorldService(lifetime: WorldLifetime.Singleton)]`）

### Entitas 适配（`com.abilitykit.world.entitas/Runtime/World/`）

- `EntitasWorld` / `EntitasWorldComposer` / `EntitasWorldContext`
- `IEntitasWorld` / `IEntitasWorldContext` / **`IEntitasContextsFactory`**（旧 skill 写的 `EntitasContextsFactory` 实际是接口；moba 实现：`com.abilitykit.demo.moba.runtime/Runtime/Infrastructure/Entitas/MobaEntitasContextsFactory.cs`）

### HostRuntime（`com.abilitykit.host/Runtime/Host/Framework/`）

- `HostRuntime` — 主机运行时
- `HostRuntimeOptions` — 选项
- `HostRuntimeModuleHost` — 模块宿主，`Add(IHostRuntimeModule)` / `InstallAll(server, options)`
- `IHostRuntimeModule` — 模块接口

### Host.extension 扩展模块（`com.abilitykit.host.extension/Runtime/`）

- `FrameSync/FrameSyncDriverModule` / `ClientPredictionDriverModule`
- `Server/BattleHost/` — `BattleHostLifecycleRunner` / `BattleTickDriver` / `BattleInputBuffer` / `BattleInputFrameScheduler` / `BattleSnapshotPublisher` / `BattleObserverRegistry`
- `Time/FixedStepTickRunner` / `ServerFrameTimeModule`
- `WorldStart/WorldAutoStartModule`
- `Session/RoomGatewaySessionFlow`
- `Rollback/ServerRollbackModule`

## 当前标准注册范式（取自 `LogicWorldServerExample.cs`）

```csharp
// 1. WorldType → Factory 注册
var registry = new WorldTypeRegistry();
registry.Register(worldType, MinimalWorldFactory.CreateWorld);
var manager = new WorldManager(new RegistryWorldFactory(registry));

// 2. HostRuntime
var options = new HostRuntimeOptions();
var server = new HostRuntime(manager, options);

// 3. 模块化扩展
var modules = new HostRuntimeModuleHost();
modules.Add(new FrameSyncDriverModule());
modules.Add(new ServerFrameTimeModule());
modules.InstallAll(server, options);

// 4. WorldBlueprint 装配
var blueprints = new WorldBlueprintRegistry();
blueprints.Register(new DelegateWorldBlueprint(worldType, ConfigureDefaultWorld));

// 5. 创建 world（注意 ServiceBuilder 与 Modules 的传递）
var builder = WorldServiceContainerFactory.CreateDefaultOnly();
server.CreateWorld(new WorldCreateOptions(worldId, worldType) { ServiceBuilder = builder });
```

## Blueprint 内做什么

每个 `WorldType` 建议单独 blueprint（便于维护/变更审计）。在 `Configure(WorldCreateOptions options)` 中通常做：

- `options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();`
- `options.Modules.Add(...)` — 加 world modules
- `options.Extensions[...] = ...` — 例如 Entitas contexts factory

### Entitas world 必做

```csharp
options.SetEntitasContextsFactory(new MobaEntitasContextsFactory(...));
```

（扩展方法在 `com.abilitykit.world.entitas/Runtime/World/Extensions/WorldCreateOptionsEntitasExtensions.cs`）

## 显式注册（推荐）

不要在 Host 中做扫描。在应用包里提供单一入口：

```csharp
public static class WorldBlueprintsRegistration
{
    public static void RegisterAll(WorldBlueprintRegistry registry)
    {
        registry.Register(new DelegateWorldBlueprint("lobby", options => { ... }));
        registry.Register(new DelegateWorldBlueprint("battle", options => { ... }));
    }
}
```

## 常见报错与排查

### 1. `EntitasWorld` 报错：缺少 EntitasContextsFactory

错误特征：
```
EntitasContextsFactory is required. Set it via WorldCreateOptions.SetEntitasContextsFactory(...)
```

排查：
- 检查对应 `WorldType` 的 blueprint 是否调用了 `SetEntitasContextsFactory(...)`
- 注意接口实际名是 `IEntitasContextsFactory`（带 I 前缀），moba 实现是 `MobaEntitasContextsFactory`
- 检查创建路径是否真的走了 `WorldBlueprintWorldFactory`（有没有绕过）

### 2. 编译报错：找不到 `AbilityKit.Ability.Host.WorldBlueprints`

常见根因：
- asmdef 没引用 `AbilityKit.Host`（注意包名是 `com.abilitykit.host`）
- 或没引用 `AbilityKit.World.DI`、`AbilityKit.World.ECS`

排查：
- 检查 asmdef `references` 字段（asmdef 引用不传递，必须显式列出）
- 详见 [upm_asmdef_notes.md](upm_asmdef_notes.md)

### 3. 世界行为不一致（本地能跑，服务器不行 / 反之）

常见根因：
- 一条创建路径用 blueprint，另一条路径手写装配

排查：
- 搜索 `new WorldCreateOptions(` 的调用点
- 确保所有创建 world 的入口统一走 `WorldBlueprintWorldFactory`

### 4. DI 报"dependencies are registered"

常见根因：
- 某服务由 module 注册（如 `IEventBus`、`TriggerRunner<IWorldResolver>`），但 module 未装载

排查：
- 检查 `HostRuntimeModuleHost.Add(...)` 是否装载了对应 module
- moba 的关键 module 注册在 `WorldModulesStage`（`com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/Stages/`）

## 维护建议

- `WorldType` 是 `string`（不是独立类型），建议集中定义常量避免散落字符串
- blueprint 应"只做装配"，不要在里面写玩法规则
- 若 bootstrap module 过大，优先拆分为 `LobbyWorldBootstrapModule` / `BattleWorldBootstrapModule`
