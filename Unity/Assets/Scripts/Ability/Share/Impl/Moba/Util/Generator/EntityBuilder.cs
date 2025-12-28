using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Impl.Moba.Util.Generator
{
    public static class EntityBuilder
    {
        public static BuildEnterGameResult BuildEnterGameActors(ActorContext actorContext, in EnterMobaGameReq req)
        {
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));

            var spawnPos = GetSpawnPosition(req.TeamId, spawnIndex: 0);
            var transform = new Transform3(spawnPos, Quat.Identity, Vec3.One);

            ActorEntityFactory.Create(actorContext)
                .WithTransform(transform)
                .Build();

            var players = new[]
            {
                new MobaPlayerEntry(req.PlayerId, req.TeamId, req.HeroId, spawnIndex: 0)
            };

            return new BuildEnterGameResult(localActorId: 1, players: players, localActorTransform: transform);
        }

        private static Vec3 GetSpawnPosition(int teamId, int spawnIndex)
        {
            var x = teamId * 6f + spawnIndex * 2f;
            return new Vec3(x, 0f, 0f);
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
