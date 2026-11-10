using System.Collections.Generic;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    [WorldService(typeof(IProjectileBounceTargetProvider))]
    [WorldService(typeof(MobaProjectileBounceTargetProvider))]
    public sealed class MobaProjectileBounceTargetProvider : IProjectileBounceTargetProvider
    {
        private readonly MobaActorRegistry _registry;
        private readonly MobaCombatRulesService _combatRules;

        public MobaProjectileBounceTargetProvider(MobaActorRegistry registry, MobaCombatRulesService combatRules = null)
        {
            _registry = registry;
            _combatRules = combatRules;
        }

        public bool TryResolveActorId(ColliderId collider, out int actorId)
        {
            actorId = 0;
            if (_registry == null || collider.Value == 0) return false;
            var ids = _registry.CopyActorIdsInOrder();
            for (var i = 0; i < ids.Count; i++)
            {
                if (!_registry.TryGet(ids[i], out var entity) || entity == null || !entity.hasCollisionId) continue;
                if (entity.collisionId.Value.Equals(collider))
                {
                    actorId = ids[i];
                    return true;
                }
            }
            return false;
        }

        public bool TryGetTrackingTargetPosition(int actorId, out Vec3 position)
        {
            position = Vec3.Zero;
            if (_registry == null || !_registry.TryGet(actorId, out var entity) || !IsEligibleEntity(entity)) return false;
            position = entity.transform.Value.Position;
            return true;
        }

        public bool TrySelectNextTarget(int ownerId, int hitActorId, in Vec3 projectilePosition, IReadOnlyList<int> hitActorIds,
            float searchRadius,
            out int targetActorId, out Vec3 targetPosition)
        {
            targetActorId = 0;
            targetPosition = Vec3.Zero;
            if (_registry == null || !_registry.TryGet(hitActorId, out var hitEntity) || !IsEligibleEntity(hitEntity)) return false;

            // The search follows the projectile's actual collision position. Swept and arc hits
            // can be far from the target transform, so the actor center is not the bounce origin.
            var origin = projectilePosition;
            var radiusSquared = searchRadius > 0f ? searchRadius * searchRadius : float.PositiveInfinity;
            var bestDistance = float.MaxValue;
            var ids = _registry.CopyActorIdsInOrder();
            for (var i = 0; i < ids.Count; i++)
            {
                var candidateId = ids[i];
                if (candidateId == ownerId || candidateId == hitActorId || Contains(hitActorIds, candidateId)) continue;
                if (!_registry.TryGet(candidateId, out var candidate) || !IsEligibleEntity(candidate) || !AreEnemies(ownerId, candidateId)) continue;

                var position = candidate.transform.Value.Position;
                var dx = position.X - origin.X;
                var dz = position.Z - origin.Z;
                var distance = dx * dx + dz * dz;
                if (distance > radiusSquared) continue;
                // Registry order can differ after rollback/reconstruction. Break exact-distance
                // ties by ActorId so every peer chooses the same next bounce target.
                if (distance < bestDistance ||
                    (distance.Equals(bestDistance) && (targetActorId <= 0 || candidateId < targetActorId)))
                {
                    bestDistance = distance;
                    targetActorId = candidateId;
                    targetPosition = position;
                }
            }
            return targetActorId > 0;
        }

        private bool AreEnemies(int ownerId, int candidateId)
        {
            if (_combatRules != null) return _combatRules.AreEnemies(ownerId, candidateId);
            if (!_registry.TryGet(ownerId, out var owner) || owner == null || !owner.hasTeam ||
                !_registry.TryGet(candidateId, out var candidate) || candidate == null || !candidate.hasTeam) return false;
            return owner.team.Value != AbilityKit.Demo.Moba.Team.None && candidate.team.Value != AbilityKit.Demo.Moba.Team.None &&
                   owner.team.Value != candidate.team.Value;
        }

        private bool IsEligibleEntity(global::ActorEntity entity)
        {
            if (entity == null || !entity.isEnabled || !entity.hasTransform) return false;
            return _combatRules == null || _combatRules.IsAlive(entity);
        }

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            if (values == null) return false;
            for (var i = 0; i < values.Count; i++)
                if (values[i] == value) return true;
            return false;
        }

        public void Dispose()
        {
        }
    }
}
