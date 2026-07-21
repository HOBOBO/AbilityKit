# Moba 服务端开战编排

源文件：`Runtime/Moba/Server/StartGame/*.cs`

## MobaServerGameLifecycle（world-scoped service）

`MobaGameStartOrchestrator.cs` 内：

```csharp
public enum MobaServerGameLifecyclePhase {
    Created, StartRequested, Starting, Running, Failed, Stopped
}

public enum MobaServerGameLifecycleErrorCode {   // 6 码
    None, AlreadyRequested, RoomNotReady, SpecInvalid, RuntimeStartFailed, UnknownError
}

public interface IMobaServerGameLifecycle : IService {
    MobaServerGameLifecyclePhase Phase { get; }
    MobaServerGameLifecycleErrorCode LastErrorCode { get; }
    string LastMessage { get; }
    bool CanRequestStart { get; }

    void MarkStartRequested();
    void MarkStarting();
    void MarkRunning();
    void MarkFailed(MobaServerGameLifecycleErrorCode code, string message);
    void MarkStopped();
}

[WorldService(typeof(IMobaServerGameLifecycle), WorldLifetime.Scoped)]
public sealed class MobaServerGameLifecycle : IMobaServerGameLifecycle { ... }
```

## MobaGameStartOrchestrator

```csharp
public interface IMobaGameStartOrchestrator : IService {
    bool TryStartGame(IWorld world);
}

[WorldService(typeof(IMobaGameStartOrchestrator), WorldLifetime.Scoped)]
public sealed class MobaGameStartOrchestrator : IMobaGameStartOrchestrator
{
    public MobaGameStartOrchestrator(
        IMobaRoomOrchestrator room,
        IMobaServerGameLifecycle lifecycle);

    public bool TryStartGame(IWorld world) {
        // 1. CanRequestStart 检查（lifecycle.CanRequestStart）
        // 2. MarkStartRequested
        // 3. room.CanStartGame → 校验房间状态
        // 4. room.TryBuildGameStartSpec(out spec)
        // 5. 解析 IMobaBattleRuntimePort
        // 6. MarkStarting
        // 7. runtime.TryStartGame(in spec)
        // 8. MarkRunning（成功）/ MarkFailed（失败）
    }
}
```

## MobaWorldAutoStartHandler

```csharp
public sealed class MobaWorldAutoStartHandler : IWorldAutoStartHandler {
    public MobaWorldAutoStartHandler(
        IMobaRoomOrchestrator room,
        IMobaGameStartOrchestrator orchestrator);

    public bool TryAutoStart(IWorld world, float deltaTime) {
        // 委托给 orchestrator
        // room.CanStart() 时调 orchestrator.TryStartGame(world)
        // 由 WorldAutoStartModule 每 tick 驱动
    }
}
```

## 完整开战链

```
WorldAutoStartModule.OnPostTick
        ↓
world.Services.Resolve<IWorldAutoStartHandler>()   // 返回 MobaWorldAutoStartHandler
        ↓
MobaWorldAutoStartHandler.TryAutoStart(world, dt)
        ↓
room.CanStart() && lifecycle.CanRequestStart?
        ↓ yes
MobaGameStartOrchestrator.TryStartGame(world)
        ↓
MarkStartRequested → room.TryBuildGameStartSpec → runtime.TryStartGame → MarkRunning
        ↓
WorldAutoStartModule 加入 _completed，不再重试
```

**重要**：`MobaServerGameLifecycle` 和 `MobaGameStartOrchestrator` 都是 `[WorldService(typeof(...), WorldLifetime.Scoped)]`，即 world-scoped DI 服务，**不是 IHostRuntimeModule**。它们由 `WorldAutoStartModule`（这才是 module）的 OnPostTick 间接驱动。
