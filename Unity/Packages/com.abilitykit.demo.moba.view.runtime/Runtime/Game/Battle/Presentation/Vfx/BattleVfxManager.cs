using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Platform.Unity.Presentation;
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
        private readonly Dictionary<EC.IEntityId, PresentationRequest> _requests = new Dictionary<EC.IEntityId, PresentationRequest>();
        private readonly IPresentationLifetimeAdapter<GameObject> _lifetimeAdapter;
        private readonly IPresentationOrderingAdapter<GameObject> _orderingAdapter = new UnityPresentationOrderingAdapter();
        private readonly PresentationRateResolver _rateResolver = new PresentationRateResolver();

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

        internal BattleVfxManager(VfxDatabase db, BattleVfxManagerComponentFactory components, BattleViewHierarchyManager hierarchy, IBattleAssetLookup assets,
            IPresentationLifetimeAdapter<GameObject> lifetimeAdapter = null)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            _database = db;
            _lifetimeAdapter = lifetimeAdapter ?? new UnityPresentationLifetimeAdapter();
            components ??= new BattleVfxManagerComponentFactory();

            var prefabs = components.CreatePrefabs(assets);
            var pool = components.CreatePool(db, prefabs, hierarchy);
            var lifetime = components.CreateLifetimePolicy();
            _prefabs = prefabs;
            _pool = pool;
            _factory = components.CreateEntityFactory(db, prefabs, lifetime, pool, hierarchy);
            _followController = components.CreateFollowController(lifetime, _lifetimeAdapter);
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
            if (request.Lifecycle.Stack != PresentationStackPolicy.Independent)
            {
                var sameKey = new List<EC.IEntityId>();
                foreach (var pair in _requests)
                {
                    if (!world.IsAlive(pair.Key) || pair.Value.LogicalKey != request.LogicalKey ||
                        pair.Value.Attachment.ActorId != request.Attachment.ActorId) continue;
                    var existing = world.Wrap(pair.Key);
                    if (request.Lifecycle.Stack == PresentationStackPolicy.RejectSameKey &&
                        existing.TryGetRef(out BattlePresentationLifetimeComponent life) && life != null && !life.State.IsStopping) return false;
                    sameKey.Add(pair.Key);
                }
                if (request.Lifecycle.Stack == PresentationStackPolicy.ReplaceSameKey)
                {
                    var replacing = 0;
                    foreach (var previous in sameKey)
                        if (_policyKeys.TryGetValue(previous, out var previousKey) && previousKey == policyKey) replacing++;
                    if (!_policies.CanAcquire(policyKey, in policy, DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond, replacing)) return false;
                    foreach (var previous in sameKey) DestroyVfxEntity(world, previous);
                }
            }
            var nowMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            if (!_policies.TryAcquire(policyKey, in policy, nowMs)) return false;

            var position = new Vector3(request.Position.X, request.Position.Y, request.Position.Z);
            var rotation = new Quaternion(request.Rotation.X, request.Rotation.Y, request.Rotation.Z, request.Rotation.W);
            var offset = new Vector3(request.Attachment.Offset.X, request.Attachment.Offset.Y, request.Attachment.Offset.Z);
            var followActorId = request.Attachment.ActorId > 0 ? request.Attachment.ActorId : 0;
            try
            {
                if (!TryCreateVfxEntity(world, parent, vfxId, followTarget, followActorId, in offset, in position, in rotation, request.DurationMs, out entity))
                {
                    _policies.Release(policyKey);
                    return false;
                }
            }
            catch { _policies.Release(policyKey); throw; }

            _policyKeys[entity.Id] = policyKey;
            _requests[entity.Id] = request;
            entity.WithRef(new BattlePresentationLifetimeComponent { Request = request });
            if (entity.TryGetRef(out BattleViewGameObjectComponent visual) && visual?.GameObject != null)
            {
                var go = visual.GameObject;
                BattlePresentationScaleState.ApplyCueScale(go, request.Scale, request.Radius);
                var scales = go.GetComponent<BattlePresentationScaleState>();
                var state = go.GetComponent<UnityPresentationInstanceState>() ?? go.AddComponent<UnityPresentationInstanceState>();
                state.Bind("BattleVfx", in request, scales.BaseModelScale, scales.Sources, _rateResolver, requestScaleInSources: true);
                state.OrderingAdapter = _orderingAdapter;
                state.RefreshResolution(PresentationRateChannel.Vfx | PresentationRateChannel.CueLifetime,
                    PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue);
                _followController.SyncPresentation(world, entity, null, null, initial: true);
            }
            return true;
        }

        public void UpdatePresentationLifetime(EC.IECWorld world, EC.IEntityId id, in PresentationRequest request)
        {
            if (world == null || !world.IsAlive(id)) return;
            if (world.Wrap(id).TryGetRef(out BattlePresentationLifetimeComponent life) && life != null && !life.State.IsStopping)
            {
                life.Request = request;
                var entity = world.Wrap(id);
                if (entity.TryGetRef(out BattleViewGameObjectComponent visual) && visual?.GameObject != null)
                {
                    var state = visual.GameObject.GetComponent<UnityPresentationInstanceState>();
                    if (state != null) state.UpdateRequest(in request);
                    if (entity.TryGetRef(out BattleViewFollowComponent follow) && follow != null)
                    {
                        if (follow.TargetActorId != request.Attachment.ActorId) follow.Target = default;
                        follow.TargetActorId = request.Attachment.ActorId;
                    }
                    _followController.SyncPresentation(world, entity, null, null, initial: true);
                }
                // Stack identity belongs to creation; payload updates must not change it.
            }
        }

        /// <summary>Normal request stop can drain; teardown/replacement always use DestroyVfxEntity.</summary>
        public void StopPresentationVfxEntity(EC.IECWorld world, EC.IEntityId id)
        {
            if (world == null || !world.IsAlive(id)) return;
            var entity = world.Wrap(id);
            if (entity.TryGetRef(out BattlePresentationLifetimeComponent life) && life != null)
            {
                if (life.State.IsStopping) return;
                var policy = life.Request.Lifecycle;
                var canDrain = policy.Stop == PresentationStopPolicy.Drain &&
                    entity.TryGetRef(out BattleViewGameObjectComponent view) && view != null &&
                    _lifetimeAdapter.TryBeginDrain(view.GameObject);
                if (!life.State.RequestStop(in policy, canDrain)) return;
            }
            DestroyVfxEntity(world, id);
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
            if (vfxRoot.IsValid)
            {
                _clearIds.Clear();
                foreach (var id in _requests.Keys) if (!vfxRoot.World.IsAlive(id)) _clearIds.Add(id);
                foreach (var id in _clearIds) ReleasePolicy(id);
                _clearIds.Clear();
            }
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
            _requests.Clear();
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

            return true;
        }

        private void ReleasePolicy(EC.IEntityId id)
        {
            _requests.Remove(id);
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

        public BattleVfxFollowController CreateFollowController(BattleVfxLifetimePolicy lifetime,
            IPresentationLifetimeAdapter<GameObject> lifetimeAdapter = null)
        {
            return new BattleVfxFollowController(lifetime, lifetimeAdapter: lifetimeAdapter);
        }
    }
}
