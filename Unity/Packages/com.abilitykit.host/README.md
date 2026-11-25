# AbilityKit Host

`com.abilitykit.host` 提供显式装配的世界 Host 运行时 `HostRuntime`。世界由 `IWorldManager` 管理；本包不附送玩法世界或自动选择同步驱动。

## 安装与首次使用

从项目配置的包来源安装，版本、Unity 最低版本及依赖见 [package.json](package.json)。从[文档索引](Documentation~/README.md)进入；[使用指南](Documentation~/Usage.md)给出不依赖具体 ECS 的完整本地例子和关闭责任。以下较长的 blueprints 与项目 wiring 均为装配片段，不是仅安装本包即可运行的程序。

核心目标：
- 托管一个或多个 `IWorld` 实例（Server 或 Offline）。
- 由宿主调用 Tick，可额外安装时间或帧同步 driver。
- 将 gameplay 逻辑隔离在 Host 之外。
- 通过 **WorldType + World Modules** 组装不同玩法的世界。

## 分层 / 职责

- **Host（本包）**
  - world 生命周期通知、消息转发和 Tick 入口
  - 供扩展装配输入/快照管线的 capabilities 与 hooks
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
  -> Application installs the required driver; Host creates world
  -> Game routes join/leave/input to a world session handle
  -> Driver ticks: flush inputs -> world.Tick -> snapshots -> broadcast
```

## 关键契约

从 `world.Services` 解析得到的 world capabilities：
- `IWorldInputSink`：帧同步世界的输入接收器
- `IWorldStateSnapshotProvider`：提供用于广播的快照

玩家 join/leave 由应用层定义自己的端口并装配到世界；Host 不提供名为 `IWorldPlayerLifecycle` 的内置 capability。

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

// 4) 创建 world manager + host（需导入 AbilityKit.Ability.Host.Framework）
var worldManager = new WorldManager(factory);
var host = new HostRuntime(worldManager);

// 5) 使用最小 options 创建 world（其余由 blueprint 补齐）
host.CreateWorld(new WorldCreateOptions(new WorldId("room_1"), "battle"));
```

## Host modules（横切关注点）

Host modules 应保持通用性，不能依赖 gameplay。
它们应该安装在 host options 上，而不是放进 gameplay world modules 里。

`HostRuntimeModuleHost` 按添加顺序安装、逆序卸载，不自动补偿安装失败；`WorldHostBuilder` 也不返回模块清理 owner。调用方保留模块、runtime 与同一份 options，负责在退出和部分安装失败时清理，详见使用指南。

示例：
- `ServerFrameTimeModule`
- `ServerRollbackModule`

## 官方网络接入

`com.abilitykit.host` 只定义权威运行时和连接契约，不内定 TCP。官方可选包：

- `com.abilitykit.network.host`：传输无关的 Listener/Channel/Session/Pipeline，附带 TCP 与 InProcess 实现。
- `com.abilitykit.host.network`：将 NetworkHost Session 适配为 `IServerConnection`。

普通项目可以使用 `TcpHostNetwork` 快速装配；需要 WebSocket、KCP、平台 Relay 等传输时，
可从 `IChannelListener` 扩展传输装配；具体监听实现、协议成帧和平台支持仍须分别验收，不能仅凭接口可替换就认定链路已可用。

### Host 模块与 World 模块的区别

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

### 组合根接线

```csharp
var typeRegistry = new WorldTypeRegistry()
    .RegisterEntitasWorld("lobby")
    .RegisterEntitasWorld("battle");

var blueprints = new WorldBlueprintRegistry();
YourGame.WorldBlueprintsRegistration.RegisterAll(blueprints);

IWorldFactory baseFactory = new RegistryWorldFactory(typeRegistry);
IWorldFactory factory = new WorldBlueprintWorldFactory(baseFactory, blueprints);

var worldManager = new WorldManager(factory);
var host = new HostRuntime(worldManager);
```

## 真实示例：MOBA battle/lobby 接入

以下为仓库应用侧的示例路径导航，不是本包 API 或安装依赖；当前项目接线需另行核对：

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
