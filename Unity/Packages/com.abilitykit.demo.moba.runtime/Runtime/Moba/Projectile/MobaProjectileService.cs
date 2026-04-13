using System;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Util.Generator;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.Projectile;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Core.Math;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    public sealed class MobaProjectileService : IService
    {
        private const int CollisionLayer_Unit = 1 << 0;
        private const int CollisionLayer_World = 1 << 2;

        private readonly IWorldResolver _services;
        private readonly IProjectileService _projectiles;
        private readonly ActorIdAllocator _actorIds;
        private readonly MobaActorRegistry _registry;
        private readonly MobaEntityManager _entities;
        private readonly MobaProjectileLinkService _links;

        public MobaProjectileService(IWorldResolver services, IProjectileService projectiles, ActorIdAllocator actorIds, MobaActorRegistry registry, MobaEntityManager entities, MobaProjectileLinkService links)
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

            global::Entitas.IContexts contexts = null;
            _services?.TryResolve(out contexts);

            var actorContext = (contexts as global::Contexts)?.actor;
            if (actorContext == null) return false;

            var bullet = AbilityKit.Demo.Moba.Util.Generator.ActorArchetypeFactory.Create(actorContext, in info);
            if (bullet == null) return false;

            bullet.isFlyingProjectileTag = true;

            _registry?.Register(projectileActorId, bullet);

            if (_services != null && _services.TryResolve<MobaActorSpawnSnapshotService>(out var spawnSnapshots) && spawnSnapshots != null)
            {
                spawnSnapshots.Enqueue(new MobaActorSpawnSnapshotCodec.Entry(
                    netId: projectileActorId,
                    kind: (int)SpawnEntityKind.Projectile,
                    code: projectileCode,
                    ownerNetId: casterActorId,
                    x: spawnPos.X,
                    y: spawnPos.Y,
                    z: spawnPos.Z));
            }

            // Optional: register immediately, otherwise MobaEntityManagerSyncSystem will pick it up next tick.
            try { _entities?.TryRegisterFromEntity(bullet); }
            catch (Exception ex) { Log.Exception(ex, "[MobaProjectileService] TryRegisterFromEntity failed"); }

            var ignore = default(ColliderId);
            if (caster.hasCollisionId) ignore = caster.collisionId.Value;

            var collisionMask = CollisionLayer_Unit | CollisionLayer_World;

            var spawnFrame = 0;
            if (_services != null && _services.TryResolve<IFrameTime>(out var frameTime) && frameTime != null)
            {
                spawnFrame = frameTime.Frame.Value;
            }

            var spawn = new ProjectileSpawnParams(
                ownerId: casterActorId,
                templateId: projectileCode,
                launcherActorId: 0,
                rootActorId: casterActorId,
                spawnFrame: spawnFrame,
                position: spawnPos,
                direction: dir,
                speed: speed,
                returnAfterFrames: 0,
                returnSpeed: 0f,
                returnStopDistance: 0f,
                lifetimeFrames: lifetimeFrames,
                maxDistance: maxDistance,
                collisionLayerMask: collisionMask,
                ignoreCollider: ignore,
                hitFilter: new MobaTeamProjectileHitFilter(_registry));

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

        private sealed class MobaTeamProjectileHitFilter : IProjectileHitFilter
        {
            private readonly MobaActorRegistry _registry;

            public MobaTeamProjectileHitFilter(MobaActorRegistry registry)
            {
                _registry = registry;
            }

            public bool ShouldHit(int ownerId, ColliderId collider, int frame)
            {
                if (collider.Value == 0) return false;
                if (_registry == null) return true;

                if (!_registry.TryGet(ownerId, out var owner) || owner == null)
                {
                    return true;
                }

                var ownerTeam = owner.hasTeam ? owner.team.Value : Team.None;

                global::ActorEntity hitEntity = null;
                try
                {
                    foreach (var kv in _registry.Entries)
                    {
                        var e = kv.Value;
                        if (e == null || !e.hasCollisionId) continue;
                        if (e.collisionId.Value.Equals(collider))
                        {
                            hitEntity = e;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[MobaProjectileService] ShouldHit resolve collider->entity failed");
                    return true;
                }

                if (hitEntity == null) return true;

                // Never hit self.
                if (hitEntity.hasActorId && hitEntity.actorId.Value == ownerId) return false;

                var targetTeam = hitEntity.hasTeam ? hitEntity.team.Value : Team.None;

                // Default policy: block friendly fire (same team); allow hitting Neutral/None.
                if (ownerTeam != Team.None && targetTeam != Team.None && ownerTeam == targetTeam)
                {
                    return false;
                }

                return true;
            }
        }

        public bool Launch(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, in Vec3 aimPos, in Vec3 aimDir)
        {
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

            return LaunchFromSpawn(casterActorId, launcher, projectile, in spawnPos, in dir);
        }

        public bool LaunchFromSpawn(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, in Vec3 spawnPos, in Vec3 dir)
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

            var d = dir.SqrMagnitude > 0f ? dir.Normalized : caster.transform.Value.Forward.Normalized;
            if (d.SqrMagnitude <= 0f) d = Vec3.Forward;

            var sp = spawnPos.SqrMagnitude > 0f ? spawnPos : caster.transform.Value.Position;

            global::Entitas.IContexts contexts = null;
            _services?.TryResolve(out contexts);
            var actorContext = contexts != null ? contexts.Actor() : null;
            if (actorContext == null) return false;

            var frameTime = default(IFrameTime);
            _services?.TryResolve(out frameTime);

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

            var returnAfterFrames = 0;
            if (projectile.ReturnAfterMs > 0)
            {
                if (frameTime != null && frameTime.DeltaTime > 0f)
                {
                    returnAfterFrames = System.Math.Max(1, (int)System.MathF.Round(projectile.ReturnAfterMs / (frameTime.DeltaTime * 1000f)));
                }
                else
                {
                    returnAfterFrames = System.Math.Max(1, (int)System.MathF.Round(projectile.ReturnAfterMs / 33.333f));
                }
            }
            var returnSpeed = projectile.ReturnSpeed;
            var returnStopDistance = projectile.ReturnStopDistance;

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
            var rot = Quat.LookRotation(d, Vec3.Up);
            var t = new Transform3(sp, rot, Vec3.One);

            var launcherInfo = new MobaEntityInfo(
                actorId: launcherActorId,
                kind: MobaEntityKind.Projectile,
                transform: t,
                team: team,
                mainType: EntityMainType.Projectile,
                unitSubType: UnitSubType.Bullet,
                ownerPlayer: ownerPlayer,
                templateId: launcher.Id);

            var launcherEntity = AbilityKit.Demo.Moba.Util.Generator.ActorArchetypeFactory.Create(actorContext, in launcherInfo);
            if (launcherEntity == null) return false;

            _registry.Register(launcherActorId, launcherEntity);
            try { _entities.TryRegisterFromEntity(launcherEntity); }
            catch (Exception ex) { AbilityKit.Core.Common.Log.Log.Exception(ex, "[MobaProjectileService] TryRegisterFromEntity failed (launcher)"); }

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

            if (returnAfterFrames > 0)
            {
                // Ensure Tick events exist for transform sync; do not rely on MotionSystem for returning projectiles.
                tickIntervalFrames = 1;
            }

            var collisionMask = CollisionLayer_Unit | CollisionLayer_World;
            var ignore = default(ColliderId);
            if (caster.hasCollisionId) ignore = caster.collisionId.Value;

            var startFrame = frameTime != null ? frameTime.Frame.Value : 0;

            var hitPolicyKind = projectile.HitPolicyKind;
            var hitPolicyParam = 0;
            var hitsRemaining = 1;
            if (hitPolicyKind == ProjectileHitPolicyKind.Pierce)
            {
                hitPolicyParam = projectile.HitsRemaining == -1 ? -1 : (projectile.HitsRemaining > 0 ? projectile.HitsRemaining : 1);
                // Let PierceHitPolicy initialize remaining hits from MaxHits.
                hitsRemaining = 0;
            }

            var baseSpawn = new ProjectileSpawnParams(
                ownerId: casterActorId,
                templateId: projectile.Id,
                launcherActorId: launcherActorId,
                rootActorId: casterActorId,
                spawnFrame: startFrame,
                position: sp,
                direction: d,
                speed: projectile.Speed,
                returnAfterFrames: returnAfterFrames,
                returnSpeed: returnSpeed,
                returnStopDistance: returnStopDistance,
                lifetimeFrames: lifetimeFrames,
                maxDistance: projectile.MaxDistance,
                collisionLayerMask: collisionMask,
                ignoreCollider: ignore,
                hitPolicy: null,
                hitsRemaining: hitsRemaining,
                hitPolicyKind: hitPolicyKind,
                hitPolicyParam: hitPolicyParam,
                tickIntervalFrames: tickIntervalFrames,
                hitFilter: new MobaTeamProjectileHitFilter(_registry),
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

