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
        internal Action<MobaActionAckEntry> AuthorityEntryCapture { get; set; }
        private bool _clientBound;
        private bool _settledTimelineBound;
        private int _settledFrame;
        private long _settledSequence;
        private bool _replayActive;
        private FrameIndex _replayRestoredFrame;
        private FrameIndex _replayToFrame;

        internal void BindClientInput()
        {
            if (_clientBound) return;
            _clientBound = true;
            _pending.ClearAndTrim();
        }

        internal void BindSettledTimeline()
        {
            if (!_clientBound || _settledTimelineBound || _replayActive)
                throw new InvalidOperationException("Settled timeline requires a new client session.");

            _settledTimelineBound = true;
            _decisions.Clear();
            _interruptEpochs.Clear();
        }

        private sealed class SettledFrameUpdate : IWorldSettledFrameUpdate
        {
            private readonly MobaActionAckSnapshotService _owner;
            private readonly int _frame;
            public long Sequence;
            public Dictionary<ActionKey, MobaActionAckEntry> Decisions;
            public Dictionary<ActorKey, int> Epochs;

            public SettledFrameUpdate(MobaActionAckSnapshotService owner, int frame)
            {
                _owner = owner;
                _frame = frame;
                Sequence = owner._settledSequence;
            }

            public bool InvalidatesPrediction => Decisions != null || Epochs != null;

            public void Commit()
            {
                if (Decisions != null)
                    foreach (var entry in Decisions) _owner._decisions.Add(entry.Key, entry.Value);
                if (Epochs != null)
                    foreach (var epoch in Epochs) _owner._interruptEpochs[epoch.Key] = epoch.Value;
                _owner._settledFrame = _frame;
                _owner._settledSequence = Sequence;
            }
        }

        internal IWorldSettledFrameUpdate PrepareSettledFrame(FrameIndex frame, MobaSettledFrameMetadata metadata)
        {
            if (!_settledTimelineBound || frame.Value != _settledFrame + 1)
                throw new InvalidOperationException("MOBA settled timeline requires the next contiguous frame.");

            var update = new SettledFrameUpdate(this, frame.Value);
            var previousOrdinal = -1;
            foreach (var item in metadata.Entries)
            {
                var entry = item.Entry;
                if (item.Ordinal < 0 || item.Ordinal < previousOrdinal || item.Ordinal > metadata.CommandCount ||
                    entry.ActorId <= 0 || entry.EntityVersion <= 0 || entry.PredictionKey < 0 ||
                    entry.AuthoritySequence <= 0 || entry.AuthoritativeFrame < 0 || entry.AuthoritativeFrame > frame.Value ||
                    entry.InterruptEpoch < 0)
                    throw new InvalidOperationException($"Invalid authority timeline entry at frame {frame.Value}.");
                previousOrdinal = item.Ordinal;

                var actor = new ActorKey(entry.ActorId, entry.EntityVersion);
                if (entry.PredictionKey == 0)
                {
                    if (entry.AuthoritySequence <= update.Sequence) continue;
                    if (entry.InterruptEpoch == 0 || entry.AuthoritativeFrame != frame.Value || entry.Accepted ||
                        entry.Reason != (int)MobaActionAckReason.Interrupted)
                        throw new InvalidOperationException($"Invalid interrupt entry at frame {frame.Value}.");

                    (update.Epochs ??= new Dictionary<ActorKey, int>())[actor] = entry.InterruptEpoch;
                }
                else
                {
                    var key = new ActionKey(entry.ActorId, entry.EntityVersion, entry.PredictionKey);
                    if (TryGetSettledDecision(update, key, out var previous))
                    {
                        if (!previous.Equals(entry))
                            throw new InvalidOperationException($"Conflicting authority decision at frame {frame.Value}.");
                        continue;
                    }

                    var epoch = update.Epochs != null && update.Epochs.TryGetValue(actor, out var stagedEpoch)
                        ? stagedEpoch : GetInterruptEpoch(entry.ActorId, entry.EntityVersion);
                    if (entry.AuthoritySequence <= update.Sequence || entry.AuthoritativeFrame != frame.Value ||
                        item.Ordinal == metadata.CommandCount || entry.InterruptEpoch != epoch)
                        throw new InvalidOperationException($"Missing or out-of-order authority decision at frame {frame.Value}.");

                    (update.Decisions ??= new Dictionary<ActionKey, MobaActionAckEntry>()).Add(key, entry);
                }
                update.Sequence = entry.AuthoritySequence;
            }
            return update;
        }

        private bool TryGetSettledDecision(SettledFrameUpdate update, ActionKey key, out MobaActionAckEntry entry)
        {
            if (_decisions.TryGetValue(key, out entry)) return true;
            return update.Decisions != null && update.Decisions.TryGetValue(key, out entry);
        }

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

        /// <summary>
        /// Authoritative frame inputs contain only commands accepted by the server. They can
        /// therefore fill a missing action ack before replaying that command.
        /// </summary>
        public void RecordAuthoritativeReplayInput(
            int actorId,
            int entityVersion,
            int predictionKey,
            int skillId,
            int authoritativeFrame)
        {
            if (!_replayActive)
                throw new InvalidOperationException("BeginReplay must be called before recording replay inputs.");
            if (actorId <= 0 || entityVersion <= 0 || predictionKey <= 0 || authoritativeFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(predictionKey));
            if (authoritativeFrame <= _replayRestoredFrame.Value || authoritativeFrame > _replayToFrame.Value)
                throw new InvalidOperationException("The authoritative input is outside the active replay window.");

            var key = new ActionKey(actorId, entityVersion, predictionKey);
            if (_decisions.TryGetValue(key, out var existing))
            {
                if (!existing.Accepted)
                    throw new InvalidOperationException("An authoritative replay input conflicts with a rejected action decision.");
                return;
            }

            _decisions.Add(key, new MobaActionAckEntry
            {
                ActorId = actorId,
                EntityVersion = entityVersion,
                PredictionKey = predictionKey,
                SkillId = skillId,
                Accepted = true,
                AuthoritativeFrame = authoritativeFrame,
                Reason = (int)MobaActionAckReason.None,
                InterruptEpoch = GetInterruptEpoch(actorId, entityVersion),
                AuthoritySequence = 0,
            });
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
            if (_clientBound || actorId <= 0 || entityVersion <= 0 || inputSequence <= 0L) return false;

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
        /// 权威世界推进中断代数并排队控制消息；客户端只读取已收到的权威代数，
        /// 不用本地模拟的中断更新它。
        /// </summary>
        public int AdvanceInterruptEpoch(int actorId, int entityVersion, int authoritativeFrame = 0)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (entityVersion <= 0) throw new ArgumentOutOfRangeException(nameof(entityVersion));
            if (authoritativeFrame < 0) throw new ArgumentOutOfRangeException(nameof(authoritativeFrame));

            var current = GetInterruptEpoch(actorId, entityVersion);
            if (_clientBound) return current;
            var key = new ActorKey(actorId, entityVersion);
            var next = current == int.MaxValue ? 1 : current + 1;
            _interruptEpochs[key] = next;

            EnqueueAuthorityEntry(new MobaActionAckEntry
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

        /// <summary>
        /// 记录并排队权威处理结果；绑定到客户端后不生产权威回执。
        /// </summary>
        public MobaActionAckEntry Report(
            int actorId,
            int entityVersion,
            int predictionKey,
            int skillId,
            bool accepted,
            int authoritativeFrame,
            MobaActionAckReason reason)
        {
            if (_clientBound || predictionKey <= 0) return default;

            var key = new ActionKey(actorId, entityVersion, predictionKey);
            if (_decisions.TryGetValue(key, out var existing))
            {
                EnqueueAuthorityEntry(existing);
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
            EnqueueAuthorityEntry(entry);
            return entry;
        }

        /// <summary>
        /// 重发已有权威结果；客户端不发送收到的回执。
        /// </summary>
        public void Repeat(in MobaActionAckEntry decision)
        {
            if (!_clientBound && decision.PredictionKey > 0) EnqueueAuthorityEntry(decision);
        }

        private void EnqueueAuthorityEntry(MobaActionAckEntry entry)
        {
            _pending.Add(entry);
            AuthorityEntryCapture?.Invoke(entry);
        }

        /// <summary>
        /// Applies an authoritative ack received by a prediction client. Network delivery must
        /// update the replay journal without re-emitting the entry as a local snapshot.
        /// </summary>
        public void ApplyAuthoritative(IReadOnlyList<MobaActionAckEntry> entries)
        {
            if (entries == null || _settledTimelineBound) return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.ActorId <= 0 || entry.EntityVersion <= 0) continue;

                var actorKey = new ActorKey(entry.ActorId, entry.EntityVersion);
                if (entry.InterruptEpoch > GetInterruptEpoch(entry.ActorId, entry.EntityVersion))
                    _interruptEpochs[actorKey] = entry.InterruptEpoch;

                if (entry.PredictionKey <= 0) continue;

                var actionKey = new ActionKey(entry.ActorId, entry.EntityVersion, entry.PredictionKey);
                _decisions[actionKey] = entry;
                if (entry.AuthoritySequence > _nextAuthoritySequence)
                    _nextAuthoritySequence = entry.AuthoritySequence;
            }
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
