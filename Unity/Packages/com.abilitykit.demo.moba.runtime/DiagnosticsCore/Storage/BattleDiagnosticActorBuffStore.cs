using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public interface IBattleDiagnosticActorBuffStore : IBattleDiagnosticActorBuffReadStore
    {
        bool IsFrozen { get; }

        bool TryReplaceSnapshot(
            int frame,
            IReadOnlyList<long> actorIds,
            IReadOnlyList<BattleDiagnosticActorBuff> buffs);

        void SetFrozen(bool frozen);
        void Clear();
    }

    public sealed class BattleDiagnosticActorBuffStore :
        IBattleDiagnosticActorBuffStore,
        IBattleDiagnosticBuffSnapshotSource
    {
        private readonly HashSet<long> _actorIds = new HashSet<long>();
        private BattleDiagnosticActorBuff[] _buffs = Array.Empty<BattleDiagnosticActorBuff>();

        public BattleDiagnosticActorBuffStore(BattleDiagnosticSessionScope scope)
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
            IReadOnlyList<BattleDiagnosticActorBuff> buffs)
        {
            if (IsFrozen) return false;
            if (!BattleDiagnosticFrames.IsValid(frame)) return false;
            if (actorIds == null || buffs == null) return false;

            var nextActorIds = new HashSet<long>();
            for (var i = 0; i < actorIds.Count; i++)
            {
                var actorId = actorIds[i];
                if (actorId == 0) return false;
                nextActorIds.Add(actorId);
            }

            var instanceKeys = new HashSet<(long ActorId, int BuffId, long SourceActorId, long SourceContextId, long RuntimeContextId)>();
            var nextBuffs = new BattleDiagnosticActorBuff[buffs.Count];
            for (var i = 0; i < buffs.Count; i++)
            {
                var item = buffs[i];
                if (item.Scope != Scope ||
                    item.Frame != frame ||
                    item.BuffId <= 0 ||
                    !nextActorIds.Contains(item.ActorId))
                {
                    return false;
                }

                if (item.SourceContextId != 0 || item.RuntimeContextId != 0)
                {
                    var key = (item.ActorId, item.BuffId, item.SourceActorId, item.SourceContextId, item.RuntimeContextId);
                    if (!instanceKeys.Add(key)) return false;
                }
                nextBuffs[i] = item;
            }

            _actorIds.Clear();
            foreach (var actorId in nextActorIds)
                _actorIds.Add(actorId);
            _buffs = nextBuffs;
            SnapshotFrame = frame;
            Revision++;
            return true;
        }

        public BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff> CaptureBuffSnapshot()
        {
            return new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff>(
                Revision,
                SnapshotFrame,
                (BattleDiagnosticActorBuff[])_buffs.Clone());
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorBuff> QueryActorBuffs(
            long requestId,
            int frame,
            long actorId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));

            if (SnapshotFrame == BattleDiagnosticFrames.Invalid)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorBuff>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotProduced,
                    "No actor buff snapshot has been sampled yet.");
            }

            if (frame != 0 && frame != SnapshotFrame)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorBuff>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Requested frame {frame} is unavailable; latest-only buff snapshot is frame {SnapshotFrame}.");
            }

            if (!_actorIds.Contains(actorId))
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorBuff>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Actor {actorId} is not present in buff snapshot frame {SnapshotFrame}.");
            }

            var result = new List<BattleDiagnosticActorBuff>();
            for (var i = 0; i < _buffs.Length; i++)
            {
                if (_buffs[i].ActorId == actorId)
                    result.Add(_buffs[i]);
            }

            return BattleDiagnosticQueryResult<BattleDiagnosticActorBuff>.FromItems(
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
            _buffs = Array.Empty<BattleDiagnosticActorBuff>();
            SnapshotFrame = BattleDiagnosticFrames.Invalid;
            Revision++;
        }
    }
}
