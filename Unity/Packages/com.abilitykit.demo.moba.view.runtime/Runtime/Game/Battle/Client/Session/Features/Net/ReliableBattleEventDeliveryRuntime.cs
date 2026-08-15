using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleRecoveryTransportOperations
    {
        Task<long> AcknowledgeReliableEventsAsync(string epoch, long sequence);
        Task<bool> RequestFullStateSyncAsync(string reason, int lastAuthoritativeFrame);
        void Disconnect();
    }

    internal sealed class NetworkBattleRecoveryTransportOperations : IBattleRecoveryTransportOperations
    {
        private readonly NetworkTransport _transport;

        internal NetworkBattleRecoveryTransportOperations(NetworkTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public Task<long> AcknowledgeReliableEventsAsync(string epoch, long sequence)
        {
            return _transport.AcknowledgeReliableEventsAsync(epoch, sequence);
        }

        public Task<bool> RequestFullStateSyncAsync(string reason, int lastAuthoritativeFrame)
        {
            return _transport.RequestFullStateSyncAsync(reason, lastAuthoritativeFrame);
        }

        public void Disconnect()
        {
            _transport.Disconnect();
        }
    }

    /// <summary>
    /// 将 MOBA 线协议、日志和恢复原因适配到通用可靠事件交付运行时。
    /// 通用层负责世代隔离、基线队列、提交时序、ACK 重试和检查点保存。
    /// </summary>
    internal sealed class ReliableBattleEventDeliveryRuntime : IDisposable
    {
        private readonly ReliableEventDeliveryOptions _options;
        private ReliableEventSession<WireReliableBattleEvent> _session;

        /// <summary>将 MOBA 领域检查点存储适配为 SDK 通用存储契约。</summary>
        private sealed class CheckpointStoreAdapter : IReliableEventCheckpointStore
        {
            private readonly IMobaReliableBattleEventCheckpointStore _store;

            internal CheckpointStoreAdapter(IMobaReliableBattleEventCheckpointStore store)
            {
                _store = store;
            }

            public bool TryLoad(string streamId, out ReliableEventCheckpoint checkpoint)
            {
                if (_store != null && _store.TryLoad(streamId, out var value) && value.IsValid)
                {
                    checkpoint = new ReliableEventCheckpoint(
                        value.BattleId,
                        value.Epoch,
                        value.LastAcknowledgedSequence);
                    return true;
                }

                checkpoint = default;
                return false;
            }

            public void Save(in ReliableEventCheckpoint checkpoint)
            {
                if (_store == null || !checkpoint.IsValid) return;

                var value = new MobaReliableBattleEventCheckpoint(
                    checkpoint.StreamId,
                    checkpoint.TimelineId,
                    checkpoint.LastAcknowledgedSequence);
                _store.Save(in value);
            }

            public bool Remove(string streamId)
            {
                // 旧 MOBA 存储契约暂未声明删除能力；后续可在领域边界扩展时透传。
                return false;
            }
        }

        internal ReliableBattleEventDeliveryRuntime(Func<int, Task> retryDelay = null)
        {
            _options = new ReliableEventDeliveryOptions
            {
                MaxPendingBatches = 32,
                MaxAcknowledgementAttempts = 3,
                InvalidateOnEventSinkFailure = false,
                AcknowledgementRetryDelay = retryDelay
            };
        }

        internal string Epoch => _session?.TimelineId ?? string.Empty;
        internal long LastAcknowledgedSequence => _session?.LastAcknowledgedSequence ?? 0L;
        internal bool AwaitingBaseline => _session?.AwaitingBaseline ?? false;
        internal int PendingBatchCount => _session?.PendingBatchCount ?? 0;

        internal void BeginGeneration(
            NetworkSyncSessionDescriptor syncSession,
            MobaReliableBattleEventCursor cursor,
            IBattleRecoveryTransportOperations transport,
            IMobaReliableBattleEventCheckpointStore checkpointStore,
            Action<WireReliableBattleEvent> eventSink,
            Action<string> timelineInvalidated)
        {
            if (syncSession == null) throw new ArgumentNullException(nameof(syncSession));
            BeginGenerationCore(
                syncSession,
                cursor,
                transport,
                checkpointStore,
                eventSink,
                timelineInvalidated);
        }

        /// <summary>保留给尚未绑定同步会话描述的兼容接入与既有测试。</summary>
        internal void BeginGeneration(
            MobaReliableBattleEventCursor cursor,
            IBattleRecoveryTransportOperations transport,
            IMobaReliableBattleEventCheckpointStore checkpointStore,
            Action<WireReliableBattleEvent> eventSink,
            Action<string> timelineInvalidated)
        {
            BeginGenerationCore(
                syncSession: null,
                cursor,
                transport,
                checkpointStore,
                eventSink,
                timelineInvalidated);
        }

        private void BeginGenerationCore(
            NetworkSyncSessionDescriptor syncSession,
            MobaReliableBattleEventCursor cursor,
            IBattleRecoveryTransportOperations transport,
            IMobaReliableBattleEventCheckpointStore checkpointStore,
            Action<WireReliableBattleEvent> eventSink,
            Action<string> timelineInvalidated)
        {
            if (cursor == null) throw new ArgumentNullException(nameof(cursor));
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (timelineInvalidated == null) throw new ArgumentNullException(nameof(timelineInvalidated));

            _session?.Dispose();
            IReliableEventCheckpointStore commonCheckpointStore = checkpointStore == null
                ? syncSession == null
                    ? null
                    : new InMemoryReliableEventCheckpointStore()
                : new CheckpointStoreAdapter(checkpointStore);
            _session = new ReliableEventSessionBuilder<WireReliableBattleEvent>(
                new ReliableEventSessionOptions<WireReliableBattleEvent>
                {
                    Cursor = cursor,
                    DeliveryOptions = _options,
                    NegotiatedSession = syncSession,
                    ApplyNegotiatedPolicy = syncSession != null,
                    EventSink = eventSink ?? (_ => { }),
                    TimelineInvalidated = failure =>
                        timelineInvalidated(MapInvalidationReason(in failure)),
                    Acknowledge = transport.AcknowledgeReliableEventsAsync,
                    CheckpointStore = commonCheckpointStore,
                    FailureObserved = LogFailure,
                    AwaitAuthoritativeBaseline = true
                }).Build();
        }

        internal void RequireAuthoritativeBaseline()
        {
            _session?.RequireAuthoritativeBaseline();
        }

        internal void Handle(in WireReliableBattleEventPush push)
        {
            var batch = MobaReliableBattleEventCursor.ToCommonBatch(in push);
            _session?.Handle(in batch);
        }

        internal bool AdoptAuthoritativeBaseline(string epoch, long eventWatermark)
        {
            return _session != null &&
                   _session.AdoptAuthoritativeBaseline(epoch, eventWatermark);
        }

        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
        }

        private static string MapInvalidationReason(
            in ReliableEventDeliveryFailure failure)
        {
            switch (failure.Kind)
            {
                case ReliableEventDeliveryFailureKind.BatchRejected:
                    return $"reliable-events:{MapStatus(failure.BatchStatus)}";
                case ReliableEventDeliveryFailureKind.PendingQueueOverflow:
                    return "reliable-events:pending-queue-overflow";
                case ReliableEventDeliveryFailureKind.CommitRejected:
                    return "reliable-events:commit-rejected";
                case ReliableEventDeliveryFailureKind.EventSinkFailed:
                    return "reliable-events:event-sink-failed";
                default:
                    return "reliable-events:ack-incomplete";
            }
        }

        private static string MapStatus(ReliableEventBatchStatus? status)
        {
            switch (status)
            {
                case ReliableEventBatchStatus.Accepted:
                    return MobaReliableBattleEventBatchStatus.Accepted.ToString();
                case ReliableEventBatchStatus.DuplicateOnly:
                    return MobaReliableBattleEventBatchStatus.DuplicateOnly.ToString();
                case ReliableEventBatchStatus.InvalidStream:
                    return MobaReliableBattleEventBatchStatus.InvalidBattle.ToString();
                case ReliableEventBatchStatus.InvalidTimeline:
                    return MobaReliableBattleEventBatchStatus.InvalidEpoch.ToString();
                case ReliableEventBatchStatus.TimelineChanged:
                    return MobaReliableBattleEventBatchStatus.EpochChanged.ToString();
                case ReliableEventBatchStatus.RetentionGap:
                    return MobaReliableBattleEventBatchStatus.RetentionGap.ToString();
                default:
                    return MobaReliableBattleEventBatchStatus.SequenceGap.ToString();
            }
        }

        private static void LogFailure(ReliableEventDeliveryFailure failure)
        {
            if (failure.Exception != null)
            {
                Log.Exception(
                    failure.Exception,
                    $"[ReliableBattleEventDeliveryRuntime] {failure.Kind}. " +
                    $"epoch={failure.TimelineId} sequence={failure.RequestedSequence} " +
                    $"attempt={failure.Attempt}");
                return;
            }

            if (failure.Kind == ReliableEventDeliveryFailureKind.BatchRejected ||
                failure.Kind == ReliableEventDeliveryFailureKind.AcknowledgementIncomplete)
            {
                Log.Warning(
                    $"[ReliableBattleEventDeliveryRuntime] {failure.Kind}. " +
                    $"status={failure.BatchStatus} epoch={failure.TimelineId} " +
                    $"sequence={failure.RequestedSequence}");
            }
        }
    }
}
