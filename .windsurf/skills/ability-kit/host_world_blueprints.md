---
name: host_world_blueprints
version: 1
---

# Host / WorldBlueprints：接入与排查（Skill）

## 0. 什么时候用

当你需要：
- 在不同玩法（`WorldType`）之间复用同一套 Host 运行时
- 让 world 的装配（modules/services/extensions）不散落在各个创建调用点
- 在本地模拟 / 服务器 / 内存传输三种场景保持一致的 world 创建行为

就应该用 `WorldBlueprintRegistry + WorldBlueprintWorldFactory`。

## 1. 关键对象速查

- `WorldTypeRegistry`
  - 负责把 `WorldType` 映射到具体 world runtime 实现（例如 EntitasWorld）
- `RegistryWorldFactory`
  - 根据 `WorldTypeRegistry` 创建 world
- `IWorldBlueprint`
  - 绑定某个 `WorldType`，负责填充/修正 `WorldCreateOptions`
- `WorldBlueprintRegistry`
  - 注册 `IWorldBlueprint`
- `WorldBlueprintWorldFactory`
  - 包装底层 `IWorldFactory`，在创建前应用 blueprint

## 2. 推荐接线（Composition Root）

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

之后创建 world 只需要：

```csharp
host.CreateWorld(new WorldCreateOptions(new WorldId("room_1"), "battle"));
```

## 3. Blueprint 内应该做什么

建议每个 `WorldType` 都有一个单独的 blueprint（便于维护/变更审计）。

在 `Configure(WorldCreateOptions options)` 中通常做：
- `options.ServiceBuilder ??= ...`（补齐默认 ServiceBuilder）
- `options.Modules.Add(...)`（加 world modules）
- `options.Extensions[...] = ...`（例如 Entitas contexts factory）

### 常见例子：Entitas

如果 world 是 Entitas runtime：
- 必须设置 `options.SetEntitasContextsFactory(...)`
- 并确保必要的 world modules 被加入（例如你的 bootstrap module 会再添加 `EntitasEcsWorldModule` 等）

## 4. 显式注册（推荐）

不要在 Host 中做扫描。推荐在应用包里提供单一入口：

```csharp
public static class WorldBlueprintsRegistration
{
    public static void RegisterAll(WorldBlueprintRegistry registry)
    {
        registry
            .Register(new LobbyWorldBlueprint())
            .Register(new BattleWorldBlueprint());
    }
}
```

## 5. 常见报错与排查路径

### 5.1 `EntitasWorld` 报错：缺少 EntitasContextsFactory

错误特征：
- 抛出类似：`EntitasContextsFactory is required. Set it via WorldCreateOptions.SetEntitasContextsFactory(...)`

排查：
- 检查对应 `WorldType` 的 blueprint 是否调用了 `SetEntitasContextsFactory(...)`
- 检查创建路径是否真的走了 `WorldBlueprintWorldFactory`（有没有绕过）

### 5.2 编译报错：找不到 `AbilityKit.Ability.Host.WorldBlueprints`

常见根因：
- asmdef 没引用 `AbilityKit.Host`
- Unity 生成的 `*.csproj` 显式 include 没包含 `WorldBlueprints/*.cs`

排查：
- 检查 `com.abilitykit.host` 的 asmdef 是否被依赖方引用
- 检查 `AbilityKit.Host.csproj` 是否包含 `Runtime/Host/WorldBlueprints/*.cs`

### 5.3 世界行为不一致（本地能跑，服务器不行 / 反之）

常见根因：
- 一条创建路径使用 blueprint，另一条路径手写装配

排查：
- 搜索 `new WorldCreateOptions(` 的调用点
- 确保所有创建 world 的入口统一走 `WorldBlueprintWorldFactory`

## 6. 维护建议

- `WorldType` 常量建议集中定义（例如 `public const string Type = "battle";`），避免散落字符串。
- blueprint 应尽量“只做装配”，不要在里面写玩法规则。
- 若 bootstrap module 过大，优先拆分为：
  - `LobbyWorldBootstrapModule`
  - `BattleWorldBootstrapModule`
  再由对应 blueprint 引用。
