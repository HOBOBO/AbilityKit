# Transport 与 Codec

源文件：`Runtime/Transport/IRemoteBattleSyncTransport.cs` + `CoordinatorInputSubmitBridge.cs` + `Runtime/Data/CoordinatorPayloadCodec.cs` + `Data/PlayerInput.cs` + `Data/EntityState.cs` + `Data/FrameSnapshotData.cs` + `Data/NetworkEndpoint.cs`

## IRemoteBattleSyncTransport

```csharp
public interface IRemoteBattleSyncTransport : IService {
    void Connect(NetworkEndpoint endpoint, string roomId, int playerId);
    void Disconnect();
    void SubmitInput(PlayerInput input);
    event Action<FrameSnapshotData>? FrameSnapshotReceived;
    event Action? Disconnected;
}

public sealed class NullRemoteBattleSyncTransport : IRemoteBattleSyncTransport { ... }   // 空实现
```

由应用层 / 项目侧实现（如 shooter 的 `ShooterGatewayCoordinatorInputTransport`）。

## CoordinatorInputSubmitBridge（异步输入桥，shooter 关键）

`Runtime/Transport/CoordinatorInputSubmitBridge.cs`，泛型 `<TLocalSubmitResult, TRemoteSubmitResult>`。

```csharp
public sealed class CoordinatorInputSubmitBridge<TLocalSubmitResult, TRemoteSubmitResult> {
    public CoordinatorInputSubmitBridge(
        Func<TLocalSubmitResult, TimeSpan, Task<TRemoteSubmitResult>> submitAsync,
        TimeSpan timeout,
        Func<TRemoteSubmitResult, bool>? shouldRequestResync = null);

    public bool TrySubmit(PlayerInput input);
    public Task<TRemoteSubmitResult> SubmitViaCoordinatorAsync(
        SessionCoordinator coordinator,
        TLocalSubmitResult local,
        ...);
}
```

流程（shooter 用例）：
1. 应用层调 `SubmitViaCoordinatorAsync(coordinator, local, ...)`
2. 内部 `_createInput(local)` 生成 `PlayerInput` → `coordinator.SubmitLocalInput(input)`
3. coordinator → `syncAdapter.SubmitInput` → `transport.SubmitInput` → `_submitBridge.TrySubmit(input)`
4. 匹配 pending 的 local → `_submitAsync(local, ...)` 返回远程结果 Task

详见 [integration_recipes.md](integration_recipes.md) 的"模式 B"。

## CoordinatorPayloadCodec

`Runtime/Data/CoordinatorPayloadCodec.cs`，基于 `WireSerializer`（来自 `com.abilitykit.protocol`）。

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class CoordinatorPayloadAttribute : Attribute {
    public int OpCode { get; }
    public CoordinatorPayloadAttribute(int opCode);
}

public static class CoordinatorPayloadCodec {
    public static byte[] Encode<T>(T payload);
    public static T Decode<T>(byte[] data);
    public static bool TryDecode<T>(byte[] data, out T payload);
}
```

应用层用 `[CoordinatorPayload(opCode)]` 标注自定义载荷 struct，codec 用 WireSerializer 编解码。

## PlayerInput（注意：不是 README 写的字典形式）

`Runtime/Data/PlayerInput.cs`：

```csharp
[MemoryPackable]
public readonly struct PlayerInput {
    public readonly int PlayerId;
    public readonly int OpCode;       // 不是 InputType 枚举
    public readonly byte[] Payload;   // 不是 InputPayload 字典

    public static PlayerInput Create<T>(int playerId, int opCode, T payload);
    public static PlayerInput CreateMove(int playerId, in MoveInputPayload payload);
    public static PlayerInput CreateSkill(int playerId, in SkillInputPayload payload);
    public static PlayerInput CreateStop(int playerId);
    public T GetPayload<T>();
}

[MemoryPackable]
public readonly struct MoveInputPayload { /* X/Y 等字段 */ }

[MemoryPackable]
public readonly struct SkillInputPayload { /* SkillId/Slot/Target 等字段 */ }

public static class InputOpCodes {
    public const int Move = 1001;
    public const int Skill = 1002;
    public const int Stop = 1003;
    public const int UseItem = 1004;
    public const int Ping = 1005;
}
```

## EntityState / SnapshotEntityState

```csharp
[MemoryPackable]
public struct EntityState {   // 默认载荷
    public int EntityId;
    public float PosX, PosY, PosZ;
    public float RotY;
    public int Hp;
    // ... 其他字段
}

public readonly struct SnapshotEntityState {   // 信封
    public readonly int EntityId;
    public readonly int OpCode;       // 标识真实载荷类型
    public readonly byte[] Payload;   // MemoryPack 序列化的载荷
    public static SnapshotEntityState Create<T>(int entityId, int opCode, T payload);
    public T GetPayload<T>();
}
```

`ILogicWorldDriverBridge.GetAllEntityStates()` 返回 `SnapshotEntityState[]`（不是 `EntityState[]`），允许不同实体用不同载荷。

## FrameSnapshotData / SnapshotType

```csharp
public readonly struct FrameSnapshotData {
    public readonly int Frame;
    public readonly SnapshotType Type;
    public readonly SnapshotEntityState[] States;
}

public enum SnapshotType {
    EnterGame,
    ActorTransform,
    DamageEvent,
    // ...
}
```

`IViewEventSink.OnEnterGameSnapshot/OnActorTransformSnapshot/OnDamageEventSnapshot` 收到的就是这个结构。

## NetworkEndpoint

```csharp
public readonly struct NetworkEndpoint : IEquatable<NetworkEndpoint> {
    public readonly string Host;
    public readonly int Port;
    public NetworkEndpoint(string host, int port);
    public static NetworkEndpoint Parse(string s);
    public static bool TryParse(string s, out NetworkEndpoint endpoint);
}
```

用于 `SessionConfig.ServerEndpoint` 和 `IRemoteBattleSyncTransport.Connect`。
