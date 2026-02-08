using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Impl.Moba.Util.Generator
{
    /*
     * Spawn 管线：根据外部提供的 Loadout 批量生成 ActorEntity（骨架）并注册到 ActorRegistry。
     *
     * 职责边界：
     * 1) 仅负责“创建骨架 + 注册 + 产出本地玩家的 LocalActorId”。
     * 2) 骨架组件的挂载由 ActorArchetypeFactory 负责。
     * 3) 读表初始化（属性/技能等）不在这里做，通过 initializer 回调挂接（ActorEntityInitPipeline）。
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

                /* 创建骨架实体（基础组件挂载 + meta 写入） */
                var built = ActorArchetypeFactory.Create(actorContext, in info);
                onActorBuilt?.Invoke(built, p);

                /* 注册（actorId -> entity），用于后续查找/同步 */
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

    public static class EntityBuilder
    {
        /*
         * 兼容层（历史命名）。
         * 新代码请直接使用 ActorSpawnPipeline。
         */
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
            return ActorSpawnPipeline.BuildActorsFromLoadoutsAndInitialize(actorContext, actorIds, registry, entities, localPlayerId, loadouts, initializer, onActorBuilt);
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
            return ActorSpawnPipeline.BuildActorsFromLoadouts(actorContext, actorIds, registry, entities, localPlayerId, loadouts, onActorBuilt);
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
            return ActorSpawnPipeline.BuildActorsFromEnterGameReqAndInitialize(actorContext, actorIds, registry, entities, req, initializer, onActorBuilt);
        }

        public static BuildActorsResult BuildActorsFromEnterGameReq(
            ActorContext actorContext,
            ActorIdAllocator actorIds,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            in EnterMobaGameReq req,
            Action<ActorEntity, MobaPlayerLoadout> onActorBuilt = null)
        {
            return ActorSpawnPipeline.BuildActorsFromEnterGameReq(actorContext, actorIds, registry, entities, req, onActorBuilt);
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
