using System;
using System.Collections.Generic;
using AbilityKit.Ability.Battle.EntityManager;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager
{
    public sealed class MobaEntityManager : IService
    {
        private readonly Dictionary<int, global::ActorEntity> _byActorId = new Dictionary<int, global::ActorEntity>();

        public readonly BattleEntityManager<int> Index;

        public readonly KeyedEntityIndex<Team, int> ByTeam;
        public readonly KeyedEntityIndex<EntityMainType, int> ByMainType;
        public readonly KeyedEntityIndex<UnitSubType, int> ByUnitSubType;
        public readonly KeyedEntityIndex<PlayerId, int> ByOwnerPlayer;

        public MobaEntityManager()
        {
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

            _byActorId[actorId] = entity;

            if (!Index.Registry.Contains(actorId))
            {
                Index.Add(actorId);
            }
            ByTeam.SetKey(actorId, team);
            ByMainType.SetKey(actorId, mainType);
            ByUnitSubType.SetKey(actorId, unitSubType);
            ByOwnerPlayer.SetKey(actorId, ownerPlayer);
        }

        public void Unregister(int actorId)
        {
            if (actorId <= 0) return;
            _byActorId.Remove(actorId);
            Index.Remove(actorId);
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
