using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Protocol.Room;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Grains.Battle;

/// <summary>
/// 状态同步 Observer Grain
/// 桥接 BattleLogicHostGrain 和 Gateway，负责向客户端推送状态快照
/// </summary>
public sealed class StateSyncObserverGrain : Grain, IStateSyncObserverGrain, IStateSyncObserverLifecycleGrain
{
    internal const int ReliableEventQueueCapacity = 512;

    private readonly ILogger<StateSyncObserverGrain> _logger;
    private readonly StateSyncObserverRuntimeSettings _runtimeSettings;
    private readonly StateSyncObserverSubscriptionState _subscriptionState = new();
    private readonly StateSyncBaselineRefreshState _baselineRefresh = new();
    private readonly StateSyncGatewayPushBinding _gatewayPushBinding = new();
    private readonly Queue<OutboundReliableEvents> _reliableEventQueue = new();
    private SnapshotSendQueue<OutboundSnapshot>? _sendQueue;
    private string _queueNetworkEnvironmentId = string.Empty;
    private long _nextDeliveryPerformanceLogUtcTicks;
    private IDisposable? _drainTimer;
    private string _observerKey = string.Empty;
    private string _activationToken = string.Empty;
    private long _activatedAtUtcTicks;
    private string _accountId = string.Empty;
    private string _roomId = string.Empty;

    public StateSyncObserverGrain(
        ILogger<StateSyncObserverGrain> logger,
        IOptions<StateSyncObserverOptions> options)
    {
        _logger = logger;
        _runtimeSettings = StateSyncObserverOptionsMapper.Map(options.Value);
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var key = this.GetPrimaryKeyString();
        _observerKey = key;
        _activationToken = Guid.NewGuid().ToString("N");
        _activatedAtUtcTicks = DateTime.UtcNow.Ticks;
        _sendQueue = CreateSendQueue(_activatedAtUtcTicks, networkEnvironmentId: null);
        _drainTimer = RegisterTimer(
            _ => DrainQueueAsync(),
            state: null,
            dueTime: _runtimeSettings.DrainInterval,
            period: _runtimeSettings.DrainInterval);
        _logger.LogInformation(
            "[StateSyncObserver] Activated with key: {Key}, ActivationToken: {ActivationToken}",
            key,
            _activationToken);

        // key 格式: "accountId:roomId"
        var parts = key.Split(':');
        if (parts.Length >= 2)
        {
            _accountId = parts[0];
            _roomId = parts[1];
        }

        return Task.CompletedTask;
    }

    public Task BindGatewayPushObserverAsync(
        string bindingId,
        IStateSyncGatewayPushObserver observer)
    {
        _gatewayPushBinding.Bind(bindingId, observer);
        return Task.CompletedTask;
    }

