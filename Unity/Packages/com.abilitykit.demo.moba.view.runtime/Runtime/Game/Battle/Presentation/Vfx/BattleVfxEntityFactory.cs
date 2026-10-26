using System;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Flow;
using AbilityKit.World.ECS;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Battle.Vfx
{
    internal sealed class BattleVfxEntityFactory
    {
        private readonly VfxDatabase _db;
        private readonly BattleVfxGameObjectFactory _gameObjects;
        private readonly BattleVfxLifetimePolicy _lifetime;
        private readonly BattleVfxEntityBuilder _entities;
        private readonly BattleViewHierarchyManager _hierarchy;

        public BattleVfxEntityFactory(
            VfxDatabase db,
            BattleVfxPrefabCache prefabs,
            BattleVfxLifetimePolicy lifetime = null,
            BattleVfxGameObjectFactory gameObjects = null,
            BattleVfxGameObjectPool pool = null,
            BattleViewHierarchyManager hierarchy = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _lifetime = lifetime ?? new BattleVfxLifetimePolicy();
            _gameObjects = gameObjects ?? new BattleVfxGameObjectFactory(prefabs, pool: pool);
            _entities = new BattleVfxEntityBuilder(_lifetime);
            _hierarchy = hierarchy;
        }

        public bool TryCreateEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, in Vector3 position, out EC.IEntity entity)
        {
            return TryCreateEntity(world, parent, vfxId, followTarget, in position, Quaternion.identity, out entity);
        }

        public bool TryCreateEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, in Vector3 position, in Quaternion rotation, out EC.IEntity entity)
        {
            return TryCreateEntity(world, parent, vfxId, followTarget, 0, in position, in rotation, out entity);
        }

        public bool TryCreateEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, int followTargetActorId, in Vector3 position, in Quaternion rotation, out EC.IEntity entity)
        {
            return TryCreateEntity(world, parent, vfxId, followTarget, followTargetActorId, in position, in rotation, 0, out entity);
        }

        public bool TryCreateEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, int followTargetActorId, in Vector3 position, in Quaternion rotation, int durationMsOverride, out EC.IEntity entity)
        {
            var followOffset = Vector3.zero;
            return TryCreateEntity(world, parent, vfxId, followTarget, followTargetActorId, in followOffset, in position, in rotation, durationMsOverride, out entity);
        }

        public bool TryCreateEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, int followTargetActorId, in Vector3 followOffset, in Vector3 position, in Quaternion rotation, int durationMsOverride, out EC.IEntity entity)
        {
            entity = default;
            if (world == null) return false;
            if (!parent.IsValid) return false;
            if (!ReferenceEquals(parent.World, world)) return false;
            if (vfxId <= 0) return false;

            var durationMs = 650;
            GameObject go;
            if (_db.TryGet(vfxId, out var dto) && dto != null)
            {
                var resource = string.IsNullOrEmpty(dto.ResourceKey) ? dto.Resource : dto.ResourceKey;
                go = !string.IsNullOrEmpty(resource)
                    ? _gameObjects.Create(vfxId, resource)
                    : _gameObjects.CreatePlaceholder(vfxId);
                durationMs = dto.DurationMs;
            }
            else
            {
                go = _gameObjects.CreatePlaceholder(vfxId);
            }

            if (go == null) return false;

            if (durationMsOverride > 0)
            {
                durationMs = durationMsOverride;
            }

            try
            {
                go.transform.SetPositionAndRotation(position, rotation);

                if (vfxId > 0 && go.GetComponent<BattleVfxCueMetadata>() == null)
                    go.AddComponent<BattleVfxCueMetadata>();

                if (_hierarchy != null)
                {
                    _hierarchy.ParentActive(BattleViewCategory.ActiveVfx, vfxId, go);
                }

                entity = _entities.Create(world, parent, vfxId, followTarget, followTargetActorId, in followOffset, go, durationMs);
                return true;
            }
            catch
            {
                _gameObjects.ReturnOrDestroy(vfxId, go);
                throw;
            }
        }
    }

    internal sealed class BattleVfxEntityBuilder
    {
        private readonly BattleVfxLifetimePolicy _lifetime;

        public BattleVfxEntityBuilder(BattleVfxLifetimePolicy lifetime)
        {
            _lifetime = lifetime ?? new BattleVfxLifetimePolicy();
        }

        public EC.IEntity Create(
            EC.IECWorld world,
            EC.IEntity parent,
            int vfxId,
            EC.IEntityId followTarget,
            int followTargetActorId,
            in Vector3 followOffset,
            GameObject go,
            int durationMs)
        {
            var vfxEntity = world.CreateChild(parent);
            try
            {
                vfxEntity.SetName($"Vfx_{vfxId}");
                vfxEntity.WithRef(new BattleVfxComponent { VfxId = vfxId });
                vfxEntity.WithRef(new BattleViewGameObjectComponent { GameObject = go });
                vfxEntity.WithRef(new BattleViewFollowComponent { Target = followTarget, TargetActorId = followTargetActorId, Offset = followOffset });

                _lifetime.AttachIfNeeded(vfxEntity, durationMs);
                return vfxEntity;
            }
            catch
            {
                if (vfxEntity.IsValid) vfxEntity.Destroy();
                throw;
            }
        }
    }
}
