using System;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;
using AbilityKit.Ability.Battle.EntityManager;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Eventing;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Services.Observability;
using StableStringId = AbilityKit.Triggering.Eventing.StableStringId;

namespace AbilityKit.Demo.Moba.Services.EntityManager
{
    [WorldService(typeof(MobaEntityManager))]
    public sealed class MobaEntityManager :
        IMobaRuntimeObjectBootstrapContributor,
        IService
    {
        private static readonly ObjectPool<List<int>> s_actorIdListPool = Pools.GetPool(
            createFunc: () => new List<int>(256),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64,
            collectionCheck: false);

        private readonly Dictionary<int, global::ActorEntity> _byActorId = new Dictionary<int, global::ActorEntity>();

        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;

        [WorldInject(required: false)] private IFrameTime _frameTime = null;
        [WorldInject(required: false)] private IMobaRuntimeObjectLifecycleHook _objectLifecycle = null;
        [WorldInject(required: false)] private IMobaRuntimeObjectBootstrapRegistry _objectBootstrap = null;

        private bool _objectBootstrapRegistered;

        public readonly BattleEntityManager<int> Index;

        public readonly KeyedEntityIndex<Team, int> ByTeam;
        public readonly KeyedEntityIndex<EntityMainType, int> ByMainType;
        public readonly KeyedEntityIndex<UnitSubType, int> ByUnitSubType;
        public readonly KeyedEntityIndex<PlayerId, int> ByOwnerPlayer;

        public MobaEntityManager(AbilityKit.Triggering.Eventing.IEventBus eventBus)
        {
            _eventBus = eventBus;
            Index = new BattleEntityManager<int>();
            ByTeam = Index.CreateKeyedIndex<Team>();
            ByMainType = Index.CreateKeyedIndex<EntityMainType>();
            ByUnitSubType = Index.CreateKeyedIndex<UnitSubType>();
            ByOwnerPlayer = Index.CreateKeyedIndex<PlayerId>();
        }

        public bool TryGetActorEntity(int actorId, out global::ActorEntity entity)
        {
            return _byActorId.TryGetValue(actorId, out entity);
        }

        public void GetRegisteredActorIds(List<int> dst)
        {
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            dst.Clear();
            foreach (var id in Index.Registry.Entities)
            {
                dst.Add(id);
            }
        }

        public bool TryRegisterFromEntity(global::ActorEntity e)
        {
            if (e == null) return false;
            if (!e.hasActorId) return false;
            if (!e.hasTeam) return false;
            if (!e.hasEntityMainType) return false;
            if (!e.hasUnitSubType) return false;
            if (!e.hasOwnerPlayerId) return false;

            var actorId = e.actorId.Value;
            if (actorId <= 0) return false;

            RegisterSilently(
                actorId: actorId,
                entity: e,
                team: e.team.Value,
                mainType: e.entityMainType.Value,
                unitSubType: e.unitSubType.Value,
                ownerPlayer: e.ownerPlayerId.Value);

            return true;
        }

        public void Register(
            int actorId,
            global::ActorEntity entity,
            Team team,
            EntityMainType mainType,
            UnitSubType unitSubType,
            PlayerId ownerPlayer)
        {
            var isNew = RegisterSilently(actorId, entity, team, mainType, unitSubType, ownerPlayer);
            if (isNew)
            {
                PublishSpawn(entity);
            }
        }

        internal bool RegisterSilently(
            int actorId,
            global::ActorEntity entity,
            Team team,
            EntityMainType mainType,
            UnitSubType unitSubType,
            PlayerId ownerPlayer)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            EnsureObjectBootstrapRegistered();

            var indexContains = Index.Registry.Contains(actorId);
            var dictionaryContains = _byActorId.TryGetValue(actorId, out var existingEntity);
            if (indexContains != dictionaryContains)
            {
                throw new InvalidOperationException($"Actor {actorId} registration indexes are inconsistent.");
            }
            if (dictionaryContains && !ReferenceEquals(existingEntity, entity))
            {
                throw new InvalidOperationException($"Actor {actorId} is already registered with a different entity.");
            }

            var isNew = !indexContains;
            var hadTeam = ByTeam.TryGetKey(actorId, out var oldTeam);
            var hadMainType = ByMainType.TryGetKey(actorId, out var oldMainType);
            var hadUnitSubType = ByUnitSubType.TryGetKey(actorId, out var oldUnitSubType);
            var hadOwnerPlayer = ByOwnerPlayer.TryGetKey(actorId, out var oldOwnerPlayer);
            try
            {
                _byActorId[actorId] = entity;
                if (isNew)
                {
                    Index.Add(actorId);
                }
                ByTeam.SetKey(actorId, team);
                ByMainType.SetKey(actorId, mainType);
                ByUnitSubType.SetKey(actorId, unitSubType);
                ByOwnerPlayer.SetKey(actorId, ownerPlayer);
                if (isNew)
                {
                    PublishObjectLifecycle(entity, MobaRuntimeObjectLifecycleStage.Created);
                }
                return isNew;
            }
            catch
            {
                if (isNew)
                {
                    _byActorId.Remove(actorId);
                    if (Index.Registry.Contains(actorId))
                    {
                        Index.Remove(actorId);
                    }
                }
                else
                {
                    RestoreKey(ByTeam, actorId, hadTeam, oldTeam);
                    RestoreKey(ByMainType, actorId, hadMainType, oldMainType);
                    RestoreKey(ByUnitSubType, actorId, hadUnitSubType, oldUnitSubType);
                    RestoreKey(ByOwnerPlayer, actorId, hadOwnerPlayer, oldOwnerPlayer);
                }
                throw;
            }
        }

