# CatchUp 子系统

源文件：`Runtime/FrameSync/CatchUp/Shared/*.cs` + `Runtime/Server/FrameSync/CatchUp/InMemoryFrameSyncInputHistory.cs` + `Runtime/Client/FrameSync/CatchUp/IFrameSyncCatchUpSink.cs`

## 用途

补帧/快照补传：客户端落后时，服务端根据 CatchUp 策略决定是补发输入序列还是发全量快照。

## FrameSyncCatchUpPolicy（决策器）

`Runtime/FrameSync/CatchUp/Shared/FrameSyncCatchUpPolicy.cs`

```csharp
public enum FrameSyncCatchUpDecisionKind { None, SendInputs, SendSnapshot }

public readonly struct FrameSyncCatchUpDecision {
    public FrameSyncCatchUpDecisionKind Kind;
    public static readonly FrameSyncCatchUpDecision None = ...;
    public static FrameSyncCatchUpDecision SendSnapshot(...);
    public static FrameSyncCatchUpDecision SendInputs(...);
}

public readonly struct FrameSyncCatchUpPolicyOptions {
    public int MaxCatchUpFrames;   // 默认 600
    public int MaxBatchFrames;     // 默认 120
    public int SafetyMargin;       // 默认 2
    public static readonly FrameSyncCatchUpPolicyOptions Default;
}

public static class FrameSyncCatchUpPolicy {
    public static FrameSyncCatchUpDecision Decide(
        in FrameSyncCatchUpRequest request,
        in FrameSyncCatchUpPolicyOptions options);
}
```

## FrameSyncCatchUpRequest / Payload

`Runtime/FrameSync/CatchUp/Shared/FrameSyncCatchUpTypes.cs`

```csharp
public readonly struct FrameSyncCatchUpRequest {
    public WorldId WorldId;
    public FrameIndex FromFrameExclusive;
    public FrameIndex ToFrameInclusive;
}

public readonly struct FrameSyncCatchUpPayload {
    public WorldId WorldId;
    public FrameIndex StartFrame;
    public PlayerInputCommand[][] Inputs;
}
```

## FrameSyncCatchUpMessages

`Runtime/FrameSync/CatchUp/Shared/FrameSyncCatchUpMessages.cs`

```csharp
public enum FrameSyncCatchUpMessageKind { ... }

public sealed class FrameSyncCatchUpRequestMessage {
    public FrameSyncCatchUpRequest FromFrames(...);   // 构造
}

public sealed class FrameSyncCatchUpPayloadMessage { ... }
```

## IFrameSyncInputHistory（输入历史端口）

```csharp
public interface IFrameSyncInputHistory {
    bool TryBuildCatchUp(in FrameSyncCatchUpRequest request, out FrameSyncCatchUpPayload payload);
    void Append(WorldId worldId, FrameIndex frame, PlayerInputCommand[] inputs);
    void TrimBefore(WorldId worldId, FrameIndex frame);
}
```

**服务端内存实现**：`Runtime/Server/FrameSync/CatchUp/InMemoryFrameSyncInputHistory.cs`（`SortedDictionary<int, PlayerInputCommand[]>` per world）

## WorldCatchUpDriver（静态）

`Runtime/FrameSync/FrameSyncDriverModule.cs` 内联 `static class WorldCatchUpDriver`：

```csharp
public static class WorldCatchUpDriver {
    public static void CatchUpAndFeedSnapshots(...);
}
```

负责应用 CatchUp payload + 喂快照给 jitter buffer。

> 注：`Runtime/FrameSync/WorldCatchUpDriver.cs` 还有 `internal static class WorldCatchUpDriverInternal`（与 public 版本重复实现，internal 版本）。

## 客户端：WorldStartFrameCatchUpCalculator

`Runtime/Client/FrameSync/WorldStartFrameCatchUpCalculator.cs`，静态计算器。

```csharp
public readonly struct WorldStartFrameAnchor {
    public bool IsValid;
    public static readonly WorldStartFrameAnchor Invalid;
}

public readonly struct WorldFrameCatchUpResult { ... }

public static class WorldStartFrameCatchUpCalculator {
    public static WorldFrameCatchUpResult Calculate(...);
    public static WorldFrameCatchUpResult CalculateFromSnapshotFrame(...);
}
```

## 客户端：RemoteTimeAnchorProjector

`Runtime/Client/FrameSync/RemoteTimeAnchorProjector.cs`

```csharp
public readonly struct RemoteTimeAnchorProjection { ... }

public static class RemoteTimeAnchorProjector {
    public static RemoteTimeAnchorProjection Project(in WorldStartFrameAnchor anchor, long serverNowTicks);
}
```

输出 `SyncTimeAnchor`，供 time sync 使用。

## 客户端：IFrameSyncCatchUpSink

`Runtime/Client/FrameSync/CatchUp/IFrameSyncCatchUpSink.cs`

```csharp
public interface IFrameSyncCatchUpSink {
    void ApplyCatchUp(in FrameSyncCatchUpPayload payload);
}
```

客户端实现此接口以应用服务端补发的 CatchUp payload。
