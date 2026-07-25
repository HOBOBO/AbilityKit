using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public interface IBattleDiagnosticActorTagStore : IBattleDiagnosticActorTagReadStore
    {
        bool IsFrozen { get; }

        bool TryReplaceSnapshot(
            int frame,
            IReadOnlyList<long> actorIds,
            IReadOnlyList<BattleDiagnosticActorTag> tags);

        void SetFrozen(bool frozen);
        void Clear();
    }

    public sealed class BattleDiagnosticActorTagStore :
        IBattleDiagnosticActorTagStore,
        IBattleDiagnosticTagSnapshotSource
    {
        private readonly HashSet<long> _actorIds = new HashSet<long>();
        private BattleDiagnosticActorTag[] _tags = Array.Empty<BattleDiagnosticActorTag>();

        public BattleDiagnosticActorTagStore(BattleDiagnosticSessionScope scope)
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
            IReadOnlyList<BattleDiagnosticActorTag> tags)
        {
            if (IsFrozen) return false;
            if (!BattleDiagnosticFrames.IsValid(frame)) return false;
            if (actorIds == null || tags == null) return false;

            var nextActorIds = new HashSet<long>();
            for (var i = 0; i < actorIds.Count; i++)
            {
                var actorId = actorIds[i];
                if (actorId == 0) return false;
                nextActorIds.Add(actorId);
            }

            var tagKeys = new HashSet<(long ActorId, int TagId)>();
            var nextTags = new BattleDiagnosticActorTag[tags.Count];
            for (var i = 0; i < tags.Count; i++)
            {
                var item = tags[i];
                if (item.Scope != Scope ||
                    item.Frame != frame ||
                    item.TagId <= 0 ||
                    !nextActorIds.Contains(item.ActorId) ||
                    !tagKeys.Add((item.ActorId, item.TagId)))
                {
                    return false;
                }

                nextTags[i] = item;
            }

            _actorIds.Clear();
            foreach (var actorId in nextActorIds)
                _actorIds.Add(actorId);
            _tags = nextTags;
            SnapshotFrame = frame;
            Revision++;
            return true;
        }

        public BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag> CaptureTagSnapshot()
        {
            return new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag>(
                Revision,
                SnapshotFrame,
                (BattleDiagnosticActorTag[])_tags.Clone());
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorTag> QueryActorTags(
            long requestId,
            int frame,
            long actorId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));

            if (SnapshotFrame == BattleDiagnosticFrames.Invalid)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorTag>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotProduced,
                    "No actor tag snapshot has been sampled yet.");
            }

            if (frame != 0 && frame != SnapshotFrame)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorTag>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Requested frame {frame} is unavailable; latest-only tag snapshot is frame {SnapshotFrame}.");
            }

            if (!_actorIds.Contains(actorId))
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorTag>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Actor {actorId} is not present in tag snapshot frame {SnapshotFrame}.");
            }

            var result = new List<BattleDiagnosticActorTag>();
            for (var i = 0; i < _tags.Length; i++)
            {
                if (_tags[i].ActorId == actorId)
                    result.Add(_tags[i]);
            }

            return BattleDiagnosticQueryResult<BattleDiagnosticActorTag>.FromItems(
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
            _tags = Array.Empty<BattleDiagnosticActorTag>();
            SnapshotFrame = BattleDiagnosticFrames.Invalid;
            Revision++;
        }
    }
}
