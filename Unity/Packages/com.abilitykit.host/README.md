# AbilityKit Host

`com.abilitykit.host` 提供 **权威（authoritative）的世界 Host 运行时**。

核心目标：
- 托管一个或多个 `IWorld` 实例（Server 或 Offline）。
- 通过 driver 驱动世界运行（帧同步 / 实时）。
- 将 gameplay 逻辑隔离在 Host 之外。
- 通过 **WorldType + World Modules** 组装不同玩法的世界。

## 分层 / 职责

- **Host（本包）**
  - world 生命周期、路由、tick 驱动
  - 输入/快照管线集成（通过 world capabilities）
  - 传输抽象（连接 + 消息）
  - 横切型 host modules（time/rollback/record/metrics）

- **Gameplay 应用层包**
  - 定义 `WorldType`（例如 `battle`, `lobby`）
  - 提供 World Modules 与 services/capabilities

## 架构边界（这个包是什么 / 不是什么）

本包属于 **框架/运行时基础设施层**。

它应该：
- 负责 world 生命周期 + tick 驱动 + 路由
- 负责横切型 host modules（time/rollback/record/metrics）
- 仅通过从 `world.Services` 解析得到的 world capabilities 与 gameplay 交互

它不应该：
- 实现 gameplay 规则
- 写死任何玩法相关的 world 生命周期（matchmaking/lobby 规则/英雄配置等）

玩法差异应通过以下方式表达：
- `WorldType`（合同/协议）
- `WorldCreateOptions` 的组装（World Modules + ServiceBuilder）

## 运行时管线（高层视角）

```text
Game/App
  -> chooses WorldType + creates minimal WorldCreateOptions
  -> Host creates world, selects driver by capability
  -> Game routes join/leave/input to a world session handle
  -> Driver ticks: flush inputs -> world.Tick -> snapshots -> broadcast
```

## 关键契约

从 `world.Services` 解析得到的 world capabilities：
- `IWorldInputSink`：帧同步世界的输入接收器
- `IWorldStateSnapshotProvider`：提供用于广播的快照
- `IWorldPlayerLifecycle`：玩法侧的玩家 join/leave 语义

### Capability 的经验法则

- Host 只有在 *必须* 与玩法交互时才依赖 capability。
- capability 接口应稳定且尽量小。
- capability 存在于 world container（`world.Services`）中。

## World Blueprints（WorldType -> 组装）

为提升工程可维护性，推荐使用 **World Blueprints**：

- `IWorldBlueprint`：把 `WorldType` 绑定到一份 `WorldCreateOptions` 配置逻辑。
- `WorldBlueprintRegistry`：blueprint 注册表。
- `WorldBlueprintWorldFactory`：包装现有 `IWorldFactory`，在创建前应用 blueprint。

### 为什么需要 blueprints

Blueprints 把重复的装配逻辑（modules/services/extensions）从零散的调用点收敛到一处。
从而让 world 创建在以下场景保持一致：
- dedicated server
- local/offline simulation
- in-memory transport

### 推荐 wiring

```csharp
// 1) 注册 world factories（Entitas/其他 runtime）
var typeRegistry = new WorldTypeRegistry()
    .RegisterEntitasWorld("battle")
    .RegisterEntitasWorld("lobby");

IWorldFactory baseFactory = new RegistryWorldFactory(typeRegistry);

// 2) 注册 world blueprints（玩法装配）
var blueprints = new WorldBlueprintRegistry()
    .Register(new DelegateWorldBlueprint("battle", options => {
        options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();
        // options.ServiceBuilder.RegisterInstance(...)
        // options.Modules.Add(new BattleWorldBootstrapModule());
    }))
    .Register(new DelegateWorldBlueprint("lobby", options => {
        options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();
        // options.Modules.Add(new LobbyWorldBootstrapModule());
    }));

// 3) 包装 factory，使创建前自动应用 blueprint
IWorldFactory factory = new WorldBlueprintWorldFactory(baseFactory, blueprints);

// 4) 创建 world manager + host
var worldManager = new WorldManager(factory);
var host = new LogicWorldServer(worldManager);

// 5) 使用最小 options 创建 world（其余由 blueprint 补齐）
host.CreateWorld(new WorldCreateOptions(new WorldId("room_1"), "battle"));
```

