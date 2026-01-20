using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services.Projectile
{
    public sealed class MobaProjectileService : IService
    {
        private readonly IWorldServices _services;
        private readonly IProjectileService _projectiles;
        private readonly ActorIdAllocator _actorIds;
        private readonly MobaActorRegistry _registry;
        private readonly MobaEntityManager _entities;
        private readonly MobaProjectileLinkService _links;

        public MobaProjectileService(IWorldServices services, IProjectileService projectiles, ActorIdAllocator actorIds, MobaActorRegistry registry, MobaEntityManager entities, MobaProjectileLinkService links)
        {
            _services = services;
            _projectiles = projectiles;
            _actorIds = actorIds;
            _registry = registry;
            _entities = entities;
            _links = links;
        }

        public bool Shoot(int casterActorId, ProjectileEmitterType emitterType, int projectileCode, float speed, int lifetimeFrames, float maxDistance, in Vec3 aimPos, in Vec3 aimDir)
        {
            if (_projectiles == null) return false;
            if (casterActorId <= 0) return false;
            if (projectileCode <= 0) return false;
            if (speed <= 0f) return false;
            if (lifetimeFrames <= 0 && maxDistance <= 0f) return false;

            if (!_entities.TryGetActorEntity(casterActorId, out var caster) || caster == null || !caster.hasTransform)
            {
                return false;
            }

            var spawnPos = aimPos.SqrMagnitude > 0f ? aimPos : caster.transform.Value.Position;
            var dir = aimDir.SqrMagnitude > 0f ? aimDir : caster.transform.Value.Forward;
            dir = dir.Normalized;
            if (dir.SqrMagnitude <= 0f) dir = Vec3.Forward;

            var projectileActorId = _actorIds.Next();

            var rot = Quat.LookRotation(dir, Vec3.Up);
            var t = new Transform3(spawnPos, rot, Vec3.One);

            var team = caster.hasTeam ? caster.team.Value : Team.None;
            var ownerPlayer = caster.hasOwnerPlayerId ? caster.ownerPlayerId.Value : default(PlayerId);

            var info = new MobaEntityInfo(
                actorId: projectileActorId,
                kind: MobaEntityKind.Projectile,
                transform: t,
                team: team,
                mainType: EntityMainType.Projectile,
                unitSubType: UnitSubType.Bullet,
                ownerPlayer: ownerPlayer,
                templateId: projectileCode);

            global::Contexts contexts = null;
            _services?.TryGet(out contexts);

            var actorContext = contexts != null ? contexts.actor : null;
            if (actorContext == null) return false;

            var bullet = MobaEntitySpawnFactory.Create(actorContext, in info);
            if (bullet == null) return false;

            bullet.isFlyingProjectileTag = true;

            _registry?.Register(projectileActorId, bullet);

            // Optional: register immediately, otherwise MobaEntityManagerSyncSystem will pick it up next tick.
            try { _entities?.TryRegisterFromEntity(bullet); }
            catch { }

            var collisionMask = -1;
            var ignore = default(ColliderId);
            if (caster.hasCollisionLayer) collisionMask = caster.collisionLayer.Mask;
            if (caster.hasCollisionId) ignore = caster.collisionId.Value;

            var spawn = new ProjectileSpawnParams(
                ownerId: casterActorId,
                position: spawnPos,
                direction: dir,
                speed: speed,
                lifetimeFrames: lifetimeFrames,
                maxDistance: maxDistance,
                collisionLayerMask: collisionMask,
                ignoreCollider: ignore);

            ProjectileId pid;
            switch (emitterType)
            {
                case ProjectileEmitterType.Linear:
                default:
                    pid = _projectiles.Spawn(in spawn);
                    break;
            }

            _links?.Link(pid, projectileActorId);
            return true;
        }

        public void Dispose()
        {
        }
    }
}
