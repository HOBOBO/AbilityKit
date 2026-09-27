using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Vfx;
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
        /// <summary>
        /// Cross-source deduplication: tracks which projectile actor IDs have already
        /// produced a hit VFX this session (regardless of whether the signal came
        /// from Trigger or Snapshot). Cleared on world change to avoid stale entries
        /// after reconnect or replay.
        /// </summary>
        private readonly HashSet<int> _seenHitActorIds = new HashSet<int>();

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

            // Cross-source deduplication: if this projectile already produced a hit VFX
            // (via Snapshot), skip the Trigger path.
            if (projectileId > 0 && !_seenHitActorIds.Add(projectileId)) return;

            var spec = new BattleProjectileVfxSpawnSpec(vfxId, in pos, default);
            _vfxSpawner.TrySpawn(in spec);
        }

        public void HandleSnapshot(ProjectileEventData[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (!_vfxSpawner.CanSpawn) return;
            if (_query == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                HandleSnapshotEntry(entries[i]);
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
            ResetCrossSourceDeduplication();
        }

        public void ClearPool() => _shellPool?.Clear();

        /// <summary>
        /// Clears cross-source deduplication state.
        /// Call this when entering a new world or starting a replay.
        /// </summary>
        public void ResetCrossSourceDeduplication()
        {
            _seenHitActorIds.Clear();
        }

        private void HandleSnapshotEntry(ProjectileEventData entry)
        {
            if (entry.Kind == ProjectilePresentationEventKind.Exit)
            {
                // Exit cleanup is idempotent. Completed projectile identities must not remain
                // in session-lifetime caches after a high-volume launch has finished.
                _deduplicator.ForgetLifecycle(in entry);
                _seenHitActorIds.Remove(entry.ProjectileActorId);
                _vfxSpawner.StopFollowingActor(entry.ProjectileActorId);
                _shellSpawner?.StopAndReturn(entry.TemplateId, entry.ProjectileActorId);
                return;
            }

            if (!_deduplicator.ShouldHandle(in entry)) return;

            // For hit events, register in cross-source deduplication so Trigger path skips.
            if (entry.Kind == ProjectilePresentationEventKind.Hit && entry.ProjectileActorId > 0)
            {
                _seenHitActorIds.Add(entry.ProjectileActorId);
            }

            if (!_snapshotVfx.TryResolve(in entry, out var spec)) return;
            var spawnedConfiguredVfx = _vfxSpawner.TrySpawn(in spec);

            // The shell is a fallback for missing/broken VFX, not a second visual for every projectile.
            if (!spawnedConfiguredVfx && _shellSpawner != null)
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
