using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Impl.Moba.Util.Generator
{
    public static class EntityBuilder
    {
        public static BuildEnterGameResult BuildEnterGameActors(ActorContext actorContext, ActorIdAllocator actorIds, MobaActorRegistry registry, MobaEntityManager entities, in EnterMobaGameReq req, Action<ActorEntity, MobaPlayerLoadout> onActorBuilt = null)
        {
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));
            if (actorIds == null) throw new ArgumentNullException(nameof(actorIds));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var loadouts = req.Players;
            if (loadouts == null || loadouts.Length == 0) throw new InvalidOperationException("EnterMobaGameReq.Players is required");

            var players = new MobaPlayerEntry[loadouts.Length];
            var localActorId = 0;
            var localTransform = Transform3.Identity;

            for (int i = 0; i < loadouts.Length; i++)
            {
                var p = loadouts[i];
                if (p.HasSpawnPosition == 0)
                {
                    throw new InvalidOperationException($"PlayerLoadout spawn position is required. playerId={p.PlayerId.Value} teamId={p.TeamId} spawnIndex={p.SpawnIndex}");
                }

                var spawnPos = new Vec3(p.SpawnX, p.SpawnY, p.SpawnZ);
                var transform = new Transform3(spawnPos, Quat.Identity, Vec3.One);
                var actorId = actorIds.Next();

                var info = new MobaEntityInfo(
                    actorId: actorId,
                    kind: MobaEntitySpawnFactory.CreateKindFromType((EntityMainType)p.MainType, (UnitSubType)p.UnitSubType),
                    transform: transform,
                    team: (Team)p.TeamId,
                    mainType: (EntityMainType)p.MainType,
                    unitSubType: (UnitSubType)p.UnitSubType,
                    ownerPlayer: p.PlayerId,
                    templateId: p.AttributeTemplateId);

                var built = MobaEntitySpawnFactory.Create(actorContext, in info);
                onActorBuilt?.Invoke(built, p);
                registry.Register(actorId, built);

                players[i] = new MobaPlayerEntry(p.PlayerId, p.TeamId, p.HeroId, p.SpawnIndex);

                if (localActorId == 0 && p.PlayerId.Equals(req.PlayerId))
                {
                    localActorId = actorId;
                    localTransform = transform;
                }
            }

            if (localActorId == 0)
            {
                throw new InvalidOperationException($"EnterMobaGameReq.PlayerId not found in Players. playerId={req.PlayerId.Value}");
            }

            return new BuildEnterGameResult(localActorId: localActorId, players: players, localActorTransform: localTransform);
        }
    }

    public readonly struct BuildEnterGameResult
    {
        public readonly int LocalActorId;
        public readonly MobaPlayerEntry[] Players;
        public readonly Transform3 LocalActorTransform;

        public BuildEnterGameResult(int localActorId, MobaPlayerEntry[] players, in Transform3 localActorTransform)
        {
            LocalActorId = localActorId;
            Players = players;
            LocalActorTransform = localActorTransform;
        }
    }
}
