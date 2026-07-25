using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using UnityEngine;
using EC = AbilityKit.World.ECS;
using EntityBattleNetId = AbilityKit.Game.Battle.Entity.BattleNetId;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattlePresentationCueViewEventHandler
    {
        private readonly BattleContext _ctx;
        private readonly IBattleEntityQuery _query;
        private readonly BattlePresentationCueVfxSpawner _spawner;
        private readonly BattlePresentationCueResolver _resolver;
        private readonly Dictionary<BattlePresentationCueRequestKey, EC.IEntityId> _activeByRequestKey = new();

        public BattlePresentationCueViewEventHandler(
            BattleContext ctx,
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
            : this(ctx, query, vfx, in vfxNode, null)
        {
        }

        internal BattlePresentationCueViewEventHandler(
            BattleContext ctx,
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattlePresentationCueViewEventHandlerFactory handlers)
        {
            _ctx = ctx;
            _query = query;
            handlers ??= new BattlePresentationCueViewEventHandlerFactory();
            _spawner = handlers.CreateSpawner(ctx, vfx, in vfxNode);
            _resolver = handlers.CreateResolver();
        }

        public void HandleSnapshot(PresentationCueData[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (!_spawner.CanSpawn) return;

            for (int i = 0; i < entries.Length; i++)
            {
                HandleSnapshotEntry(entries[i]);
            }
        }

        private void HandleSnapshotEntry(in PresentationCueData data)
        {
            var decision = _resolver.Resolve(in data);
            if (decision.IsNone) return;

            if (decision.Kind == BattlePresentationCueDecisionKind.Play)
            {
                Play(in decision, in data);
                return;
            }

            if (decision.Kind == BattlePresentationCueDecisionKind.Stop)
            {
                Stop(decision.RequestKey);
            }
        }

        private void Play(in BattlePresentationCueDecision decision, in PresentationCueData data)
        {
            var spawnRequest = decision.SpawnRequest;
            var position = ResolvePosition(in spawnRequest);
            var followTarget = ResolveFollowTarget(in spawnRequest);

            if (_activeByRequestKey.TryGetValue(decision.RequestKey, out var existingId))
            {
                // Refresh: update existing VFX parameters instead of ignoring.
                _spawner.Update(existingId, spawnRequest.Scale, spawnRequest.Radius, spawnRequest.DurationMsOverride);
                return;
            }

            if (_spawner.TrySpawn(spawnRequest.VfxId, in position, followTarget, spawnRequest.DurationMsOverride, spawnRequest.Scale, spawnRequest.Radius, out var entity))
            {
                _activeByRequestKey[decision.RequestKey] = entity.Id;
            }
        }

        private void Stop(BattlePresentationCueRequestKey requestKey)
        {
            if (!_activeByRequestKey.TryGetValue(requestKey, out var entityId)) return;

            _activeByRequestKey.Remove(requestKey);
            _spawner.Destroy(entityId);
        }

        private Vector3 ResolvePosition(in BattlePresentationCueSpawnRequest request)
        {
            var offset = ToVector3(request.Offset);
            if (request.HasExplicitPosition) return ToVector3(request.ExplicitPosition) + offset;
            if (TryResolveActorPosition(request.TargetActorId, out var targetPosition)) return targetPosition + offset;
            if (TryResolveActorPosition(request.FirstTargetActorId, out var firstTargetPosition)) return firstTargetPosition + offset;
            if (TryResolveActorPosition(request.SourceActorId, out var sourcePosition)) return sourcePosition + offset;
            return offset;
        }

        private EC.IEntityId ResolveFollowTarget(in BattlePresentationCueSpawnRequest request)
        {
            if (TryResolveActorEntity(request.TargetActorId, out var target)) return target.Id;
            if (TryResolveActorEntity(request.FirstTargetActorId, out var firstTarget)) return firstTarget.Id;
            if (TryResolveActorEntity(request.SourceActorId, out var source)) return source.Id;
            return default;
        }

        private static Vector3 ToVector3(SnapshotVec3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private bool TryResolveActorPosition(int actorId, out Vector3 position)
        {
            position = default;
            if (!TryResolveActorEntity(actorId, out var entity)) return false;
            if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null) return false;

            position = transform.Position;
            return true;
        }

        private bool TryResolveActorEntity(int actorId, out EC.IEntity entity)
        {
            entity = default;
            if (actorId <= 0 || _query == null) return false;

            return _query.TryResolve(new EntityBattleNetId(actorId), out entity);
        }
    }

    internal sealed class BattlePresentationCueViewEventHandlerFactory
    {
        public BattlePresentationCueResolver CreateResolver()
        {
            return new BattlePresentationCueResolver();
        }

        public BattlePresentationCueVfxSpawner CreateSpawner(
            BattleContext ctx,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            return new BattlePresentationCueVfxSpawner(ctx, vfx, in vfxNode);
        }
    }

    internal sealed class BattlePresentationCueVfxSpawner
    {
        private readonly BattleContext _ctx;
        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;

        public BattlePresentationCueVfxSpawner(BattleContext ctx, BattleVfxManager vfx, in EC.IEntity vfxNode)
        {
            _ctx = ctx;
            _vfx = vfx;
            _vfxNode = vfxNode;
        }

        public bool CanSpawn
        {
            get
            {
                if (_ctx?.EntityWorld == null) return false;
                if (_vfx == null) return false;
                if (!_vfxNode.IsValid) return false;
                return true;
            }
        }

        public bool TrySpawn(int vfxId, in Vector3 position, EC.IEntityId followTarget, int durationMsOverride, float scale, float radius, out EC.IEntity entity)
        {
            entity = default;
            if (!CanSpawn) return false;
            if (vfxId <= 0) return false;

            if (!_vfx.TryCreateVfxEntity(
                    _ctx.EntityWorld,
                    _vfxNode,
                    vfxId,
                    followTarget,
                    0,
                    in position,
                    Quaternion.identity,
                    durationMsOverride,
                    out entity))
            {
                return false;
            }

            ApplyPresentationScale(entity, scale, radius);
            return true;
        }

        private static void ApplyPresentationScale(EC.IEntity entity, float scale, float radius)
        {
            if (!entity.IsValid) return;
            if (!entity.TryGetRef(out BattleViewGameObjectComponent goComp) || goComp == null || goComp.GameObject == null) return;

            var resolvedScale = scale > 0f ? scale : 1f;
            var radiusScale = radius > 0f ? radius : 1f;
            goComp.GameObject.transform.localScale = Vector3.one * resolvedScale * radiusScale;
        }

        public void Destroy(EC.IEntityId id)
        {
            if (_ctx?.EntityWorld == null) return;
            if (id == default) return;

            _vfx.DestroyVfxEntity(_ctx.EntityWorld, id);
        }

        /// <summary>
        /// Updates an active VFX instance for a Cue refresh. A positive duration override
        /// restarts the remaining visual lifetime; zero or negative values preserve it.
        /// </summary>
        public void Update(EC.IEntityId id, float scale, float radius, int durationMsOverride)
        {
            if (!id.IsValid) return;

            ApplyPresentationScale(id, scale, radius);
            RefreshLifetime(id, durationMsOverride);
        }

        private void RefreshLifetime(EC.IEntityId id, int durationMsOverride)
        {
            if (durationMsOverride <= 0) return;

            var world = _ctx?.EntityWorld;
            if (world == null || !world.IsAlive(id)) return;

            var entity = world.Wrap(id);
            if (!entity.TryGetRef(out BattleVfxLifetimeComponent lifetime) || lifetime == null) return;

            lifetime.ExpireAtTime = Time.time + (durationMsOverride / 1000f);
        }

        private void ApplyPresentationScale(EC.IEntityId id, float scale, float radius)
        {
            if (!id.IsValid) return;
            var world = _ctx.EntityWorld;
            if (world == null || !world.IsAlive(id)) return;
            var entity = world.Wrap(id);
            if (!entity.TryGetRef(out BattleViewGameObjectComponent goComp) || goComp == null || goComp.GameObject == null) return;

            var resolvedScale = scale > 0f ? scale : 1f;
            var radiusScale = radius > 0f ? radius : 1f;
            goComp.GameObject.transform.localScale = Vector3.one * resolvedScale * radiusScale;
        }
    }
}
