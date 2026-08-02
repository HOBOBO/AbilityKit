# Moba CreateWorld 数据模型

> ⚠ **MIGRATION (2026-07-31)**：本文档描述的代码已从 `com.abilitykit.host.extension/Runtime/Moba/` 提取至 `com.abilitykit.demo.moba.host/Runtime/Moba/`（version 0.1.0）。所有 API 命名空间和 asmdef 名称未变，仅物理位置与企业级依赖声明变更。


源文件：`Runtime/Moba/Shared/CreateWorld/*.cs` + `Runtime/Moba/Shared/Struct/MobaGameStartSpec.cs`

## MobaHostCreateWorldSpec

```csharp
public sealed class MobaHostCreateWorldSpec {
    public static MobaHostCreateWorldSpec FromRoomSpec(in MobaRoomGameStartSpec roomSpec);
    public ProtocolMoba.CreateMobaWorldReq ToProtocolSpec();
    public (PlayerId, int opCode, byte[] payload) ToEnterReq(...);
}
```

## MobaBattleStartPlan

```csharp
public sealed class MobaBattleStartPlan {
    public static MobaBattleStartPlan FromRoomSpec(in MobaRoomGameStartSpec roomSpec);
    public static MobaBattleStartPlan FromEnterReq(...);
    public (PlayerId, int, byte[]) ToEnterReq(...);
    public byte[] ToCreateWorldInitPayload();
    public WorldInitData ToWorldInitData(int initOpCode);
    public MobaRoomGameStartSpec ToGameStartSpec();
}

public static class MobaBattleStartPlanBuilder {
    // FromHostSpawns 已废弃抛 InvalidOperationException
}
```

## MobaBattleLaunchSpec（3 个枚举）

```csharp
public enum MobaBattleLaunchMode {
    Unspecified, ViewFastEnter, RoomFlow, EtServer, ConsoleSimulation, Replay
}

public enum MobaBattleLaunchSyncMode {
    Unspecified, FrameSync, StateSync, Hybrid, Replay
}

public enum MobaBattleLaunchAuthorityMode {
    Unspecified, LocalAuthority, ServerAuthority, ClientPrediction
}

public sealed class MobaBattleLaunchSpec {
    public static MobaBattleLaunchSpec FromEnterReq(...);
    public static MobaBattleLaunchSpec FromCreateWorldSpec(...);
    public MobaBattleCreateWorldSpec ToCreateWorldSpec();
    public MobaBattleStartPlan ToStartPlan();
}

public readonly struct MobaBattleLaunchProfile { ... }

public static class MobaBattleLaunchSpecBuilder {
    public static ... FromLoadouts(...);
    public static ... FromEnterReq(...);
    public static ... FromStartPlan(...);
}
```

## MobaBattleSimulationLaunchPlan（多实例模拟）

```csharp
public sealed class MobaBattleSimulationLaunchPlan {
    public static MobaBattleSimulationLaunchPlan LocalMultiClient(
        MobaBattleLaunchSpec baseSpec, int count, ...);
}

public sealed class MobaBattleLaunchInstanceSpec {
    public MobaBattleLaunchSpec BuildSpec(int index);
    // 按 index 覆盖 battleId/worldId/clientId/playerId/seed
}
```

用途：Console Demo / 测试场景的多客户端单进程模拟。

## MobaGameStartSpec 等 struct

`Runtime/Moba/Shared/Struct/MobaGameStartSpec.cs`：

```csharp
public sealed class MobaRoomLoadoutOverrides { ... }

public readonly struct MobaHostSpawnData {
    public static MobaHostSpawnData CreateLocalPlayer(...);
}

public readonly struct MobaRoomPlayerSlot {
    public MobaPlayerLoadout ToPlayerLoadout(int spawnIndexFallback);
}

public sealed class MobaRoomGameStartSpec {
    public (PlayerId, int, byte[]) ToEnterReq(PlayerId playerId)
        → EnterMobaGameReq;
}

public static class MobaHostSpawnPlanBuilder {
    // 全部方法已 [Obsolete]，抛 InvalidOperationException
    // 引导改用显式 player loadout
}
```

**注意**：`MobaHostSpawnPlanBuilder` 全部方法已废弃。新代码必须用显式 player loadout（`MobaRoomPlayerSlot.ToPlayerLoadout`）。
