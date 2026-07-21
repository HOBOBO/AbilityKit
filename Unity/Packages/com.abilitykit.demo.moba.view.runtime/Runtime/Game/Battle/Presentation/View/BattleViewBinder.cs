using System;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Vfx;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleViewBinder : IMonoViewHandleRegistry
    {
        private readonly BattleViewHandleStore _handles = new BattleViewHandleStore();
        private readonly BattleViewShellController _shells;
        private readonly BattleViewAttachedVfxController _attachedVfx;
        private readonly BattleViewTransformController _transforms;
        private readonly BattleViewEntitySyncController _sync;
        private readonly BattleViewLifecycleController _lifecycle;
        private readonly BattleViewHandleQuery _queries;
        private readonly BattleViewWorldRebinder _rebinder;
        private readonly BattleViewMonoHandleDestroyHandler _monoHandleDestroy;

        public BattleViewBinder(
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            IBattleViewShellLoader shellLoader = null,
            BattleViewResourceProvider resources = null)
            : this(vfx, in vfxNode, resources, pool: null, controllers: null)
        {
        }

        internal BattleViewBinder(
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattleViewResourceProvider resources,
            BattleViewShellPool pool,
            BattleViewBinderControllerFactory controllers,
            BattleViewHierarchyManager hierarchy = null)
        {
            resources = BattleViewResourceProvider.OrDefault(resources);
            controllers ??= new BattleViewBinderControllerFactory();

            _shells = controllers.CreateShells(resources, this, pool, hierarchy);
            _attachedVfx = controllers.CreateAttachedVfx(vfx, in vfxNode, resources);
            _transforms = controllers.CreateTransforms(_handles, _attachedVfx);
            _sync = controllers.CreateSync(_handles, _shells, _attachedVfx, _transforms, resources);
            _lifecycle = controllers.CreateLifecycle(_handles, _shells, _attachedVfx, _transforms);
            _queries = controllers.CreateQueries(_handles);
            _rebinder = controllers.CreateRebinder(_sync);
            _monoHandleDestroy = controllers.CreateMonoHandleDestroyer(_handles);
        }

        public bool InterpolationEnabled
        {
            get => _transforms.InterpolationEnabled;
            set => _transforms.InterpolationEnabled = value;
        }

        public float BackTimeTicks
        {
            get => _transforms.BackTimeTicks;
            set => _transforms.BackTimeTicks = value;
        }

        public float MaxLagTicks
        {
            get => _transforms.MaxLagTicks;
            set => _transforms.MaxLagTicks = value;
        }

        public float SmoothingHz
        {
            get => _transforms.SmoothingHz;
            set => _transforms.SmoothingHz = value;
        }

        public bool TryGetShellGameObject(EC.IEntityId id, out GameObject go)
        {
            return _queries.TryGetShellGameObject(id, out go);
        }

        public bool TryGetInterpolatedPos(EC.IEntityId id, out Vector3 pos)
        {
            return _transforms.TryGetInterpolatedPos(id, out pos);
        }

        public void ForEachShellGameObject(Action<int, EC.IEntityId, GameObject> visitor)
        {
            _queries.ForEachShellGameObject(visitor);
        }

        public bool TryGetAttachRoot(BattleNetId netId, out Transform transform)
        {
            return _queries.TryGetAttachRoot(netId, out transform);
        }

        public void Sync(EC.IEntity entity)
        {
            _sync.Sync(entity);
        }

        public void Sync(EC.IEntity entity, BattleContext ctx)
        {
            _sync.Sync(entity, ctx);
        }

        public void TickInterpolation(BattleContext ctx, float deltaTime)
        {
            _transforms.Tick(ctx, deltaTime);
        }

        public void OnDestroyed(EC.IEntityId id)
        {
            _lifecycle.OnDestroyed(id);
        }

        public void Clear()
        {
            _lifecycle.Clear();
        }

        public void RebindAll(EC.IECWorld world)
        {
            _rebinder.RebindAll(world);
        }

        public void RebindAll(EC.IECWorld world, BattleContext ctx)
        {
            _rebinder.RebindAll(world, ctx);
        }

        void IMonoViewHandleRegistry.OnMonoViewHandleDestroyed(MonoViewHandle handle)
        {
            _monoHandleDestroy.OnDestroyed(handle);
        }
    }

        internal sealed class BattleViewBinderControllerFactory
        {
            /// <summary>
            /// Creates a shell controller backed by the given pool.
            /// The pool must be constructed with a factory so it can create fresh instances on demand.
            /// </summary>
            public BattleViewShellController CreateShells(
                BattleViewResourceProvider resources,
                IMonoViewHandleRegistry registry,
                BattleViewShellPool pool,
                BattleViewHierarchyManager hierarchy = null)
            {
                resources = BattleViewResourceProvider.OrDefault(resources);

                IBattleViewShellLoader loader;
                if (pool != null)
                {
                    loader = new PooledBattleViewShellLoader(pool, hierarchy);
                }
                else
                {
                    loader = new ResourceBattleViewShellLoader(resources);
                }

                var handleBinder = new BattleViewShellHandleBinder(registry);
                var destroyer = new BattleViewShellDestroyer(handleBinder, pool);
                return new BattleViewShellController(loader, registry, handleBinder, destroyer);
            }

        public BattleViewAttachedVfxController CreateAttachedVfx(
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattleViewResourceProvider resources)
        {
            return new BattleViewAttachedVfxController(vfx, in vfxNode, resources);
        }

        public BattleViewTransformController CreateTransforms(
            BattleViewHandleStore handles,
            BattleViewAttachedVfxController attachedVfx)
        {
            return new BattleViewTransformController(handles, attachedVfx);
        }

        public BattleViewEntitySyncController CreateSync(
            BattleViewHandleStore handles,
            BattleViewShellController shells,
            BattleViewAttachedVfxController attachedVfx,
            BattleViewTransformController transforms,
            BattleViewResourceProvider resources)
        {
            return new BattleViewEntitySyncController(handles, shells, attachedVfx, transforms, resources);
        }

        public BattleViewLifecycleController CreateLifecycle(
            BattleViewHandleStore handles,
            BattleViewShellController shells,
            BattleViewAttachedVfxController attachedVfx,
            BattleViewTransformController transforms)
        {
            return new BattleViewLifecycleController(handles, shells, attachedVfx, transforms);
        }

        public BattleViewHandleQuery CreateQueries(BattleViewHandleStore handles)
        {
            return new BattleViewHandleQuery(handles);
        }

        public BattleViewWorldRebinder CreateRebinder(BattleViewEntitySyncController sync)
        {
            return new BattleViewWorldRebinder(sync);
        }

        public BattleViewMonoHandleDestroyHandler CreateMonoHandleDestroyer(BattleViewHandleStore handles)
        {
            return new BattleViewMonoHandleDestroyHandler(handles);
        }
    }
}
