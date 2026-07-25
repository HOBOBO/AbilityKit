using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public interface IBattleDiagnosticActorAttributeStore :
        IBattleDiagnosticActorAttributeReadStore
    {
        bool IsFrozen { get; }

        bool TryReplaceSnapshot(
            int frame,
            IReadOnlyList<long> actorIds,
            IReadOnlyList<BattleDiagnosticActorAttribute> attributes,
            IReadOnlyList<BattleDiagnosticActorAttributeModifier> modifiers);

        void SetFrozen(bool frozen);
        void Clear();
    }

    public sealed class BattleDiagnosticActorAttributeStore :
        IBattleDiagnosticActorAttributeStore,
        IBattleDiagnosticAttributeSnapshotSource
    {
        private readonly HashSet<long> _actorIds = new HashSet<long>();
        private BattleDiagnosticActorAttribute[] _attributes =
            Array.Empty<BattleDiagnosticActorAttribute>();
        private BattleDiagnosticActorAttributeModifier[] _modifiers =
            Array.Empty<BattleDiagnosticActorAttributeModifier>();

        public BattleDiagnosticActorAttributeStore(
            BattleDiagnosticSessionScope scope)
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
            IReadOnlyList<BattleDiagnosticActorAttribute> attributes,
            IReadOnlyList<BattleDiagnosticActorAttributeModifier> modifiers)
        {
            if (IsFrozen) return false;
            if (!BattleDiagnosticFrames.IsValid(frame)) return false;
            if (actorIds == null || attributes == null || modifiers == null) return false;

            var nextActorIds = new HashSet<long>();
            for (var i = 0; i < actorIds.Count; i++)
            {
                var actorId = actorIds[i];
                if (actorId == 0) return false;
                nextActorIds.Add(actorId);
            }

            var attributeKeys = new HashSet<(long ActorId, int AttributeId)>();
            var nextAttributes = new BattleDiagnosticActorAttribute[attributes.Count];
            for (var i = 0; i < attributes.Count; i++)
            {
                var item = attributes[i];
                if (item.Scope != Scope ||
                    item.Frame != frame ||
                    item.AttributeId <= 0 ||
                    !nextActorIds.Contains(item.ActorId))
                {
                    return false;
                }

                attributeKeys.Add((item.ActorId, item.AttributeId));
                nextAttributes[i] = item;
            }

            var nextModifiers = new BattleDiagnosticActorAttributeModifier[modifiers.Count];
            for (var i = 0; i < modifiers.Count; i++)
            {
                var item = modifiers[i];
                if (item.Scope != Scope ||
                    item.Frame != frame ||
                    !attributeKeys.Contains((item.ActorId, item.AttributeId)))
                {
                    return false;
                }

                nextModifiers[i] = item;
            }

            _actorIds.Clear();
            foreach (var actorId in nextActorIds)
                _actorIds.Add(actorId);
            _attributes = nextAttributes;
            _modifiers = nextModifiers;
            SnapshotFrame = frame;
            Revision++;
            return true;
        }

        public BattleDiagnosticAttributeTrackSnapshot CaptureAttributeSnapshot()
        {
            return new BattleDiagnosticAttributeTrackSnapshot(
                Revision,
                SnapshotFrame,
                (BattleDiagnosticActorAttribute[])_attributes.Clone(),
                (BattleDiagnosticActorAttributeModifier[])_modifiers.Clone());
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> QueryActorAttributes(
            long requestId,
            int frame,
            long actorId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));

            if (!TryValidateQueryFrame(requestId, frame, out var unavailable))
                return unavailable;
            if (!_actorIds.Contains(actorId))
                return BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Actor {actorId} is not present in attribute snapshot frame {SnapshotFrame}.");

            var result = new List<BattleDiagnosticActorAttribute>();
            for (var i = 0; i < _attributes.Length; i++)
            {
                if (_attributes[i].ActorId == actorId)
                    result.Add(_attributes[i]);
            }

            return BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute>.FromItems(
                requestId,
                Revision,
                result,
                false);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> QueryActorAttributeModifiers(
            long requestId,
            int frame,
            long actorId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));

            if (!TryValidateModifierQueryFrame(requestId, frame, out var unavailable))
                return unavailable;
            if (!_actorIds.Contains(actorId))
                return BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Actor {actorId} is not present in attribute snapshot frame {SnapshotFrame}.");

            var result = new List<BattleDiagnosticActorAttributeModifier>();
            for (var i = 0; i < _modifiers.Length; i++)
            {
                if (_modifiers[i].ActorId == actorId)
                    result.Add(_modifiers[i]);
            }

            return BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier>.FromItems(
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
            _attributes = Array.Empty<BattleDiagnosticActorAttribute>();
            _modifiers = Array.Empty<BattleDiagnosticActorAttributeModifier>();
            SnapshotFrame = BattleDiagnosticFrames.Invalid;
            Revision++;
        }

        private bool TryValidateQueryFrame(
            long requestId,
            int frame,
            out BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> unavailable)
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (SnapshotFrame == BattleDiagnosticFrames.Invalid)
            {
                unavailable = BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotProduced,
                    "No actor attribute snapshot has been sampled yet.");
                return false;
            }

            if (frame != 0 && frame != SnapshotFrame)
            {
                unavailable = BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Requested frame {frame} is unavailable; latest-only attribute snapshot is frame {SnapshotFrame}.");
                return false;
            }

            unavailable = default;
            return true;
        }

        private bool TryValidateModifierQueryFrame(
            long requestId,
            int frame,
            out BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> unavailable)
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (SnapshotFrame == BattleDiagnosticFrames.Invalid)
            {
                unavailable = BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotProduced,
                    "No actor attribute snapshot has been sampled yet.");
                return false;
            }

            if (frame != 0 && frame != SnapshotFrame)
            {
                unavailable = BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier>.Unavailable(
                    requestId,
                    Revision,
                    BattleDiagnosticDataAvailability.NotCaptured,
                    $"Requested frame {frame} is unavailable; latest-only attribute snapshot is frame {SnapshotFrame}.");
                return false;
            }

            unavailable = default;
            return true;
        }
    }
}