## Host modules（横切关注点）

Host modules 应保持通用性，不能依赖 gameplay。
它们应该安装在 host options 上，而不是放进 gameplay world modules 里。

Example:
- `ServerFrameTimeModule`
- `ServerRollbackModule`

### Host module vs World module

- Host module：横切型运行时能力，应与玩法无关。
- World module：玩法 world 的组装，应由应用包持有。

## 备注

- 本包刻意不提供任何 gameplay world 实现。
- 不同玩法世界应通过 `WorldType` + World Modules 组装，并通过 blueprints 注册。

## 推荐模式：应用侧显式注册（A）

为了保持 Host 框架的通用性（不做反射扫描、不引入隐式全局状态），推荐：

- gameplay/application 包拥有自己的 blueprints。
- 每个应用包提供一个显式入口：
  - `RegisterAll(WorldBlueprintRegistry registry)`

### 推荐的应用包目录结构

```text
com.yourgame.runtime/
  Runtime/
    Worlds/
      Lobby/
        LobbyWorldBootstrapModule.cs
        LobbyWorldBlueprint.cs
      Battle/
        BattleWorldBootstrapModule.cs
        BattleWorldBlueprint.cs
    WorldBlueprintsRegistration.cs   // contains RegisterAll(...)
```

### 模板：注册入口

```csharp
using AbilityKit.Ability.Host.WorldBlueprints;

namespace YourGame
{
    public static class WorldBlueprintsRegistration
    {
        public static void RegisterAll(WorldBlueprintRegistry registry)
        {
            registry
                .Register(new LobbyWorldBlueprint())
                .Register(new BattleWorldBlueprint());
        }
    }
}
```

### Composition root wiring

```csharp
var typeRegistry = new WorldTypeRegistry()
    .RegisterEntitasWorld("lobby")
    .RegisterEntitasWorld("battle");

var blueprints = new WorldBlueprintRegistry();
YourGame.WorldBlueprintsRegistration.RegisterAll(blueprints);

IWorldFactory baseFactory = new RegistryWorldFactory(typeRegistry);
IWorldFactory factory = new WorldBlueprintWorldFactory(baseFactory, blueprints);

var worldManager = new WorldManager(factory);
var host = new LogicWorldServer(worldManager);
```

## 真实示例：MOBA battle/lobby 接入

本仓库提供了一个应用侧显式注册的示例：

- Blueprints:
  - `com.abilitykit.demo.moba.runtime/Runtime/Worlds/Blueprints/MobaLobbyWorldBlueprint.cs`
  - `com.abilitykit.demo.moba.runtime/Runtime/Worlds/Blueprints/MobaBattleWorldBlueprint.cs`
  - `com.abilitykit.demo.moba.runtime/Runtime/Worlds/Blueprints/MobaWorldBlueprintsRegistration.cs`

本地 host wiring 已接入：

- `com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/BattleLogicSession.cs`

高层行为：
- 为 `"lobby"` 与 `"battle"` 注册 Entitas world factories。
- 通过 `MobaWorldBlueprintsRegistration.RegisterAll(...)` 注册 MOBA blueprints。
- 用 `WorldBlueprintWorldFactory` 包装 `RegistryWorldFactory`，使得创建 world 时自动完成 `WorldCreateOptions` 的装配。

这意味着 gameplay 代码可以用最小 options 创建 world：

```csharp
host.CreateWorld(new WorldCreateOptions(worldId, worldType));
```

并由 blueprint 补齐：
- entitas contexts factory（例如 `MobaEntitasContextsFactory`）
- 必需的 world modules（例如 `MobaWorldBootstrapModule`）