        private static void RestoreKey<TKey>(KeyedEntityIndex<TKey, int> index, int actorId, bool hadKey, TKey oldKey)
        {
            if (hadKey)
            {
                index.SetKey(actorId, oldKey);
            }
            else
            {
                index.ClearKey(actorId);
            }
        }

        internal void PublishSpawn(global::ActorEntity entity)
        {
            PublishEntityEvent(entity, MobaUnitTriggering.Events.Spawn, MobaTraceKind.UnitSpawn);
        }

        public void Unregister(int actorId)
        {
            if (!UnregisterSilently(actorId, out var entity)) return;
            PublishDespawn(entity);
        }

        internal void PublishDespawn(global::ActorEntity entity)
        {
            PublishEntityEvent(entity, MobaUnitTriggering.Events.Despawn, MobaTraceKind.UnitDespawn);
        }

        private void PublishObjectLifecycle(
            global::ActorEntity entity,
            MobaRuntimeObjectLifecycleStage stage)
        {
            EnsureObjectBootstrapRegistered();
            var hook = _objectLifecycle;
            if (hook == null || !hook.IsEnabled || entity == null || !entity.hasActorId) return;
            PublishObjectLifecycleTo(
                hook,
                entity,
                stage,
                _frameTime != null ? _frameTime.Frame.Value : -1);
        }

        void IMobaRuntimeObjectBootstrapContributor.CaptureActiveRuntimeObjects(
            IMobaRuntimeObjectLifecycleHook hook,
            int frame)
        {
            foreach (var entity in _byActorId.Values)
            {
                PublishObjectLifecycleTo(
                    hook,
                    entity,
                    MobaRuntimeObjectLifecycleStage.Created,
                    frame);
            }
        }

        private static void PublishObjectLifecycleTo(
            IMobaRuntimeObjectLifecycleHook hook,
            global::ActorEntity entity,
            MobaRuntimeObjectLifecycleStage stage,
            int frame)
        {
            if (hook == null || !hook.IsEnabled || entity == null || !entity.hasActorId) return;
            var actorId = entity.actorId.Value;
            if (actorId <= 0) return;

            var ownerActorId = entity.hasOwnerLink && entity.ownerLink != null
                ? entity.ownerLink.OwnerActorId
                : 0;
            var definitionId = entity.hasModelId ? entity.modelId.Value : 0;
            var observation = new MobaRuntimeObjectLifecycleObservation(
                stage,
                MobaRuntimeObjectKind.Actor,
                actorId,
                frame,
                MobaRuntimeObjectDefinitionKind.Actor,
                definitionId,
                relatedActorId: actorId,
                ownerActorId: ownerActorId);
            hook.TryObserve(in observation);
        }

        private void EnsureObjectBootstrapRegistered()
        {
            if (_objectBootstrapRegistered) return;
            var registry = _objectBootstrap;
            if (registry != null && registry.Register(this))
                _objectBootstrapRegistered = true;
        }

