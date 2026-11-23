using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Vfx;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleProjectileViewEventHandler
    {
        private readonly IBattleEntityQuery _query;
        private readonly BattleProjectileVfxSpawner _vfxSpawner;
        private readonly BattleProjectileShellSpawner _shellSpawner;
        private readonly BattleProjectileShellPool _shellPool;
        internal BattleProjectileShellPool PoolForStats => _shellPool;
        private readonly BattleProjectileVfxResolver _vfx;
        private readonly BattleProjectileSnapshotVfxResolver _snapshotVfx;
        private readonly BattleProjectileSnapshotDeduplicator _deduplicator;
        private const int RecentImpactCapacity = 1024;
        private readonly Dictionary<ImpactKey, ImpactSource> _recentImpacts = new();
        private readonly Queue<ImpactKey> _impactOrder = new();

        private enum ImpactSource { Projectile, Cue, Paired }

        private readonly struct ImpactKey : System.IEquatable<ImpactKey>
        {
            private readonly long _id;
            private readonly int _frame;
            private readonly int _vfx;
            private readonly Vector3 _position;

            internal ImpactKey(long id, int frame, int vfx, Vector3 position)
            {
                _id = id;
                _frame = frame;
                _vfx = vfx;
                _position = position;
            }

            public bool IsValid => _id > 0 && _frame > 0 && _vfx > 0;
            public bool Equals(ImpactKey other) => _id == other._id && _frame == other._frame &&
                _vfx == other._vfx && _position.Equals(other._position);
            public override bool Equals(object obj) => obj is ImpactKey other && Equals(other);
            public override int GetHashCode() => System.HashCode.Combine(_id, _frame, _vfx, _position);
        }
        /// <summary>
        /// 记录阶段表现所属的实体代际，避免旧阶段或退出事件清除新实例。
        /// </summary>
        private readonly Dictionary<int, int> _phaseActorVersions = new Dictionary<int, int>();

        public BattleProjectileViewEventHandler(
            EC.IECWorld world,
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattleViewResourceProvider resources = null,
            BattleProjectileShellPool shellPool = null)
            : this(world, query, vfx, in vfxNode, resources, shellPool, null, null)
        {
        }

        internal BattleProjectileViewEventHandler(
            EC.IECWorld world,
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattleViewResourceProvider resources,
            BattleProjectileShellPool shellPool,
            BattleProjectileViewEventHandlerFactory handlers,
            BattleViewHierarchyManager hierarchy)
        {
            handlers ??= new BattleProjectileViewEventHandlerFactory();

            _query = query;
            _shellPool = shellPool;
            _vfxSpawner = handlers.CreateSpawner(world, vfx, in vfxNode);
            _shellSpawner = handlers.CreateShellSpawner(shellPool, query, hierarchy);
            _vfx = handlers.CreateTriggerResolver(resources);
            _snapshotVfx = handlers.CreateSnapshotResolver(query, resources);
            _deduplicator = handlers.CreateSnapshotDeduplicator();
        }

        public void HandleTriggerHit(in ProjectileHitEvent evt)
        {
            if (!_vfxSpawner.CanSpawn) return;
            if (!_vfx.TryResolveTriggerHit(evt, out var vfxId, out var pos, out var projectileId)) return;

            var hitKey = BattleProjectileSnapshotKey.FromHit(projectileId, evt.TemplateId, evt.LauncherActorId,
                evt.HitCollider.Value, evt.Frame, evt.Point.X, evt.Point.Y, evt.Point.Z);
            if (_deduplicator.HasHitVisual(projectileId, in hitKey)) return;

            var spec = new BattleProjectileVfxSpawnSpec(vfxId, in pos, default);
            var impact = new ImpactKey(projectileId, evt.Frame, vfxId, pos);
            if (TryPairImpact(impact, ImpactSource.Projectile) || _vfxSpawner.TrySpawn(in spec))
            {
                RecordImpact(impact, ImpactSource.Projectile);
                _deduplicator.RecordHitVisual(projectileId, in hitKey);
            }
        }

        public void HandleSnapshot(ProjectileEventData[] entries, int snapshotFrame = 0)
        {
            if (entries == null || entries.Length == 0) return;
            if (!_vfxSpawner.CanSpawn) return;
            if (_query == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                HandleSnapshotEntry(entries[i], snapshotFrame);
            }
        }

        /// <summary>
        /// Call once per frame to update projectile shell positions.
        /// </summary>
        public void Tick()
        {
            _shellSpawner?.Tick();
        }

        /// <summary>
        /// Clears all active projectile shells and VFX.
        /// </summary>
        public void Clear()
        {
            _shellSpawner?.Clear();
            _deduplicator.Clear();
            ClearRecentImpacts();
            _phaseActorVersions.Clear();
            _vfxSpawner.ClearPhaseHandles();
        }

        public void ClearPool() => _shellPool?.Clear();

        private void ClearRecentImpacts()
        {
            _recentImpacts.Clear();
            _impactOrder.Clear();
        }

        /// <summary>
        /// 回滚只解除投射物终态屏蔽；已经播放的命中不可撤销，回执继续参与重演去重。
        /// </summary>
        internal void ResetCompletedProjectilesForReplay() => _deduplicator.ClearCompleted();

        internal bool TryPairCueImpact(long projectileId, int frame, in BattlePresentationCueSpawnRequest request)
        {
            var key = ToCueImpactKey(projectileId, frame, in request);
            if (!key.IsValid || !_recentImpacts.TryGetValue(key, out var prior)) return false;
            if (prior == ImpactSource.Projectile) _recentImpacts[key] = ImpactSource.Paired;
            return true;
        }

        internal void RecordCueImpact(long projectileId, int frame, in BattlePresentationCueSpawnRequest request)
        {
            RecordImpact(ToCueImpactKey(projectileId, frame, in request), ImpactSource.Cue);
        }

        private static ImpactKey ToCueImpactKey(long projectileId, int frame, in BattlePresentationCueSpawnRequest request)
        {
            if (!request.HasExplicitPosition) return default;
            var point = request.ExplicitPosition;
            return new ImpactKey(projectileId, frame, request.VfxId, new Vector3(point.X, point.Y, point.Z));
        }

        private bool TryPairImpact(ImpactKey key, ImpactSource source)
        {
            if (!key.IsValid || !_recentImpacts.TryGetValue(key, out var prior) ||
                prior == source || prior == ImpactSource.Paired) return false;
            _recentImpacts[key] = ImpactSource.Paired;
            return true;
        }

        private void RecordImpact(ImpactKey key, ImpactSource source)
        {
            if (!key.IsValid || _recentImpacts.ContainsKey(key)) return;
            if (_impactOrder.Count == RecentImpactCapacity) _recentImpacts.Remove(_impactOrder.Dequeue());
            _recentImpacts.Add(key, source);
            _impactOrder.Enqueue(key);
        }

        private void HandleSnapshotEntry(ProjectileEventData entry, int snapshotFrame)
        {
            if (entry.Kind == ProjectilePresentationEventKind.Phase)
            {
                if (!_vfxSpawner.HasPhaseBinding(in entry)) return;
                if (_phaseActorVersions.TryGetValue(entry.ProjectileActorId, out var version) && version != entry.ActorVersion)
                {
                    if (entry.ActorVersion < version) return;
                    _vfxSpawner.StopFollowingActor(entry.ProjectileActorId);
                }
                _phaseActorVersions[entry.ProjectileActorId] = entry.ActorVersion;
                if (!_vfxSpawner.ApplyPhase(in entry) && _snapshotVfx.TryResolve(in entry, out var phaseSpec))
                {
                    _vfxSpawner.TrySpawn(in phaseSpec, retainLifecycle: true);
                    _vfxSpawner.ApplyPhase(in entry);
                }
                return;
            }
            if (entry.Kind == ProjectilePresentationEventKind.Exit)
            {
                if (entry.ActorVersion > 0 && _phaseActorVersions.TryGetValue(entry.ProjectileActorId, out var version) &&
                    version != entry.ActorVersion) return;
                _phaseActorVersions.Remove(entry.ProjectileActorId);
                _deduplicator.CompleteLifecycle(in entry);
                _vfxSpawner.StopFollowingActor(entry.ProjectileActorId);
                _shellSpawner?.StopAndReturn(entry.TemplateId, entry.ProjectileActorId);
                return;
            }

            if (!_deduplicator.ShouldHandle(in entry, snapshotFrame)) return;
            var isHit = entry.Kind == ProjectilePresentationEventKind.Hit;
            var identity = BattleProjectileSnapshotDeduplicator.ResolveIdentity(in entry);
            var hitKey = isHit ? BattleProjectileSnapshotKey.From(in entry, snapshotFrame) : default;
            if (isHit && _deduplicator.HasHitVisual(identity, in hitKey)) return;

            if (!_snapshotVfx.TryResolve(in entry, out var spec)) return;
            var impact = isHit ? new ImpactKey(entry.ProjectileId,
                entry.SnapshotFrame > 0 ? entry.SnapshotFrame : snapshotFrame, spec.VfxId, spec.Position) : default;
            var spawnedConfiguredVfx = TryPairImpact(impact, ImpactSource.Projectile) || _vfxSpawner.TrySpawn(in spec);

            // The shell is a fallback for missing/broken VFX, not a second visual for every projectile.
            if (!isHit && !spawnedConfiguredVfx && _shellSpawner != null)
            {
                var position = spec.Position;
                var forward = spec.Rotation * UnityEngine.Vector3.forward;
                _shellSpawner.TrySpawn(
                    entry.TemplateId,
                    entry.ProjectileActorId,
                    in position,
                    in forward,
                    entry.LauncherActorId);
            }

            // Register only after the snapshot produced a visible result. If its
            // configuration is unavailable, a later Trigger event may still provide
            // the presentation and must not be suppressed.
            if (isHit && spawnedConfiguredVfx)
            {
                RecordImpact(impact, ImpactSource.Projectile);
                _deduplicator.RecordHitVisual(identity, in hitKey);
            }
        }
    }

    internal sealed class BattleProjectileViewEventHandlerFactory
    {
        public BattleProjectileVfxSpawner CreateSpawner(
            EC.IECWorld world,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            return new BattleProjectileVfxSpawner(world, vfx, in vfxNode);
        }

        public BattleProjectileShellSpawner CreateShellSpawner(
            BattleProjectileShellPool pool,
            IBattleEntityQuery query,
            BattleViewHierarchyManager hierarchy = null)
        {
            return new BattleProjectileShellSpawner(pool, new BattleProjectileShellFollowResolver(query), hierarchy);
        }

        public BattleProjectileVfxResolver CreateTriggerResolver(BattleViewResourceProvider resources)
        {
            return new BattleProjectileVfxResolver(resources);
        }

        public BattleProjectileSnapshotVfxResolver CreateSnapshotResolver(
            IBattleEntityQuery query,
            BattleViewResourceProvider resources)
        {
            return new BattleProjectileSnapshotVfxResolver(new BattleProjectileFollowTargetResolver(query), resources);
        }

        public BattleProjectileSnapshotDeduplicator CreateSnapshotDeduplicator()
        {
            return new BattleProjectileSnapshotDeduplicator();
        }
    }
}
