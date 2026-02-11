using System;
using System.Collections.Generic;
using AbilityKit.Ability.Battle.EntityManager;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Eventing;
using StableStringId = AbilityKit.Triggering.Eventing.StableStringId;

namespace AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager
{
    public sealed class MobaEntityManager : IService
    {
        private readonly Dictionary<int, global::ActorEntity> _byActorId = new Dictionary<int, global::ActorEntity>();

        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;

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

            Register(
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
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var isNew = !Index.Registry.Contains(actorId);

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
                PublishUnitEvent(MobaUnitTriggering.Events.Spawn, actorId, team, mainType, unitSubType, ownerPlayer, entity);
            }
        }

        public void Unregister(int actorId)
        {
            if (actorId <= 0) return;

            if (_byActorId.TryGetValue(actorId, out var entity) && entity != null)
            {
                var team = entity.hasTeam ? entity.team.Value : Team.None;
                var mainType = entity.hasEntityMainType ? entity.entityMainType.Value : EntityMainType.Unit;
                var unitSubType = entity.hasUnitSubType ? entity.unitSubType.Value : UnitSubType.Hero;
                var ownerPlayer = entity.hasOwnerPlayerId ? entity.ownerPlayerId.Value : default;

                PublishUnitEvent(MobaUnitTriggering.Events.Despawn, actorId, team, mainType, unitSubType, ownerPlayer, entity);
            }

            _byActorId.Remove(actorId);
            Index.Remove(actorId);
        }

        private void PublishUnitEvent(string eventId, int actorId, Team team, EntityMainType mainType, UnitSubType unitSubType, PlayerId ownerPlayer, global::ActorEntity entity)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var templateId = 0;
            try
            {
                if (entity != null && entity.hasModelId) templateId = entity.modelId.Value;
            }
            catch
            {
                templateId = 0;
            }

            var payload = new UnitEventPayload(actorId, team, mainType, unitSubType, ownerPlayer, templateId);

            var eventBus = _eventBus;
            if (eventBus == null) return;
            var eid = TriggeringIdUtil.GetEventEid(eventId);
            eventBus.Publish(new EventKey<UnitEventPayload>(eid), in payload);
            object boxed = payload;
            eventBus.Publish(new EventKey<object>(eid), in boxed);
        }

        public IReadOnlyCollection<int> GetTeam(Team team) => ByTeam.Get(team);

        public IReadOnlyCollection<int> GetMainType(EntityMainType type) => ByMainType.Get(type);

        public IReadOnlyCollection<int> GetUnitSubType(UnitSubType type) => ByUnitSubType.Get(type);

        public IReadOnlyCollection<int> GetOwner(PlayerId playerId) => ByOwnerPlayer.Get(playerId);

        public void Clear()
        {
            _byActorId.Clear();
            var tmp = new List<int>(Index.Registry.Count);
            foreach (var id in Index.Registry.Entities)
            {
                tmp.Add(id);
            }

            Index.Registry.RemoveRange(tmp);
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
