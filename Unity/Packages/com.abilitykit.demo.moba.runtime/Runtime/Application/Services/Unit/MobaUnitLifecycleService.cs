using System;
using System.Runtime.ExceptionServices;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services.EntityManager;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaUnitRespawnFailure
    {
        None = 0,
        InvalidActor = 1,
        ActorNotFound = 2,
        AlreadyAlive = 3,
        MissingAttributes = 4,
        InvalidMaximumHealth = 5,
    }

    public readonly struct MobaUnitRespawnResult
    {
        public MobaUnitRespawnResult(bool succeeded, int actorId, float restoredHp, MobaUnitRespawnFailure failure)
        {
            Succeeded = succeeded;
            ActorId = actorId;
            RestoredHp = restoredHp;
            Failure = failure;
        }

        public bool Succeeded { get; }
        public int ActorId { get; }
        public float RestoredHp { get; }
        public MobaUnitRespawnFailure Failure { get; }
    }

    /// <summary>
    /// Applies an already-approved respawn to an existing actor. Match rules own timing,
    /// spawn-point selection and respawn limits; this service owns the state transition.
    /// </summary>
    [WorldService(typeof(MobaUnitLifecycleService))]
    public sealed class MobaUnitLifecycleService : IService
    {
        private readonly MobaActorLookupService _actors;
        private readonly MobaEntityManager _entities;
        private readonly MobaUnitDeathSubscriber _deaths;
        private readonly MobaDamageService _damage;

        public MobaUnitLifecycleService(
            MobaActorLookupService actors,
            MobaEntityManager entities,
            MobaUnitDeathSubscriber deaths,
            MobaDamageService damage)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _deaths = deaths ?? throw new ArgumentNullException(nameof(deaths));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
        }

        public MobaUnitRespawnResult TryRespawn(int actorId, float healthRatio = 1f)
        {
            return TryRespawn(actorId, hasRespawnPosition: false, default, healthRatio);
        }

        public MobaUnitRespawnResult TryRespawn(int actorId, in Vec3 respawnPosition, float healthRatio = 1f)
        {
            return TryRespawn(actorId, hasRespawnPosition: true, respawnPosition, healthRatio);
        }

        private MobaUnitRespawnResult TryRespawn(int actorId, bool hasRespawnPosition, in Vec3 respawnPosition, float healthRatio)
        {
            if (actorId <= 0)
            {
                return Failed(actorId, MobaUnitRespawnFailure.InvalidActor);
            }

            if (!_actors.TryGetActorEntity(actorId, out var actor) || actor == null)
            {
                return Failed(actorId, MobaUnitRespawnFailure.ActorNotFound);
            }

            if (!actor.hasAttributeGroup || !actor.hasResourceContainer || actor.resourceContainer.Value == null)
            {
                return Failed(actorId, MobaUnitRespawnFailure.MissingAttributes);
            }

            var attributes = new MobaAttrs(actor);
            if (attributes.Hp > 0f)
            {
                return Failed(actorId, MobaUnitRespawnFailure.AlreadyAlive);
            }

            var maximumHp = attributes.MaxHp;
            if (maximumHp <= 0f)
            {
                return Failed(actorId, MobaUnitRespawnFailure.InvalidMaximumHealth);
            }

            var normalizedRatio = Clamp(healthRatio, 0.01f, 1f);
            var requestedHp = maximumHp * normalizedRatio;
            MobaHealthChangeResult committed = default;
            Exception firstFailure = null;
            try
            {
                committed = _damage.CommitHeal(
                    healerActorId: actorId,
                    targetActorId: actorId,
                    healType: 0,
                    value: requestedHp,
                    allowDeadTarget: true);
            }
            catch (Exception ex)
            {
                firstFailure = ex;
            }

            var restoredHp = attributes.Hp;
            if (!committed.Succeeded && restoredHp <= 0f)
            {
                if (firstFailure != null)
                {
                    ExceptionDispatchInfo.Capture(firstFailure).Throw();
                }

                return Failed(actorId, MobaUnitRespawnFailure.MissingAttributes);
            }

            var committedRespawnPosition = respawnPosition;
            TryPostCommitStep(() =>
            {
                if (!hasRespawnPosition || !actor.hasTransform) return;
                var transform = actor.transform.Value;
                actor.ReplaceTransform(new Transform3(committedRespawnPosition, transform.Rotation, transform.Scale));
            }, ref firstFailure);
            TryPostCommitStep(() => _deaths.NotifyRespawned(actorId), ref firstFailure);
            TryPostCommitStep(() => _entities.PublishRespawn(actor), ref firstFailure);

            if (firstFailure != null)
            {
                ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }

            return new MobaUnitRespawnResult(true, actorId, restoredHp, MobaUnitRespawnFailure.None);
        }

        private static void TryPostCommitStep(Action action, ref Exception firstFailure)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                if (firstFailure == null) firstFailure = ex;
            }
        }

        private static MobaUnitRespawnResult Failed(int actorId, MobaUnitRespawnFailure failure)
        {
            return new MobaUnitRespawnResult(false, actorId, 0f, failure);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        public void Dispose()
        {
        }
    }
}
