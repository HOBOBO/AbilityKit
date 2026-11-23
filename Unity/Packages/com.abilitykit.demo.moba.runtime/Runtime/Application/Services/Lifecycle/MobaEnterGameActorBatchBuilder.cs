using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 组装入战玩家的生成请求，并投影已准备的实体批次；不负责发布或回滚。
    /// </summary>
    internal static class MobaEnterGameActorBatchBuilder
    {
        internal static MobaActorSpawnRequest[] CreateRequests(
            MobaPlayerLoadout[] loadouts,
            ActorIdAllocator actorIds,
            ActorEntityInitPipeline initializer)
        {
            var requests = new MobaActorSpawnRequest[loadouts.Length];
            for (var index = 0; index < loadouts.Length; index++)
            {
                var loadout = loadouts[index];
                var spec = MobaConverter.ToActorBuildSpec(actorIds.Next(), in loadout);
                var request = MobaActorSpawnRequest.FromSpec(in spec);
                request.Initializer = (entity, _) =>
                {
                    if (!initializer.TryInitializeFromLoadout(entity, in loadout, out var error))
                    {
                        throw new InvalidOperationException(
                            error ?? $"actor loadout initialization failed. playerId={loadout.PlayerId.Value} heroId={loadout.HeroId}");
                    }
                };
                requests[index] = request;
            }
            return requests;
        }

        internal static BuildActorsResult ProjectPreparedBatch(
            MobaPlayerLoadout[] loadouts,
            PlayerId localPlayerId,
            MobaActorSpawnResult[] spawnResults,
            IWorldResolver services,
            out IReadOnlyList<MobaActorSpawnSnapshotEntry> snapshotEntries)
        {
            var spawnEntries = new List<MobaActorSpawnSnapshotEntry>(loadouts.Length);
            var players = new MobaPlayerEntry[loadouts.Length];
            var playerActors = new MobaPlayerActorEntry[loadouts.Length];
            var localActorId = 0;
            var localTransform = Transform3.Identity;
            for (var index = 0; index < loadouts.Length; index++)
            {
                var loadout = loadouts[index];
                var actor = spawnResults[index];
                var actorId = actor.ActorId;
                if (actorId <= 0)
                {
                    throw new InvalidOperationException($"actor id is invalid after build. playerId={loadout.PlayerId.Value}, heroId={loadout.HeroId}");
                }

                players[index] = new MobaPlayerEntry(loadout.PlayerId, loadout.TeamId, loadout.HeroId, loadout.SpawnIndex);
                playerActors[index] = new MobaPlayerActorEntry(loadout.PlayerId, actorId);
                spawnEntries.Add(new MobaActorSpawnSnapshotEntry
                {
                    NetId = actorId,
                    Kind = (int)SpawnEntityKind.Character,
                    Code = loadout.HeroId,
                    OwnerNetId = 0,
                    X = loadout.SpawnX,
                    Y = loadout.SpawnY,
                    Z = loadout.SpawnZ,
                    EntityVersion = ResolveEntityVersion(services, actorId)
                });

                if (localActorId == 0 && loadout.PlayerId.Equals(localPlayerId))
                {
                    localActorId = actorId;
                    localTransform = actor.Spec.Info.Transform;
                }
            }

            if (localActorId <= 0)
            {
                throw new InvalidOperationException($"localPlayerId not found in loadouts. playerId={localPlayerId.Value}");
            }

            snapshotEntries = spawnEntries;
            return new BuildActorsResult(
                localActorId,
                players,
                playerActors,
                in localTransform,
                spawnResults);
        }

        private static int ResolveEntityVersion(IWorldResolver services, int actorId)
        {
            return services != null &&
                   services.TryResolve<MobaActorRegistry>(out var actors) &&
                   actors != null
                ? actors.GetEntityVersion(actorId)
                : 1;
        }
    }
}
