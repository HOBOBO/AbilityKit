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

### HybridSyncAdapter（Hybrid 客户端预测，**当前未完成且不在 demo 主路径**）

`SyncMode.Hybrid`。客户端预测 + 服务端权威对账。

**当前状态（2026-07-20 核校）**：4 处 TODO 全部未实现（`SubmitInput` / `Tick` / `Reconcile` / `GetAllEntityStates`），预测循环、对账比对、状态读取均为空壳。

**重要**：MOBA demo 与 Shooter demo 的生产战斗路径**都不经过本类**：
- MOBA 走 `com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule`（框架级 IHostRuntimeModule）+ view.runtime 的 `RemoteDrivenRollbackRegistryFactory` / `RemoteDrivenStateHashFactory` / `RemoteDrivenPredictionContextBinder`
- Shooter 走 `ShooterClientPredictionRuntimeAdapter`

也就是说 coordinator 的整套 `ISyncAdapter` 体系（Local/Remote/Hybrid）目前是**自闭环通用框架**，没有 demo 实际接入。补全 HybridSyncAdapter 是通用框架未来工作，**不阻塞演示级联机**。补全前应先确认 coordinator 通用会话框架有实际接入方。

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
