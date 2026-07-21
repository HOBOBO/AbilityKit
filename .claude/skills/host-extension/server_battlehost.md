# Server BattleHost 子系统

源文件：`Runtime/Server/BattleHost/*.cs`（namespace `AbilityKit.Ability.Host.Extensions.Server.BattleHost`）

## 核心类清单

### BattleHostState

`BattleHostState.cs`

```csharp
public sealed class BattleHostState {
    public ulong WorldId { get; }
    public string BattleId { get; }
    public int TickRate { get; }
    public int Frame { get; }
    public bool Initialized { get; }
    public void Initialize(ulong worldId, string battleId, int tickRate);
    public void AdvanceFrame();
    public void Reset();
}
```

### BattleHostLifecycleRunner（9 委托编排器）

`BattleHostLifecycleRunner.cs`，构造接收 9 个委托：`createHost / resolveRuntime / validateRuntimeStart / startRuntime / resolveSnapshotProvider / publishInitialSnapshot / startTimer / cleanup` + 一个配置项。

```csharp
public sealed class BattleHostLifecycleRunner {
    public BattleHostLifecycleResult Start(BattleHostStartContext context);
    public BattleHostLifecycleResult Stop();
}
```

- `BattleHostStartContext`：含 `TickInterval` 属性
- `BattleHostLifecycleContext`：内部状态
- `BattleHostLifecycleResult`：`Success / Fail(errorCode, message)` 静态工厂
- `BattleHostLifecycleErrorCode`（10 码）：None / AlreadyStarted / CreateHostFailed / RuntimeNotResolved / RuntimeNotReadyForStart / StartRuntimeRejected / SnapshotProviderNotResolved / TimerStartFailed / StopFailed / InvalidContext

### BattleTickDriver<TInput>

```csharp
public sealed class BattleTickDriver<TInput> : IBattleTickDriver<TInput> {
    public BattleTickResult Tick(BattleHostState state, IBattleInputBuffer<TInput> inputBuffer);
}

public delegate void BattleInputSubmitter<TInput>(FrameIndex frame, IReadOnlyList<TInput> inputs);
public delegate void BattleWorldTicker(float deltaTime);

public interface IBattleTickDriver<TInput> { ... }
```

### BattleInputBuffer<TInput>

```csharp
public sealed class BattleInputBuffer<TInput> : IBattleInputBuffer<TInput> {
    public void Enqueue(FrameIndex frame, TInput input);
    public BattleInputDrainResult Drain(int frame);
    public void ClearFrame(int frame);
    public void ClearBefore(int frame);
    public void Clear();
}

public interface IBattleInputBuffer<TInput> { ... }
```

### BattleInputFrameScheduler（静态）

```csharp
public static class BattleInputFrameScheduler {
    public static BattleInputFrameScheduleResult Schedule(
        int requestedFrame, int currentFrame, int inputDelayFrames, Options options);
}

public enum BattleInputAcceptStatus {
    Accepted, RemappedTooEarly, RemappedLate, RejectedInvalidFrame, RejectedTooFarFuture
}
```

### BattleSnapshotPublisher<TObserver, TSnapshot>

```csharp
public sealed class BattleSnapshotPublisher<TObserver, TSnapshot> {
    public void Publish(...);                   // 全体观察者
    public void PublishPerObserver(...);        // per-observer（如 AOI 裁剪）
    public void PublishTo(TObserver observer, ...);
}

public delegate TSnapshot BattleSnapshotFactory(FrameIndex frame);
public delegate TSnapshot BattleObserverSnapshotFactory(FrameIndex frame, TObserver observer);
public delegate void BattleSnapshotSender(TObserver observer, TSnapshot snapshot);
public delegate void BattleSnapshotPublishErrorHandler(Exception ex, TObserver observer);
```

### BattleSnapshotSyncPolicy

```csharp
public sealed class BattleSnapshotSyncPolicy {
    public bool ShouldPublish(int observerCount, bool worldTicked);
    public bool ShouldCreateFullSnapshot(int frame);
    public int FullSnapshotInterval { get; set; }   // 默认 30
}
```

### BattleObserverRegistry<TObserver>

```csharp
public sealed class BattleObserverRegistry<TObserver> {
    public void Subscribe(TObserver observer);
    public void Unsubscribe(TObserver observer);
    public TObserver[] Snapshot();
    public void Clear();
}
```

## 典型用法

```
var state = new BattleHostState();
state.Initialize(worldId, battleId, tickRate);

var inputBuffer = new BattleInputBuffer<PlayerInputCommand>();
var tickDriver = new BattleTickDriver<PlayerInputCommand>(submitter, worldTicker);
var snapshotPublisher = new BattleSnapshotPublisher<TObserver, TSnapshot>(...);
var observerRegistry = new BattleObserverRegistry<TObserver>();

var runner = new BattleHostLifecycleRunner(
    createHost: ...,
    resolveRuntime: ...,
    validateRuntimeStart: ...,
    startRuntime: ...,
    resolveSnapshotProvider: ...,
    publishInitialSnapshot: ...,
    startTimer: ...,
    cleanup: ...);

var startResult = runner.Start(new BattleHostStartContext { TickInterval = ... });
if (!startResult.Success) { /* handle startResult.ErrorCode */ }

// 主循环
while (running) {
    var tickResult = tickDriver.Tick(state, inputBuffer);
    if (snapshotSync.ShouldPublish(observerRegistry.Snapshot().Length, tickResult.WorldTicked)) {
        snapshotPublisher.PublishPerObserver(...);
    }
    state.AdvanceFrame();
}

var stopResult = runner.Stop();
```
