using System;
using System.ComponentModel;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    public static partial class ActorSpawnPipeline
    {
        /*
         * 保留旧的公开签名与立即注册行为供现有调用方迁移。
         * 新的正式生成走 IMobaActorSpawnService；入战批量编排留在生命周期层。
         */
        /// <summary>
        /// 构建并立即注册单个实体；注册异常时执行旧版补偿并重新抛出。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static BuildActorResult BuildActorAndRegister(
            ActorContext actorContext,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            in MobaActorBuildSpec spec,
            Action<ActorEntity, MobaActorBuildSpec> initializer = null,
            Action<ActorEntity, MobaActorBuildSpec> onActorBuilt = null)
        {
            var result = BuildActor(actorContext, in spec, initializer, onActorBuilt);
            try
            {
                RegisterBuiltActor(registry, entities, result.Entity, in spec);
                return result;
            }
            catch
            {
                new MobaActorSpawnRegistrar(registry, entities).Unregister(
                    spec.Info.ActorId,
                    out _,
                    publishDespawn: false);
                DestroyBuiltEntity(result.Entity);
                throw;
            }
        }

        /// <summary>
        /// 将已构建的实体写入指定索引，并在实体管理器注册成功时发布生成事件。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterBuiltActor(
            MobaActorRegistry registry,
            MobaEntityManager entities,
            ActorEntity entity,
            in MobaActorBuildSpec spec)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (spec.Info.ActorId <= 0) throw new InvalidOperationException("actorId is required");

            var registrar = new MobaActorSpawnRegistrar(registry, entities);
            var registered = registrar.Register(
                entity,
                in spec,
                registerActor: registry != null,
                registerEntityManager: entities != null,
                registerEntityManagerFromEntity: false);
            if (registered && entities != null)
            {
                entities.PublishSpawn(entity);
            }
        }

        /// <summary>
        /// 按负载生成并立即注册一批实体，注册前执行指定的初始化和构建回调。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
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

        /// <summary>
        /// 按负载分配 Actor ID，构建并立即注册一批实体。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static BuildActorsResult BuildActorsFromLoadouts(
            ActorContext actorContext,
            ActorIdAllocator actorIds,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            PlayerId localPlayerId,
            MobaPlayerLoadout[] loadouts,
            Action<ActorEntity, MobaPlayerLoadout> onActorBuilt = null)
        {
            if (actorIds == null) throw new ArgumentNullException(nameof(actorIds));
            if (loadouts == null || loadouts.Length == 0) throw new InvalidOperationException("loadouts is required");

            var specs = new MobaActorBuildSpec[loadouts.Length];
            for (int i = 0; i < loadouts.Length; i++)
            {
                var loadout = loadouts[i];
                if (loadout.HasSpawnPosition == 0)
                {
                    throw new InvalidOperationException($"PlayerLoadout spawn position is required. playerId={loadout.PlayerId.Value} teamId={loadout.TeamId} spawnIndex={loadout.SpawnIndex}");
                }

                specs[i] = MobaConverter.ToActorBuildSpec(actorIds.Next(), in loadout);
            }

            return BuildActorsFromSpecs(
                actorContext,
                registry,
                entities,
                localPlayerId,
                loadouts,
                specs,
                onActorBuilt);
        }

        /// <summary>
        /// 按构建规格立即注册实体；中途异常时逆序注销并销毁本批次已完成的实体。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static BuildActorsResult BuildActorsFromSpecs(
            ActorContext actorContext,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            PlayerId localPlayerId,
            MobaPlayerLoadout[] loadouts,
            MobaActorBuildSpec[] specs,
            Action<ActorEntity, MobaPlayerLoadout> onActorBuilt = null)
        {
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            if (loadouts == null || loadouts.Length == 0) throw new InvalidOperationException("loadouts is required");
            if (specs == null || specs.Length != loadouts.Length) throw new InvalidOperationException("specs must match loadouts");

            var players = new MobaPlayerEntry[loadouts.Length];
            var playerActors = new MobaPlayerActorEntry[loadouts.Length];
            var localActorId = 0;
            var localTransform = Transform3.Identity;

            var builtActors = new BuildActorResult[loadouts.Length];
            var builtCount = 0;
            try
            {
                for (int i = 0; i < loadouts.Length; i++)
                {
                    var loadout = loadouts[i];
                    var spec = specs[i];
                    var built = BuildActorAndRegister(
                        actorContext,
                        registry,
                        entities,
                        in spec,
                        onActorBuilt: (entity, buildSpec) => onActorBuilt?.Invoke(entity, loadout));
                    builtActors[builtCount++] = built;

                    players[i] = new MobaPlayerEntry(loadout.PlayerId, loadout.TeamId, loadout.HeroId, loadout.SpawnIndex);
                    playerActors[i] = new MobaPlayerActorEntry(loadout.PlayerId, built.Spec.Info.ActorId);

                    if (localActorId == 0 && loadout.PlayerId.Equals(localPlayerId))
                    {
                        localActorId = built.Spec.Info.ActorId;
                        localTransform = built.Spec.Info.Transform;
                    }
                }

                if (localActorId == 0)
                {
                    throw new InvalidOperationException($"localPlayerId not found in loadouts. playerId={localPlayerId.Value}");
                }

                return new BuildActorsResult(localActorId: localActorId, players: players, playerActors: playerActors, localActorTransform: localTransform);
            }
            catch
            {
                var registrar = new MobaActorSpawnRegistrar(registry, entities);
                for (var i = builtCount - 1; i >= 0; i--)
                {
                    var built = builtActors[i];
                    registrar.Unregister(
                        built.Spec.Info.ActorId,
                        out _,
                        publishDespawn: false);
                    DestroyBuiltEntity(built.Entity);
                }

                throw;
            }
        }

        /// <summary>
        /// 从旧入战请求读取负载，逐个初始化并立即注册实体。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
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

        /// <summary>
        /// 从旧入战请求读取负载，构建并立即注册实体。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
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
}
