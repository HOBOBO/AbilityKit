# 共享网络诊断接口（P3）

## 新增类型（`network.runtime/Runtime/Network/Runtime/Sync/NetworkDiagnosticsSnapshot.cs`）

### `NetworkDiagnosticsSnapshot`（只读结构体）
统一快照，聚合所有关键网络健康指标：
- `EstimatedRttMs` / `ClockOffsetMs` — 延迟 + 时钟偏移
- `CurrentFrame` / `LastAuthoritativeFrame` / `FrameGap` — 帧进度 + 帧差
- `ResyncCount` / `SnapshotsReceived` / `InputsSubmitted` / `InputsRejected` — 计数
- `ReconnectPhase` — 快重连阶段（`FastReconnectPhase`）
- `RecentHealthEvents` — 最近 `SyncHealthEvent` 列表
- `IsHealthy` — 一键判断

### `INetworkDiagnostics`
```
NetworkDiagnosticsSnapshot GetDiagnostics();
```
实现者：各 demo 的 sync controller。消费者：监控 UI、调试器。

## 设计原则
1. 不替代现有细粒度类型（SyncHealthEvent / InterpolationDiagnostics 等仍是权威来源）
2. 线程安全（值可能略滞后但不抛异常）
3. 零侵入（demo 不实现也能工作——可选诊断接口）

## 用法
```csharp
// 实现
public sealed class MySyncController : INetworkDiagnostics {
    public NetworkDiagnosticsSnapshot GetDiagnostics() => new(...);
}

// 消费
if (syncController is INetworkDiagnostics diag) {
    var snap = diag.GetDiagnostics();
    label.text = snap.IsHealthy ? "Healthy" : "Degraded";
}
```