    public Task UnbindGatewayPushObserverAsync(string bindingId)
    {
        _gatewayPushBinding.Unbind(bindingId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 订阅战斗状态同步
    /// </summary>
    public async Task SubscribeAsync(string battleGrainKey, ReliableBattleEventSubscribeCursor eventCursor)
    {
        eventCursor ??= new ReliableBattleEventSubscribeCursor();
        var battleGrain = GrainFactory.GetGrain<IBattleLogicHostGrain>(battleGrainKey);
        var decision = _subscriptionState.DecideSubscribe(battleGrainKey);
        _logger.LogInformation(
            "[StateSyncObserver] Forwarding subscription to battle host. ObserverKey: {ObserverKey}, BattleKey: {BattleKey}, Action: {Action}",
            _observerKey,
            battleGrainKey,
            decision.Action);
        if (decision.Action == StateSyncObserverSubscriptionAction.RefreshFullSnapshot)
        {
            ResetDeliveryState();
            _logger.LogInformation(
                "[StateSyncObserver] Duplicate subscription refreshed full snapshot and reliable event replay. Battle: {BattleKey}, Account: {AccountId}",
                battleGrainKey,
                _accountId);
            await battleGrain.SubscribeAsync(this, CreateObserverInfo(), eventCursor);
            return;
        }

        if (decision.Action == StateSyncObserverSubscriptionAction.SwitchBattle)
        {
            await UnsubscribeBattleAsync(decision.PreviousBattleKey, "switch battle subscription", logFailureAsWarning: false);
            battleGrain = GrainFactory.GetGrain<IBattleLogicHostGrain>(battleGrainKey);
        }

        ResetDeliveryState();
        await battleGrain.SubscribeAsync(this, CreateObserverInfo(), eventCursor);
        _logger.LogInformation(
            "[StateSyncObserver] Battle host subscription returned. ObserverKey: {ObserverKey}, BattleKey: {BattleKey}",
            _observerKey,
            battleGrainKey);
        _subscriptionState.MarkSubscribed(battleGrainKey);

        _logger.LogInformation(
            "[StateSyncObserver] Subscribed to battle: {BattleKey}, Account: {AccountId}",
            battleGrainKey, _accountId);
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    public Task UnsubscribeAsync(string battleGrainKey)
    {
        return UnsubscribeBattleAsync(battleGrainKey, "explicit unsubscribe", logFailureAsWarning: true);
    }

    /// <summary>
    /// 接收 BattleLogicHostGrain 的状态快照推送。
    /// </summary>
    public Task<StateSyncDeliveryResult> OnSnapshotPushedAsync(StateSyncPush push)
    {
        try
        {
            var wire = ToWireSnapshotPush(push);
            var payload = WireRoomGatewayBinary.Serialize(in wire).ToArray();
            var opCode = push.IsFullSnapshot
                ? RoomGatewayOpCodes.SnapshotPushed
                : RoomGatewayOpCodes.DeltaSnapshotPushed;
            var priority = push.IsFullSnapshot
                ? SnapshotDeliveryPriority.FullBaseline
                : SnapshotDeliveryPriority.Delta;
            var nowTicks = DateTime.UtcNow.Ticks;
            var outbound = new OutboundSnapshot(opCode, payload);
            var item = new SnapshotSendQueueItem<OutboundSnapshot>(
                outbound,
                push.Frame,
                payload.Length,
                priority,
                replaceable: !push.IsFullSnapshot,
                producedAtTicks: nowTicks);
            var result = GetSendQueue(nowTicks, push.NetworkEnvironmentId).Enqueue(in item, nowTicks);
            if (result.BaselineInvalidated)
            {
                _baselineRefresh.Request();
            }

            return Task.FromResult(ToDeliveryResult(in result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StateSyncObserver] Error serializing snapshot for account: {AccountId}", _accountId);
            return Task.FromResult(new StateSyncDeliveryResult
            {
                Status = StateSyncDeliveryStatus.Failed
            });
        }
    }

    public Task OnReliableEventsPushedAsync(ReliableBattleEventBatch batch)
    {
        if (batch == null)
        {
            return Task.CompletedTask;
        }

        var wire = ToWireReliableEventPush(batch);
        var payload = WireRoomGatewayBinary.Serialize(in wire).ToArray();
        var outbound = new OutboundReliableEvents(payload, batch.Watermark, batch.RetentionGap);
        EnqueueBoundedReliableEvent(
            _reliableEventQueue,
            outbound,
            ReliableEventQueueCapacity,
            () => CreateReliableEventGap(batch),
            static item => item.RetentionGap);
        return Task.CompletedTask;
    }

    public Task<ReliableBattleEventAckResult> AcknowledgeReliableEventsAsync(string battleGrainKey, string epoch, long sequence)
    {
        if (!_subscriptionState.IsSubscribed
            || !string.Equals(_subscriptionState.CurrentBattleKey, battleGrainKey, StringComparison.Ordinal))
        {
            return Task.FromResult(new ReliableBattleEventAckResult
            {
                Accepted = false,
                Epoch = epoch ?? string.Empty,
                RequiresResync = true
            });
        }

        var battle = GrainFactory.GetGrain<IBattleLogicHostGrain>(battleGrainKey);
        return battle.AcknowledgeReliableEventsAsync(_observerKey, epoch, sequence);
    }

    public Task<StateSyncDeliveryMetrics> GetDeliveryMetricsAsync()
    {
        var metrics = GetSendQueue(DateTime.UtcNow.Ticks).CreateMetrics(DateTime.UtcNow.Ticks);
        return Task.FromResult(new StateSyncDeliveryMetrics
        {
            ProducedBytes = metrics.ProducedBytes,
            SentBytes = metrics.SentBytes,
            DroppedBytes = metrics.DroppedBytes,
            MergedBytes = metrics.MergedBytes,
            QueueLength = metrics.QueueLength,
            QueueAgeTicks = metrics.QueueAgeTicks,
            BaselineAgeTicks = metrics.BaselineAgeTicks,
            ResyncCount = metrics.ResyncCount
        });
    }

    public Task<StateSyncObserverActivationInfo> GetActivationInfoAsync()
    {
        return Task.FromResult(CreateActivationInfo());
    }

    public Task<StateSyncObserverActivationInfo> RequestDeactivationAsync()
    {
        var activation = CreateActivationInfo();
        _logger.LogInformation(
            "[StateSyncObserver] Controlled deactivation requested. ObserverKey: {ObserverKey}, ActivationToken: {ActivationToken}",
            _observerKey,
            _activationToken);
        DeactivateOnIdle();
        return Task.FromResult(activation);
    }

    private StateSyncObserverActivationInfo CreateActivationInfo()
    {
        return new StateSyncObserverActivationInfo
        {
            ObserverKey = _observerKey,
            ActivationToken = _activationToken,
            ActivatedAtUtcTicks = _activatedAtUtcTicks
        };
    }

    private StateSyncObserverInfo CreateObserverInfo()
    {
        return new StateSyncObserverInfo
        {
            ObserverKey = _observerKey,
            AccountId = _accountId,
            RoomId = _roomId
        };
    }

    private async Task DrainQueueAsync()
    {
        LogDeliveryPerformanceIfDue(DateTime.UtcNow.Ticks);
        if (TryPeekReliableEvent(_reliableEventQueue, out var reliable))
        {
            try
            {
                var success = await PushToGatewayAsync(
                    ReliableBattleEventGatewayOpCodes.EventsPushed,
                    reliable.Payload);
                if (success)
                {
                    CompleteReliableEventDelivery(_reliableEventQueue);
                }
                else
                {
                    _logger.LogWarning(
                        "[StateSyncObserver] Reliable event push target is offline. Account: {AccountId}, Watermark: {Watermark}",
                        _accountId,
                        reliable.Watermark);
                    await UnsubscribeCurrentBattleAsync("reliable event push target offline");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[StateSyncObserver] Reliable event delivery failed and remains queued. Account: {AccountId}, Watermark: {Watermark}",
                    _accountId,
                    reliable.Watermark);
                return;
            }
        }

        if (_subscriptionState.IsSubscribed && _baselineRefresh.TryBegin())
        {
            var succeeded = false;
            try
            {
                var battle = GrainFactory.GetGrain<IBattleLogicHostGrain>(_subscriptionState.CurrentBattleKey);
                await battle.RequestFullSnapshotAsync(this, CreateObserverInfo());
                succeeded = true;
            }
            finally
            {
                _baselineRefresh.Complete(succeeded);
            }
        }

        var nowTicks = DateTime.UtcNow.Ticks;
        var queue = GetSendQueue(nowTicks);
        var dequeued = TryDequeueAndDetectBaselineInvalidation(
            queue,
            nowTicks,
            out var item,
            out var baselineInvalidated);
        if (baselineInvalidated)
        {
            _baselineRefresh.Request();
        }
        if (!dequeued)
        {
            return;
        }

        try
        {
            var success = await PushToGatewayAsync(
                item.Value.OpCode,
                item.Value.Payload);
            if (success)
            {
                queue.MarkSent(in item, DateTime.UtcNow.Ticks);
                return;
            }

            queue.MarkDeliveryFailed(in item, baselineInvalidated: true);
            _logger.LogWarning(
                "[StateSyncObserver] Snapshot push target is offline. Account: {AccountId}, Frame: {Frame}",
                _accountId,
                item.Frame);
            ResetDeliveryState();
            await UnsubscribeCurrentBattleAsync("gateway push target offline");
        }
        catch (Exception ex)
        {
            queue.MarkDeliveryFailed(in item, baselineInvalidated: true);
            _baselineRefresh.Request();
            _logger.LogError(
                ex,
                "[StateSyncObserver] Snapshot delivery failed. Account: {AccountId}, Frame: {Frame}",
                _accountId,
                item.Frame);
        }
    }

    private Task<bool> PushToGatewayAsync(uint opCode, byte[] payload)
    {
        if (_gatewayPushBinding.TryPush(opCode, payload))
        {
            return Task.FromResult(true);
        }

        if (_gatewayPushBinding.UsesGatewayPushObserver)
        {
            return Task.FromResult(false);
        }

        var gatewayPush = GrainFactory.GetGrain<IGatewayPushTargetGrain>(_accountId);
        return gatewayPush.PushToAccountAsync(_accountId, opCode, payload);
    }

    internal static void EnqueueBoundedReliableEvent<T>(
        Queue<T> queue,
        T item,
        int capacity,
        Func<T> createGapItem,
        Func<T, bool> isGapItem)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(createGapItem);
        ArgumentNullException.ThrowIfNull(isGapItem);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        if (queue.Any(isGapItem))
        {
            return;
        }

        if (queue.Count < capacity)
        {
            queue.Enqueue(item);
            return;
        }

        queue.Clear();
        queue.Enqueue(createGapItem());
    }

    internal static bool TryPeekReliableEvent<T>(Queue<T> queue, out T item)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return queue.TryPeek(out item!);
    }

    internal static void CompleteReliableEventDelivery<T>(Queue<T> queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        queue.Dequeue();
    }

    internal static bool TryDequeueAndDetectBaselineInvalidation<T>(
        SnapshotSendQueue<T> queue,
        long nowTicks,
        out SnapshotSendQueueItem<T> item,
        out bool baselineInvalidated)
    {
        var resyncCountBeforeDrain = queue.CreateMetrics(nowTicks).ResyncCount;
        var dequeued = queue.TryDequeue(nowTicks, out item);
        baselineInvalidated = queue.CreateMetrics(nowTicks).ResyncCount > resyncCountBeforeDrain;
        return dequeued;
    }

    private static WireStateSyncSnapshotPush ToWireSnapshotPush(StateSyncPush push)
    {
        var source = push.Actors;
        var actors = source == null || source.Count == 0
            ? null
            : new List<WireStateSyncActorSnapshot>(source.Count);

        if (actors != null && source != null)
        {
            foreach (var actor in source)
            {
                actors.Add(new WireStateSyncActorSnapshot
                {
                    ActorId = actor.ActorId,
                    X = actor.X,
                    Y = actor.Y,
                    Z = actor.Z,
                    Rotation = actor.Rotation,
                    VelocityX = actor.VelocityX,
                    VelocityZ = actor.VelocityZ,
                    Hp = actor.Hp,
                    HpMax = actor.HpMax,
                    TeamId = actor.TeamId,
                    Kind = actor.Kind,
                    Code = actor.Code,
                    OwnerNetId = actor.OwnerNetId
                });
            }
        }

        return new WireStateSyncSnapshotPush
        {
            WorldId = push.WorldId,
            Frame = push.Frame,
            Timestamp = push.Timestamp,
            IsFullSnapshot = push.IsFullSnapshot,
            Actors = actors,
            PayloadOpCode = push.PayloadOpCode,
            Payload = push.Payload,
            ServerTicks = push.ServerTicks,
            EventWatermark = push.EventWatermark,
            SchemaVersion = push.SchemaVersion,
            RemovedActorIds = push.RemovedActorIds == null || push.RemovedActorIds.Count == 0
                ? null
                : new List<int>(push.RemovedActorIds),
            EventEpoch = push.EventEpoch ?? string.Empty
        };
    }

    private static OutboundReliableEvents CreateReliableEventGap(ReliableBattleEventBatch batch)
    {
        var gap = new ReliableBattleEventBatch
        {
            BattleId = batch.BattleId,
            Epoch = batch.Epoch,
            FirstAvailableSequence = batch.FirstAvailableSequence,
            Watermark = batch.Watermark,
            RetentionGap = true
        };
        var wire = ToWireReliableEventPush(gap);
        var payload = WireRoomGatewayBinary.Serialize(in wire).ToArray();
        return new OutboundReliableEvents(payload, gap.Watermark, RetentionGap: true);
    }

    private static WireReliableBattleEventPush ToWireReliableEventPush(ReliableBattleEventBatch batch)
    {
        var events = new List<WireReliableBattleEvent>(batch.Events.Count);
        foreach (var item in batch.Events)
        {
            events.Add(new WireReliableBattleEvent
            {
                EventId = item.EventId,
                BattleId = item.BattleId,
                Epoch = item.Epoch,
                Sequence = item.Sequence,
                SourceFrame = item.SourceFrame,
                EventType = item.EventType,
                Payload = item.Payload
            });
        }

        return new WireReliableBattleEventPush
        {
            BattleId = batch.BattleId,
            Epoch = batch.Epoch,
            FirstAvailableSequence = batch.FirstAvailableSequence,
            Watermark = batch.Watermark,
            RetentionGap = batch.RetentionGap,
            Events = events
        };
    }

    private Task UnsubscribeCurrentBattleAsync(string reason)
    {
        return UnsubscribeBattleAsync(_subscriptionState.CurrentBattleKey, reason, logFailureAsWarning: false);
    }

    private async Task UnsubscribeBattleAsync(string battleGrainKey, string reason, bool logFailureAsWarning)
    {
        if (!_subscriptionState.IsSubscribed || string.IsNullOrWhiteSpace(battleGrainKey))
        {
            return;
        }

        try
        {
            var battleGrain = GrainFactory.GetGrain<IBattleLogicHostGrain>(battleGrainKey);
            await battleGrain.UnsubscribeAsync(this);
            _logger.LogInformation(
                "[StateSyncObserver] Unsubscribed from battle: {BattleKey}, Reason: {Reason}",
                battleGrainKey,
                reason);
        }
        catch (Exception ex) when (!logFailureAsWarning)
        {
            _logger.LogDebug(
                ex,
                "[StateSyncObserver] Ignored unsubscribe failure. BattleKey: {BattleKey}, Reason: {Reason}",
                battleGrainKey,
                reason);
        }
        finally
        {
            _subscriptionState.Clear();
            ResetDeliveryState();
        }
    }

    private SnapshotSendQueue<OutboundSnapshot> GetSendQueue(long nowTicks)
    {
        return GetSendQueue(nowTicks, networkEnvironmentId: null);
    }

    private SnapshotSendQueue<OutboundSnapshot> GetSendQueue(long nowTicks, string? networkEnvironmentId)
    {
        var normalizedEnvironment = networkEnvironmentId?.Trim() ?? string.Empty;
        if (_sendQueue == null
            || (normalizedEnvironment.Length > 0
                && !string.Equals(_queueNetworkEnvironmentId, normalizedEnvironment, StringComparison.OrdinalIgnoreCase)))
        {
            _queueNetworkEnvironmentId = normalizedEnvironment;
            _sendQueue = CreateSendQueue(nowTicks, normalizedEnvironment);
        }

        return _sendQueue;
    }

    private SnapshotSendQueue<OutboundSnapshot> CreateSendQueue(long nowTicks, string? networkEnvironmentId)
    {
        var policy = StateSyncObserverOptionsMapper.ResolveQueuePolicy(
            _runtimeSettings.QueuePolicy,
            networkEnvironmentId);
        return new SnapshotSendQueue<OutboundSnapshot>(policy, nowTicks);
    }

    private void LogDeliveryPerformanceIfDue(long nowTicks)
    {
        if (nowTicks < _nextDeliveryPerformanceLogUtcTicks)
        {
            return;
        }

        _nextDeliveryPerformanceLogUtcTicks = nowTicks + TimeSpan.TicksPerSecond * 5;
        var metrics = GetSendQueue(nowTicks).CreateMetrics(nowTicks);
        _logger.LogInformation(
            "[StateSyncObserver] DeliveryPerformance Observer={ObserverKey} Account={AccountId} Environment={Environment} ProducedBytes={ProducedBytes} SentBytes={SentBytes} DroppedBytes={DroppedBytes} MergedBytes={MergedBytes} QueueLength={QueueLength} QueueAgeMs={QueueAgeMs:F1} BaselineAgeMs={BaselineAgeMs:F1} ResyncCount={ResyncCount}",
            _observerKey,
            _accountId,
            string.IsNullOrWhiteSpace(_queueNetworkEnvironmentId) ? "default" : _queueNetworkEnvironmentId,
            metrics.ProducedBytes,
            metrics.SentBytes,
            metrics.DroppedBytes,
            metrics.MergedBytes,
            metrics.QueueLength,
            TimeSpan.FromTicks(metrics.QueueAgeTicks).TotalMilliseconds,
            TimeSpan.FromTicks(metrics.BaselineAgeTicks).TotalMilliseconds,
            metrics.ResyncCount);
    }

    private void ResetDeliveryState()
    {
        _sendQueue?.Clear();
        _reliableEventQueue.Clear();
        _baselineRefresh.Clear();
    }

    private static StateSyncDeliveryResult ToDeliveryResult(in SnapshotSendQueueResult result)
    {
        return new StateSyncDeliveryResult
        {
            Status = (StateSyncDeliveryStatus)(int)result.Status,
            QueueLength = result.QueueLength,
            DroppedItems = result.DroppedItems,
            DroppedBytes = result.DroppedBytes,
            BaselineInvalidated = result.BaselineInvalidated
        };
    }

    internal sealed class StateSyncGatewayPushBinding
    {
        private string _bindingId = string.Empty;
        private IStateSyncGatewayPushObserver? _observer;

        public bool UsesGatewayPushObserver { get; private set; }

        public void Bind(string bindingId, IStateSyncGatewayPushObserver observer)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
            ArgumentNullException.ThrowIfNull(observer);

            _bindingId = bindingId;
            _observer = observer;
            UsesGatewayPushObserver = true;
        }

        public void Unbind(string bindingId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
            if (!string.Equals(_bindingId, bindingId, StringComparison.Ordinal))
            {
                return;
            }

            _bindingId = string.Empty;
            _observer = null;
        }

        public bool TryPush(uint opCode, byte[] payload)
        {
            if (_observer == null)
            {
                return false;
            }

            _observer.OnPush(opCode, payload);
            return true;
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _drainTimer?.Dispose();
        _drainTimer = null;
        await UnsubscribeCurrentBattleAsync($"deactivate: {reason}");
        ResetDeliveryState();

        _logger.LogInformation("[StateSyncObserver] Deactivating: {Reason}", reason);
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    private readonly record struct OutboundSnapshot(uint OpCode, byte[] Payload);

    private readonly record struct OutboundReliableEvents(byte[] Payload, long Watermark, bool RetentionGap);
}
