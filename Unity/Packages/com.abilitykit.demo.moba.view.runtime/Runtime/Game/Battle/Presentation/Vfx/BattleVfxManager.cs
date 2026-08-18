using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Flow;
using AbilityKit.World.ECS;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Battle.Vfx
{
    public sealed class BattleVfxManager
    {
        private readonly BattleVfxEntityFactory _factory;
        private readonly BattleVfxFollowController _followController;
        private readonly BattleVfxGameObjectPool _pool;
        private readonly BattleVfxEntityCollector _collector = new BattleVfxEntityCollector();
        private readonly List<EC.IEntityId> _clearIds = new List<EC.IEntityId>(32);

        public BattleVfxManager(VfxDatabase db)
            : this(db, null)
        {
        }

        /// <summary>
        /// Backwards-compatible constructor that does not wire the hierarchy manager.
        /// New code should pass a manager via the internal constructor.
        /// </summary>
        internal BattleVfxManager(VfxDatabase db, BattleVfxManagerComponentFactory components)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            components ??= new BattleVfxManagerComponentFactory();

            var prefabs = components.CreatePrefabs();
            var pool = components.CreatePool(db, prefabs);
            var lifetime = components.CreateLifetimePolicy();
            _pool = pool;
            _factory = components.CreateEntityFactory(db, prefabs, lifetime, pool);
            _followController = components.CreateFollowController(lifetime);
        }

        /// <summary>
        /// Internal constructor that wires the hierarchy manager so VFX entities
        /// are parented under the categorized active root.
        /// </summary>
        internal BattleVfxManager(VfxDatabase db, BattleVfxManagerComponentFactory components, BattleViewHierarchyManager hierarchy)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            components ??= new BattleVfxManagerComponentFactory();

            var prefabs = components.CreatePrefabs();
            var pool = components.CreatePool(db, prefabs, hierarchy);
            var lifetime = components.CreateLifetimePolicy();
            _pool = pool;
            _factory = components.CreateEntityFactory(db, prefabs, lifetime, pool, hierarchy);
            _followController = components.CreateFollowController(lifetime);
        }

        /// <summary>
        /// Pool managed by this VFX manager. Available when the host (e.g.
        /// <see cref="BattleVfxFeature"/>) opts in via the component factory.
        /// Returns null when pooling is disabled.
        /// </summary>
        internal BattleVfxGameObjectPool Pool => _pool;

        /// <summary>
        /// Public accessor used by diagnostic overlays. Returns the underlying
        /// <see cref="BattleVfxGameObjectPool"/> when pooling is enabled, or null.
        /// </summary>
        public BattleVfxGameObjectPool PoolForStats => _pool;

        /// <summary>
        /// Whether this manager is alive and can create VFX entities.
        /// </summary>
        public bool CanSpawn => _factory != null;

        public bool TryCreateVfxEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, in Vector3 position, out EC.IEntity entity)
        {
            return TryCreateVfxEntity(world, parent, vfxId, followTarget, in position, Quaternion.identity, out entity);
        }

        public bool TryCreateVfxEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, in Vector3 position, in Quaternion rotation, out EC.IEntity entity)
        {
            return TryCreateVfxEntity(world, parent, vfxId, followTarget, 0, in position, in rotation, out entity);
        }

        public bool TryCreateVfxEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, int followTargetActorId, in Vector3 position, in Quaternion rotation, out EC.IEntity entity)
        {
            return TryCreateVfxEntity(world, parent, vfxId, followTarget, followTargetActorId, in position, in rotation, 0, out entity);
        }

        public bool TryCreateVfxEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, int followTargetActorId, in Vector3 position, in Quaternion rotation, int durationMsOverride, out EC.IEntity entity)
        {
            return _factory.TryCreateEntity(world, parent, vfxId, followTarget, followTargetActorId, in position, in rotation, durationMsOverride, out entity);
        }

        /// <summary>
        /// Creates a VFX entity at a world-space position with optional rotation and lifetime.
        /// Convenience overload that does not follow any actor.
        /// </summary>
        public bool TryCreateAoeVfx(in EC.IEntity parent, int vfxId, in Vector3 position, in Quaternion rotation, int durationMsOverride = 0)
        {
            return TryCreateAoeVfx(parent.World, parent, vfxId, in position, in rotation, durationMsOverride);
        }

        /// <summary>
        /// Creates a VFX entity at a world-space position with optional rotation and lifetime.
        /// </summary>
        public bool TryCreateAoeVfx(EC.IECWorld world, in EC.IEntity parent, int vfxId, in Vector3 position, in Quaternion rotation, int durationMsOverride = 0)
        {
            return TryCreateVfxEntity(
                world: world,
                parent: parent,
                vfxId: vfxId,
                followTarget: default,
                followTargetActorId: 0,
                in position,
                in rotation,
                durationMsOverride: durationMsOverride,
                out _);
        }

        public void Tick(in EC.IEntity vfxRoot)
        {
            Tick(vfxRoot, binder: null, query: null);
        }

        public void Tick(in EC.IEntity vfxRoot, BattleViewBinder binder)
        {
            Tick(vfxRoot, binder, query: null);
        }

        public void Tick(in EC.IEntity vfxRoot, BattleViewBinder binder, IBattleEntityQuery query)
        {
            _followController.Tick(vfxRoot, binder, query, DestroyVfxEntity);
        }

        public void DestroyVfxEntity(EC.IECWorld world, EC.IEntityId id)
        {
            if (world == null) return;
            if (!world.IsAlive(id)) return;

            var e = world.Wrap(id);
            DestroyVfxGameObject(e);

            if (e.IsValid) e.Destroy();
        }

        public int DestroyVfxByFollowTargetActorId(in EC.IEntity vfxRoot, int targetActorId)
        {
            return _followController.DestroyByFollowTargetActorId(vfxRoot, targetActorId, DestroyVfxEntity);
        }

        /// <summary>
        /// Destroys every active VFX below the supplied root and releases retained pool objects.
        /// Safe to call repeatedly during partial attach or teardown recovery.
        /// </summary>
        public void Clear(in EC.IEntity vfxRoot)
        {
            if (vfxRoot.IsValid)
            {
                var world = vfxRoot.World;
                _clearIds.Clear();
                _collector.Collect(vfxRoot, _clearIds);
                for (var i = 0; i < _clearIds.Count; i++)
                {
                    DestroyVfxEntity(world, _clearIds[i]);
                }
                _clearIds.Clear();
            }

            _pool?.Clear();
        }

        private void DestroyVfxGameObject(EC.IEntity entity)
        {
            if (!entity.IsValid) return;
            if (entity.TryGetRef(out BattleViewGameObjectComponent goComp) && goComp != null && goComp.GameObject != null)
            {
                var go = goComp.GameObject;

                if (_pool != null)
                {
                    var vfxId = entity.TryGetRef(out BattleVfxComponent vfxComp) && vfxComp != null
                        ? vfxComp.VfxId
                        : BattleVfxPoolableTag.Read(go);
                    if (vfxId > 0 && _pool.Return(vfxId, go))
                    {
                        goComp.GameObject = null;
                        return;
                    }
                }

#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(go);
                else UnityEngine.Object.Destroy(go);
#else
                UnityEngine.Object.Destroy(go);
#endif
                goComp.GameObject = null;
            }
        }

        public void SyncFollow(EC.IECWorld world, EC.IEntityId vfxEntityId, in Vector3 targetPos)
        {
            _followController.SyncFollow(world, vfxEntityId, in targetPos);
        }

        public void SyncFollow(EC.IECWorld world, EC.IEntityId vfxEntityId, in Vector3 targetPos, in Vector3 targetForward)
        {
            _followController.SyncFollow(world, vfxEntityId, in targetPos, in targetForward);
        }
    }

    internal sealed class BattleVfxManagerComponentFactory
    {
        public BattleVfxPrefabCache CreatePrefabs()
        {
            return new BattleVfxPrefabCache();
        }

        public BattleVfxGameObjectPool CreatePool(VfxDatabase db, BattleVfxPrefabCache prefabs)
        {
            return CreatePool(db, prefabs, hierarchy: null);
        }

        public BattleVfxGameObjectPool CreatePool(VfxDatabase db, BattleVfxPrefabCache prefabs, BattleViewHierarchyManager hierarchy)
        {
            var gameObjects = new BattleVfxGameObjectFactory(prefabs);
            return new BattleVfxGameObjectPool(vfxId =>
            {
                if (db.TryGet(vfxId, out var dto) && dto != null && !string.IsNullOrEmpty(dto.Resource))
                {
                    return gameObjects.Create(vfxId, dto.Resource);
                }

                return gameObjects.CreatePlaceholder(vfxId);
            }, hierarchy: hierarchy);
        }

        public BattleVfxLifetimePolicy CreateLifetimePolicy()
        {
            return new BattleVfxLifetimePolicy();
        }

        public BattleVfxEntityFactory CreateEntityFactory(
            VfxDatabase db,
            BattleVfxPrefabCache prefabs,
            BattleVfxLifetimePolicy lifetime,
            BattleVfxGameObjectPool pool = null,
            BattleViewHierarchyManager hierarchy = null)
        {
            return new BattleVfxEntityFactory(db, prefabs, lifetime, pool: pool, hierarchy: hierarchy);
        }

        public BattleVfxFollowController CreateFollowController(BattleVfxLifetimePolicy lifetime)
        {
            return new BattleVfxFollowController(lifetime);
        }
    }
}
