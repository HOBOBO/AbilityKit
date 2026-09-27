using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using UnityEngine;
using EC = AbilityKit.World.ECS;
using EntityBattleNetId = AbilityKit.Game.Battle.Entity.BattleNetId;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattlePresentationCueUnityPort :
        IPresentationCueViewPort<
            BattlePresentationCueRequestKey,
            BattlePresentationCueSpawnRequest,
            EC.IEntityId>,
        IPresentationActorPlacementSource
    {
        private readonly IBattleEntityQuery _query;
        private readonly BattlePresentationCueVfxSpawner _spawner;

        public BattlePresentationCueUnityPort(
            IBattleEntityQuery query,
            BattlePresentationCueVfxSpawner spawner)
        {
            _query = query;
            _spawner = spawner;
        }

        public bool IsAlive(EC.IEntityId handle)
        {
            return _spawner != null && _spawner.IsAlive(handle);
        }

        public bool TryCreate(
            BattlePresentationCueRequestKey key,
            in BattlePresentationCueSpawnRequest payload,
            out EC.IEntityId handle)
        {
            handle = default;
            if (_spawner == null) return false;

            var position = ToVector3(BattlePresentationCuePlacementResolver.ResolvePosition(in payload, this));
            var followActorId = BattlePresentationCuePlacementResolver.ResolveFollowActorId(in payload, this);
            var followTarget = TryResolveActorEntity(followActorId, out var target) ? target.Id : default;
            if (!_spawner.TrySpawn(
                    payload.VfxId,
                    in position,
                    followTarget,
                    payload.DurationMsOverride,
                    payload.Scale,
                    payload.Radius,
                    out var entity))
            {
                return false;
            }

            handle = entity.Id;
            return true;
        }

        public void Update(
            EC.IEntityId handle,
            in BattlePresentationCueSpawnRequest payload)
        {
            _spawner?.Update(
                handle,
                payload.Scale,
                payload.Radius,
                payload.DurationMsOverride);
        }

        public void Destroy(EC.IEntityId handle)
        {
            _spawner?.Destroy(handle);
        }

        private static Vector3 ToVector3(SnapshotVec3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        public bool TryGetPosition(int actorId, out SnapshotVec3 position)
        {
            position = default;
            if (!TryResolveActorEntity(actorId, out var entity)) return false;
            if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null) return false;

            position = new SnapshotVec3(transform.Position.x, transform.Position.y, transform.Position.z);
            return true;
        }

        public bool HasActor(int actorId)
        {
            return TryResolveActorEntity(actorId, out _);
        }

        private bool TryResolveActorEntity(int actorId, out EC.IEntity entity)
        {
            entity = default;
            if (actorId <= 0 || _query == null) return false;

            return _query.TryResolve(new EntityBattleNetId(actorId), out entity);
        }
    }

    internal sealed class BattlePresentationCueVfxSpawner
    {
        private readonly EC.IECWorld _world;
        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;

        public BattlePresentationCueVfxSpawner(
            EC.IECWorld world,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            _world = world;
            _vfx = vfx;
            _vfxNode = vfxNode;
        }

        public bool CanSpawn
        {
            get
            {
                if (_world == null) return false;
                if (_vfx == null) return false;
                if (!_vfxNode.IsValid) return false;
                return true;
            }
        }

        public bool IsAlive(EC.IEntityId id)
        {
            return id.IsValid && _world != null && _world.IsAlive(id);
        }

        public bool TrySpawn(
            int vfxId,
            in Vector3 position,
            EC.IEntityId followTarget,
            int durationMsOverride,
            float scale,
            float radius,
            out EC.IEntity entity)
        {
            entity = default;
            if (!CanSpawn || vfxId <= 0) return false;

            if (!_vfx.TryCreateVfxEntity(
                    _world,
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

        public void Destroy(EC.IEntityId id)
        {
            if (_world == null || id == default) return;
            _vfx.DestroyVfxEntity(_world, id);
        }

        public void Update(
            EC.IEntityId id,
            float scale,
            float radius,
            int durationMsOverride)
        {
            if (!id.IsValid) return;

            ApplyPresentationScale(id, scale, radius);
            RefreshLifetime(id, durationMsOverride);
        }

        private static void ApplyPresentationScale(
            EC.IEntity entity,
            float scale,
            float radius)
        {
            if (!entity.IsValid) return;
            if (!entity.TryGetRef(out BattleViewGameObjectComponent goComp) ||
                goComp == null ||
                goComp.GameObject == null)
            {
                return;
            }

            var resolvedScale = scale > 0f ? scale : 1f;
            var radiusScale = radius > 0f ? radius : 1f;
            goComp.GameObject.transform.localScale =
                Vector3.one * resolvedScale * radiusScale;
        }

        private void RefreshLifetime(EC.IEntityId id, int durationMsOverride)
        {
            if (durationMsOverride <= 0) return;
            if (_world == null || !_world.IsAlive(id)) return;

            var entity = _world.Wrap(id);
            if (!entity.TryGetRef(out BattleVfxLifetimeComponent lifetime) ||
                lifetime == null)
            {
                return;
            }

            lifetime.ExpireAtTime = Time.time + (durationMsOverride / 1000f);
        }

        private void ApplyPresentationScale(
            EC.IEntityId id,
            float scale,
            float radius)
        {
            if (!id.IsValid || _world == null || !_world.IsAlive(id)) return;

            var entity = _world.Wrap(id);
            ApplyPresentationScale(entity, scale, radius);
        }
    }
}
