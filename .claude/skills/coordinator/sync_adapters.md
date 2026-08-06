> ⚠ **2026-08-06 整体移除（session 引擎清理）**：整个 `Adapters/` 子树（ISyncAdapter + Local/RemoteSyncAdapter + HybridSyncAdapter[Obsolete] + SyncAdapterFactory）已删除（死代码，无 demo 使用 —— demo 各自实现 `ILogicWorldDriverBridge` 直接驱动 runtime）。本节仅作历史参考。

# SyncAdapter（同步策略适配器）

源文件：`Runtime/Adapters/ISyncAdapter.cs` + `LocalSyncAdapter.cs` + `RemoteSyncAdapter.cs` + `HybridSyncAdapter.cs`（⚠ `[Obsolete]`, D2 已决） + `SyncAdapterFactory.cs`。coordinator 版本 `0.1.0`（Beta，`AbilityKitStable=true`）。

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

### HybridSyncAdapter（📛 `[Obsolete]`，0.1.0 排除，D2 已决）

`SyncMode.Hybrid`。客户端预测 + 服务端权威对账。

**当前状态（2026-07-20 核校）**：4 处 TODO 全部未实现（`SubmitInput` / `Tick` / `Reconcile` / `GetAllEntityStates`），预测循环、对账比对、状态读取均为空壳。

**重要**：MOBA demo 与 Shooter demo 的生产战斗路径**都不经过本类**：
- MOBA 走 `com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule`（框架级 IHostRuntimeModule）+ view.runtime 的 `RemoteDrivenRollbackRegistryFactory` / `RemoteDrivenStateHashFactory` / `RemoteDrivenPredictionContextBinder`
- Shooter 走 `ShooterClientPredictionRuntimeAdapter`

HybridSyncAdapter 已标记 `[Obsolete]`，0.1.0 承诺的同步模式为 Local/Remote only。如需客户端预测，请走 host.extension/ClientPredictionDriverModule (MOBA) 或 ShooterClientPredictionRuntimeAdapter (Shooter)。

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
| Hybrid | 不用 | 不用 | HybridSyncAdapter（**`[Obsolete]`，排除**） |

注：`SessionRuntimePolicy` 的规则是 `HostMode.Local` 强制 `EffectiveSyncMode = Lockstep`，所以本地场景实际只走 LocalSyncAdapter。

## Architecture Decision: MOBA 远程路径绕过 Coordinator（2026-08-01）

MOBA demo 的 `BattleLogicMode.Remote` 路径**刻意绕过** `SessionCoordinator` + `RemoteSyncAdapter`，直接使用 `host.extension` 的 `FrameSyncDriverModule` + `ClientPredictionDriverModule` + `FramePacketNetAdapter`。理由：

1. **双世界架构**：帧同步需要 `RemoteDrivenWorld`（客户端预测）和 `ConfirmedAuthorityWorld`（确认权威）两套并行世界，各自有独立的 `FrameJitterBuffer`。Coordinator 的 `ISyncAdapter.Tick(deltaTime)` 单世界模型无法表达双世界。
2. **Jitter Buffer 集成**：帧输入经过 `FrameJitterBuffer`（FillDefault 模式）而不是 Coordinator 的 `SubmitInput` → `AdvanceFrame` 同步模型。
3. **性能路径**：绕过 Coordinator 额外抽象层减少每帧的虚调用和分配。

**Coordinator 适用场景**：
- 本地单机 / LAN 联机 → `LocalSyncAdapter`
- 简单状态同步（SnapshotAuthority / StateSync）→ `RemoteSyncAdapter` + `IRemoteBattleSyncTransport`
- Shooter demo 的 demo harness 模式 → `ShooterDemoHarnessCarrier`（Coordinator 适配层）

**待 v0.2.0**：若 Coordinator 支持多世界适配 + jitter buffer 抽象，MOBA 可迁回 Coordinator 体系。
`HybridSyncAdapter` 已在 v0.1.0 设为 `[Obsolete(error: true)]`，计划 v0.2.0 移除。
