# 应用层端口（ISessionCoordinatorHost + Driver + Gate + Spawn）

源文件：`Runtime/Core/ISessionCoordinatorHost.cs` + `ExistingWorldSessionCoordinatorHost.cs` + `ILogicWorldDriverBridge.cs` + `ILogicWorldDriveGate.cs` + `ISpawnService.cs`

## ISessionCoordinatorHost（应用层必须实现）

```csharp
public interface ISessionCoordinatorHost {
    IWorldHost CreateWorldHost(SessionConfig config);
    void ConfigureWorldCreateOptions(in SessionConfig config, WorldCreateOptions options);
    void RegisterServices(IWorld world, SessionConfig config);
    void LoadConfig(IWorld world, SessionConfig config);
    PlayerSpawnData[] CreatePlayerSpawnData(SessionConfig config);
}

public interface ISessionCoordinatorConfigPolicy {
    void ConfigureSession(ref SessionConfig config);
}
```

注意 5 个方法（**不是 README 的 4 个**），另有 `ISessionCoordinatorConfigPolicy` 可选实现。

## ExistingWorldSessionCoordinatorHost（包内唯一 host 实现）

**用途**：把一个已经存在的 `IWorld` 适配成 `ISessionCoordinatorHost` 契约。适合"world 已被外部创建好"的场景（shooter demo 就是用这个）。

构造：
```csharp
public ExistingWorldSessionCoordinatorHost(
    IWorld world,
    IEnumerable<object>? serviceOverrides = null,
    SessionConfigConfigurator? configureSession = null,
    bool initializeExistingWorld = false);
// 或 params object[] serviceOverrides 重载
```

5 个 host 方法实现极简：
- `CreateWorldHost` → 返回内部 `ExistingWorldHost`
- `ConfigureWorldCreateOptions / RegisterServices / LoadConfig` → 空方法
- `CreatePlayerSpawnData` → `Array.Empty<PlayerSpawnData>()`

出生和服务注入由 `serviceOverrides` / 应用层另外处理。

**不存在 `NewWorldSessionCoordinatorHost` 或任何其他 `*CoordinatorHost` 实现类**——"新建世界"形态由应用层自行实现 `ISessionCoordinatorHost`（moba demo 就是这么做的）。

## ILogicWorldDriverBridge（应用层必须实现）

这才是 README 写的 `IBattleDriverHost` 的当前形态（已重命名 + 扩展）。

```csharp
public interface ILogicWorldDriverBridge {
    int CurrentFrame { get; }
    double LogicTimeSeconds { get; }
    bool IsRunning { get; }
    void Start();
    void Stop();
    void SubmitInputs(PlayerInput[] inputs);
    void AdvanceFrame(float deltaTime);
    SnapshotEntityState[] GetAllEntityStates();   // 注意：SnapshotEntityState[]，不是 EntityState[]
}
```

关键差异（vs 旧 README 的 `IBattleDriverHost`）：
- 方法名 `SetDriverHost` → 实际是 `SetLogicWorldDriver`
- 多了 `AdvanceFrame` / `Start` / `Stop`
- `GetAllEntityStates()` 返回 `SnapshotEntityState[]`（不是 `EntityState[]`）

## ILogicWorldDriveGate（可选，但 moba 强制要求）

```csharp
public interface ILogicWorldDriveGate {
    bool CanDriveLogicWorld(float deltaTime);
}
```

玩法层闸门，决定逻辑世界能否推进一帧。典型实现：moba demo 的 `MobaLogicWorldDriveGate`（`[WorldService(typeof(ILogicWorldDriveGate), WorldLifetime.Scoped)]`），依次校验：deltaTime 有限 → phase 存在 → phase.InGame → runtime 存在 → runtime.Status.IsReadyForBattleLoop → 上次 validation report 不阻断。

## ISpawnService（可选）

```csharp
public interface ISpawnService : IService {   // IService 来自 AbilityKit.Ability.World.Services
    bool CreateSpawns(PlayerSpawnData[] spawns);
}
```

继承 `IService`，由游戏项目实现，把出生数据变为实体。moba demo 实现：`MobaSpawnService`。

若 `SessionConfig.UseCoordinatorSpawnService=true` 且能解析到 `ISpawnService`，coordinator 在 `Start` 时会调它创建出生；若 `UseCoordinatorSpawnService=true` 但找不到 `ISpawnService`，打 warning；若 `UseCoordinatorSpawnService=false`，出生完全由应用层负责（moba demo 选这种方式）。

## 两种接入模式总结

| 模式 | Host 实现 | 适用场景 | 真实案例 |
|------|----------|---------|---------|
| 直接实现 `ISessionCoordinatorHost` | 应用层自写 | world 由会话创建 | moba demo（`MobaSessionCoordinatorHost`） |
| 用 `ExistingWorldSessionCoordinatorHost` 组合 | 包内现成 | world 已被外部创建 | shooter demo（`ShooterCoordinatorSessionHost` 内部持有 `_host`） |

详见 [integration_recipes.md](integration_recipes.md)。
