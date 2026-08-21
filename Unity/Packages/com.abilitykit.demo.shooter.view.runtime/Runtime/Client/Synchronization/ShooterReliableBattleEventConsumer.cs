#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterReliableBattleEventConsumer
    {
        private static readonly ReliableEventDescriptor<WireReliableBattleEvent> Descriptor =
            new ReliableEventDescriptor<WireReliableBattleEvent>(
                item => item.BattleId,
                item => item.Epoch,
                item => item.Sequence,
                item => !string.IsNullOrWhiteSpace(item.EventId));

        private readonly ReliableEventCursorOptions _cursorOptions;
        private readonly ReliableEventDeliveryOptions _deliveryOptions;
        private readonly NetworkSyncSessionDescriptor? _syncSession;
        private readonly Action<ReliableEventCheckpoint>? _saveCheckpoint;
        private readonly IReliableEventCheckpointStore? _checkpointStore;
        private readonly ReliableEventCheckpointLifecycleCoordinator _checkpointLifecycle;
        private ReliableEventSession<WireReliableBattleEvent>? _session;
        private ReliableEventCheckpoint? _latestCheckpoint;
        private Action<WireReliableBattleEvent>? _activeSink;
        private List<WireReliableBattleEvent>? _deliveredInCurrentBatch;
        private int _deliveredCountInCurrentBatch;
        private string _baselineBattleId = string.Empty;
        private string _baselineEpoch = string.Empty;

        public ShooterReliableBattleEventConsumer(int maxPendingEvents = 512)
            : this(
                syncSession: null,
                maxPendingEvents,
                saveCheckpoint: null,
                checkpointStore: null,
                lifecycleOptions: null,
                useNegotiatedPolicy: false)
        {
        }

        /// <summary>使用同步会话协商结果创建可靠事件消费者。</summary>
        public ShooterReliableBattleEventConsumer(
            NetworkSyncSessionDescriptor syncSession,
            int maxPendingEvents = 512,
            Action<ReliableEventCheckpoint>? saveCheckpoint = null,
            IReliableEventCheckpointStore? checkpointStore = null,
            ReliableEventCheckpointLifecycleOptions? lifecycleOptions = null)
            : this(
                syncSession ?? throw new ArgumentNullException(nameof(syncSession)),
                maxPendingEvents,
                saveCheckpoint,
                checkpointStore,
                lifecycleOptions,
                true)
        {
        }

        private ShooterReliableBattleEventConsumer(
            NetworkSyncSessionDescriptor? syncSession,
            int maxPendingEvents,
            Action<ReliableEventCheckpoint>? saveCheckpoint = null,
            IReliableEventCheckpointStore? checkpointStore = null,
            ReliableEventCheckpointLifecycleOptions? lifecycleOptions = null,
            bool useNegotiatedPolicy = false)
        {
            _syncSession = useNegotiatedPolicy ? syncSession : null;
            _saveCheckpoint = saveCheckpoint;
            _checkpointStore = useNegotiatedPolicy
                ? checkpointStore ?? new InMemoryReliableEventCheckpointStore()
                : checkpointStore;
            _checkpointLifecycle = new ReliableEventCheckpointLifecycleCoordinator(
                _checkpointStore,
                lifecycleOptions);
            _cursorOptions = new ReliableEventCursorOptions
            {
                MaxPendingEvents = Math.Max(1, maxPendingEvents),
                // 未绑定同步会话时保留旧接入行为；正式会话由 SDK 根据协商结果覆盖策略。
                GapPolicy = useNegotiatedPolicy
                    ? ReliableEventGapPolicy.Reject
                    : ReliableEventGapPolicy.BufferWithinCapacity,
                BaselineAcknowledgementPolicy = useNegotiatedPolicy
                    ? ReliableEventBaselineAcknowledgementPolicy.PreserveConfirmedWithinWatermark
                    : ReliableEventBaselineAcknowledgementPolicy.ConfirmWatermark,
                RequireBaselineAtObservedWatermark = !useNegotiatedPolicy,
                InferRetentionGapFromFirstAvailableSequence = !useNegotiatedPolicy,
                BindTimelineOnAdmission = !useNegotiatedPolicy
            };
            _deliveryOptions = new ReliableEventDeliveryOptions
            {
                // 未绑定同步会话时保留旧接入行为；正式会话由 SDK 根据协商结果覆盖策略。
                AcknowledgementStrategy = useNegotiatedPolicy
                    ? ReliableEventAcknowledgementStrategy.Disabled
                    : ReliableEventAcknowledgementStrategy.External,
                AcknowledgeAuthoritativeBaseline = false,
                InvalidateOnEventSinkFailure = true
            };
        }

        public string BattleId => _session?.StreamId ?? string.Empty;

        public string Epoch => _session?.TimelineId ?? string.Empty;

        public long LastAcknowledgedSequence => _session?.LastAcknowledgedSequence ?? 0L;

        public long LastObservedWatermark => _session?.LastObservedWatermark ?? 0L;

        public bool RequiresResync { get; private set; }

        /// <summary>获取框架最近生成的持久化检查点。</summary>
        public ReliableEventCheckpoint? LatestCheckpoint => _latestCheckpoint;

        /// <summary>获取可靠事件检查点的生命周期诊断。</summary>
        public ReliableEventCheckpointLifecycleDiagnostics CheckpointLifecycleDiagnostics =>
            _checkpointLifecycle.GetDiagnostics();

        /// <summary>检查点生命周期 flush 失败时触发。</summary>
        public event Action<ReliableEventCheckpointLifecycleFailure>? CheckpointLifecycleFailure
        {
            add => _checkpointLifecycle.Failure += value;
            remove => _checkpointLifecycle.Failure -= value;
        }

        /// <summary>等待当前检查点存储中已排队的写入完成。</summary>
        public Task FlushCheckpointStoreAsync(CancellationToken cancellationToken = default)
        {
            return _checkpointLifecycle.FlushAsync(
                ReliableEventCheckpointFlushTrigger.Manual,
                cancellationToken);
        }

        /// <summary>按指定生命周期原因等待检查点写入完成。</summary>
        public Task<ReliableEventCheckpointFlushResult> FlushCheckpointStoreAsync(
            ReliableEventCheckpointFlushTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            return _checkpointLifecycle.FlushAsync(trigger, cancellationToken);
        }

        public ShooterReliableBattleEventConsumeResult Consume(in WireReliableBattleEventPush push)
        {
            return Consume(in push, null, collectCommittedEvents: true);
        }

        /// <summary>
        /// Delivers a batch without materializing the committed-event list. The normal Consume
        /// contract remains available for callers that need to inspect committed events (tests,
        /// replay and tooling), while the live gateway path can avoid one list allocation per push.
        /// </summary>
        public ShooterReliableBattleEventConsumeResult ConsumeAndDispatch(
            in WireReliableBattleEventPush push,
            Action<WireReliableBattleEvent>? eventSink)
        {
            return Consume(in push, eventSink, collectCommittedEvents: false);
        }

        /// <summary>
        /// 先调用业务事件处理器，全部成功后才推进本地可靠事件确认位置。
        /// 处理器失败时保留旧确认位置，并要求通过全量快照重建基线。
        /// </summary>
        public ShooterReliableBattleEventConsumeResult Consume(
            in WireReliableBattleEventPush push,
            Action<WireReliableBattleEvent>? eventSink)
        {
            return Consume(in push, eventSink, collectCommittedEvents: true);
        }

        private ShooterReliableBattleEventConsumeResult Consume(
            in WireReliableBattleEventPush push,
            Action<WireReliableBattleEvent>? eventSink,
            bool collectCommittedEvents)
        {
            var pushBattleId = push.BattleId ?? string.Empty;
            var pushEpoch = push.Epoch ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pushBattleId) || string.IsNullOrWhiteSpace(pushEpoch))
            {
                return MarkGap();
            }

            if (_session == null)
            {
                BeginCursor(pushBattleId);
            }
            else if (!string.Equals(BattleId, pushBattleId, StringComparison.Ordinal) ||
                     (!string.IsNullOrEmpty(Epoch) &&
                      !string.Equals(Epoch, pushEpoch, StringComparison.Ordinal)))
            {
                return MarkGap(pushBattleId, pushEpoch);
            }

            if (RequiresResync)
            {
                return MarkGap(pushBattleId, pushEpoch);
            }

            var batch = new ReliableEventBatch<WireReliableBattleEvent>(
                pushBattleId,
                pushEpoch,
                push.FirstAvailableSequence,
                push.Watermark,
                push.RetentionGap,
                push.Events);
            _baselineBattleId = pushBattleId;
            _baselineEpoch = pushEpoch;
            _activeSink = eventSink;
            _deliveredInCurrentBatch = collectCommittedEvents
                ? new List<WireReliableBattleEvent>()
                : null;
            try
            {
                _session!.Handle(in batch);
                var committed = RequiresResync || !collectCommittedEvents
                    ? (IReadOnlyList<WireReliableBattleEvent>)Array.Empty<WireReliableBattleEvent>()
                    : _deliveredInCurrentBatch ?? (IReadOnlyList<WireReliableBattleEvent>)Array.Empty<WireReliableBattleEvent>();
                return new ShooterReliableBattleEventConsumeResult(
                    committed,
                    LastAcknowledgedSequence,
                    RequiresResync,
                    !RequiresResync && _deliveredCountInCurrentBatch > 0);
            }
            finally
            {
                _activeSink = null;
                _deliveredInCurrentBatch = null;
                _deliveredCountInCurrentBatch = 0;
                if (!RequiresResync)
                {
                    _baselineBattleId = string.Empty;
                    _baselineEpoch = string.Empty;
                }
            }
        }

        public void Invalidate()
        {
            MarkGap();
        }

        public void RestoreCursor(string battleId, string epoch, long lastAcknowledgedSequence)
        {
            var checkpoint = new ReliableEventCheckpoint(
                battleId ?? string.Empty,
                epoch ?? string.Empty,
                lastAcknowledgedSequence);
            _checkpointStore?.Save(in checkpoint);
            BeginCursor(battleId ?? string.Empty, checkpoint);
            RequiresResync = false;
            _baselineBattleId = string.Empty;
            _baselineEpoch = string.Empty;
        }

        public bool TryApplyFullSnapshotBaseline(long eventWatermark)
        {
            if (!RequiresResync
                || string.IsNullOrWhiteSpace(_baselineBattleId)
                || string.IsNullOrWhiteSpace(_baselineEpoch)
                || eventWatermark < LastObservedWatermark)
            {
                return false;
            }

            var baseline = Math.Max(LastAcknowledgedSequence, eventWatermark);
            BeginCursor(_baselineBattleId);
            if (!_session!.AdoptAuthoritativeBaseline(
                    _baselineEpoch,
                    baseline))
            {
                return false;
            }

            RequiresResync = false;
            _baselineBattleId = string.Empty;
            _baselineEpoch = string.Empty;
            return true;
        }

        private void BeginCursor(
            string battleId,
            ReliableEventCheckpoint? initialCheckpoint = null)
        {
            _session?.Dispose();
            _session = new ReliableEventSessionBuilder<WireReliableBattleEvent>(
                new ReliableEventSessionOptions<WireReliableBattleEvent>
                {
                    StreamId = battleId,
                    Descriptor = Descriptor,
                    CursorOptions = _cursorOptions,
                    InitialCheckpoint = initialCheckpoint,
                    CheckpointStore = _checkpointStore,
                    DeliveryOptions = _deliveryOptions,
                    NegotiatedSession = _syncSession,
                    ApplyNegotiatedPolicy = _syncSession != null,
                    EventSink = DeliverToActiveSink,
                    TimelineInvalidated = _ =>
                        MarkGap(_baselineBattleId, _baselineEpoch),
                    SaveCheckpoint = SaveCheckpoint,
                    AwaitAuthoritativeBaseline = false
                }).Build();
        }

        private void SaveCheckpoint(ReliableEventCheckpoint checkpoint)
        {
            _latestCheckpoint = checkpoint;
            _saveCheckpoint?.Invoke(checkpoint);
        }

        private void DeliverToActiveSink(WireReliableBattleEvent item)
        {
            _activeSink?.Invoke(item);
            _deliveredInCurrentBatch?.Add(item);
            _deliveredCountInCurrentBatch++;
        }

        private ShooterReliableBattleEventConsumeResult MarkGap(string? baselineBattleId = null, string? baselineEpoch = null)
        {
            if (!string.IsNullOrWhiteSpace(baselineBattleId) && !string.IsNullOrWhiteSpace(baselineEpoch))
            {
                _baselineBattleId = baselineBattleId;
                _baselineEpoch = baselineEpoch;
            }
            else if (string.IsNullOrWhiteSpace(_baselineBattleId) && !string.IsNullOrWhiteSpace(BattleId) && !string.IsNullOrWhiteSpace(Epoch))
            {
                _baselineBattleId = BattleId;
                _baselineEpoch = Epoch;
            }

            RequiresResync = true;
            _session?.DiscardPending();
            return new ShooterReliableBattleEventConsumeResult(
                Array.Empty<WireReliableBattleEvent>(),
                LastAcknowledgedSequence,
                requiresResync: true,
                hasCommittedEvents: false);
        }
    }

    public readonly struct ShooterReliableBattleEventConsumeResult
    {
        public ShooterReliableBattleEventConsumeResult(
            IReadOnlyList<WireReliableBattleEvent> committedEvents,
            long acknowledgedSequence,
            bool requiresResync,
            bool hasCommittedEvents = false)
        {
            CommittedEvents = committedEvents ?? Array.Empty<WireReliableBattleEvent>();
            AcknowledgedSequence = acknowledgedSequence;
            RequiresResync = requiresResync;
            HasCommittedEvents = hasCommittedEvents || CommittedEvents.Count > 0;
        }

        public IReadOnlyList<WireReliableBattleEvent> CommittedEvents { get; }

        public long AcknowledgedSequence { get; }

        public bool RequiresResync { get; }

        public bool HasCommittedEvents { get; }

        public bool ShouldAcknowledge => !RequiresResync && HasCommittedEvents;
    }
}
