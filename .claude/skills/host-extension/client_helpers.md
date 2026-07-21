# 客户端辅助原语

源文件：`Runtime/Client/FrameSync/*.cs` + `Runtime/Client/StateSync/RemoteClientInputSubmitQueue.cs` + `Runtime/Session/FramePacketNetAdapter.cs`

> 这些类**不是** `IHostRuntimeModule`，是客户端同步路径的 generic primitives，被 `ConfirmedAuthorityWorldRuntimeFactory`（moba）/ shooter demo 的 client sync adapter 使用。

## ClientPredictionInputHistory<TInput>

`Runtime/Client/FrameSync/ClientPredictionInputHistory.cs`

```csharp
public struct ClientPredictionReplayResult { /* 12 字段 */ }

public sealed class ClientPredictionInputHistory<TInput> {
    public void Record(FrameIndex frame, TInput input);
    public void TrimBefore(FrameIndex frame);
    public void SubmitFrame(FrameIndex frame, TInput input);
    public ClientPredictionReplayResult ReplayTo(FrameIndex targetFrame);
    public void Clear();
}
```

记录客户端预测时本地提交的输入序列，reconcile 时用于回放重演。

## ClientPredictionReconciliationCoordinator<TInput>

`Runtime/Client/FrameSync/ClientPredictionReconciliationCoordinator.cs`

```csharp
public struct ClientPredictionReconciliationResult { /* 12 字段 */ }

public sealed class ClientPredictionReconciliationCoordinator<TInput> {
    public ClientPredictionReconciliationCoordinator(...);

    public void RecordLocalInput(FrameIndex frame, TInput input);
    public ClientPredictionReconciliationResult ReconcileAfterAuthoritativeSnapshot(
        FrameIndex authoritativeFrame,
        IReadOnlyList<TInput> authoritativeInputs);
}
```

权威快照到达后，对比本地历史输入与权威输入，输出 reconciliation 结果（是否发散、从哪帧开始重演）。

## RemoteClientInputSubmitQueue<TLocalSubmitResult, TRemoteSubmitResult>

`Runtime/Client/StateSync/RemoteClientInputSubmitQueue.cs`，**状态同步客户端提交队列**。

```csharp
public sealed class RemoteClientInputSubmitQueue<TLocalSubmitResult, TRemoteSubmitResult> {
    public RemoteClientInputSubmitQueue(
        Func<TLocalSubmitResult, TimeSpan, Task<TRemoteSubmitResult>> submitAsync,
        TimeSpan timeout,
        Func<TRemoteSubmitResult, bool>? shouldRequestResync = null);

    public bool SubmitOrQueue(TLocalSubmitResult local);   // at-most-one in-flight + one queued
    public bool CompleteIfFinished();
    public void Reset();
}
```

统计：`SubmittedCount / QueuedCount / ReplacedCount / CompletedCount / FailedCount / ResyncRequestedCount`

用途：状态同步客户端向服务端提交输入请求（带 resync 触发判定）。shooter demo 用到。

## FramePacketNetAdapter

`Runtime/Session/FramePacketNetAdapter.cs`

```csharp
public interface IFramePacketNetAdapterContext {
    // 暴露 RemoteDriven 和 Confirmed 两套 world
    // 暴露输入 source/sink/consumable
    FrameSnapshotDispatcher Snapshots { get; }
}

public sealed class FramePacketNetAdapter {
    public void ProcessAndFeed(FramePacket packet);
    public void ProcessAndFeed(FramePacket packet, ...);
    public void ProcessAndFeed(...);
}
```

把网络帧路由到 jitter buffer 并 feed snapshots。`moba view.runtime` 的网络层用这个。
