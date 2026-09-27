using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaActionReplayDisposition
    {
        DecisionMissing = 0,
        ExecuteAccepted = 1,
        SkipRejected = 2,
        SkipDuplicate = 3,
    }

    /// <summary>
    /// Authoritative action-decision journal. It is independent from state snapshots: an ack
    /// confirms the decision only; resource, cooldown and action-state repair still comes from
    /// the corresponding authoritative snapshots. The journal intentionally stays outside the
    /// rollback registry so late network duplicates remain recognizable after world restoration.
    /// </summary>
    [MobaSnapshotEmitter(80)]
    [WorldService(typeof(MobaActionAckSnapshotService))]
    public sealed class MobaActionAckSnapshotService : IService, IMobaSnapshotEmitter
    {
        private readonly struct ActionKey : IEquatable<ActionKey>
        {
            public ActionKey(int actorId, int entityVersion, int predictionKey)
            {
                ActorId = actorId;
                EntityVersion = entityVersion;
                PredictionKey = predictionKey;
            }

            private int ActorId { get; }
            private int EntityVersion { get; }
            private int PredictionKey { get; }

            public bool Equals(ActionKey other) =>
                ActorId == other.ActorId &&
                EntityVersion == other.EntityVersion &&
                PredictionKey == other.PredictionKey;

            public override bool Equals(object obj) => obj is ActionKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = ActorId;
                    hash = (hash * 397) ^ EntityVersion;
                    return (hash * 397) ^ PredictionKey;
                }
            }
        }

        private readonly struct ActorKey : IEquatable<ActorKey>
        {
            public ActorKey(int actorId, int entityVersion)
            {
                ActorId = actorId;
                EntityVersion = entityVersion;
            }

            private int ActorId { get; }
            private int EntityVersion { get; }

            public bool Equals(ActorKey other) =>
                ActorId == other.ActorId && EntityVersion == other.EntityVersion;

            public override bool Equals(object obj) => obj is ActorKey other && Equals(other);
            public override int GetHashCode() => unchecked((ActorId * 397) ^ EntityVersion);
        }

        private readonly Dictionary<ActionKey, MobaActionAckEntry> _decisions =
            new Dictionary<ActionKey, MobaActionAckEntry>();
        private readonly Dictionary<ActorKey, long> _inputSequenceWatermarks =
            new Dictionary<ActorKey, long>();
        private readonly Dictionary<ActorKey, int> _interruptEpochs =
            new Dictionary<ActorKey, int>();
        private readonly HashSet<ActionKey> _replayedActions = new HashSet<ActionKey>();
        private readonly MobaSnapshotBuffer<MobaActionAckEntry> _pending =
            new MobaSnapshotBuffer<MobaActionAckEntry>(16, 256);
        private long _nextAuthoritySequence;
        private bool _replayActive;
        private FrameIndex _replayRestoredFrame;
        private FrameIndex _replayToFrame;

        public void BeginReplay(FrameIndex restoredFrame, FrameIndex replayToFrame)
        {
            if (_replayActive)
                throw new InvalidOperationException("An action replay is already active.");
            if (replayToFrame.Value < restoredFrame.Value)
                throw new ArgumentOutOfRangeException(nameof(replayToFrame));

            _replayedActions.Clear();
            foreach (var pair in _decisions)
            {
                if (pair.Value.Accepted && pair.Value.AuthoritativeFrame <= restoredFrame.Value)
                    _replayedActions.Add(pair.Key);
            }
            _replayRestoredFrame = restoredFrame;
            _replayToFrame = replayToFrame;
            _replayActive = true;
        }

        public MobaActionReplayDisposition ResolveReplay(
            int actorId,
            int entityVersion,
            int predictionKey,
            out MobaActionAckEntry decision)
        {
            if (!_replayActive)
                throw new InvalidOperationException("BeginReplay must be called before resolving replay decisions.");

            decision = default;
            var key = new ActionKey(actorId, entityVersion, predictionKey);
            if (predictionKey <= 0 || !_decisions.TryGetValue(key, out decision))
                return MobaActionReplayDisposition.DecisionMissing;
            if (!decision.Accepted)
                return MobaActionReplayDisposition.SkipRejected;
            return _replayedActions.Contains(key)
                ? MobaActionReplayDisposition.SkipDuplicate
                : MobaActionReplayDisposition.ExecuteAccepted;
        }

        public void CompleteReplay(int actorId, int entityVersion, int predictionKey)
        {
            if (!_replayActive)
                throw new InvalidOperationException("BeginReplay must be called before completing replay decisions.");

            var key = new ActionKey(actorId, entityVersion, predictionKey);
            if (!_decisions.TryGetValue(key, out var decision) || !decision.Accepted)
                throw new InvalidOperationException("Only an accepted authoritative action can complete replay.");
            if (decision.AuthoritativeFrame <= _replayRestoredFrame.Value ||
                decision.AuthoritativeFrame > _replayToFrame.Value)
                throw new InvalidOperationException("The accepted action is outside the active replay window.");

            if (!_replayedActions.Add(key))
                throw new InvalidOperationException("An accepted action cannot complete replay more than once.");
        }

        public void EndReplay()
        {
            if (!_replayActive) return;

            MobaActionAckEntry missing = default;
            var hasMissing = false;
            foreach (var pair in _decisions)
            {
                var decision = pair.Value;
                if (!decision.Accepted ||
                    decision.AuthoritativeFrame <= _replayRestoredFrame.Value ||
                    decision.AuthoritativeFrame > _replayToFrame.Value ||
                    _replayedActions.Contains(pair.Key))
                {
                    continue;
                }

                missing = decision;
                hasMissing = true;
                break;
            }

            ResetReplay();
            if (hasMissing)
            {
                throw new InvalidOperationException(
                    $"Accepted action was not executed during replay. actor={missing.ActorId}, " +
                    $"entityVersion={missing.EntityVersion}, predictionKey={missing.PredictionKey}, " +
                    $"authoritativeFrame={missing.AuthoritativeFrame}.");
            }
        }

        public bool TryGetDecision(
            int actorId,
            int entityVersion,
            int predictionKey,
            out MobaActionAckEntry decision)
        {
            decision = default;
            return predictionKey > 0 && _decisions.TryGetValue(
                new ActionKey(actorId, entityVersion, predictionKey),
                out decision);
        }

        public bool TryAcceptInputSequence(int actorId, int entityVersion, long inputSequence)
        {
            if (actorId <= 0 || entityVersion <= 0 || inputSequence <= 0L) return false;

            var key = new ActorKey(actorId, entityVersion);
            if (_inputSequenceWatermarks.TryGetValue(key, out var latest) && inputSequence <= latest)
                return false;

            _inputSequenceWatermarks[key] = inputSequence;
            return true;
        }

        public int GetInterruptEpoch(int actorId, int entityVersion)
        {
            return _interruptEpochs.TryGetValue(new ActorKey(actorId, entityVersion), out var epoch)
                ? epoch
                : 0;
        }

        /// <summary>
        /// Advances the authoritative interrupt epoch and queues a prediction-key-zero control
        /// entry so clients learn about server-side cancellation before submitting another action.
        /// </summary>
        public int AdvanceInterruptEpoch(int actorId, int entityVersion, int authoritativeFrame = 0)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (entityVersion <= 0) throw new ArgumentOutOfRangeException(nameof(entityVersion));
            if (authoritativeFrame < 0) throw new ArgumentOutOfRangeException(nameof(authoritativeFrame));

            var key = new ActorKey(actorId, entityVersion);
            var current = GetInterruptEpoch(actorId, entityVersion);
            var next = current == int.MaxValue ? 1 : current + 1;
            _interruptEpochs[key] = next;

            _pending.Add(new MobaActionAckEntry
            {
                ActorId = actorId,
                EntityVersion = entityVersion,
                PredictionKey = 0,
                SkillId = 0,
                Accepted = false,
                AuthoritativeFrame = authoritativeFrame,
                Reason = (int)MobaActionAckReason.Interrupted,
                InterruptEpoch = next,
                AuthoritySequence = NextAuthoritySequence(),
            });
            return next;
        }

        public MobaActionAckEntry Report(
            int actorId,
            int entityVersion,
            int predictionKey,
            int skillId,
            bool accepted,
            int authoritativeFrame,
            MobaActionAckReason reason)
        {
            if (predictionKey <= 0) return default;

            var key = new ActionKey(actorId, entityVersion, predictionKey);
            if (_decisions.TryGetValue(key, out var existing))
            {
                _pending.Add(existing);
                return existing;
            }

            var entry = new MobaActionAckEntry
            {
                ActorId = actorId,
                EntityVersion = entityVersion,
                PredictionKey = predictionKey,
                SkillId = skillId,
                Accepted = accepted,
                AuthoritativeFrame = authoritativeFrame,
                Reason = (int)reason,
                InterruptEpoch = GetInterruptEpoch(actorId, entityVersion),
                AuthoritySequence = NextAuthoritySequence(),
            };
            _decisions.Add(key, entry);
            _pending.Add(entry);
            return entry;
        }

        public void Repeat(in MobaActionAckEntry decision)
        {
            if (decision.PredictionKey > 0) _pending.Add(decision);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (_pending.Count == 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = new WorldStateSnapshot(
                MobaOpCodes.Snapshot.ActionAck,
                MobaActionAckCodec.Serialize(_pending.ToArrayClearAndTrim()));
            return true;
        }

        public void Dispose()
        {
            ResetReplay();
            _decisions.Clear();
            _inputSequenceWatermarks.Clear();
            _interruptEpochs.Clear();
            _pending.ClearAndTrim();
            _nextAuthoritySequence = 0L;
        }

        private void ResetReplay()
        {
            _replayActive = false;
            _replayRestoredFrame = default;
            _replayToFrame = default;
            _replayedActions.Clear();
        }

        private long NextAuthoritySequence()
        {
            _nextAuthoritySequence++;
            if (_nextAuthoritySequence <= 0L) _nextAuthoritySequence = 1L;
            return _nextAuthoritySequence;
        }
    }
}
