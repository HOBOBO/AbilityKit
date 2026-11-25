using System.Collections.Immutable;

namespace AbilityKit.Orleans.Contracts.Battle;

/// <summary>
/// 状态同步观察者 Grain 接口
/// 桥接 BattleLogicHostGrain 和 Gateway
/// </summary>
public interface IStateSyncObserverGrain : IGrainWithStringKey
{
    /// <summary>
    /// 绑定当前 Gateway 进程中的推送观察者。
    /// 后续绑定会替换旧绑定，bindingId 用于防止旧连接清理新连接的观察者。
    /// </summary>
    Task BindGatewayPushObserverAsync(
        string bindingId,
        IStateSyncGatewayPushObserver observer,
        Guid connectionGeneration);

    /// <summary>
    /// 仅当 bindingId 仍是当前绑定时解除 Gateway 推送观察者。
    /// </summary>
    Task UnbindGatewayPushObserverAsync(string bindingId);

    /// <summary>
    /// 订阅战斗状态同步，并从客户端可靠事件累计确认位置恢复。
    /// </summary>
    Task SubscribeAsync(string battleGrainKey, ReliableBattleEventSubscribeCursor eventCursor);

    /// <summary>
    /// 为该观察者订阅显式固定基线快照，保持可靠事件游标契约不变。
    /// </summary>
    Task SubscribeFixedBaselineAsync(string battleGrainKey, ReliableBattleEventSubscribeCursor eventCursor,
        StateSyncPreparedRecipient? recipient = null);

    /// <summary>
    /// 取消订阅
    /// </summary>
    Task UnsubscribeAsync(string battleGrainKey);

    /// <summary>
    /// 序列化并接收战斗状态快照，返回本观察者队列的接纳结果。
    /// </summary>
    Task<StateSyncDeliveryResult> OnSnapshotPushedAsync(StateSyncPush push);

    Task<StateSyncDeliveryResult> OnPreparedFixedBaselineSnapshotPushedAsync(PreparedFixedBaselineSnapshot snapshot);

    /// <summary>
    /// 将可靠事件批次加入独立 FIFO 投递队列。
    /// </summary>
    Task OnReliableEventsPushedAsync(ReliableBattleEventBatch batch);

    /// <summary>
    /// 转发客户端累计 ACK 到当前战斗。
    /// </summary>
    Task<ReliableBattleEventAckResult> AcknowledgeReliableEventsAsync(string battleGrainKey, string epoch, long sequence);

    /// <summary>
    /// 获取本观察者的累计投递指标与实时队列状态。
    /// </summary>
    Task<StateSyncDeliveryMetrics> GetDeliveryMetricsAsync();
}

[GenerateSerializer, Immutable]
public sealed record PreparedFixedBaselineSnapshot(
    [property: Id(0)] int Frame,
    [property: Id(1)] bool IsFullSnapshot,
    [property: Id(2)] string NetworkEnvironmentId,
    [property: Id(3)] ImmutableArray<byte> Payload)
{
    [Id(4)] public FixedBaselineSnapshotRevision Revision { get; init; }
    [Id(5)] public StateSyncPreparedRecipient? Recipient { get; init; }
}

/// <summary>
/// 服务端成功发布的固定基线版本，不表示客户端已导入或确认了输入。
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct FixedBaselineSnapshotRevision(
    [property: Id(0)] ulong WorldId,
    [property: Id(1)] Guid StreamGeneration,
    [property: Id(2)] long SnapshotId,
    [property: Id(3)] long BaseSnapshotId);

/// <summary>
/// 单次固定基线订阅的接收身份。连接代际不等于客户端输入代际。
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct StateSyncPreparedRecipient(
    [property: Id(0)] string BindingId,
    [property: Id(1)] Guid ConnectionGeneration,
    [property: Id(2)] Guid SubscriptionGeneration = default,
    [property: Id(3)] Guid InputGeneration = default,
    [property: Id(4)] uint PlayerId = 0,
    [property: Id(5)] bool ReceivesMovementProof = false);

/// <summary>
/// Gateway 进程内的状态同步推送观察者，通过 Orleans object reference 暴露给 Silo。
/// </summary>
public interface IStateSyncGatewayPushObserver : IGrainObserver
{
    void OnPush(uint opCode, byte[] payload);
}

/// <summary>
/// StateSyncObserver 的受控生命周期诊断接口。
/// 该接口只用于运维与故障验证，不承诺 activation 内存态透明恢复。
/// </summary>
public interface IStateSyncObserverLifecycleGrain : IGrainWithStringKey
{
    /// <summary>
    /// 获取当前 activation 的稳定标识。
    /// </summary>
    Task<StateSyncObserverActivationInfo> GetActivationInfoAsync();

    /// <summary>
    /// 请求在当前调用完成后停用 activation。
    /// </summary>
    Task<StateSyncObserverActivationInfo> RequestDeactivationAsync();
}

[GenerateSerializer]
public sealed class StateSyncObserverActivationInfo
{
    [Id(0)] public string ObserverKey { get; set; } = string.Empty;
    [Id(1)] public string ActivationToken { get; set; } = string.Empty;
    [Id(2)] public long ActivatedAtUtcTicks { get; set; }
}

public enum StateSyncDeliveryStatus
{
    Accepted = 0,
    Queued = 1,
    DroppedStale = 2,
    Backpressured = 3,
    Offline = 4,
    Failed = 5
}

[GenerateSerializer]
public sealed class StateSyncDeliveryResult
{
    [Id(0)] public StateSyncDeliveryStatus Status { get; set; }
    [Id(1)] public int QueueLength { get; set; }
    [Id(2)] public int DroppedItems { get; set; }
    [Id(3)] public long DroppedBytes { get; set; }
    [Id(4)] public bool BaselineInvalidated { get; set; }
}

[GenerateSerializer]
public sealed class StateSyncDeliveryMetrics
{
    [Id(0)] public long ProducedBytes { get; set; }
    [Id(1)] public long SentBytes { get; set; }
    [Id(2)] public long DroppedBytes { get; set; }
    [Id(3)] public long MergedBytes { get; set; }
    [Id(4)] public int QueueLength { get; set; }
    [Id(5)] public long QueueAgeTicks { get; set; }
    [Id(6)] public long BaselineAgeTicks { get; set; }
    [Id(7)] public long ResyncCount { get; set; }
}

[GenerateSerializer]
public sealed class StateSyncObserverInfo
{
    [Id(0)] public string ObserverKey { get; set; } = string.Empty;
    [Id(1)] public string AccountId { get; set; } = string.Empty;
    [Id(2)] public string RoomId { get; set; } = string.Empty;
    [Id(3)] public bool UsesFixedBaseline { get; set; }
    [Id(4)] public StateSyncPreparedRecipient? PreparedRecipient { get; set; }
}
