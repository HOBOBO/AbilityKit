# GameStartSource 路由体系

源文件：`Runtime/Moba/Shared/StartSources/*.cs`（namespace `AbilityKit.Ability.Host.Extensions.Moba.StartSources`）

## 核心抽象

```csharp
public readonly struct MobaGameStartSourceKey : IEquatable<MobaGameStartSourceKey> {
    public string Value;
    public bool IsValid;
}

public interface IMobaGameStartSource {
    MobaGameStartSourceKey Key { get; }
    int Priority { get; }   // 大者先试
    bool TryBuild(PlayerId localPlayerId, out MobaRoomGameStartSpec spec);
}
```

## MobaGameStartSourceRouter

```csharp
public sealed class MobaGameStartSourceRouter : IMobaGameStartSource {
    public MobaGameStartSourceKey PreferredKey { get; set; }   // 可选偏好
    public MobaGameStartSourceKey Key => new("router");
    public int Priority => int.MinValue;   // router 本身最低

    public void Register(IMobaGameStartSource source);   // 按 Priority 降序 + 注册顺序
    public bool TryBuild(PlayerId localPlayerId, out MobaRoomGameStartSpec spec);
    public bool TryBuild(MobaGameStartSourceKey key, PlayerId localPlayerId, out MobaRoomGameStartSpec spec);
}
```

排序：先 `Priority` 降序，平手按 `RegistrationIndex` 升序。`TryBuild(playerId)` 先试 `PreferredKey`，再按优先级遍历；`TryBuild(key, playerId, ...)` 指定 key 查找。

## 3 个内置 Source（按 Priority 从高到低）

### MatchmakingGameStartSource（Priority=200）

`MatchmakingGameStartSource.cs`，Key = `"matchmaking"`，依赖 `IMobaMatchmakingSpecInbox`。

校验 `localPlayerId` 非空，从 inbox `TryDequeue` 取 spec。

### RoomGameStartSource（Priority=100）

`RoomGameStartSource.cs`，Key = `"room"`，依赖 `IMobaRoomOrchestrator`。

委托 `_room.TryBuildRoomGameStartSpec(out spec)`。

### DungeonPresetGameStartSource（Priority=0）

`DungeonPresetGameStartSource.cs`，Key = `"dungeon-preset"`，依赖 `IMobaDungeonPresetResolver + int dungeonId, int presetId`。

校验 playerId → resolver 解析 preset → 组装单玩家 `MobaRoomPlayerSlot`，matchId 回退 `dungeon_{id}_{pid}`。

## 3 个支持接口 + 内存实现

### IMobaMatchmakingSpecInbox

```csharp
public interface IMobaMatchmakingSpecInbox {
    bool TryDequeue(out MobaRoomGameStartSpec spec);
    void Enqueue(MobaRoomGameStartSpec spec);
}
```

内存实现：`InMemoryMobaMatchmakingSpecInbox`（`Queue<MobaRoomGameStartSpec>`）

### IMobaDungeonPresetResolver

```csharp
public interface IMobaDungeonPresetResolver {
    bool TryResolve(int dungeonId, int presetId, out MobaDungeonPreset preset);
}
```

内存实现：`InMemoryMobaDungeonPresetResolver`（`Dictionary<long, MobaDungeonPreset>`，key = `(dungeonId<<32)|presetId`）

### IMobaPendingGameStartSpecStore

```csharp
public interface IMobaPendingGameStartSpecStore {
    bool HasSpec { get; }
    bool HasPlan { get; }
    void Set(in MobaRoomGameStartSpec spec);
    void Set(in MobaBattleStartPlan plan);
    bool TryGet(out MobaRoomGameStartSpec spec);
    bool TryGetPlan(out MobaBattleStartPlan plan);
    MobaGameStartSpecValidationResult ValidatePendingPlan(...);
    MobaBattleStartPlanValidationResult ValidatePendingSpec(...);
    void Clear();
}
```

配套 `MobaGameStartSpecValidationResult` / `MobaBattleStartPlanValidationResult`（各带 Success/Fail 静态工厂）。

**注意**：本包未提供 `IMobaPendingGameStartSpecStore` 的实现类（仅契约，实现由 moba demo 包提供）。

## MobaDungeonPreset

`MobaDungeonPreset.cs`，14 字段 readonly struct：

```csharp
public readonly struct MobaDungeonPreset {
    public readonly int DungeonId;
    public readonly int PresetId;
    public readonly string MatchId;
    public readonly int MapId;
    public readonly int RandomSeed;
    public readonly int TickRate;
    public readonly int InputDelayFrames;
    public readonly int TeamId;
    public readonly int HeroId;
    public readonly int SpawnPointId;
    public readonly int Level;
    public readonly int AttributeTemplateId;
    public readonly int BasicAttackSkillId;
    public readonly int[] SkillIds;
}
```

## 路由数据流

```
MatchmakingInbox(200) → 匹配成功 spec 出队
        ↓ 否则
Room(100) → 房间就绪时构建
        ↓ 否则
DungeonPreset(0) → 单人副本预设
        ↓
统一输出 MobaRoomGameStartSpec
        ↓
MobaHostCreateWorldSpec.FromRoomSpec / MobaBattleStartPlan.FromRoomSpec 转换
```
