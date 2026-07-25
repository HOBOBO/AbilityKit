using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Triggering;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Protocol.Moba.StateSync;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleProjectileViewEventHandler
    {
        private readonly IBattleEntityQuery _query;
        private readonly BattleProjectileVfxSpawner _vfxSpawner;
        private readonly BattleProjectileShellSpawner _shellSpawner;
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
            BattleContext ctx,
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattleViewResourceProvider resources = null,
            BattleProjectileShellPool shellPool = null)
            : this(ctx, query, vfx, in vfxNode, resources, shellPool, null, null)
        {
        }

        internal BattleProjectileViewEventHandler(
            BattleContext ctx,
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
            _vfxSpawner = handlers.CreateSpawner(ctx, vfx, in vfxNode);
            _shellSpawner = handlers.CreateShellSpawner(shellPool, query, hierarchy);
            _vfx = handlers.CreateTriggerResolver(resources);
            _snapshotVfx = handlers.CreateSnapshotResolver(query, resources);
            _deduplicator = handlers.CreateSnapshotDeduplicator();
        }

        public void HandleTriggerHit(in TriggerEvent evt)
        {
            if (!_vfxSpawner.CanSpawn) return;
            if (!_vfx.TryResolveTriggerHit(evt, out var vfxId, out var pos, out var projectileId)) return;

            // Cross-source deduplication: if this projectile already produced a hit VFX
            // (via Snapshot), skip the Trigger path.
            if (projectileId > 0 && !_seenHitActorIds.Add(projectileId)) return;

            var spec = new BattleProjectileVfxSpawnSpec(vfxId, in pos, default);
            _vfxSpawner.TrySpawn(in spec);
        }

        public void HandleSnapshot(MobaProjectileEventSnapshotEntry[] entries)
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
            ResetCrossSourceDeduplication();
        }

        /// <summary>
        /// Clears cross-source deduplication state.
        /// Call this when entering a new world or starting a replay.
        /// </summary>
        public void ResetCrossSourceDeduplication()
        {
            _seenHitActorIds.Clear();
        }

        private void HandleSnapshotEntry(MobaProjectileEventSnapshotEntry entry)
        {
            if (!_deduplicator.ShouldHandle(in entry)) return;

            if (entry.Kind == (int)ProjectileEventKind.Exit)
            {
                _vfxSpawner.StopFollowingActor(entry.ProjectileActorId);
                _shellSpawner?.StopAndReturn(entry.TemplateId, entry.ProjectileActorId);
                return;
            }

            // For hit events, register in cross-source deduplication so Trigger path skips.
            if (entry.Kind == (int)ProjectileEventKind.Hit && entry.ProjectileActorId > 0)
            {
                _seenHitActorIds.Add(entry.ProjectileActorId);
            }

            if (!_snapshotVfx.TryResolve(in entry, out var spec)) return;
            _vfxSpawner.TrySpawn(in spec);

            if (_shellSpawner != null)
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

    internal readonly struct BattleProjectileSnapshotKey : IEquatable<BattleProjectileSnapshotKey>
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

        public static BattleProjectileSnapshotKey From(in MobaProjectileEventSnapshotEntry entry)
        {
            var identity = entry.ProjectileId > 0 ? entry.ProjectileId : entry.ProjectileActorId;
            if (identity > 0)
            {
                return new BattleProjectileSnapshotKey(
                    entry.Kind,
                    identity,
                    entry.TemplateId,
                    0,
                    0,
                    0,
                    0,
                    hasIdentity: true);
            }

            return new BattleProjectileSnapshotKey(
                entry.Kind,
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

    internal sealed class BattleProjectileSnapshotDeduplicator
    {
        private readonly HashSet<BattleProjectileSnapshotKey> _handled = new HashSet<BattleProjectileSnapshotKey>();

        public bool ShouldHandle(in MobaProjectileEventSnapshotEntry entry)
        {
            var key = BattleProjectileSnapshotKey.From(in entry);
            return _handled.Add(key);
        }
    }

    internal sealed class BattleProjectileViewEventHandlerFactory
    {
        public BattleProjectileVfxSpawner CreateSpawner(
            BattleContext ctx,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            return new BattleProjectileVfxSpawner(ctx, vfx, in vfxNode);
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
