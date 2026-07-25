using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public interface IBattleDiagnosticActorEffectStore : IBattleDiagnosticActorEffectReadStore
    {
        bool IsFrozen { get; }

        bool TryReplaceSnapshot(
            int frame,
            IReadOnlyList<long> actorIds,
            IReadOnlyList<BattleDiagnosticActorEffect> effects);

        void SetFrozen(bool frozen);
        void Clear();
    }

    public sealed class BattleDiagnosticActorEffectStore :
        IBattleDiagnosticActorEffectStore,
        IBattleDiagnosticEffectSnapshotSource
    {
        private readonly HashSet<long> _actorIds = new HashSet<long>();
        private BattleDiagnosticActorEffect[] _effects = Array.Empty<BattleDiagnosticActorEffect>();

        public BattleDiagnosticActorEffectStore(BattleDiagnosticSessionScope scope)
        {
            if (!scope.IsValid) throw new ArgumentException("Invalid session scope.", nameof(scope));
            Scope = scope;
            SnapshotFrame = BattleDiagnosticFrames.Invalid;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public long Revision { get; private set; }
        public int SnapshotFrame { get; private set; }
        public bool IsFrozen { get; private set; }

        public bool TryReplaceSnapshot(
            int frame,
            IReadOnlyList<long> actorIds,
            IReadOnlyList<BattleDiagnosticActorEffect> effects)
        {
            if (IsFrozen) return false;
            if (!BattleDiagnosticFrames.IsValid(frame)) return false;
            if (actorIds == null || effects == null) return false;

            var nextActorIds = new HashSet<long>();
            for (var i = 0; i < actorIds.Count; i++)
            {
                var actorId = actorIds[i];
                if (actorId == 0) return false;
                nextActorIds.Add(actorId);
            }

            var effectKeys = new HashSet<(long ActorId, int InstanceId)>();
            var nextEffects = new BattleDiagnosticActorEffect[effects.Count];
            for (var i = 0; i < effects.Count; i++)
            {
                var item = effects[i];
                if (item.Scope != Scope ||
                    item.Frame != frame ||
                    item.InstanceId <= 0 ||
                    !nextActorIds.Contains(item.ActorId) ||
                    !effectKeys.Add((item.ActorId, item.InstanceId)))
                {
                    return false;
                }

                nextEffects[i] = item;
            }

            _actorIds.Clear();
            foreach (var actorId in nextActorIds)
                _actorIds.Add(actorId);
            _effects = nextEffects;
            SnapshotFrame = frame;
            Revision++;
            return true;
        }

        public BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect> CaptureEffectSnapshot()
        {
            return new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect>(
                Revision,
                SnapshotFrame,
                (BattleDiagnosticActorEffect[])_effects.Clone());
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorEffect> QueryActorEffects(
            long requestId,
            int frame,
            long actorId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));

            if (SnapshotFrame == BattleDiagnosticFrames.Invalid)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorEffect>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotProduced,
                    "No actor effect snapshot has been sampled yet.");
            }

            if (frame != 0 && frame != SnapshotFrame)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorEffect>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Requested frame {frame} is unavailable; latest-only effect snapshot is frame {SnapshotFrame}.");
            }

            if (!_actorIds.Contains(actorId))
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorEffect>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Actor {actorId} is not present in effect snapshot frame {SnapshotFrame}.");
            }

            var result = new List<BattleDiagnosticActorEffect>();
            for (var i = 0; i < _effects.Length; i++)
            {
                if (_effects[i].ActorId == actorId)
                    result.Add(_effects[i]);
            }

            return BattleDiagnosticQueryResult<BattleDiagnosticActorEffect>.FromItems(
                requestId,
                Revision,
                result,
                false);
        }

        public void SetFrozen(bool frozen)
        {
            IsFrozen = frozen;
        }

        public void Clear()
        {
            if (IsFrozen) return;
            _actorIds.Clear();
            _effects = Array.Empty<BattleDiagnosticActorEffect>();
            SnapshotFrame = BattleDiagnosticFrames.Invalid;
            Revision++;
        }
    }
}
