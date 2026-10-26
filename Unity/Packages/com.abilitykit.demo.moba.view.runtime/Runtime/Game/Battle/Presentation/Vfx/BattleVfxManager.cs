using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Shared.Assets;
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
        private readonly BattleVfxPrefabCache _prefabs;
        private readonly BattleVfxEntityCollector _collector = new BattleVfxEntityCollector();
        private readonly List<EC.IEntityId> _clearIds = new List<EC.IEntityId>(32);
        private readonly VfxDatabase _database;
        private readonly PresentationEmissionQueue<PresentationRequest> _presentationQueue =
            new PresentationEmissionQueue<PresentationRequest>(256);
        private readonly PresentationEffectPolicyLedger<string> _policies =
            new PresentationEffectPolicyLedger<string>(StringComparer.Ordinal);
        private readonly Dictionary<EC.IEntityId, string> _policyKeys =
            new Dictionary<EC.IEntityId, string>();
        private readonly Func<PresentationRequest, bool> _defaultPresentationEmitter;
        private Func<PresentationRequest, bool> _presentationEmitter;
        private EC.IEntity _tickRoot;
        private BattleViewBinder _tickBinder;
        private IBattleEntityQuery _tickQuery;

        public BattleVfxManager(VfxDatabase db)
            : this(db, null)
        {
        }

        /// <summary>
        /// Backwards-compatible constructor that does not wire the hierarchy manager.
        /// New code should pass a manager via the internal constructor.
        /// </summary>
        internal BattleVfxManager(VfxDatabase db, BattleVfxManagerComponentFactory components)
            : this(db, components, null, null)
        {
        }

        /// <summary>
        /// Internal constructor that wires the hierarchy manager so VFX entities
        /// are parented under the categorized active root.
        /// </summary>
        internal BattleVfxManager(VfxDatabase db, BattleVfxManagerComponentFactory components, BattleViewHierarchyManager hierarchy)
            : this(db, components, hierarchy, null)
        {
        }

        internal BattleVfxManager(VfxDatabase db, BattleVfxManagerComponentFactory components, BattleViewHierarchyManager hierarchy, IBattleAssetLookup assets)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            _database = db;
            components ??= new BattleVfxManagerComponentFactory();

            var prefabs = components.CreatePrefabs(assets);
            var pool = components.CreatePool(db, prefabs, hierarchy);
            var lifetime = components.CreateLifetimePolicy();
            _prefabs = prefabs;
            _pool = pool;
            _factory = components.CreateEntityFactory(db, prefabs, lifetime, pool, hierarchy);
            _followController = components.CreateFollowController(lifetime);
            _defaultPresentationEmitter = EmitQueuedPresentation;
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

        public bool TryResolvePresentationVfxId(in PresentationRequest request, out int vfxId)
        {
            if (request.AssetId > 0)
            {
                vfxId = request.AssetId;
                return true;
            }
            return _database.TryFindIdByResource(request.ResourceKey, out vfxId);
        }

        public bool TryCreatePresentationRequest(
            int vfxId,
            string logicalKey,
            PresentationVector3 position,
            out PresentationRequest request,
            PresentationAttachment attachment = default,
            PresentationRotation rotation = default,
            float scale = 1f)
        {
            return _database.TryCreateRequest(
                vfxId,
                logicalKey,
                position,
                out request,
                attachment,
                rotation,
                scale);
        }

        public int PendingPresentationCount => _presentationQueue.Count;
        public long DroppedPresentationCount => _presentationQueue.DroppedCount;

        /// <summary>Queues a visual emission so callers can absorb bursty skill output.</summary>
        public bool EnqueuePresentation(in PresentationRequest request, int priority = 0)
        {
            return request.IsValid && _presentationQueue.Enqueue(request, priority);
        }

        /// <summary>Sets the consumer used by Tick to create queued presentation instances.</summary>
        public void SetPresentationEmitter(Func<PresentationRequest, bool> emitter)
        {
            _presentationEmitter = emitter;
        }

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

        public bool TryCreateVfxEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, int followTargetActorId, in Vector3 followOffset, in Vector3 position, in Quaternion rotation, int durationMsOverride, out EC.IEntity entity)
        {
            return _factory.TryCreateEntity(world, parent, vfxId, followTarget, followTargetActorId, in followOffset, in position, in rotation, durationMsOverride, out entity);
        }

        public bool TryCreatePresentationVfxEntity(
            EC.IECWorld world,
            EC.IEntity parent,
            in PresentationRequest request,
            EC.IEntityId followTarget,
            out EC.IEntity entity)
        {
            entity = default;
            if (!request.IsValid || world == null || !parent.IsValid) return false;
            if (!TryResolvePresentationVfxId(in request, out var vfxId)) return false;

            var policy = request.Policy;
            if (!policy.IsConfigured) _database.TryGetPolicy(vfxId, out policy);
            var policyKey = BuildPolicyKey(request, policy, vfxId);
            var nowMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            if (!_policies.TryAcquire(policyKey, in policy, nowMs)) return false;

            var position = new Vector3(request.Position.X, request.Position.Y, request.Position.Z);
            var rotation = new Quaternion(request.Rotation.X, request.Rotation.Y, request.Rotation.Z, request.Rotation.W);
            var offset = new Vector3(request.Attachment.Offset.X, request.Attachment.Offset.Y, request.Attachment.Offset.Z);
            var followActorId = request.Attachment.ActorId > 0 ? request.Attachment.ActorId : 0;
            if (!TryCreateVfxEntity(world, parent, vfxId, followTarget, followActorId, in offset, in position, in rotation, request.DurationMs, out entity))
            {
                _policies.Release(policyKey);
                return false;
            }

            _policyKeys[entity.Id] = policyKey;
            return true;
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
            _tickRoot = vfxRoot;
            _tickBinder = binder;
            _tickQuery = query;
            try
            {
                _presentationQueue.Drain(32, TimeSpan.FromMilliseconds(1), _presentationEmitter ?? _defaultPresentationEmitter);
            }
            finally
            {
                _tickRoot = default;
                _tickBinder = null;
                _tickQuery = null;
            }
        }

        public void DestroyVfxEntity(EC.IECWorld world, EC.IEntityId id)
        {
            if (world == null) return;
            if (!world.IsAlive(id))
            {
                ReleasePolicy(id);
                return;
            }

            var e = world.Wrap(id);
            DestroyVfxGameObject(e);
            ReleasePolicy(id);

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
            _presentationQueue.Clear();
            _presentationEmitter = null;
            _policyKeys.Clear();
            _policies.Clear();
            _tickRoot = default;
            _tickBinder = null;
            _tickQuery = null;
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
            _prefabs.Clear();
        }

        private bool EmitQueuedPresentation(PresentationRequest request)
        {
            if (!_tickRoot.IsValid) return false;
            var followTarget = default(EC.IEntityId);
            var followActorId = request.Attachment.ActorId;
            if (followActorId > 0 && _tickQuery != null &&
                _tickQuery.TryResolve(new BattleNetId(followActorId), out var actor))
                followTarget = actor.Id;

            if (!TryCreatePresentationVfxEntity(_tickRoot.World, _tickRoot, in request, followTarget, out var entity)) return false;

            if (entity.TryGetRef(out BattleViewGameObjectComponent goComponent) &&
                goComponent != null && goComponent.GameObject != null)
            {
                goComponent.GameObject.transform.localScale = Vector3.one * request.Scale *
                    (request.Radius > 0f ? request.Radius : 1f);
            }
            return true;
        }

        private void ReleasePolicy(EC.IEntityId id)
        {
            if (!_policyKeys.TryGetValue(id, out var policyKey)) return;
            _policyKeys.Remove(id);
            _policies.Release(policyKey);
        }

        private static string BuildPolicyKey(in PresentationRequest request, in PresentationEffectPolicy policy, int vfxId)
        {
            var group = string.IsNullOrEmpty(policy.GroupKey)
                ? "vfx:" + vfxId
                : policy.GroupKey;
            return group + ":owner:" + request.Attachment.ActorId;
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
        public BattleVfxPrefabCache CreatePrefabs(IBattleAssetLookup assets = null)
        {
            return new BattleVfxPrefabCache(assets: assets);
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
