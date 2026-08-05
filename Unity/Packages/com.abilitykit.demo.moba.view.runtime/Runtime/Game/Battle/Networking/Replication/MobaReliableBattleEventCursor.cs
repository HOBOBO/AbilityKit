#nullable enable
using System;
using System.Collections.Generic;
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
    /// Tracks reliable battle-event delivery separately from server-confirmed ACK state.
    /// Delivery is committed only after every exposed event has been consumed successfully.
    /// </summary>
    public sealed class MobaReliableBattleEventCursor
    {
        private static readonly WireReliableBattleEvent[] EmptyEvents =
            Array.Empty<WireReliableBattleEvent>();

        private readonly string _battleId;
        private string _epoch = string.Empty;
        private long _lastDeliveredSequence;
        private long _lastAcknowledgedSequence;

        public MobaReliableBattleEventCursor(string battleId)
        {
            _battleId = battleId ?? string.Empty;
        }

        public string Epoch => _epoch;
        public long LastDeliveredSequence => _lastDeliveredSequence;
        public long LastAcknowledgedSequence => _lastAcknowledgedSequence;

        public bool TryRestore(in MobaReliableBattleEventCheckpoint checkpoint)
        {
            if (!checkpoint.IsValid ||
                checkpoint.LastAcknowledgedSequence < 0 ||
                (!string.IsNullOrEmpty(_battleId) &&
                 !string.Equals(
                     _battleId,
                     checkpoint.BattleId,
                     StringComparison.Ordinal)))
            {
                return false;
            }

            _epoch = checkpoint.Epoch;
            _lastDeliveredSequence = checkpoint.LastAcknowledgedSequence;
            _lastAcknowledgedSequence = checkpoint.LastAcknowledgedSequence;
            return true;
        }

        public MobaReliableBattleEventCheckpoint CreateCheckpoint()
        {
            return new MobaReliableBattleEventCheckpoint(
                _battleId,
                _epoch,
                _lastAcknowledgedSequence);
        }

        public MobaReliableBattleEventBatchResult Admit(
            in WireReliableBattleEventPush push)
        {
            var pushBattleId = push.BattleId ?? string.Empty;
            var pushEpoch = push.Epoch ?? string.Empty;

            if (!string.IsNullOrEmpty(_battleId) &&
                !string.Equals(_battleId, pushBattleId, StringComparison.Ordinal))
            {
                return Reject(
                    MobaReliableBattleEventBatchStatus.InvalidBattle,
                    pushEpoch,
                    _lastDeliveredSequence + 1,
                    0);
            }

            if (string.IsNullOrWhiteSpace(pushEpoch))
            {
                return Reject(
                    MobaReliableBattleEventBatchStatus.InvalidEpoch,
                    pushEpoch,
                    _lastDeliveredSequence + 1,
                    0);
            }

            if (!string.IsNullOrEmpty(_epoch) &&
                !string.Equals(_epoch, pushEpoch, StringComparison.Ordinal))
            {
                return Reject(
                    MobaReliableBattleEventBatchStatus.EpochChanged,
                    pushEpoch,
                    _lastDeliveredSequence + 1,
                    0);
            }

            if (push.RetentionGap)
            {
                return Reject(
                    MobaReliableBattleEventBatchStatus.RetentionGap,
                    pushEpoch,
                    _lastDeliveredSequence + 1,
                    push.FirstAvailableSequence);
            }

            var source = push.Events;
            if (source == null || source.Count == 0)
            {
                return new MobaReliableBattleEventBatchResult(
                    MobaReliableBattleEventBatchStatus.DuplicateOnly,
                    EmptyEvents,
                    pushEpoch,
                    _lastDeliveredSequence,
                    _lastDeliveredSequence + 1,
                    0);
            }

            var expected = _lastDeliveredSequence + 1;
            List<WireReliableBattleEvent>? deliverable = null;
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var itemEpoch = item.Epoch ?? string.Empty;
                var itemBattleId = item.BattleId ?? string.Empty;
                if (!string.Equals(itemEpoch, pushEpoch, StringComparison.Ordinal))
                {
                    return Reject(
                        MobaReliableBattleEventBatchStatus.EpochChanged,
                        pushEpoch,
                        expected,
                        item.Sequence);
                }

                if (!string.IsNullOrEmpty(_battleId) &&
                    !string.Equals(itemBattleId, _battleId, StringComparison.Ordinal))
                {
                    return Reject(
                        MobaReliableBattleEventBatchStatus.InvalidBattle,
                        pushEpoch,
                        expected,
                        item.Sequence);
                }

                if (item.Sequence <= _lastDeliveredSequence)
                {
                    continue;
                }

                if (item.Sequence != expected)
                {
                    return Reject(
                        MobaReliableBattleEventBatchStatus.SequenceGap,
                        pushEpoch,
                        expected,
                        item.Sequence);
                }

                deliverable ??= new List<WireReliableBattleEvent>(source.Count - i);
                deliverable.Add(item);
                expected++;
            }

            if (deliverable == null || deliverable.Count == 0)
            {
                return new MobaReliableBattleEventBatchResult(
                    MobaReliableBattleEventBatchStatus.DuplicateOnly,
                    EmptyEvents,
                    pushEpoch,
                    _lastDeliveredSequence,
                    _lastDeliveredSequence + 1,
                    0);
            }

            return new MobaReliableBattleEventBatchResult(
                MobaReliableBattleEventBatchStatus.Accepted,
                deliverable.ToArray(),
                pushEpoch,
                expected - 1,
                _lastDeliveredSequence + 1,
                expected - 1);
        }

        public bool CommitDelivered(
            string epoch,
            long sequence)
        {
            if (string.IsNullOrWhiteSpace(epoch) ||
                sequence < _lastDeliveredSequence ||
                (!string.IsNullOrEmpty(_epoch) &&
                 !string.Equals(_epoch, epoch, StringComparison.Ordinal)))
            {
                return false;
            }

            _epoch = epoch;
            _lastDeliveredSequence = sequence;
            return true;
        }

        public bool AdoptAuthoritativeBaseline(string epoch, long eventWatermark)
        {
            if (string.IsNullOrWhiteSpace(epoch) || eventWatermark < 0)
            {
                return false;
            }

            var epochChanged = !string.Equals(_epoch, epoch, StringComparison.Ordinal);
            _epoch = epoch;
            _lastDeliveredSequence = eventWatermark;
            _lastAcknowledgedSequence = epochChanged
                ? 0
                : Math.Min(_lastAcknowledgedSequence, eventWatermark);
            return true;
        }

        public bool ConfirmAcknowledged(
            string epoch,
            long acceptedSequence)
        {
            if (string.IsNullOrEmpty(_epoch) ||
                !string.Equals(_epoch, epoch, StringComparison.Ordinal) ||
                acceptedSequence < 0 ||
                acceptedSequence > _lastDeliveredSequence)
            {
                return false;
            }

            // ACK requests may complete out of order. An older accepted cursor is
            // already covered by the newest confirmation and is therefore idempotent.
            if (acceptedSequence > _lastAcknowledgedSequence)
            {
                _lastAcknowledgedSequence = acceptedSequence;
            }

            return true;
        }

        public void Reset()
        {
            _epoch = string.Empty;
            _lastDeliveredSequence = 0;
            _lastAcknowledgedSequence = 0;
        }

        private MobaReliableBattleEventBatchResult Reject(
            MobaReliableBattleEventBatchStatus status,
            string epoch,
            long expectedSequence,
            long receivedSequence)
        {
            return new MobaReliableBattleEventBatchResult(
                status,
                EmptyEvents,
                epoch,
                _lastDeliveredSequence,
                expectedSequence,
                receivedSequence);
        }
    }
}
