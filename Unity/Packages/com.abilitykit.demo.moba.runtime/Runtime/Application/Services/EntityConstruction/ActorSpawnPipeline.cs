using System;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Mathematics;
using AbilityKit.Protocol.Moba;
using AbilityKit.Demo.Moba.Util.Converter;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    public static class ActorSpawnPipeline
    {
        public static BuildActorResult BuildActor(
            ActorContext actorContext,
            in MobaActorBuildSpec spec,
            Action<ActorEntity, MobaActorBuildSpec> initializer = null,
            Action<ActorEntity, MobaActorBuildSpec> onActorBuilt = null)
        {
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));
            if (spec.Info.ActorId <= 0) throw new InvalidOperationException("actorId is required");

            var info = spec.Info;
            var entity = ActorArchetypeFactory.Create(actorContext, in info);
            try
            {
                initializer?.Invoke(entity, spec);
                onActorBuilt?.Invoke(entity, spec);
                return new BuildActorResult(entity, spec);
            }
            catch
            {
                DestroyBuiltEntity(entity);
                throw;
            }
        }

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
                entities?.Unregister(spec.Info.ActorId);
                registry?.Unregister(spec.Info.ActorId);
                DestroyBuiltEntity(result.Entity);
                throw;
            }
        }

        public static void RegisterBuiltActor(
            MobaActorRegistry registry,
            MobaEntityManager entities,
            ActorEntity entity,
            in MobaActorBuildSpec spec)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (spec.Info.ActorId <= 0) throw new InvalidOperationException("actorId is required");

            registry?.Register(spec.Info.ActorId, entity);

            if (entities == null) return;
            entities.Register(
                actorId: spec.Info.ActorId,
                entity: entity,
                team: spec.Info.Team,
                mainType: spec.Info.MainType,
                unitSubType: spec.Info.UnitSubType,
                ownerPlayer: spec.Info.OwnerPlayer);
        }

        internal static void DestroyBuiltEntity(ActorEntity entity)
        {
            if (entity != null && entity.isEnabled)
            {
                entity.Destroy();
            }
        }

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
                for (var i = builtCount - 1; i >= 0; i--)
                {
                    var built = builtActors[i];
                    entities.Unregister(built.Spec.Info.ActorId);
                    registry.Unregister(built.Spec.Info.ActorId);
                    DestroyBuiltEntity(built.Entity);
                }

                throw;
            }
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

    public enum MobaActorBuildSourceKind
    {
        Unknown = 0,
        PlayerLoadout = 1,
        Summon = 2,
        Projectile = 3,
        ProjectileLauncher = 4,
    }

    public readonly struct MobaActorBuildSpec
    {
        public readonly MobaEntityInfo Info;
        public readonly MobaActorBuildSourceKind SourceKind;
        public readonly int SourceId;
        public readonly int OwnerActorId;

        public MobaActorBuildSpec(in MobaEntityInfo info, MobaActorBuildSourceKind sourceKind, int sourceId, int ownerActorId)
        {
            Info = info;
            SourceKind = sourceKind;
            SourceId = sourceId;
            OwnerActorId = ownerActorId;
        }
    }

    public readonly struct BuildActorResult
    {
        public readonly ActorEntity Entity;
        public readonly MobaActorBuildSpec Spec;

        public BuildActorResult(ActorEntity entity, in MobaActorBuildSpec spec)
        {
            Entity = entity;
            Spec = spec;
        }
    }

    public readonly struct MobaPlayerActorEntry
    {
        public readonly PlayerId PlayerId;
        public readonly int ActorId;

        public MobaPlayerActorEntry(PlayerId playerId, int actorId)
        {
            PlayerId = playerId;
            ActorId = actorId;
        }
    }

    public readonly struct BuildActorsResult
    {
        public readonly int LocalActorId;
        public readonly MobaPlayerEntry[] Players;
        public readonly MobaPlayerActorEntry[] PlayerActors;
        public readonly Transform3 LocalActorTransform;

        public BuildActorsResult(int localActorId, MobaPlayerEntry[] players, MobaPlayerActorEntry[] playerActors, in Transform3 localActorTransform)
        {
            LocalActorId = localActorId;
            Players = players;
            PlayerActors = playerActors;
            LocalActorTransform = localActorTransform;
        }
    }
}

