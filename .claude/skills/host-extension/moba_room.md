# Moba Room 域

> ⚠ **MIGRATION (2026-07-31)**：本文档描述的代码已从 `com.abilitykit.host.extension/Runtime/Moba/` 提取至 `com.abilitykit.demo.moba.host/Runtime/Moba/`（version 0.1.0）。所有 API 命名空间和 asmdef 名称未变，仅物理位置与企业级依赖声明变更。


源文件：`Runtime/Moba/Shared/Room/*.cs` + `Runtime/Moba/Server/Room/MobaRoomOrchestrator.cs`

## MobaRoomState（核心房间状态）

`Runtime/Moba/Shared/Room/MobaRoomState.cs`

```csharp
public sealed class MobaRoomState {
    public int Revision { get; }              // 乐观锁版本号
    public string MatchId { get; }
    public int MapId { get; }
    public int RandomSeed { get; }
    public int TickRate { get; }
    public int InputDelayFrames { get; }
    public int MinPlayers { get; }
    public int MaxPlayers { get; }

    public void Configure(...);
    public bool TryJoin(...);
    public bool TryLeave(...);
    public bool TrySetReady(...);
    public bool TrySetTeam(...);
    public bool TrySetSpawnPoint(...);
    public bool TryPickHero(...);
    public bool TryGetPlayer(...);
    public bool CanStart();
    public bool TryBuildGameStartSpec(out MobaRoomGameStartSpec spec);
    public bool TryBuildRoomGameStartSpec(out MobaRoomGameStartSpec spec);
    public MobaRoomSnapshot BuildSnapshot();
    public MobaRoomPersistentSnapshot ExportPersistentState();

    public static MobaRoomState RestorePersistentState(MobaRoomPersistentSnapshot snapshot);
}
```

内嵌 `PlayerSlot`（含 `With*` 不可变更新器）、`MobaRoomPersistentSnapshot` / `MobaRoomPersistentPlayer`（持久化/重连恢复）。

## IMobaRoomEvents / IMobaRoomOrchestrator

```csharp
public interface IMobaRoomEvents {
    event Action<MobaRoomChangedArgs> Changed;   // AddChanged / RemoveChanged
}

public interface IMobaRoomOrchestrator : IMobaRoomEvents {
    MobaRoomState State { get; }
    MobaRoomSnapshot Snapshot { get; }

    bool TryJoin(...);
    bool TryLeave(...);
    bool TrySetReady(...);
    bool TryPickHero(...);
    bool TrySetSpawnPoint(...);
    bool TryBuildGameStartSpec(out MobaRoomGameStartSpec spec);
    bool TryBuildRoomGameStartSpec(out MobaRoomGameStartSpec spec);
    MobaRoomCommandResult Apply(in MobaRoomCommand command);   // 命令模式
}
```

## MobaRoomOrchestrator（Server 实现）

`Runtime/Moba/Server/Room/MobaRoomOrchestrator.cs`（**注意**：物理路径在 Server asmdef，命名空间是 Shared 的 `...Moba.Room`）

```csharp
public sealed class MobaRoomOrchestrator : IMobaRoomOrchestrator {
    public MobaRoomOrchestrator(IMobaRoomGameStartSpecBuilder specBuilder);
    // 默认 specBuilder = DefaultMobaRoomGameStartSpecBuilder

    public MobaRoomCommandResult Apply(in MobaRoomCommand command);
}
```

`Apply` 逻辑：
1. `ClientSeq` 幂等去重（同 client 同 seq 已处理过则跳过）
2. `ExpectedRevision` 乐观锁校验（不匹配则返回 `Fail`）
3. switch 处理：Join / Leave / SetReady / PickHero / SetSpawnPoint
4. 失败返回 `MobaRoomCommandResult.Fail(...)`
5. `Snapshot` 懒计算（`_snapshotDirty`）
6. `OnChanged` 触发外部订阅者

## IMobaRoomGameStartSpecBuilder

```csharp
public interface IMobaRoomGameStartSpecBuilder {
    bool TryBuild(MobaRoomState state, out MobaRoomGameStartSpec spec);
}

public sealed class DefaultMobaRoomGameStartSpecBuilder : IMobaRoomGameStartSpecBuilder { ... }
```

默认实现：从 `state.Players` 构建 `MobaRoomPlayerSlot[]`。

## MobaRoomChangedArgs

```csharp
public sealed class MobaRoomChangedArgs {   // 触发 OnChanged 的载荷
    public MobaRoomChangeKind Kind;
    public int PlayerId;
    public int Revision;
    public MobaRoomSnapshot Snapshot;
    // ...
}
```

被 `MobaRoomSyncServerBroadcaster` / `MobaRoomSyncServerDeltaBroadcaster` 订阅，转化为网络消息入 outbox。
