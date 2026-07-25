#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterReliableBattleEventConsumer
    {
        private readonly int _maxPendingEvents;
        private readonly SortedDictionary<long, WireReliableBattleEvent> _pending = new SortedDictionary<long, WireReliableBattleEvent>();
        private string _baselineBattleId = string.Empty;
        private string _baselineEpoch = string.Empty;

        public ShooterReliableBattleEventConsumer(int maxPendingEvents = 512)
        {
            _maxPendingEvents = Math.Max(1, maxPendingEvents);
        }

        public string BattleId { get; private set; } = string.Empty;

        public string Epoch { get; private set; } = string.Empty;

        public long LastAcknowledgedSequence { get; private set; }

        public long LastObservedWatermark { get; private set; }

        public bool RequiresResync { get; private set; }

        public ShooterReliableBattleEventConsumeResult Consume(in WireReliableBattleEventPush push)
        {
            var pushBattleId = push.BattleId ?? string.Empty;
            var pushEpoch = push.Epoch ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pushBattleId) || string.IsNullOrWhiteSpace(pushEpoch))
            {
                return MarkGap();
            }

            if (string.IsNullOrEmpty(Epoch))
            {
                BattleId = pushBattleId;
                Epoch = pushEpoch;
            }
            else if (!string.Equals(BattleId, pushBattleId, StringComparison.Ordinal)
                || !string.Equals(Epoch, pushEpoch, StringComparison.Ordinal))
            {
                return MarkGap(pushBattleId, pushEpoch);
            }

            LastObservedWatermark = Math.Max(LastObservedWatermark, push.Watermark);
            if (RequiresResync
                || push.RetentionGap
                || (push.FirstAvailableSequence > 0 && LastAcknowledgedSequence + 1 < push.FirstAvailableSequence))
            {
                return MarkGap(pushBattleId, pushEpoch);
            }

            var events = push.Events;
            if (events != null)
            {
                foreach (var battleEvent in events)
                {
                    if (!IsValid(in battleEvent))
                    {
                        return MarkGap();
                    }

                    if (battleEvent.Sequence <= LastAcknowledgedSequence || _pending.ContainsKey(battleEvent.Sequence))
                    {
                        continue;
                    }

                    if (_pending.Count >= _maxPendingEvents)
                    {
                        return MarkGap();
                    }

                    _pending.Add(battleEvent.Sequence, battleEvent);
                }
            }

            List<WireReliableBattleEvent>? committed = null;
            while (_pending.Remove(LastAcknowledgedSequence + 1, out var next))
            {
                committed ??= new List<WireReliableBattleEvent>();
                committed.Add(next);
                LastAcknowledgedSequence = next.Sequence;
            }

            return new ShooterReliableBattleEventConsumeResult(
                committed ?? (IReadOnlyList<WireReliableBattleEvent>)Array.Empty<WireReliableBattleEvent>(),
                LastAcknowledgedSequence,
                requiresResync: false);
        }

        public void Invalidate()
        {
            MarkGap();
        }

        public void RestoreCursor(string battleId, string epoch, long lastAcknowledgedSequence)
        {
            BattleId = battleId ?? string.Empty;
            Epoch = epoch ?? string.Empty;
            LastAcknowledgedSequence = Math.Max(0, lastAcknowledgedSequence);
            LastObservedWatermark = LastAcknowledgedSequence;
            RequiresResync = false;
            _baselineBattleId = string.Empty;
            _baselineEpoch = string.Empty;
            _pending.Clear();
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

            RestoreCursor(
                _baselineBattleId,
                _baselineEpoch,
                Math.Max(LastAcknowledgedSequence, eventWatermark));
            return true;
        }

        private bool IsValid(in WireReliableBattleEvent battleEvent)
        {
            return battleEvent.Sequence > 0
                && string.Equals(battleEvent.BattleId, BattleId, StringComparison.Ordinal)
                && string.Equals(battleEvent.Epoch, Epoch, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(battleEvent.EventId);
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
            _pending.Clear();
            return new ShooterReliableBattleEventConsumeResult(
                Array.Empty<WireReliableBattleEvent>(),
                LastAcknowledgedSequence,
                requiresResync: true);
        }
    }

    public readonly struct ShooterReliableBattleEventConsumeResult
    {
        public ShooterReliableBattleEventConsumeResult(
            IReadOnlyList<WireReliableBattleEvent> committedEvents,
            long acknowledgedSequence,
            bool requiresResync)
        {
            CommittedEvents = committedEvents ?? Array.Empty<WireReliableBattleEvent>();
            AcknowledgedSequence = acknowledgedSequence;
            RequiresResync = requiresResync;
        }

        public IReadOnlyList<WireReliableBattleEvent> CommittedEvents { get; }

        public long AcknowledgedSequence { get; }

        public bool RequiresResync { get; }

        public bool ShouldAcknowledge => !RequiresResync && CommittedEvents.Count > 0;
    }
}
