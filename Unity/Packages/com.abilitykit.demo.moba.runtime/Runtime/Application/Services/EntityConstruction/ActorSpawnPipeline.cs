using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    /// <summary>
    /// 创建和初始化底层 Actor 实体，不负责索引注册或生成事件发布。
    /// </summary>
    public static partial class ActorSpawnPipeline
    {
        /// <summary>
        /// 创建未注册的 Actor 实体；初始化或构建回调抛出异常时销毁实体并重新抛出。
        /// </summary>
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

        internal static void DestroyBuiltEntity(ActorEntity entity)
        {
            if (entity != null && entity.isEnabled)
            {
                entity.Destroy();
            }
        }
    }

    public enum MobaActorBuildSourceKind
    {
        Unknown = 0,
        PlayerLoadout = 1,
        Summon = 2,
        Projectile = 3,
        ProjectileLauncher = 4,
        Minion = 5,
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
        public readonly MobaActorSpawnResult[] SpawnResults;

        public BuildActorsResult(int localActorId, MobaPlayerEntry[] players, MobaPlayerActorEntry[] playerActors, in Transform3 localActorTransform)
            : this(localActorId, players, playerActors, in localActorTransform, null)
        {
        }

        public BuildActorsResult(
            int localActorId,
            MobaPlayerEntry[] players,
            MobaPlayerActorEntry[] playerActors,
            in Transform3 localActorTransform,
            MobaActorSpawnResult[] spawnResults)
        {
            LocalActorId = localActorId;
            Players = players;
            PlayerActors = playerActors;
            LocalActorTransform = localActorTransform;
            SpawnResults = spawnResults ?? Array.Empty<MobaActorSpawnResult>();
        }
    }
}

