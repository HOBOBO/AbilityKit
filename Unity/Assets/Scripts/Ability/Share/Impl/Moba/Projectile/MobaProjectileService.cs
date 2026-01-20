using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
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
                templateId: projectileCode,
                launcherActorId: 0,
                rootActorId: casterActorId,
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

        public bool Launch(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, in Vec3 aimPos, in Vec3 aimDir)
        {
            if (_projectiles == null) return false;
            if (_actorIds == null) return false;
            if (_registry == null) return false;
            if (_entities == null) return false;
            if (casterActorId <= 0) return false;
            if (launcher == null) return false;
            if (projectile == null) return false;

            if (!_entities.TryGetActorEntity(casterActorId, out var caster) || caster == null || !caster.hasTransform)
            {
                return false;
            }

            var spawnPos = aimPos.SqrMagnitude > 0f ? aimPos : caster.transform.Value.Position;
            var dir = aimDir.SqrMagnitude > 0f ? aimDir : caster.transform.Value.Forward;
            dir = dir.Normalized;
            if (dir.SqrMagnitude <= 0f) dir = Vec3.Forward;

            global::Contexts contexts = null;
            _services?.TryGet(out contexts);
            var actorContext = contexts != null ? contexts.actor : null;
            if (actorContext == null) return false;

            var frameTime = default(IFrameTime);
            _services?.TryGet(out frameTime);

            var nowMs = 0L;
            if (frameTime != null)
            {
                nowMs = (long)System.MathF.Round(frameTime.Time * 1000f);
            }

            var intervalFrames = 1;
            if (launcher.IntervalMs > 0)
            {
                if (frameTime != null && frameTime.DeltaTime > 0f)
                {
                    intervalFrames = System.Math.Max(1, (int)System.MathF.Round(launcher.IntervalMs / (frameTime.DeltaTime * 1000f)));
                }
                else
                {
                    intervalFrames = System.Math.Max(1, (int)System.MathF.Round(launcher.IntervalMs / 33.333f));
                }
            }

            var count = 1;
            if (launcher.DurationMs > 0 && launcher.IntervalMs > 0)
            {
                count = System.Math.Max(1, (launcher.DurationMs / launcher.IntervalMs) + 1);
            }

            var lifetimeFrames = 0;
            if (projectile.LifetimeMs > 0)
            {
                if (frameTime != null && frameTime.DeltaTime > 0f)
                {
                    lifetimeFrames = System.Math.Max(1, (int)System.MathF.Round(projectile.LifetimeMs / (frameTime.DeltaTime * 1000f)));
                }
                else
                {
                    lifetimeFrames = System.Math.Max(1, (int)System.MathF.Round(projectile.LifetimeMs / 33.333f));
                }
            }

            var team = caster.hasTeam ? caster.team.Value : Team.None;
            var ownerPlayer = caster.hasOwnerPlayerId ? caster.ownerPlayerId.Value : default(PlayerId);

            var launcherActorId = _actorIds.Next();
            var rot = Quat.LookRotation(dir, Vec3.Up);
            var t = new Transform3(spawnPos, rot, Vec3.One);

            var launcherInfo = new MobaEntityInfo(
                actorId: launcherActorId,
                kind: MobaEntityKind.Projectile,
                transform: t,
                team: team,
                mainType: EntityMainType.Projectile,
                unitSubType: UnitSubType.Bullet,
                ownerPlayer: ownerPlayer,
                templateId: launcher.Id);

            var launcherEntity = MobaEntitySpawnFactory.Create(actorContext, in launcherInfo);
            if (launcherEntity == null) return false;

            _registry.Register(launcherActorId, launcherEntity);
            try { _entities.TryRegisterFromEntity(launcherEntity); }
            catch { }

            var hitCooldownFrames = 0;
            if (projectile.HitCooldownMs > 0)
            {
                if (frameTime != null && frameTime.DeltaTime > 0f)
                {
                    hitCooldownFrames = System.Math.Max(1, (int)System.MathF.Round(projectile.HitCooldownMs / (frameTime.DeltaTime * 1000f)));
                }
                else
                {
                    hitCooldownFrames = System.Math.Max(1, (int)System.MathF.Round(projectile.HitCooldownMs / 33.333f));
                }
            }

            var tickIntervalFrames = 0;
            if (projectile.TickIntervalMs > 0)
            {
                if (frameTime != null && frameTime.DeltaTime > 0f)
                {
                    tickIntervalFrames = System.Math.Max(1, (int)System.MathF.Round(projectile.TickIntervalMs / (frameTime.DeltaTime * 1000f)));
                }
                else
                {
                    tickIntervalFrames = System.Math.Max(1, (int)System.MathF.Round(projectile.TickIntervalMs / 33.333f));
                }
            }

            var collisionMask = -1;
            var ignore = default(ColliderId);
            if (caster.hasCollisionLayer) collisionMask = caster.collisionLayer.Mask;
            if (caster.hasCollisionId) ignore = caster.collisionId.Value;

            var baseSpawn = new ProjectileSpawnParams(
                ownerId: casterActorId,
                templateId: projectile.Id,
                launcherActorId: launcherActorId,
                rootActorId: casterActorId,
                position: spawnPos,
                direction: dir,
                speed: projectile.Speed,
                lifetimeFrames: lifetimeFrames,
                maxDistance: projectile.MaxDistance,
                collisionLayerMask: collisionMask,
                ignoreCollider: ignore,
                hitPolicy: null,
                hitsRemaining: projectile.HitsRemaining > 0 ? projectile.HitsRemaining : 1,
                hitPolicyKind: projectile.HitPolicyKind,
                hitPolicyParam: 0,
                tickIntervalFrames: tickIntervalFrames,
                hitFilter: null,
                hitCooldownFrames: hitCooldownFrames);

            IProjectileSpawnPattern pattern = null;
            if (launcher.EmitterType == ProjectileEmitterType.Linear)
            {
                pattern = new SingleShotPattern();
            }
            else
            {
                pattern = new SingleShotPattern();
            }

            var startFrame = frameTime != null ? frameTime.Frame.Value : 0;
            var schedule = ProjectileScheduleParams.Repeat(startFrame, intervalFrames: intervalFrames, count: count);
            var scheduleId = _projectiles.ScheduleEmit(pattern, in baseSpawn, in schedule);

            var endTimeMs = launcher.DurationMs > 0 ? nowMs + launcher.DurationMs : nowMs;
            launcherEntity.AddProjectileLauncher(
                newLauncherId: launcher.Id,
                newProjectileId: projectile.Id,
                newRootActorId: casterActorId,
                newEndTimeMs: endTimeMs,
                newActiveBullets: 0,
                newScheduleId: scheduleId.Value,
                newIntervalFrames: intervalFrames,
                newTotalCount: count);

            return true;
        }

        public void Dispose()
        {
        }
    }
}
