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
        private readonly int _frame;
        private readonly (int X, int Y, int Z) _position;
        private readonly bool _hasIdentity;

        private BattleProjectileSnapshotKey(
            int kind,
            int identity,
            int templateId,
            int launcherActorId,
            int hitCollider,
            int exitReason,
            int frame,
            (int, int, int) position,
            bool hasIdentity)
        {
            _kind = kind;
            _identity = identity;
            _templateId = templateId;
            _launcherActorId = launcherActorId;
            _hitCollider = hitCollider;
            _exitReason = exitReason;
            _frame = frame;
            _position = position;
            _hasIdentity = hasIdentity;
        }

        public static BattleProjectileSnapshotKey From(in ProjectileEventData entry, int snapshotFrame = 0)
        {
            if (entry.Kind == ProjectilePresentationEventKind.Hit)
                return FromHit(BattleProjectileSnapshotDeduplicator.ResolveIdentity(in entry), entry.TemplateId,
                    entry.LauncherActorId, entry.HitCollider,
                    entry.SnapshotFrame > 0 ? entry.SnapshotFrame : snapshotFrame, entry.X, entry.Y, entry.Z);
            return From(in entry, entry.Kind);
        }

        /// <summary>
        /// 使用已有字段标识单次命中，供本地事件与快照共用。
        /// </summary>
        public static BattleProjectileSnapshotKey FromHit(
            int identity, int templateId, int launcherActorId, int collider, int frame, float x, float y, float z)
        {
            return new BattleProjectileSnapshotKey((int)ProjectilePresentationEventKind.Hit, identity, templateId,
                identity > 0 ? 0 : launcherActorId, collider, 0, frame,
                (Quantize(x), Quantize(y), Quantize(z)), identity > 0);
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
                    default,
                    hasIdentity: true);
            }

            return new BattleProjectileSnapshotKey(
                (int)kind,
                0,
                entry.TemplateId,
                entry.LauncherActorId,
                entry.HitCollider,
                entry.ExitReason,
                0,
                (Quantize(entry.X), Quantize(entry.Y), Quantize(entry.Z)),
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
                && _frame == other._frame
                && _position.Equals(other._position)
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
                hash = (hash * 397) ^ _frame;
                hash = (hash * 397) ^ _position.GetHashCode();
                hash = (hash * 397) ^ (_hasIdentity ? 1 : 0);
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
        private const int CompletedWindowCapacity = 1024;
        private const int PlayedHitCapacity = 4096;
        private readonly HashSet<BattleProjectileSnapshotKey> _playedHits = new();
        private readonly Queue<BattleProjectileSnapshotKey> _playedHitOrder = new();
        private readonly HashSet<BattleProjectileSnapshotKey> _handled =
            new HashSet<BattleProjectileSnapshotKey>();
        private readonly Dictionary<int, Dictionary<BattleProjectileSnapshotKey, bool>> _activeHits = new();
        private readonly Dictionary<int, Dictionary<BattleProjectileSnapshotKey, bool>> _completed = new();
        private readonly Queue<int> _completedOrder = new Queue<int>();

        public int Count
        {
            get
            {
                var count = _handled.Count;
                foreach (var hits in _activeHits.Values) count += hits.Count;
                return count;
            }
        }

        public bool ShouldHandle(in ProjectileEventData entry, int snapshotFrame = 0)
        {
            var identity = ResolveIdentity(in entry);
            if (_completed.ContainsKey(identity)) return false;
            var key = BattleProjectileSnapshotKey.From(in entry, snapshotFrame);
            return entry.Kind == ProjectilePresentationEventKind.Hit && identity > 0
                ? GetOrCreateHits(_activeHits, identity).TryAdd(key, false)
                : _handled.Add(key);
        }

        /// <summary>
        /// 释放活跃记录，仅保留有界的最近终态窗口，阻止重复批次重新播放。
        /// </summary>
        public void CompleteLifecycle(in ProjectileEventData exit)
        {
            var identity = ResolveIdentity(in exit);
            if (identity <= 0) return;

            _handled.Remove(BattleProjectileSnapshotKey.From(
                in exit,
                ProjectilePresentationEventKind.Spawn));
            if (_completed.ContainsKey(identity)) return;
            if (_completedOrder.Count == CompletedWindowCapacity) _completed.Remove(_completedOrder.Dequeue());
            _activeHits.Remove(identity, out var hits);
            _completed.Add(identity, hits);
            _completedOrder.Enqueue(identity);
        }

        /// <summary>
        /// 查询快照或本地事件是否已成功播放这一次命中的表现。
        /// </summary>
        public bool HasHitVisual(int identity, in BattleProjectileSnapshotKey key)
        {
            if (_playedHits.Contains(key)) return true;
            if (!_completed.TryGetValue(identity, out var hits)) _activeHits.TryGetValue(identity, out hits);
            return hits != null && hits.TryGetValue(key, out var played) && played;
        }

        /// <summary>
        /// 仅在成功创建命中表现后记录，保留另一来源的补播机会。
        /// </summary>
        public void RecordHitVisual(int identity, in BattleProjectileSnapshotKey key)
        {
            if (identity <= 0) return;
            if (_playedHits.Add(key))
            {
                _playedHitOrder.Enqueue(key);
                if (_playedHitOrder.Count > PlayedHitCapacity) _playedHits.Remove(_playedHitOrder.Dequeue());
            }
            var histories = _completed.ContainsKey(identity) ? _completed : _activeHits;
            GetOrCreateHits(histories, identity)[key] = true;
        }

        private static Dictionary<BattleProjectileSnapshotKey, bool> GetOrCreateHits(
            Dictionary<int, Dictionary<BattleProjectileSnapshotKey, bool>> histories, int identity)
        {
            if (histories.TryGetValue(identity, out var hits) && hits != null) return hits;
            hits = new Dictionary<BattleProjectileSnapshotKey, bool>();
            histories[identity] = hits;
            return hits;
        }

        /// <summary>
        /// 优先使用逻辑投射物 ID，缺失时回退到投射物 Actor ID。
        /// </summary>
        public static int ResolveIdentity(in ProjectileEventData entry)
        {
            return entry.ProjectileId > 0 ? entry.ProjectileId : entry.ProjectileActorId;
        }

        public void Clear()
        {
            _handled.Clear();
            _activeHits.Clear();
            _playedHits.Clear();
            _playedHitOrder.Clear();
            ClearCompleted();
        }

        /// <summary>
        /// 回滚后逻辑投射物 ID 可被重演复用，不延续上一代际的终态窗口。
        /// </summary>
        public void ClearCompleted()
        {
            _completed.Clear();
            _completedOrder.Clear();
        }
    }
}
