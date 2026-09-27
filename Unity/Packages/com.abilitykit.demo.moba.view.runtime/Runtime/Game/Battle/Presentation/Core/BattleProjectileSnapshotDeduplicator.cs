using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    /// <summary>Stable, platform-neutral identity for a projectile presentation event.</summary>
    public readonly struct BattleProjectileSnapshotKey : IEquatable<BattleProjectileSnapshotKey>
    {
        private readonly int _kind;
        private readonly int _identity;
        private readonly int _templateId;
        private readonly int _launcherActorId;
        private readonly int _hitCollider;
        private readonly int _exitReason;
        private readonly int _positionHash;
        private readonly bool _hasIdentity;

        private BattleProjectileSnapshotKey(
            int kind,
            int identity,
            int templateId,
            int launcherActorId,
            int hitCollider,
            int exitReason,
            int positionHash,
            bool hasIdentity)
        {
            _kind = kind;
            _identity = identity;
            _templateId = templateId;
            _launcherActorId = launcherActorId;
            _hitCollider = hitCollider;
            _exitReason = exitReason;
            _positionHash = positionHash;
            _hasIdentity = hasIdentity;
        }

        public static BattleProjectileSnapshotKey From(in ProjectileEventData entry)
        {
            return From(in entry, entry.Kind);
        }

        internal static BattleProjectileSnapshotKey From(
            in ProjectileEventData entry,
            ProjectilePresentationEventKind kind)
        {
            var identity = entry.ProjectileId > 0 ? entry.ProjectileId : entry.ProjectileActorId;
            if (identity > 0)
            {
                return new BattleProjectileSnapshotKey(
                    (int)kind,
                    identity,
                    entry.TemplateId,
                    0,
                    0,
                    0,
                    0,
                    hasIdentity: true);
            }

            return new BattleProjectileSnapshotKey(
                (int)kind,
                0,
                entry.TemplateId,
                entry.LauncherActorId,
                entry.HitCollider,
                entry.ExitReason,
                HashPosition(entry.X, entry.Y, entry.Z),
                hasIdentity: false);
        }

        public bool Equals(BattleProjectileSnapshotKey other)
        {
            return _kind == other._kind
                && _identity == other._identity
                && _templateId == other._templateId
                && _launcherActorId == other._launcherActorId
                && _hitCollider == other._hitCollider
                && _exitReason == other._exitReason
                && _positionHash == other._positionHash
                && _hasIdentity == other._hasIdentity;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleProjectileSnapshotKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _kind;
                hash = (hash * 397) ^ _identity;
                hash = (hash * 397) ^ _templateId;
                hash = (hash * 397) ^ _launcherActorId;
                hash = (hash * 397) ^ _hitCollider;
                hash = (hash * 397) ^ _exitReason;
                hash = (hash * 397) ^ _positionHash;
                hash = (hash * 397) ^ (_hasIdentity ? 1 : 0);
                return hash;
            }
        }

        private static int HashPosition(float x, float y, float z)
        {
            unchecked
            {
                var hash = Quantize(x);
                hash = (hash * 397) ^ Quantize(y);
                hash = (hash * 397) ^ Quantize(z);
                return hash;
            }
        }

        private static int Quantize(float value)
        {
            return (int)Math.Round(value * 1000f);
        }
    }

    /// <summary>Session-scoped idempotency state for projectile snapshot presentation.</summary>
    public sealed class BattleProjectileSnapshotDeduplicator
    {
        private readonly HashSet<BattleProjectileSnapshotKey> _handled =
            new HashSet<BattleProjectileSnapshotKey>();

        public int Count => _handled.Count;

        public bool ShouldHandle(in ProjectileEventData entry)
        {
            return _handled.Add(BattleProjectileSnapshotKey.From(in entry));
        }

        public void ForgetLifecycle(in ProjectileEventData exit)
        {
            var identity = exit.ProjectileId > 0 ? exit.ProjectileId : exit.ProjectileActorId;
            if (identity <= 0) return;

            _handled.Remove(BattleProjectileSnapshotKey.From(
                in exit,
                ProjectilePresentationEventKind.Spawn));
            _handled.Remove(BattleProjectileSnapshotKey.From(
                in exit,
                ProjectilePresentationEventKind.Hit));
        }

        public void Clear()
        {
            _handled.Clear();
        }
    }
}
