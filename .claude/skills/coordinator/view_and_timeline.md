> ⚠ **2026-08-06 整体移除（session 引擎清理）**：`IViewEventSink` + 整个 `Timeline/`（IViewTimeline/ViewTimeline/SampleBuffer）已删除（死代码，无 demo 使用 —— demo 自带 `IBattleViewEventSink`/自己的 ViewTimeline）。本节仅作历史参考。

# ViewEventSink 与 ViewTimeline

源文件：`Runtime/Core/IViewEventSink.cs` + `Runtime/Timeline/IViewTimeline.cs` + `Timeline/ViewTimeline.cs` + `Timeline/ScalarSampleBuffer.cs` + `Timeline/VectorSampleBuffer.cs`

## IViewEventSink（应用层可选实现）

```csharp
public interface IViewEventSink {
    void OnEnterGameSnapshot(in FrameSnapshotData snapshot);
    void OnActorTransformSnapshot(in FrameSnapshotData snapshot);
    void OnDamageEventSnapshot(in FrameSnapshotData snapshot);
    void OnFrameSyncComplete(int frame);
    void OnBattleStart(int frame);
    void OnBattleEnd(int frame, int winTeamId);
    void OnCustomEvent(string eventType, int entityId, byte[] customData);
}
```

**设计原则**：框架只传数据，应用层解释。

adapter / 应用层通过 `SessionCoordinator.Notify*` 推事件给 sink：
- `NotifyEnterGameSnapshot(in FrameSnapshotData)`
- `NotifyActorTransformSnapshot(in FrameSnapshotData)`
- `NotifyDamageSnapshot(in FrameSnapshotData)`
- `NotifyFrameSyncComplete(int frame)`
- `NotifyBattleStart(int frame)`
- `NotifyBattleEnd(int frame, int winTeamId)`
- `NotifyCustomEvent(string eventType, int entityId, byte[] customData)`

## ViewTimeline（位置/旋转采样插值）

```csharp
public interface IViewTimeline {
    void PushTransform(int entityId, in Vec3 position, in Vec3 rotation, double timeSeconds);
    bool TrySample(int entityId, double timeSeconds, out Vec3 position, out Vec3 rotation);
    void ClearEntity(int entityId);
    void Clear();
}

public sealed class ViewTimeline : IViewTimeline { ... }
```

实现细节：
- 环形缓冲容量 **4**（每个 entity 独立缓冲）
- 默认插值回溯 `InterpolationBackTimeSeconds = 0.1s`
- 配套 `ISampleBuffer<T>` + `ScalarSampleBuffer` + `VectorSampleBuffer`

典型用法：adapter 收到快照后 `viewTimeline.PushTransform(entityId, pos, rot, timeSeconds)`，表现层每帧 `viewTimeline.TrySample(entityId, renderTime, out pos, out rot)` 取插值结果。

## SessionHooks 里的视图钩子（README 漏掉）

`SessionHooks` 除常规钩子外，还含 3 个视图钩子（README 未列）：

- `Action? OnViewBinderReady`
- `Action? OnViewsRebound`
- `Action? OnViewFrameAligned`

子功能或应用层可订阅这些钩子，在视图就绪/重绑定/帧对齐时执行副作用。
