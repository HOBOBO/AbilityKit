# MobaHostRuntimeBuilder + IMobaBattleRuntimePort

源文件：`Runtime/Moba/Shared/Runtime/MobaHostRuntimeBuilder.cs` + `Runtime/Moba/Shared/Runtime/IMobaBattleRuntimePort.cs`

## MobaHostRuntimeBuilder（静态类）

`Runtime/Moba/Shared/Runtime/MobaHostRuntimeBuilder.cs`，namespace `AbilityKit.Ability.Host.Extensions.Moba.Runtime`。

把 4 个 host module 的安装封装成一次调用。

### MobaHostRuntimeProfile

```csharp
public readonly struct MobaHostRuntimeProfile {
    public readonly bool EnableFrameSync, EnableServerFrameTime, EnableWorldAutoStart, EnableRollback;
    public readonly int RollbackHistoryFrames, RollbackCaptureEveryNFrames;
    public readonly float FixedDeltaSeconds;

    public static MobaHostRuntimeProfile LocalAuthoritative(bool enableRollback);
    // 预设：全开，history=600, captureEvery=30

    public MobaHostRuntimeProfile Normalize();
    // 填默认值（history<=0→600, capture<=0→30）
}
```

### MobaHostRuntimeBuildResult

```csharp
public readonly struct MobaHostRuntimeBuildResult {
    public readonly HostRuntime Runtime;
    public readonly HostRuntimeOptions Options;
    public readonly HostRuntimeModuleHost Modules;
    public readonly ServerRollbackModule RollbackModule;   // 若 EnableRollback=false 则为 null
}
```

### MobaHostRuntimeBuilder 静态方法

```csharp
public static class MobaHostRuntimeBuilder {
    public static MobaHostRuntimeBuildResult CreateRuntime(
        IWorldManager worldManager,
        in MobaHostRuntimeProfile profile,
        Func<IWorld, RollbackRegistry> buildRollbackRegistry);

    public static HostRuntimeModuleHost CreateModules(
        in MobaHostRuntimeProfile profile,
        Func<IWorld, RollbackRegistry> buildRollbackRegistry,
        out ServerRollbackModule rollbackModule);
}
```

### CreateModules 安装顺序

```
1. EnableFrameSync       → modules.Add(new FrameSyncDriverModule())
2. EnableServerFrameTime → modules.Add(new ServerFrameTimeModule(normalized.FixedDeltaSeconds))
3. EnableWorldAutoStart  → modules.Add(new WorldAutoStartModule())
4. EnableRollback        → modules.Add(new ServerRollbackModule(history, captureEvery, buildRollbackRegistry))
```

### 典型用法

```csharp
var worldManager = new WorldManager(new RegistryWorldFactory(registry));
var profile = MobaHostRuntimeProfile.LocalAuthoritative(enableRollback: true);
var build = MobaHostRuntimeBuilder.CreateRuntime(worldManager, in profile, _ => new RollbackRegistry());
// build.Runtime / build.Options / build.Modules / build.RollbackModule
```

## IMobaBattleRuntimePort（战斗运行时端口）

`Runtime/Moba/Shared/Runtime/IMobaBattleRuntimePort.cs`，namespace `AbilityKit.Ability.Host.Extensions.Moba.Runtime`。

### 配套类型

```csharp
public enum MobaGameStartFailureCode {   // 13 码
    None, InvalidSpec, InvalidRoomState, InvalidWorldId, InvalidWorldType,
    InvalidMapId, InvalidPlayers, InvalidSpawns, InvalidConfig,
    RuntimeNotReady, GameAlreadyStarted, UnknownError, ...
}

public readonly struct MobaGameStartResult {
    public bool Success;
    public MobaGameStartFailureCode FailureCode;
    public string FailureMessage;
    public static MobaGameStartResult Succeed();
    public static MobaGameStartResult Fail(MobaGameStartFailureCode code, string message);
}

public enum MobaInputSubmitFailureCode {   // 8 码
    None, InvalidFrame, InvalidInputs, RuntimeNotReady, TooFarAhead, TooFarBehind,
    AlreadySubmitted, UnknownError
}

public readonly struct MobaInputSubmitResult { /* Success/Fail */ }

public readonly struct LogicWorldEntityState {
    // Position/Rotation/Velocity/Hp/TeamId/标志位
}

public readonly struct MobaDiagnosticEntityState {
    // 诊断读模型
}

[Flags]
public enum MobaBattleRuntimeCapability {
    None = 0,
    GameStart = 1,
    Input = 2,
    SnapshotOutput = 4,
    StateReadModel = 8
}

public readonly struct MobaBattleRuntimeStatus {
    public bool IsReadyForBattleLoop { get; }
    public bool IsReadyForGameStart { get; }
}
```

### 接口

```csharp
public interface IMobaGameStartPort : IService {
    MobaGameStartResult TryStartGame(in MobaGameStartSpec spec);
}

public interface IMobaBattleRuntimePort : IService {
    MobaBattleRuntimeStatus Status { get; }
    MobaGameStartResult TryStartGame(in MobaGameStartSpec spec);
    MobaInputSubmitResult Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);
    bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot);

    // 批量收集（推荐）
    void CollectSnapshots(...);
    void GetDiagnosticEntityStates(...);
    void FillDiagnosticEntityStates(...);   // 推荐（避免分配）
    void GetAllEntityStates(...);
    void FillAllEntityStates(...);          // 推荐（避免分配）
}
```

### MobaBattleRuntimeFacadeContract

`static class MobaBattleRuntimeFacadeContract` — 声明聚合端口（`IMobaBattleRuntimePort`）与外部消费者契约，可作为编译期参考。

## 实现提示

`IMobaBattleRuntimePort` 的实际实现在 moba demo 包（`com.abilitykit.demo.moba.runtime`），不在 host.extension。host.extension 只定义契约。
