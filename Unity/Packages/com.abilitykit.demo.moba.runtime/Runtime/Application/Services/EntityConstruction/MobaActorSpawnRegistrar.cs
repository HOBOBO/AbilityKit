using System;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    public sealed class MobaActorSpawnRegistrar
    {
        private readonly MobaActorRegistry _registry;
        private readonly MobaEntityManager _entities;

        public MobaActorSpawnRegistrar(MobaActorRegistry registry, MobaEntityManager entities)
        {
            _registry = registry;
            _entities = entities;
        }

        public bool Register(
            global::ActorEntity entity,
            in MobaActorBuildSpec spec,
            bool registerActor,
            bool registerEntityManager,
            bool registerEntityManagerFromEntity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var actorId = spec.Info.ActorId;
            if (registerActor && _registry != null && _registry.Contains(actorId))
            {
                throw new InvalidOperationException($"Actor {actorId} is already registered.");
            }
            if (registerEntityManager && _entities != null && _entities.TryGetActorEntity(actorId, out _))
            {
                throw new InvalidOperationException($"Actor {actorId} is already registered in MobaEntityManager.");
            }

            var actorRegistered = false;
            var entityRegistered = false;
            try
            {
                if (registerActor && _registry != null)
                {
                    _registry.Register(actorId, entity);
                    actorRegistered = true;
                }

                if (!registerEntityManager || _entities == null) return actorRegistered;

                if (registerEntityManagerFromEntity)
                {
                    if (!entity.hasActorId ||
                        !entity.hasTeam ||
                        !entity.hasEntityMainType ||
                        !entity.hasUnitSubType ||
                        !entity.hasOwnerPlayerId)
                    {
                        throw new InvalidOperationException($"Actor {actorId} is missing components required by MobaEntityManager.");
                    }

                    entityRegistered = _entities.RegisterSilently(
                        entity.actorId.Value,
                        entity,
                        entity.team.Value,
                        entity.entityMainType.Value,
                        entity.unitSubType.Value,
                        entity.ownerPlayerId.Value);
                }
                else
                {
                    entityRegistered = _entities.RegisterSilently(
                        actorId,
                        entity,
                        spec.Info.Team,
                        spec.Info.MainType,
                        spec.Info.UnitSubType,
                        spec.Info.OwnerPlayer);
                }

                return entityRegistered;
            }
            catch
            {
                if (entityRegistered)
                {
                    _entities?.UnregisterSilently(actorId, out _);
                }
                if (actorRegistered)
                {
                    _registry?.Unregister(actorId);
                }
                throw;
            }
        }

        public bool Unregister(
            int actorId,
            out global::ActorEntity entity,
            bool publishDespawn = true)
        {
            entity = null;
            if (actorId <= 0) return false;

            global::ActorEntity actorEntity = null;
            global::ActorEntity indexedEntity = null;
            var actorRegistered =
                _registry != null &&
                _registry.TryGetRegistered(actorId, out actorEntity);
            var entityRegistered =
                _entities != null &&
                _entities.TryGetActorEntity(actorId, out indexedEntity);
            if (actorRegistered &&
                entityRegistered &&
                !ReferenceEquals(actorEntity, indexedEntity))
            {
                throw new InvalidOperationException(
                    $"Actor {actorId} is registered with different entity instances.");
            }

            entity = entityRegistered ? indexedEntity : actorEntity;
            if (!actorRegistered && !entityRegistered) return false;

            if (entityRegistered && !_entities.UnregisterSilently(actorId, out entity))
            {
                throw new InvalidOperationException(
                    $"Actor {actorId} disappeared from MobaEntityManager during unregister.");
            }
            if (actorRegistered)
            {
                _registry.Unregister(actorId);
            }

            if (publishDespawn && entityRegistered)
            {
                _entities.PublishDespawn(entity);
            }
            return true;
        }
    }
}
