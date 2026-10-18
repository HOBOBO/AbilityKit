#nullable enable
using System;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Battle.Agent
{
    public readonly struct MobaReliableBattleEventCheckpoint
    {
        public readonly string BattleId;
        public readonly string Epoch;
        public readonly long LastAcknowledgedSequence;

        public MobaReliableBattleEventCheckpoint(
            string battleId,
            string epoch,
            long lastAcknowledgedSequence)
        {
            BattleId = battleId ?? string.Empty;
            Epoch = epoch ?? string.Empty;
            LastAcknowledgedSequence = Math.Max(0L, lastAcknowledgedSequence);
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(BattleId) &&
            !string.IsNullOrWhiteSpace(Epoch);
    }

    public interface IMobaReliableBattleEventCheckpointStore
    {
        bool TryLoad(
            string battleId,
            out MobaReliableBattleEventCheckpoint checkpoint);

        void Save(in MobaReliableBattleEventCheckpoint checkpoint);
    }

    public enum MobaReliableBattleEventBatchStatus
    {
        Accepted = 0,
        DuplicateOnly = 1,
        InvalidBattle = 2,
        InvalidEpoch = 3,
        EpochChanged = 4,
        RetentionGap = 5,
        SequenceGap = 6
    }

    public readonly struct MobaReliableBattleEventBatchResult
    {
        public readonly MobaReliableBattleEventBatchStatus Status;
        public readonly WireReliableBattleEvent[] Events;
        public readonly string Epoch;
        public readonly long CommitSequence;
        public readonly long ExpectedSequence;
        public readonly long ReceivedSequence;

        public bool Accepted =>
            Status == MobaReliableBattleEventBatchStatus.Accepted ||
            Status == MobaReliableBattleEventBatchStatus.DuplicateOnly;

        public bool ShouldRequestFullResync => !Accepted;

        public MobaReliableBattleEventBatchResult(
            MobaReliableBattleEventBatchStatus status,
            WireReliableBattleEvent[] events,
            string epoch,
            long commitSequence,
            long expectedSequence,
            long receivedSequence)
        {
            Status = status;
            Events = events ?? Array.Empty<WireReliableBattleEvent>();
            Epoch = epoch ?? string.Empty;
            CommitSequence = commitSequence;
            ExpectedSequence = expectedSequence;
            ReceivedSequence = receivedSequence;
        }
    }

    /// <summary>
    /// 将 MOBA 线协议适配到通用可靠事件游标，并保留示例原有的公开 API。
    /// 只有所有业务事件均成功消费后，调用方才应提交投递位置。
    /// </summary>
    public sealed class MobaReliableBattleEventCursor :
        IReliableEventDeliveryCursor<WireReliableBattleEvent>,
        IReliableEventCheckpointRestorable
    {
        private static readonly ReliableEventDescriptor<WireReliableBattleEvent> Descriptor =
            new ReliableEventDescriptor<WireReliableBattleEvent>(
                item => item.BattleId,
                item => item.Epoch,
                item => item.Sequence);

        private readonly ReliableEventCursor<WireReliableBattleEvent> _cursor;

        public MobaReliableBattleEventCursor(string battleId)
        {
            _cursor = new ReliableEventCursor<WireReliableBattleEvent>(
                battleId,
                Descriptor,
                new ReliableEventCursorOptions
                {
                    GapPolicy = ReliableEventGapPolicy.Reject,
                    BaselineAcknowledgementPolicy =
                        ReliableEventBaselineAcknowledgementPolicy.PreserveConfirmedWithinWatermark,
                    InferRetentionGapFromFirstAvailableSequence = false
                });
        }

        public string Epoch => _cursor.TimelineId;
        public long LastDeliveredSequence => _cursor.LastDeliveredSequence;
        public long LastAcknowledgedSequence => _cursor.LastAcknowledgedSequence;

        public bool TryRestore(in MobaReliableBattleEventCheckpoint checkpoint)
        {
            var commonCheckpoint = new ReliableEventCheckpoint(
                checkpoint.BattleId,
                checkpoint.Epoch,
                checkpoint.LastAcknowledgedSequence);
            return checkpoint.IsValid && _cursor.TryRestore(in commonCheckpoint);
        }

        bool IReliableEventCheckpointRestorable.TryRestore(
            in ReliableEventCheckpoint checkpoint)
        {
            return _cursor.TryRestore(in checkpoint);
        }

        public MobaReliableBattleEventCheckpoint CreateCheckpoint()
        {
            return new MobaReliableBattleEventCheckpoint(
                _cursor.StreamId,
                _cursor.TimelineId,
                _cursor.LastAcknowledgedSequence);
        }

        public MobaReliableBattleEventBatchResult Admit(
            in WireReliableBattleEventPush push)
        {
            var batch = ToCommonBatch(in push);
            var result = _cursor.Admit(in batch);
            return new MobaReliableBattleEventBatchResult(
                MapStatus(result.Status),
                result.Events,
                result.TimelineId,
                result.CommitSequence,
                result.ExpectedSequence,
                result.ReceivedSequence);
        }

        public bool CommitDelivered(
            string epoch,
            long sequence)
        {
            return _cursor.CommitDelivered(epoch, sequence);
        }

        public bool AdoptAuthoritativeBaseline(string epoch, long eventWatermark)
        {
            return _cursor.AdoptAuthoritativeBaseline(epoch, eventWatermark);
        }

        public bool ConfirmAcknowledged(
            string epoch,
            long acceptedSequence)
        {
            return _cursor.ConfirmAcknowledged(epoch, acceptedSequence);
        }

        public void Reset()
        {
            _cursor.Reset();
        }

        /// <summary>将 MOBA 推送映射为协议无关批次。</summary>
        public static ReliableEventBatch<WireReliableBattleEvent> ToCommonBatch(
            in WireReliableBattleEventPush push)
        {
            return new ReliableEventBatch<WireReliableBattleEvent>(
                push.BattleId,
                push.Epoch,
                push.FirstAvailableSequence,
                push.Watermark,
                push.RetentionGap,
                push.Events);
        }

        ReliableEventBatchResult<WireReliableBattleEvent>
            IReliableEventDeliveryCursor<WireReliableBattleEvent>.Admit(
                in ReliableEventBatch<WireReliableBattleEvent> batch)
        {
            return _cursor.Admit(in batch);
        }

        ReliableEventCheckpoint
            IReliableEventDeliveryCursor<WireReliableBattleEvent>.CreateCheckpoint()
        {
            return _cursor.CreateCheckpoint();
        }

        string IReliableEventDeliveryCursor<WireReliableBattleEvent>.StreamId =>
            _cursor.StreamId;

        string IReliableEventDeliveryCursor<WireReliableBattleEvent>.TimelineId =>
            _cursor.TimelineId;

        long IReliableEventDeliveryCursor<WireReliableBattleEvent>.LastObservedWatermark =>
            _cursor.LastObservedWatermark;

        void IReliableEventDeliveryCursor<WireReliableBattleEvent>.DiscardPending()
        {
            _cursor.DiscardPending();
        }

        private static MobaReliableBattleEventBatchStatus MapStatus(
            ReliableEventBatchStatus status)
        {
            switch (status)
            {
                case ReliableEventBatchStatus.Accepted:
                    return MobaReliableBattleEventBatchStatus.Accepted;
                case ReliableEventBatchStatus.DuplicateOnly:
                    return MobaReliableBattleEventBatchStatus.DuplicateOnly;
                case ReliableEventBatchStatus.InvalidStream:
                    return MobaReliableBattleEventBatchStatus.InvalidBattle;
                case ReliableEventBatchStatus.InvalidTimeline:
                    return MobaReliableBattleEventBatchStatus.InvalidEpoch;
                case ReliableEventBatchStatus.TimelineChanged:
                    return MobaReliableBattleEventBatchStatus.EpochChanged;
                case ReliableEventBatchStatus.RetentionGap:
                    return MobaReliableBattleEventBatchStatus.RetentionGap;
                default:
                    return MobaReliableBattleEventBatchStatus.SequenceGap;
            }
        }
    }
}
