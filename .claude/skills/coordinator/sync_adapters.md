# SyncAdapter（同步策略适配器）

源文件：`Runtime/Adapters/ISyncAdapter.cs` + `LocalSyncAdapter.cs` + `RemoteSyncAdapter.cs` + `HybridSyncAdapter.cs` + `SyncAdapterFactory.cs`

## ISyncAdapter 基础接口

```csharp
public interface ISyncAdapter {
    SyncMode Mode { get; }
    void Attach(SessionCoordinator coordinator);
    void Attach(SessionCoordinator coordinator, ILogicWorldDriverBridge driverHost);
    void Tick(float deltaTime);
    void SubmitInput(PlayerInput input);
    SnapshotEntityState[] GetAllEntityStates();
    void Detach();
}
```

子接口：
- `ILocalSyncAdapter : ISyncAdapter`
- `IRemoteSyncAdapter : ISyncAdapter`（多了 `Connect(NetworkEndpoint, string roomId, int playerId)` / `Disconnect()`）
- `IPredictionSyncAdapter : ISyncAdapter`

## 三种实现

### LocalSyncAdapter（Lockstep）

`SyncMode.Lockstep`。本地步进：累积时间到 `frameInterval` 时调 `ProcessLogicFrame` → `driverHost.SubmitInputs` / `Start` / `AdvanceFrame`。

**适用**：单机、本地联机、HostMode.Local。

### RemoteSyncAdapter（SnapshotAuthority / StateSync）

`SyncMode.SnapshotAuthority` 或 `SyncMode.StateSync`。远端驱动：通过 `IRemoteBattleSyncTransport` 走网络。

- `Connect(NetworkEndpoint, roomId, playerId)` 转发给 transport
- 收远端快照 → 经 `SessionCoordinator.NotifyEnterGameSnapshot / NotifyActorTransformSnapshot / NotifyDamageSnapshot` 推给 `IViewEventSink`
- 客户端提交本地输入 → 经 transport 上行

### HybridSyncAdapter（Hybrid 客户端预测）

`SyncMode.Hybrid`。客户端预测 + 服务端权威对账。

**警告**：`HybridSyncAdapter` 当前**多处 TODO 未实现**（预测推进、校正逻辑部分为空）。skill 用户若选 Hybrid，必须打开源码看哪部分已完成、哪部分待补，**不要假设它开箱即用**。

## SyncAdapterFactory

```csharp
public sealed class SyncAdapterFactory : ISyncAdapterFactory {
    public static readonly SyncAdapterFactory Default = new SyncAdapterFactory();
    public ISyncAdapter Create(SyncMode mode);
}

public interface ISyncAdapterFactory {
    ISyncAdapter Create(SyncMode mode);
}

public sealed class DefaultSyncAdapterFactory : ISyncAdapterFactory { ... }
```

## SyncMode × HostMode 组合矩阵

| SyncMode | HostMode.Local | HostMode.Host | HostMode.Client |
|----------|---------------|---------------|-----------------|
| Lockstep | LocalSyncAdapter（典型） | LocalSyncAdapter | 不典型 |
| SnapshotAuthority | 不用 | RemoteSyncAdapter（Host 侧） | RemoteSyncAdapter（Client 侧） |
| StateSync | 不用 | RemoteSyncAdapter（Host 侧） | RemoteSyncAdapter（Client 侧） |
| Hybrid | 不用 | 不用 | HybridSyncAdapter（**TODO 未完成**） |

注：`SessionRuntimePolicy` 的规则是 `HostMode.Local` 强制 `EffectiveSyncMode = Lockstep`，所以本地场景实际只走 LocalSyncAdapter。
