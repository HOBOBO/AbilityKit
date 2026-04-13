using System;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Math;
using AbilityKit.Protocol.Moba;
using AbilityKit.Ability.Share.Impl.Moba.Struct;

namespace AbilityKit.Demo.Moba.Util.Generator
{
    /*
     * Spawn 绠＄嚎锛氭牴鎹閮ㄦ彁渚涚殑 Loadout 鎵归噺鐢熸垚 ActorEntity锛堥鏋讹級骞舵敞鍐屽埌 ActorRegistry銆?
     *
     * 鑱岃矗杈圭晫锛?
     * 1) 浠呰礋璐ｂ€滃垱寤洪鏋?+ 娉ㄥ唽 + 浜у嚭鏈湴鐜╁鐨?LocalActorId鈥濄€?
     * 2) 楠ㄦ灦缁勪欢鐨勬寕杞界敱 ActorArchetypeFactory 璐熻矗銆?
     * 3) 璇昏〃鍒濆鍖栵紙灞炴€?鎶€鑳界瓑锛変笉鍦ㄨ繖閲屽仛锛岄€氳繃 initializer 鍥炶皟鎸傛帴锛圓ctorEntityInitPipeline锛夈€?
     */
    public static class ActorSpawnPipeline
    {
        public static BuildActorsResult BuildActorsFromLoadoutsAndInitialize(
            ActorContext actorContext,
            ActorIdAllocator actorIds,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            PlayerId localPlayerId,
            MobaPlayerLoadout[] loadouts,
            Action<ActorEntity, MobaPlayerLoadout> initializer,
            Action<ActorEntity, MobaPlayerLoadout> onActorBuilt = null)
        {
            return BuildActorsFromLoadouts(
                actorContext,
                actorIds,
                registry,
                entities,
                localPlayerId,
                loadouts,
                onActorBuilt: (entity, loadout) =>
                {
                    initializer?.Invoke(entity, loadout);
                    onActorBuilt?.Invoke(entity, loadout);
                });
        }

        public static BuildActorsResult BuildActorsFromLoadouts(
            ActorContext actorContext,
            ActorIdAllocator actorIds,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            PlayerId localPlayerId,
            MobaPlayerLoadout[] loadouts,
            Action<ActorEntity, MobaPlayerLoadout> onActorBuilt = null)
        {
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));
            if (actorIds == null) throw new ArgumentNullException(nameof(actorIds));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            if (loadouts == null || loadouts.Length == 0) throw new InvalidOperationException("loadouts is required");

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
                    kind: ActorArchetypeFactory.CreateKindFromType((EntityMainType)p.MainType, (UnitSubType)p.UnitSubType),
                    transform: transform,
                    team: (Team)p.TeamId,
                    mainType: (EntityMainType)p.MainType,
                    unitSubType: (UnitSubType)p.UnitSubType,
                    ownerPlayer: p.PlayerId,
                    templateId: p.AttributeTemplateId);

                /* 鍒涘缓楠ㄦ灦瀹炰綋锛堝熀纭€缁勪欢鎸傝浇 + meta 鍐欏叆锛?*/
                var built = ActorArchetypeFactory.Create(actorContext, in info);
                onActorBuilt?.Invoke(built, p);

                /* 娉ㄥ唽锛坅ctorId -> entity锛夛紝鐢ㄤ簬鍚庣画鏌ユ壘/鍚屾 */
                registry.Register(actorId, built);

                players[i] = new MobaPlayerEntry(p.PlayerId, p.TeamId, p.HeroId, p.SpawnIndex);

                if (localActorId == 0 && p.PlayerId.Equals(localPlayerId))
                {
                    localActorId = actorId;
                    localTransform = transform;
                }
            }

            if (localActorId == 0)
            {
                throw new InvalidOperationException($"localPlayerId not found in loadouts. playerId={localPlayerId.Value}");
            }

            return new BuildActorsResult(localActorId: localActorId, players: players, localActorTransform: localTransform);
        }

        public static BuildActorsResult BuildActorsFromEnterGameReqAndInitialize(
            ActorContext actorContext,
            ActorIdAllocator actorIds,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            in EnterMobaGameReq req,
            Action<ActorEntity, MobaPlayerLoadout> initializer,
            Action<ActorEntity, MobaPlayerLoadout> onActorBuilt = null)
        {
            return BuildActorsFromLoadoutsAndInitialize(
                actorContext,
                actorIds,
                registry,
                entities,
                req.PlayerId,
                req.Players,
                initializer,
                onActorBuilt);
        }

        public static BuildActorsResult BuildActorsFromEnterGameReq(ActorContext actorContext, ActorIdAllocator actorIds, MobaActorRegistry registry, MobaEntityManager entities, in EnterMobaGameReq req, Action<ActorEntity, MobaPlayerLoadout> onActorBuilt = null)
        {
            return BuildActorsFromLoadouts(
                actorContext,
                actorIds,
                registry,
                entities,
                req.PlayerId,
                req.Players,
                onActorBuilt);
        }
    }

    public readonly struct BuildActorsResult
    {
        public readonly int LocalActorId;
        public readonly MobaPlayerEntry[] Players;
        public readonly Transform3 LocalActorTransform;

        public BuildActorsResult(int localActorId, MobaPlayerEntry[] players, in Transform3 localActorTransform)
        {
            LocalActorId = localActorId;
            Players = players;
            LocalActorTransform = localActorTransform;
        }
    }
}