        internal bool UnregisterSilently(int actorId, out global::ActorEntity entity)
        {
            entity = null;
            if (actorId <= 0) return false;

            var dictionaryContains = _byActorId.TryGetValue(actorId, out entity);
            var indexContains = Index.Registry.Contains(actorId);
            if (dictionaryContains != indexContains)
            {
                throw new InvalidOperationException($"Actor {actorId} registration indexes are inconsistent.");
            }
            if (!indexContains) return false;

            var hadTeam = ByTeam.TryGetKey(actorId, out var oldTeam);
            var hadMainType = ByMainType.TryGetKey(actorId, out var oldMainType);
            var hadUnitSubType = ByUnitSubType.TryGetKey(actorId, out var oldUnitSubType);
            var hadOwnerPlayer = ByOwnerPlayer.TryGetKey(actorId, out var oldOwnerPlayer);
            try
            {
                Index.Remove(actorId);
                _byActorId.Remove(actorId);
                PublishObjectLifecycle(entity, MobaRuntimeObjectLifecycleStage.Destroyed);
                return true;
            }
            catch
            {
                if (!Index.Registry.Contains(actorId))
                {
                    Index.Add(actorId);
                }
                RestoreKey(ByTeam, actorId, hadTeam, oldTeam);
                RestoreKey(ByMainType, actorId, hadMainType, oldMainType);
                RestoreKey(ByUnitSubType, actorId, hadUnitSubType, oldUnitSubType);
                RestoreKey(ByOwnerPlayer, actorId, hadOwnerPlayer, oldOwnerPlayer);
                _byActorId[actorId] = entity;
                throw;
            }
        }

        public void PublishRespawn(global::ActorEntity entity)
        {
            if (entity == null || !entity.hasActorId) return;
            if (!_byActorId.ContainsKey(entity.actorId.Value)) return;
            PublishEntityEvent(entity, MobaUnitTriggering.Events.Respawn, MobaTraceKind.UnitRespawn);
        }

        private void PublishEntityEvent(global::ActorEntity entity, string eventId, MobaTraceKind traceKind)
        {
            if (entity == null || !entity.hasActorId) return;

            var actorId = entity.actorId.Value;
            if (actorId <= 0) return;

            var team = entity.hasTeam ? entity.team.Value : Team.None;
            var mainType = entity.hasEntityMainType ? entity.entityMainType.Value : EntityMainType.Unit;
            var unitSubType = entity.hasUnitSubType ? entity.unitSubType.Value : UnitSubType.Hero;
            var ownerPlayer = entity.hasOwnerPlayerId ? entity.ownerPlayerId.Value : default;
            PublishUnitEvent(eventId, actorId, team, mainType, unitSubType, ownerPlayer, entity, traceKind);
        }

        private void PublishUnitEvent(string eventId, int actorId, Team team, EntityMainType mainType, UnitSubType unitSubType, PlayerId ownerPlayer, global::ActorEntity entity, MobaTraceKind traceKind)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var templateId = entity != null && entity.hasModelId ? entity.modelId.Value : 0;

            var payload = new UnitEventPayload(actorId, team, mainType, unitSubType, ownerPlayer, templateId, traceKind);

            var eventBus = _eventBus;
            if (eventBus == null) return;
            var eid = TriggeringIdUtil.GetEventEid(eventId);
            eventBus.Publish(new EventKey<UnitEventPayload>(eid), in payload);
            var objectKey = new EventKey<object>(eid);
            if (eventBus.HasSubscribers(objectKey))
            {
                object boxed = payload;
                eventBus.Publish(objectKey, in boxed);
            }
        }

        public IReadOnlyCollection<int> GetTeam(Team team) => ByTeam.Get(team);

        public IReadOnlyCollection<int> GetMainType(EntityMainType type) => ByMainType.Get(type);

        public IReadOnlyCollection<int> GetUnitSubType(UnitSubType type) => ByUnitSubType.Get(type);

        public IReadOnlyCollection<int> GetOwner(PlayerId playerId) => ByOwnerPlayer.Get(playerId);

        public void Clear()
        {
            _byActorId.Clear();
            var tmp = s_actorIdListPool.Get();
            try
            {
                foreach (var id in Index.Registry.Entities)
                {
                    tmp.Add(id);
                }

                Index.Registry.RemoveRange(tmp);
            }
            finally
            {
                s_actorIdListPool.Release(tmp);
            }
        }

        public void Dispose()
        {
            if (_objectBootstrapRegistered)
            {
                _objectBootstrap?.Unregister(this);
                _objectBootstrapRegistered = false;
            }
            Clear();
        }
    }
}
