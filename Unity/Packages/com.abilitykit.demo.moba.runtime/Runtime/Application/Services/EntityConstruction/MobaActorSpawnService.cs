using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    public interface IMobaActorSpawnService : IService
    {
        bool TrySpawn(in MobaActorSpawnRequest request, out MobaActorSpawnResult result);
    }

    public interface IMobaActorSpawnTransactionService : IService
    {
        bool TrySpawnUnpublished(in MobaActorSpawnRequest request, out MobaActorSpawnResult result);
        void Publish(in MobaActorSpawnResult result);
        void Rollback(in MobaActorSpawnResult result);
    }

    public sealed class MobaActorSpawnRequest
    {
        public MobaActorBuildSpec Spec;
        public bool AllocateActorIdIfMissing;
        public bool RegisterActor = true;
        public bool RegisterEntityManager = true;
        public bool RegisterEntityManagerFromEntity = true;
        public MobaActorSpawnPostSetup PostSetup;
        public Action<global::ActorEntity, MobaActorBuildSpec> Initializer;
        public Action<global::ActorEntity, MobaActorBuildSpec> OnActorBuilt;

        public static MobaActorSpawnRequest FromSpec(in MobaActorBuildSpec spec)
        {
            return new MobaActorSpawnRequest { Spec = spec };
        }
    }

    public readonly struct MobaActorSpawnResult
    {
        public readonly bool Success;
        public readonly int ActorId;
        public readonly global::ActorEntity Entity;
        public readonly MobaActorBuildSpec Spec;
        public readonly string Error;
        public readonly bool RegisteredActor;
        public readonly bool RegisteredEntityManager;

        public MobaActorSpawnResult(bool success, int actorId, global::ActorEntity entity, in MobaActorBuildSpec spec, string error)
            : this(success, actorId, entity, in spec, error, success, success)
        {
        }

        public MobaActorSpawnResult(
            bool success,
            int actorId,
            global::ActorEntity entity,
            in MobaActorBuildSpec spec,
            string error,
            bool registeredActor,
            bool registeredEntityManager)
        {
            Success = success;
            ActorId = actorId;
            Entity = entity;
            Spec = spec;
            Error = error;
            RegisteredActor = registeredActor;
            RegisteredEntityManager = registeredEntityManager;
        }

        public static MobaActorSpawnResult Failed(string error)
        {
            return new MobaActorSpawnResult(false, 0, null, default, error);
        }
    }

    public struct MobaActorSpawnPostSetup
    {
        public bool SetOwnerLink;
        public int OwnerActorId;
        public int RootOwnerActorId;

        public bool SetLifetime;
        public long LifetimeEndTimeMs;

        public bool SetSummonMeta;
        public int SummonId;
        public bool DespawnOnOwnerDie;

        public bool SetModelId;
        public int ModelId;

        public bool SetBrain;
        public int BrainId;
        public int BrainOwnerActorId;
        public int BrainSourceKind;
        public int BrainSourceId;

        public bool SetFlyingProjectileTag;

        public bool SetProjectileLauncher;
        public int LauncherId;
        public int ProjectileId;
        public int ProjectileRootActorId;
        public long ProjectileLauncherEndTimeMs;
        public int ProjectileLauncherActiveBullets;
        public int ProjectileLauncherScheduleId;
        public int ProjectileLauncherIntervalFrames;
        public int ProjectileLauncherTotalCount;
    }

    [WorldService(typeof(MobaActorSpawnService))]
    [WorldService(typeof(IMobaActorSpawnService))]
    [WorldService(typeof(IMobaActorSpawnTransactionService))]
    public sealed class MobaActorSpawnService : IMobaActorSpawnService, IMobaActorSpawnTransactionService
    {
        [WorldInject(required: false)] private global::Entitas.IContexts _contexts = null;
        [WorldInject(required: false)] private ActorIdAllocator _actorIds = null;
        [WorldInject(required: false)] private MobaActorRegistry _registry = null;
        [WorldInject(required: false)] private MobaEntityManager _entities = null;

        private MobaActorSpawnRegistrar _registrar;

        public bool TrySpawn(in MobaActorSpawnRequest request, out MobaActorSpawnResult result)
        {
            return TrySpawnCore(in request, publishSpawn: true, out result);
        }

        public bool TrySpawnUnpublished(in MobaActorSpawnRequest request, out MobaActorSpawnResult result)
        {
            return TrySpawnCore(in request, publishSpawn: false, out result);
        }

        private bool TrySpawnCore(
            in MobaActorSpawnRequest request,
            bool publishSpawn,
            out MobaActorSpawnResult result)
        {
            if (request == null)
            {
                result = MobaActorSpawnResult.Failed("request is required");
                return false;
            }

            var actorContext = ResolveActorContext();
            if (actorContext == null)
            {
                result = MobaActorSpawnResult.Failed("ActorContext is required");
                return false;
            }

            var spec = request.Spec;
            if (spec.Info.ActorId <= 0)
            {
                if (!request.AllocateActorIdIfMissing || _actorIds == null)
                {
                    result = MobaActorSpawnResult.Failed("actorId is required");
                    return false;
                }

                spec = WithActorId(in spec, _actorIds.Next());
            }

            global::ActorEntity entity = null;
            var registrationCompleted = false;
            var registeredActor = false;
            var registeredEntityManager = false;
            try
            {
                var built = ActorSpawnPipeline.BuildActor(
                    actorContext,
                    in spec,
                    request.Initializer,
                    request.OnActorBuilt);

                entity = built.Entity;
                if (entity == null)
                {
                    result = MobaActorSpawnResult.Failed("spawn returned null entity");
                    return false;
                }

                MobaActorSpawnPostSetupApplier.Apply(entity, in request.PostSetup);
                var registeredInEntityManager = CreateRegistrar().Register(
                    entity,
                    in spec,
                    request.RegisterActor,
                    request.RegisterEntityManager,
                    request.RegisterEntityManagerFromEntity);
                registrationCompleted = true;
                registeredActor = request.RegisterActor &&
                    _registry != null &&
                    _registry.TryGetRegistered(spec.Info.ActorId, out var actorRegistration) &&
                    ReferenceEquals(actorRegistration, entity);
                registeredEntityManager = request.RegisterEntityManager &&
                    _entities != null &&
                    _entities.TryGetActorEntity(spec.Info.ActorId, out var entityRegistration) &&
                    ReferenceEquals(entityRegistration, entity);
                if (publishSpawn && registeredInEntityManager && registeredEntityManager)
                {
                    _entities.PublishSpawn(entity);
                }

                result = new MobaActorSpawnResult(
                    true,
                    spec.Info.ActorId,
                    entity,
                    in spec,
                    null,
                    registeredActor,
                    registeredEntityManager);
                return true;
            }
            catch (Exception ex)
            {
                if (registrationCompleted)
                {
                    RollbackRegistration(
                        spec.Info.ActorId,
                        entity,
                        registeredActor,
                        registeredEntityManager);
                }
                ActorSpawnPipeline.DestroyBuiltEntity(entity);
                Log.Exception(ex, $"[MobaActorSpawnService] spawn failed. kind={spec.Info.Kind} actorId={spec.Info.ActorId} sourceKind={spec.SourceKind} sourceId={spec.SourceId}");
                result = MobaActorSpawnResult.Failed(ex.Message);
                return false;
            }
        }

        public void Publish(in MobaActorSpawnResult result)
        {
            if (!result.Success ||
                !result.RegisteredEntityManager ||
                result.Entity == null ||
                _entities == null) return;
            if (_entities.TryGetActorEntity(result.ActorId, out var registered) &&
                ReferenceEquals(registered, result.Entity))
            {
                _entities.PublishSpawn(result.Entity);
            }
        }

        public void Rollback(in MobaActorSpawnResult result)
        {
            if (result.ActorId <= 0 && result.Entity == null) return;

            try
            {
                RollbackRegistration(
                    result.ActorId,
                    result.Entity,
                    result.RegisteredActor,
                    result.RegisteredEntityManager);
            }
            finally
            {
                ActorSpawnPipeline.DestroyBuiltEntity(result.Entity);
            }
        }

        private void RollbackRegistration(
            int actorId,
            global::ActorEntity expectedEntity,
            bool registeredActor,
            bool registeredEntityManager)
        {
            if (registeredActor && registeredEntityManager)
            {
                CreateRegistrar().Unregister(actorId, out _, publishDespawn: false);
                return;
            }

            if (registeredEntityManager && _entities != null)
            {
                if (_entities.TryGetActorEntity(actorId, out var entityRegistration) &&
                    ReferenceEquals(entityRegistration, expectedEntity))
                {
                    _entities.UnregisterSilently(actorId, out _);
                }
            }

            if (registeredActor && _registry != null &&
                _registry.TryGetRegistered(actorId, out var actorRegistration) &&
                ReferenceEquals(actorRegistration, expectedEntity))
            {
                _registry.Unregister(actorId);
            }
        }

        private global::ActorContext ResolveActorContext()
        {
            return (_contexts as global::Contexts)?.actor;
        }

        private MobaActorSpawnRegistrar CreateRegistrar()
        {
            return _registrar ?? (_registrar = new MobaActorSpawnRegistrar(_registry, _entities));
        }

        private static MobaActorBuildSpec WithActorId(in MobaActorBuildSpec spec, int actorId)
        {
            var info = spec.Info;
            var nextInfo = new MobaEntityInfo(
                actorId: actorId,
                kind: info.Kind,
                transform: info.Transform,
                team: info.Team,
                mainType: info.MainType,
                unitSubType: info.UnitSubType,
                ownerPlayer: info.OwnerPlayer,
                templateId: info.TemplateId);

            return new MobaActorBuildSpec(in nextInfo, spec.SourceKind, spec.SourceId, spec.OwnerActorId);
        }

        public void Dispose()
        {
        }
    }
}
