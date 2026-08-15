using System;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile.Launch;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    [WorldService(typeof(MobaProjectileService))]
    public sealed class MobaProjectileService : IService, IMobaProjectileLaunchExecutor, IMobaProjectileLaunchRuntime
    {
        [WorldInject] private IProjectileService _projectiles = null;
        [WorldInject] private ActorIdAllocator _actorIds = null;
        [WorldInject] private MobaActorRegistry _registry = null;
        [WorldInject] private MobaEntityManager _entities = null;
        [WorldInject] private MobaProjectileLinkService _links = null;
        [WorldInject(required: false)] private MobaActorSpawnSnapshotService _spawnSnapshots = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;
        [WorldInject(required: false)] private MobaTraceRegistry _trace = null;
        [WorldInject(required: false)] private MobaSkillCastRuntimeService _skillRuntimes = null;
        [WorldInject(required: false)] private MobaSkillParamModifierService _skillParamModifiers = null;
        [WorldInject(required: false)] private IMobaActorSpawnService _actorSpawn = null;
        [WorldInject(required: false)] private IContinuousManager _continuous = null;
        [WorldInject(required: false)] private IMobaProjectileEmitterManager _emitters = null;
        [WorldInject(required: false)] private MobaConfigDatabase _configs = null;
        [WorldInject(required: false)] private IMobaContinuousTagTemplateRegistry _tagTemplates = null;
        [WorldInject(required: false)] private IWorldRandom _random = null;
        [WorldInject(required: false)] private IMobaBattleDiagnosticEventSink _eventCollector = null;

        public bool Shoot(int casterActorId, ProjectileEmitterType emitterType, int projectileCode, float speed, int lifetimeFrames, float maxDistance, in Vec3 aimPos, in Vec3 aimDir)
        {
            return Shoot(casterActorId, emitterType, projectileCode, speed, lifetimeFrames, maxDistance, in aimPos, in aimDir, default);
        }

        public bool Shoot(int casterActorId, ProjectileEmitterType emitterType, int projectileCode, float speed, int lifetimeFrames, float maxDistance, in Vec3 aimPos, in Vec3 aimDir, in ProjectileSourceContext sourceContext)
        {
            if (_projectiles == null) return false;
            if (_actorIds == null) return false;
            if (_entities == null) return false;
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
            dir = DeterministicMathBridge.Normalize(in dir);
            if (dir.SqrMagnitude <= 0f) dir = Vec3.Forward;

            if (_actorSpawn == null) return false;

            var projectileActorId = _actorIds.Next();
            var spec = MobaConverter.ToProjectileActorBuildSpec(projectileActorId, projectileCode, caster, in spawnPos, in dir);
            var request = MobaActorSpawnRequest.FromSpec(in spec);
            request.PostSetup = new MobaActorSpawnPostSetup
            {
                SetFlyingProjectileTag = true,
            };

            if (!_actorSpawn.TrySpawn(in request, out var spawnResult) || !spawnResult.Success)
            {
                Log.Warning($"[MobaProjectileService] projectile actor spawn failed. projectileCode={projectileCode} actorId={projectileActorId} casterActorId={casterActorId} error={spawnResult.Error}");
                return false;
            }

            var transaction = new MobaTemporaryEntitySpawnTransaction();
            transaction.Enlist("actor-spawn", () => RollbackSpawnedActor(spawnResult));
            try
            {
                var bullet = spawnResult.Entity;
                if (bullet == null)
                {
                    throw new InvalidOperationException($"Projectile spawn returned no entity. projectileCode={projectileCode} actorId={projectileActorId}");
                }

                var ignore = default(ColliderId);
                if (caster.hasCollisionId) ignore = caster.collisionId.Value;

                var collisionMask = MobaCollisionLayers.UnitMask | MobaCollisionLayers.WorldMask;

                var spawnFrame = GetCurrentFrame();

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

                if (pid.Value <= 0)
                {
                    throw new InvalidOperationException($"Projectile service returned an invalid projectile id. projectileCode={projectileCode} actorId={projectileActorId} projectileId={pid.Value}");
                }

                transaction.Enlist("projectile-runtime", () => _projectiles.Despawn(pid, spawnFrame, ProjectileExitReason.Manual));

                var createdTraceContextId = 0L;
                var acquiredRetain = default(MobaSkillRuntimeRetainHandle);
                if (_links != null)
                {
                    transaction.Enlist("projectile-link", () => _links.RollbackLink(pid, projectileActorId));
                    _links.Link(pid, projectileActorId);

                    transaction.Enlist("projectile-trace", () => EndFailedProjectileTrace(createdTraceContextId));
                    var boundSource = CreateLaunchSource(
                        casterActorId,
                        0,
                        projectileCode,
                        in sourceContext,
                        ref createdTraceContextId);
                    if (boundSource.IsValid)
                    {
                        _links.BindSource(pid, in boundSource);
                    }

                    transaction.Enlist("projectile-skill-retain", () => ReleaseProjectileSkillRuntime(acquiredRetain));
                    RetainProjectileSkillRuntime(pid, projectileCode, out acquiredRetain);
                }

                if (_spawnSnapshots != null)
                {
                    _spawnSnapshots.Enqueue(new MobaActorSpawnSnapshotEntry
                    {
                        NetId = projectileActorId,
                        Kind = (int)SpawnEntityKind.Projectile,
                        Code = projectileCode,
                        OwnerNetId = casterActorId,
                        X = spawnPos.X,
                        Y = spawnPos.Y,
                        Z = spawnPos.Z
                    });
                }

                CollectProjectileSpawned(casterActorId, projectileActorId, projectileCode, in sourceContext);
                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback(ex);
                Log.Exception(ex, $"[MobaProjectileService] projectile post-spawn initialization failed. projectileCode={projectileCode} actorId={projectileActorId} casterActorId={casterActorId}");
                return false;
            }
        }

        private sealed class MobaTeamProjectileHitFilter : IProjectileHitFilter, IProjectileCollisionResponseResolver
        {
            private readonly MobaActorRegistry _registry;

            public MobaTeamProjectileHitFilter(MobaActorRegistry registry)
            {
                _registry = registry;
            }

            public bool ShouldHit(int ownerId, ColliderId collider, int frame)
            {
                return ResolveCollision(ownerId, collider, frame) == ProjectileCollisionResponse.Hit;
            }

            public ProjectileCollisionResponse ResolveCollision(int ownerId, ColliderId collider, int frame)
            {
                if (collider.Value == 0) return ProjectileCollisionResponse.Ignore;
                if (_registry == null) return ProjectileCollisionResponse.Ignore;

                if (!_registry.TryGet(ownerId, out var owner) || owner == null)
                {
                    return ProjectileCollisionResponse.Ignore;
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
                    Log.Exception(ex, "[MobaProjectileService] ResolveCollision resolve collider->entity failed");
                    return ProjectileCollisionResponse.Ignore;
                }

                if (hitEntity == null) return ProjectileCollisionResponse.Ignore;

                // 不命中自身。
                if (hitEntity.hasActorId && hitEntity.actorId.Value == ownerId) return ProjectileCollisionResponse.Ignore;

                var targetTeam = hitEntity.hasTeam ? hitEntity.team.Value : Team.None;

                // 默认策略：阻止同队友伤；允许命中中立或未分队目标。
                if (ownerTeam != Team.None && targetTeam != Team.None && ownerTeam == targetTeam)
                {
                    return ProjectileCollisionResponse.Ignore;
                }

                return ProjectileCollisionResponse.Hit;
            }
        }

        public bool Launch(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, in Vec3 aimPos, in Vec3 aimDir)
        {
            return Launch(casterActorId, launcher, projectile, in aimPos, in aimDir, default);
        }

        public bool Launch(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, in Vec3 aimPos, in Vec3 aimDir, in ProjectileSourceContext sourceContext)
        {
            return Launch(casterActorId, launcher, projectile, launcher?.CountPerShot ?? 1, launcher?.FanAngleDeg ?? 0f, in aimPos, in aimDir, in sourceContext);
        }

        public bool Launch(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, int countPerShot, float fanAngleDeg, in Vec3 aimPos, in Vec3 aimDir, in ProjectileSourceContext sourceContext)
        {
            return Launch(casterActorId, launcher, projectile, countPerShot, fanAngleDeg, launcher?.DurationMs ?? 0, in aimPos, in aimDir, in sourceContext);
        }

        public bool Launch(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, int countPerShot, float fanAngleDeg, int durationMs, in Vec3 aimPos, in Vec3 aimDir, in ProjectileSourceContext sourceContext)
        {
            return Launch(casterActorId, launcher, projectile, countPerShot, fanAngleDeg, durationMs, 0, in aimPos, in aimDir, in sourceContext);
        }

        public bool Launch(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, int countPerShot, float fanAngleDeg, int durationMs, int continuousProcessId, in Vec3 aimPos, in Vec3 aimDir, in ProjectileSourceContext sourceContext)
        {
            return Launch(casterActorId, launcher, projectile, countPerShot, fanAngleDeg, durationMs, continuousProcessId, false, in aimPos, in aimDir, in sourceContext);
        }

        public bool Launch(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, int countPerShot, float fanAngleDeg, int durationMs, int continuousProcessId, bool trackTarget, in Vec3 aimPos, in Vec3 aimDir, in ProjectileSourceContext sourceContext)
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
            dir = DeterministicMathBridge.Normalize(in dir);
            if (dir.SqrMagnitude <= 0f) dir = Vec3.Forward;

            return LaunchFromSpawn(casterActorId, launcher, projectile, countPerShot, fanAngleDeg, durationMs, continuousProcessId, trackTarget, in spawnPos, in dir, in sourceContext);
        }

        public bool LaunchFromSpawn(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, in Vec3 spawnPos, in Vec3 dir)
        {
            return LaunchFromSpawn(casterActorId, launcher, projectile, in spawnPos, in dir, default);
        }

        public bool LaunchFromSpawn(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, in Vec3 spawnPos, in Vec3 dir, in ProjectileSourceContext sourceContext)
        {
            return LaunchFromSpawn(casterActorId, launcher, projectile, launcher?.CountPerShot ?? 1, launcher?.FanAngleDeg ?? 0f, in spawnPos, in dir, in sourceContext);
        }

        public bool LaunchFromSpawn(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, int countPerShot, float fanAngleDeg, in Vec3 spawnPos, in Vec3 dir, in ProjectileSourceContext sourceContext)
        {
            return LaunchFromSpawn(casterActorId, launcher, projectile, countPerShot, fanAngleDeg, launcher?.DurationMs ?? 0, in spawnPos, in dir, in sourceContext);
        }

        public bool LaunchFromSpawn(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, int countPerShot, float fanAngleDeg, int durationMs, in Vec3 spawnPos, in Vec3 dir, in ProjectileSourceContext sourceContext)
        {
            return LaunchFromSpawn(casterActorId, launcher, projectile, countPerShot, fanAngleDeg, durationMs, 0, in spawnPos, in dir, in sourceContext);
        }

        public bool LaunchFromSpawn(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, int countPerShot, float fanAngleDeg, int durationMs, int continuousProcessId, in Vec3 spawnPos, in Vec3 dir, in ProjectileSourceContext sourceContext)
        {
            return LaunchFromSpawn(casterActorId, launcher, projectile, countPerShot, fanAngleDeg, durationMs, continuousProcessId, false, in spawnPos, in dir, in sourceContext);
        }

        public bool LaunchFromSpawn(int casterActorId, ProjectileLauncherMO launcher, ProjectileMO projectile, int countPerShot, float fanAngleDeg, int durationMs, int continuousProcessId, bool trackTarget, in Vec3 spawnPos, in Vec3 dir, in ProjectileSourceContext sourceContext)
        {
            var request = new MobaProjectileLaunchRequest(
                casterActorId,
                launcher,
                projectile,
                countPerShot,
                fanAngleDeg,
                durationMs,
                continuousProcessId,
                trackTarget,
                in spawnPos,
                in dir,
                in sourceContext);
 
            var continuous = new MobaProjectileLaunchContinuous(in request, this, _configs, _tagTemplates);
            if (_continuous != null)
            {
                if (!_continuous.TryActivate(continuous))
                {
                    var rejectReason = (_continuous as DefaultContinuousManager)?.LastRejectReason;
                    var activationError = continuous.Result.Error;
                    Log.Warning($"[MobaProjectileService] projectile launch continuous rejected. casterActorId={casterActorId} launcherId={launcher?.Id ?? 0} projectileId={projectile?.Id ?? 0} rejectReason={rejectReason ?? "<none>"} activationError={activationError ?? "<none>"}");
                    return false;
                }

                return true;
            }

            var startedDirectly = TryStartLaunch(in request, out var directResult);
            if (!startedDirectly || !directResult.Success)
            {
                Log.Warning($"[MobaProjectileService] projectile launch direct start failed. casterActorId={casterActorId} launcherId={launcher?.Id ?? 0} projectileId={projectile?.Id ?? 0} error={directResult.Error ?? "<none>"}");
            }

            return startedDirectly && directResult.Success;
        }

        public bool TryStartLaunch(in MobaProjectileLaunchRequest request, out MobaProjectileLaunchResult result)
        {
            result = default;
            if (_projectiles == null) { result = MobaProjectileLaunchResult.Failed("Projectile service is null"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (_links == null) { result = MobaProjectileLaunchResult.Failed("Projectile link service is null"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (_actorIds == null) { result = MobaProjectileLaunchResult.Failed("Actor id allocator is null"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (_registry == null) { result = MobaProjectileLaunchResult.Failed("Actor registry is null"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (_entities == null) { result = MobaProjectileLaunchResult.Failed("Entity manager is null"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (_actorSpawn == null) { result = MobaProjectileLaunchResult.Failed("Actor spawn service is null"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (request.CasterActorId <= 0) { result = MobaProjectileLaunchResult.Failed("Caster actor id is invalid"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (request.Launcher == null) { result = MobaProjectileLaunchResult.Failed("Launcher config is null"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (request.Projectile == null) { result = MobaProjectileLaunchResult.Failed("Projectile config is null"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (request.CountPerShot <= 0) { result = MobaProjectileLaunchResult.Failed($"Projectile count per shot is invalid. countPerShot={request.CountPerShot}"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (request.FanAngleDeg < 0f) { result = MobaProjectileLaunchResult.Failed($"Projectile fan angle is invalid. fanAngleDeg={request.FanAngleDeg}"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (request.DurationMs < 0) { result = MobaProjectileLaunchResult.Failed($"Projectile duration is invalid. durationMs={request.DurationMs}"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
 
            var casterActorId = request.CasterActorId;
            var launcher = request.Launcher;
            var projectile = request.Projectile;
            if (launcher.CountPerShot <= 0) { result = MobaProjectileLaunchResult.Failed($"Launcher count per shot is invalid. launcherId={launcher.Id} countPerShot={launcher.CountPerShot}"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (launcher.FanAngleDeg < 0f) { result = MobaProjectileLaunchResult.Failed($"Launcher fan angle is invalid. launcherId={launcher.Id} fanAngleDeg={launcher.FanAngleDeg}"); Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}"); return false; }
            if (!_entities.TryGetActorEntity(casterActorId, out var caster) || caster == null || !caster.hasTransform)
            {
                result = MobaProjectileLaunchResult.Failed("Caster entity is missing transform");
                Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}");
                return false;
            }

            var d = request.Direction.SqrMagnitude > 0f ? DeterministicMathBridge.Normalize(request.Direction) : DeterministicMathBridge.Normalize(caster.transform.Value.Forward);
            if (d.SqrMagnitude <= 0f) d = Vec3.Forward;

            var sp = request.SpawnPosition.SqrMagnitude > 0f ? request.SpawnPosition : caster.transform.Value.Position;
            var frameTime = _frameTime;
            if (frameTime == null || frameTime.DeltaTime <= 0f)
            {
                result = MobaProjectileLaunchResult.Failed("Projectile launch requires valid frame time.");
                Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error}");
                return false;
            }

            var nowMs = (long)System.MathF.Round(frameTime.Time * 1000f);
            var durationMs = request.DurationMs;

            var repeatCount = ResolveRepeatCount(durationMs, launcher.IntervalMs, launcher.Id);
            var intervalFrames = repeatCount > 1
                ? ResolveRequiredFramesFromMs(launcher.IntervalMs, frameTime, $"projectile launcher interval. launcherId={launcher.Id}")
                : 0;
            var returnAfterFrames = ResolveOptionalFramesFromMs(projectile.ReturnAfterMs, frameTime);
            var returnSpeed = projectile.ReturnSpeed;
            var returnStopDistance = projectile.ReturnStopDistance;
            var count = repeatCount;

            var bulletsPerShot = request.CountPerShot;
            var requestFanAngleDeg = request.FanAngleDeg;
            var lifetimeFrames = ResolveOptionalFramesFromMs(projectile.LifetimeMs, frameTime);

            var launcherActorId = _actorIds.Next();
            var launcherSpec = MobaConverter.ToProjectileLauncherActorBuildSpec(launcherActorId, launcher.Id, caster, in sp, in d);
            var launcherRequest = MobaActorSpawnRequest.FromSpec(in launcherSpec);

            var hitCooldownFrames = ResolveOptionalFramesFromMs(projectile.HitCooldownMs, frameTime);
            var tickIntervalFrames = ResolveOptionalFramesFromMs(projectile.TickIntervalMs, frameTime);
            var hasStateMachine = !string.IsNullOrWhiteSpace(projectile.StateMachineProfileId);
            if (hasStateMachine)
            {
                tickIntervalFrames = 1;
            }
            if (returnAfterFrames > 0)
            {
                tickIntervalFrames = 1;
            }

            var collisionMask = MobaCollisionLayers.UnitMask | MobaCollisionLayers.WorldMask;
            var ignore = default(ColliderId);
            if (caster.hasCollisionId) ignore = caster.collisionId.Value;

            var startFrame = frameTime != null ? frameTime.Frame.Value : 0;
            var hitPolicyKind = projectile.HitPolicyKind;
            var hitPolicyParam = 0;
            var hitsRemaining = 1;
            if (hitPolicyKind == ProjectileHitPolicyKind.Pierce)
            {
                hitPolicyParam = projectile.HitsRemaining == -1 ? -1 : (projectile.HitsRemaining > 0 ? projectile.HitsRemaining : 1);
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
                hitCooldownFrames: hitCooldownFrames,
                startSuspended: hasStateMachine,
                trackingTargetActorId: request.TrackTarget ? request.SourceContext.InitialTargetActorId : 0,
                collisionHalfExtents: new Vec3(
                    projectile.CollisionWidth * 0.5f,
                    projectile.CollisionHeight * 0.5f,
                    projectile.CollisionLength * 0.5f));

            if (!_actorSpawn.TrySpawn(in launcherRequest, out var launcherSpawnResult) || !launcherSpawnResult.Success)
            {
                var error = $"launcher actor spawn failed. launcherId={launcher.Id} actorId={launcherActorId} casterActorId={casterActorId} error={launcherSpawnResult.Error}";
                Log.Warning($"[MobaProjectileService] {error}");
                result = MobaProjectileLaunchResult.Failed(error);
                return false;
            }

            var transaction = new MobaTemporaryEntitySpawnTransaction();
            transaction.Enlist("launcher-actor-spawn", () => RollbackSpawnedActor(launcherSpawnResult));
            var launcherSource = default(ProjectileSourceContext);
            var launcherTraceContextId = 0L;
            var launcherRetain = default(MobaSkillRuntimeRetainHandle);
            var sequenceResult = default(MobaProjectileLaunchResult);
            try
            {
                var launcherEntity = launcherSpawnResult.Entity;
                if (launcherEntity == null)
                {
                    throw new InvalidOperationException("Launcher actor spawn returned null entity");
                }

                var sourceContext = request.SourceContext;
                transaction.Enlist("launcher-link", () => _links.UnlinkLauncher(launcherActorId));
                transaction.Enlist("launcher-trace", () => EndFailedProjectileTrace(launcherTraceContextId));
                launcherSource = CreateLaunchSource(
                    casterActorId,
                    sourceContext.InitialTargetActorId,
                    projectile.Id,
                    in sourceContext,
                    ref launcherTraceContextId);
                _links.BindLauncherSource(launcherActorId, in launcherSource);
                transaction.Enlist("launcher-skill-retain", () => ReleaseProjectileSkillRuntime(launcherRetain));
                RetainLauncherSkillRuntime(launcherActorId, launcher.Id, in launcherSource, out launcherRetain);

                var endTimeMs = durationMs > 0 ? nowMs + durationMs : nowMs;
                if (!TryCreateLaunchSequence(launcher, out var sequence, out var sequenceError))
                {
                    result = MobaProjectileLaunchResult.Failed(sequenceError);
                    Log.Warning($"[MobaProjectileService] TryStartLaunch rejected. error={result.Error} launcherId={launcher.Id} projectileId={projectile.Id} launcherActorId={launcherActorId}");
                    transaction.Rollback();
                    return false;
                }

                transaction.Enlist("launcher-sequence", () => RollbackLaunchSequence(sequence, sequenceResult));

                var context = new MobaProjectileLaunchContext(
                    in request,
                    launcher,
                    projectile,
                    launcherActorId,
                    launcherEntity,
                    in baseSpawn,
                    in launcherSource,
                    startFrame,
                    endTimeMs,
                    intervalFrames,
                    count,
                    bulletsPerShot,
                    requestFanAngleDeg,
                    _projectiles,
                    _links,
                    _skillParamModifiers,
                    _random,
                    this);

                var started = sequence.TryStart(in context, out sequenceResult);
                result = sequenceResult;
                if (!started || !sequenceResult.Success)
                {
                    Log.Warning($"[MobaProjectileService] TryStartLaunch sequence start failed. launcherId={launcher.Id} projectileId={projectile.Id} launcherActorId={launcherActorId} started={started} error={sequenceResult.Error ?? "<none>"}");
                    transaction.Rollback();
                    return started;
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback(ex);
                result = MobaProjectileLaunchResult.Failed(ex.Message);
                Log.Exception(ex, $"[MobaProjectileService] TryStartLaunch failed. launcherId={launcher.Id} projectileId={projectile.Id} launcherActorId={launcherActorId}");
                return false;
            }
        }

        private void RollbackSpawnedActor(MobaActorSpawnResult spawnResult)
        {
            if (_actorSpawn is IMobaActorSpawnTransactionService spawnTransactions)
            {
                spawnTransactions.Rollback(in spawnResult);
                return;
            }

            global::ActorEntity registered = null;
            try
            {
                new MobaActorSpawnRegistrar(_registry, _entities).Unregister(
                    spawnResult.ActorId,
                    out registered,
                    publishDespawn: false);
            }
            finally
            {
                ActorSpawnPipeline.DestroyBuiltEntity(registered ?? spawnResult.Entity);
            }
        }

        private void EndFailedProjectileTrace(long sourceContextId)
        {
            if (sourceContextId == 0L || _trace == null) return;
            _trace.EndContext(sourceContextId, TraceLifecycleReason.Failed);
        }

        private void ReleaseProjectileSkillRuntime(MobaSkillRuntimeRetainHandle retainHandle)
        {
            if (!retainHandle.IsValid || _skillRuntimes == null) return;
            _skillRuntimes.ReleaseChild(in retainHandle);
        }

        private void RollbackLaunchSequence(
            IMobaProjectileLaunchSequence sequence,
            MobaProjectileLaunchResult result)
        {
            if (result.ScheduleId.Value > 0)
            {
                _projectiles?.CancelSchedule(result.ScheduleId);
            }

            sequence?.Stop(in result, ContinuousEndReason.CleanedUp);
        }

        public bool IsLaunchComplete(in MobaProjectileLaunchResult result)
        {
            return result.Sequence == null || result.Sequence.IsComplete(in result);
        }

        public void StopLaunch(in MobaProjectileLaunchResult result, ContinuousEndReason reason)
        {
            result.Sequence?.Stop(in result, reason);
        }

        public int CurrentFrame => GetCurrentFrame();
        public long NowMs => GetNowMs();

        public bool TryGetLauncherEntity(int launcherActorId, out global::ActorEntity launcherEntity)
        {
            launcherEntity = null;
            return launcherActorId > 0 && _registry != null && _registry.TryGet(launcherActorId, out launcherEntity);
        }

        private bool TryCreateLaunchSequence(ProjectileLauncherMO launcher, out IMobaProjectileLaunchSequence sequence, out string error)
        {
            sequence = null;
            if (_emitters == null)
            {
                error = "Projectile emitter manager is null";
                return false;
            }

            if (!_emitters.TryCreateSequence(launcher, out sequence) || sequence == null)
            {
                error = $"Projectile emitter sequence is not registered. launcherId={launcher?.Id ?? 0} emitterType={launcher?.EmitterType}";
                return false;
            }

            error = null;
            return true;
        }

        private static int ResolveRepeatCount(int durationMs, int intervalMs, int launcherId)
        {
            if (durationMs <= 0) return 1;
            if (intervalMs <= 0)
            {
                throw new InvalidOperationException($"Projectile launcher duration requires a positive interval. launcherId={launcherId} durationMs={durationMs} intervalMs={intervalMs}");
            }

            return System.Math.Max(1, (durationMs / intervalMs) + 1);
        }

        private static int ResolveOptionalFramesFromMs(int milliseconds, IFrameTime frameTime)
        {
            if (milliseconds <= 0) return 0;
            return ResolveRequiredFramesFromMs(milliseconds, frameTime, "projectile optional duration");
        }

        private static int ResolveRequiredFramesFromMs(int milliseconds, IFrameTime frameTime, string field)
        {
            if (milliseconds <= 0)
            {
                throw new InvalidOperationException($"Projectile frame conversion requires a positive duration. field={field} milliseconds={milliseconds}");
            }

            if (frameTime == null || frameTime.DeltaTime <= 0f)
            {
                throw new InvalidOperationException($"Projectile frame conversion requires valid frame time. field={field}");
            }

            return System.Math.Max(1, (int)System.MathF.Round(milliseconds / (frameTime.DeltaTime * 1000f)));
        }

        private int GetCurrentFrame()
        {
            if (_frameTime != null) return _frameTime.Frame.Value;
            throw new InvalidOperationException("MobaProjectileService requires IFrameTime for current frame.");
        }

        private long GetNowMs()
        {
            if (_frameTime != null) return (long)System.MathF.Round(_frameTime.Time * 1000f);
            throw new InvalidOperationException("MobaProjectileService requires IFrameTime for current time.");
        }

        private ProjectileSourceContext CreateLaunchSource(
            int sourceActorId,
            int targetActorId,
            int projectileConfigId,
            in ProjectileSourceContext sourceContext,
            ref long createdTraceContextId)
        {
            createdTraceContextId = 0L;
            var origin = sourceContext.TryGetOrigin(out var sourceOrigin)
                ? sourceOrigin.WithActors(sourceActorId, targetActorId)
                : new MobaGameplayOrigin(
                    sourceActorId,
                    targetActorId,
                    MobaTraceKind.ProjectileLaunch,
                    projectileConfigId,
                    0,
                    0,
                    0,
                    0,
                    default);

            var parentContextId = origin.EffectiveParentContextId;
            var launchContextId = 0L;
            if (_trace != null)
            {
                launchContextId = parentContextId != 0L
                    ? _trace.CreateChildContext(parentContextId, MobaTraceKind.ProjectileLaunch, projectileConfigId, sourceActorId, targetActorId)
                    : _trace.CreateRootContext(MobaTraceKind.ProjectileLaunch, projectileConfigId, sourceActorId, targetActorId);
                createdTraceContextId = launchContextId;
            }

            if (launchContextId == 0L)
            {
                throw new InvalidOperationException($"Projectile launch requires trace context. sourceActorId={sourceActorId} targetActorId={targetActorId} projectileConfigId={projectileConfigId} parentContextId={parentContextId}");
            }

            var rootContextId = origin.EffectiveRootContextId != 0L ? origin.EffectiveRootContextId : launchContextId;
            var ownerContextId = origin.OwnerContextId != 0L ? origin.OwnerContextId : launchContextId;
            origin = MobaGameplayOriginBuilder.Create()
                .FromOrigin(in origin)
                .WithActors(sourceActorId, targetActorId)
                .WithLifecycleNode(MobaTraceKind.ProjectileLaunch, projectileConfigId, launchContextId)
                .WithRootContext(rootContextId)
                .WithOwnerContext(ownerContextId)
                .Build();

            return ProjectileSourceContextBuilder.Create()
                .WithActors(sourceActorId, targetActorId)
                .WithProjectileConfig(projectileConfigId)
                .WithSourceContext(launchContextId)
                .WithRootContext(origin.EffectiveRootContextId)
                .WithOwnerContext(origin.OwnerContextId)
                .WithOrigin(in origin)
                .Build();
        }

        internal bool RetainProjectileSkillRuntime(ProjectileId projectileId, int projectileConfigId)
        {
            return RetainProjectileSkillRuntime(projectileId, projectileConfigId, out _);
        }

        private bool RetainProjectileSkillRuntime(
            ProjectileId projectileId,
            int projectileConfigId,
            out MobaSkillRuntimeRetainHandle acquiredRetain)
        {
            acquiredRetain = default;
            if (_links == null) return false;
            if (_skillRuntimes == null) return false;
            if (!_links.TryGetSource(projectileId, out var source)) return false;
            if (!source.SkillRuntimeHandle.IsValid) return false;
            if (_links.TryGetRetain(projectileId, out acquiredRetain)) return true;

            var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Projectile, projectileId.Value, source.SourceContextId, projectileConfigId);
            var runtimeHandle = source.SkillRuntimeHandle;
            if (_skillRuntimes.RetainChild(in runtimeHandle, in child, out var retainHandle))
            {
                acquiredRetain = retainHandle;
                _links.BindRetain(projectileId, in retainHandle);
                return true;
            }

            return false;
        }

        private bool RetainLauncherSkillRuntime(
            int launcherActorId,
            int launcherConfigId,
            in ProjectileSourceContext source,
            out MobaSkillRuntimeRetainHandle acquiredRetain)
        {
            acquiredRetain = default;
            if (_links == null || _skillRuntimes == null) return false;
            if (!source.SkillRuntimeHandle.IsValid) return false;
            if (_links.TryGetLauncherRetain(launcherActorId, out acquiredRetain)) return true;

            var child = new MobaSkillRuntimeChildRef(
                MobaSkillRuntimeChildKind.ProjectileLauncher,
                launcherActorId,
                source.SourceContextId,
                launcherConfigId);
            var runtimeHandle = source.SkillRuntimeHandle;
            if (!_skillRuntimes.RetainChild(in runtimeHandle, in child, out var retainHandle))
            {
                return false;
            }

            acquiredRetain = retainHandle;
            try
            {
                _links.BindLauncherRetain(launcherActorId, in retainHandle);
                return true;
            }
            catch
            {
                _skillRuntimes.ReleaseChild(in retainHandle);
                acquiredRetain = default;
                throw;
            }
        }

        internal static MobaBattleDiagnosticEventDraft CreateProjectileSpawnedDraft(
            int casterActorId,
            int projectileActorId,
            int projectileConfigId,
            in ProjectileSourceContext sourceContext)
        {
            sourceContext.TryGetOrigin(out var resolvedOrigin);
            var handle = sourceContext.SkillRuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var rootContextId = resolvedOrigin.EffectiveRootContextId != 0L
                ? resolvedOrigin.EffectiveRootContextId
                : sourceContext.RootContextId;
            var contextId = sourceContext.SourceContextId != 0L
                ? sourceContext.SourceContextId
                : resolvedOrigin.ImmediateContextId;
            var summary = $"projectileId={projectileConfigId}, projectileActorId={projectileActorId}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.ProjectileSpawned,
                BattleDiagnosticEventChannel.TemporaryEntity,
                BattleDiagnosticEventOutcome.Succeeded,
                casterActorId,
                sourceContext.InitialTargetActorId,
                projectileConfigId,
                rootContextId,
                contextId,
                runtime,
                summary: summary);
        }

        private void CollectProjectileSpawned(
            int casterActorId,
            int projectileActorId,
            int projectileConfigId,
            in ProjectileSourceContext sourceContext)
        {
            if (_eventCollector == null) return;

            try
            {
                var draft = CreateProjectileSpawnedDraft(casterActorId, projectileActorId, projectileConfigId, in sourceContext);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaProjectileService] diagnostic collect failed (ProjectileSpawned)");
            }
        }

        public void Dispose()
        {
        }
    }
}

