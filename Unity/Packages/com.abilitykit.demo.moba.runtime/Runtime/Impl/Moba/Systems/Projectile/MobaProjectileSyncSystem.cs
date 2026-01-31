using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Common.MotionSystem.Trajectory;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Projectile
{
    [WorldSystem(order: MobaSystemOrder.ProjectileSync, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaProjectileSyncSystem : WorldSystemBase
    {
        private IProjectileService _projectiles;
        private MobaProjectileLinkService _links;
        private MobaActorRegistry _registry;
        private IEventBus _eventBus;
        private MobaEffectExecutionService _effects;
        private ActorIdAllocator _actorIds;
        private MobaEntityManager _entities;
        private MobaActorSpawnSnapshotService _spawnSnapshots;
        private MobaConfigDatabase _configs;
        private MobaActorDespawnSnapshotService _despawnSnapshots;

        private readonly List<ProjectileSpawnEvent> _spawns = new List<ProjectileSpawnEvent>(64);
        private readonly List<ProjectileHitEvent> _hits = new List<ProjectileHitEvent>(128);
        private readonly List<ProjectileTickEvent> _ticks = new List<ProjectileTickEvent>(128);
        private readonly List<ProjectileExitEvent> _exits = new List<ProjectileExitEvent>(64);

        public MobaProjectileSyncSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _projectiles);
            Services.TryResolve(out _links);
            Services.TryResolve(out _registry);
            Services.TryResolve(out _eventBus);
            Services.TryResolve(out _effects);
            Services.TryResolve(out _actorIds);
            Services.TryResolve(out _entities);
            Services.TryResolve(out _spawnSnapshots);
            Services.TryResolve(out _configs);
            Services.TryResolve(out _despawnSnapshots);
        }

        protected override void OnExecute()
        {
            if (_projectiles == null || _links == null || _registry == null) return;

            _spawns.Clear();
            _projectiles.DrainSpawnEvents(_spawns);
            for (int i = 0; i < _spawns.Count; i++)
            {
                var evt = _spawns[i];

                // MobaProjectileService immediate-shoot path already creates ActorEntity and links it.
                if (_links.TryGetActorId(evt.Projectile, out var existingActorId) && existingActorId > 0) continue;
                if (_actorIds == null) continue;
                if (_entities == null) continue;

                if (!_entities.TryGetActorEntity(evt.OwnerId, out var caster) || caster == null || !caster.hasTransform)
                {
                    continue;
                }

                var projectileActorId = _actorIds.Next();

                var dir = evt.Direction;
                if (dir.SqrMagnitude <= 0f) dir = Vec3.Forward;
                dir = dir.Normalized;

                var rot = Quat.LookRotation(dir, Vec3.Up);
                var t = new Transform3(evt.Position, rot, Vec3.One);

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
                    templateId: evt.TemplateId);

                var bullet = MobaEntitySpawnFactory.Create(Contexts.Actor(), in info);
                if (bullet == null) continue;

                bullet.isFlyingProjectileTag = true;

                // Use MotionSystem to drive projectile movement (scheme B simplified).
                // Trajectory duration is derived from projectile config (lifetime/maxDistance/speed).
                try
                {
                    float speed = 0f;
                    float maxDistance = 0f;
                    float lifetimeSec = 0f;
                    var useMotion = true;
                    if (_configs != null)
                    {
                        var proj = _configs.GetProjectile(evt.TemplateId);
                        if (proj != null)
                        {
                            speed = proj.Speed;
                            maxDistance = proj.MaxDistance;
                            lifetimeSec = proj.LifetimeMs > 0 ? proj.LifetimeMs / 1000f : 0f;
                            // Returning projectiles are driven by server projectile tick; do not attach MotionSystem trajectory.
                            if (proj.ReturnAfterMs > 0) useMotion = false;
                        }
                    }

                    if (!useMotion)
                    {
                        goto SkipMotion;
                    }

                    var start = evt.Position;
                    var fwd = dir;

                    var distByLifetime = (speed > 0f && lifetimeSec > 0f) ? speed * lifetimeSec : 0f;
                    var dist = maxDistance > 0f ? maxDistance : distByLifetime;
                    if (dist <= 0f) dist = distByLifetime > 0f ? distByLifetime : 0.001f;

                    var duration = lifetimeSec > 0f ? lifetimeSec : (speed > 0f ? dist / speed : 0.001f);
                    if (duration <= 0f) duration = 0.001f;

                    var end = start + fwd * dist;
                    var traj = new LinearTrajectory3D(start, end, duration);
                    var source = new TrajectoryMotionSource(traj, priority: 10);

                    // Create Motion component and attach trajectory source.
                    var pipeline = new MotionPipeline();
                    pipeline.AddSource(source);

                    var state = new MotionState(start) { Forward = fwd };
                    var output = new MotionOutput();
                    output.Clear();

                    bullet.AddMotion(
                        newPipeline: pipeline,
                        newState: state,
                        newOutput: output,
                        newSolver: null,
                        newPolicy: null,
                        newEvents: null,
                        newInitialized: false);
                }
                catch (System.Exception ex)
                {
                    Log.Exception(ex, "[MobaProjectileSyncSystem] init projectile motion failed");
                }

            SkipMotion:
                _registry.Register(projectileActorId, bullet);

                if (_spawnSnapshots != null)
                {
                    _spawnSnapshots.Enqueue(new MobaActorSpawnSnapshotCodec.Entry(
                        netId: projectileActorId,
                        kind: (int)SpawnEntityKind.Projectile,
                        code: evt.TemplateId,
                        ownerNetId: evt.OwnerId,
                        x: evt.Position.X,
                        y: evt.Position.Y,
                        z: evt.Position.Z));
                }

                try { _entities.TryRegisterFromEntity(bullet); }
                catch (System.Exception ex) { Log.Exception(ex, "[MobaProjectileSyncSystem] TryRegisterFromEntity failed"); }

                _links.Link(evt.Projectile, projectileActorId);

                if (evt.LauncherActorId > 0 && _registry.TryGet(evt.LauncherActorId, out var launcherEntity) && launcherEntity != null && launcherEntity.hasProjectileLauncher)
                {
                    var plc = launcherEntity.projectileLauncher;
                    launcherEntity.ReplaceProjectileLauncher(
                        newLauncherId: plc.LauncherId,
                        newProjectileId: plc.ProjectileId,
                        newRootActorId: plc.RootActorId,
                        newEndTimeMs: plc.EndTimeMs,
                        newActiveBullets: plc.ActiveBullets + 1,
                        newScheduleId: plc.ScheduleId,
                        newIntervalFrames: plc.IntervalFrames,
                        newTotalCount: plc.TotalCount);
                }
            }

            _ticks.Clear();
            _projectiles.DrainTickEvents(_ticks);
            for (int i = 0; i < _ticks.Count; i++)
            {
                var evt = _ticks[i];
                if (!_links.TryGetActorId(evt.Projectile, out var actorId) || actorId <= 0) continue;
                if (!_registry.TryGet(actorId, out var e) || e == null) continue;
                if (!e.hasTransform) continue;

                // Movement is driven by MotionSystem for bullets; do not override it.
                if (e.hasMotion) continue;

                var t = e.transform.Value;
                var nt = new Transform3(evt.Position, t.Rotation, t.Scale);
                e.ReplaceTransform(nt);
            }

            _exits.Clear();
            _projectiles.DrainExitEvents(_exits);
            for (int i = 0; i < _exits.Count; i++)
            {
                var evt = _exits[i];
                if (!_links.TryGetActorId(evt.Projectile, out var actorId) || actorId <= 0) continue;

                _despawnSnapshots?.Enqueue(actorId, reason: 0);

                if (_registry.TryGet(actorId, out var e) && e != null)
                {
                    try { e.Destroy(); }
                    catch (System.Exception ex) { Log.Exception(ex, "[MobaProjectileSyncSystem] destroy projectile entity failed"); }
                }

                if (evt.LauncherActorId > 0 && _registry.TryGet(evt.LauncherActorId, out var launcherEntity) && launcherEntity != null && launcherEntity.hasProjectileLauncher)
                {
                    var plc = launcherEntity.projectileLauncher;
                    var next = plc.ActiveBullets - 1;
                    if (next < 0) next = 0;
                    launcherEntity.ReplaceProjectileLauncher(
                        newLauncherId: plc.LauncherId,
                        newProjectileId: plc.ProjectileId,
                        newRootActorId: plc.RootActorId,
                        newEndTimeMs: plc.EndTimeMs,
                        newActiveBullets: next,
                        newScheduleId: plc.ScheduleId,
                        newIntervalFrames: plc.IntervalFrames,
                        newTotalCount: plc.TotalCount);
                }

                _registry.Unregister(actorId);
                _links.UnlinkByProjectileId(evt.Projectile);
            }

            _hits.Clear();
            _projectiles.DrainHitEvents(_hits);
            HashSet<(int Frame, int ProjectileId, int HitActorId)> hitActorOnce = null;
            if (_hits.Count > 1)
            {
                hitActorOnce = new HashSet<(int, int, int)>();
            }
            for (int i = 0; i < _hits.Count; i++)
            {
                var evt = _hits[i];
                var hitActorId = ResolveActorIdByCollider(evt.HitCollider);

                if (hitActorOnce != null && hitActorId > 0 && !hitActorOnce.Add((evt.Frame, evt.Projectile.Value, hitActorId)))
                {
                    continue;
                }

                if (_eventBus != null)
                {
                    var args = PooledTriggerArgs.Rent();
                    args[EffectTriggering.Args.Source] = evt.OwnerId;
                    args[EffectTriggering.Args.Target] = hitActorId;
                    args[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                    args[EffectTriggering.Args.OriginTarget] = hitActorId;

                    args[ProjectileTriggering.Args.ProjectileId] = evt.Projectile.Value;
                    args[ProjectileTriggering.Args.OwnerId] = evt.OwnerId;
                    args[ProjectileTriggering.Args.TemplateId] = evt.TemplateId;
                    args[ProjectileTriggering.Args.LauncherActorId] = evt.LauncherActorId;
                    args[ProjectileTriggering.Args.RootActorId] = evt.RootActorId;
                    args[ProjectileTriggering.Args.Frame] = evt.Frame;

                    args[ProjectileTriggering.Args.HitCollider] = evt.HitCollider;
                    args[ProjectileTriggering.Args.HitDistance] = evt.Distance;
                    args[ProjectileTriggering.Args.HitPoint] = evt.Point;
                    args[ProjectileTriggering.Args.HitNormal] = evt.Normal;

                    args[ProjectileTriggering.Args.HitCount] = evt.HitCount;

                    _eventBus.Publish(new TriggerEvent(ProjectileTriggering.Events.Hit, payload: evt, args: args));
                }

                // Active trigger execution from projectile config (OnHitEffectId is a triggerId).
                if (_effects != null && _configs != null)
                {
                    try
                    {
                        var proj = _configs.GetProjectile(evt.TemplateId);
                        var triggerId = proj != null ? proj.OnHitEffectId : 0;
                        if (triggerId > 0)
                        {
                            var args2 = PooledTriggerArgs.Rent();
                            args2[EffectTriggering.Args.Source] = evt.OwnerId;
                            args2[EffectTriggering.Args.Target] = hitActorId;
                            args2[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                            args2[EffectTriggering.Args.OriginTarget] = hitActorId;

                            args2[ProjectileTriggering.Args.ProjectileId] = evt.Projectile.Value;
                            args2[ProjectileTriggering.Args.OwnerId] = evt.OwnerId;
                            args2[ProjectileTriggering.Args.TemplateId] = evt.TemplateId;
                            args2[ProjectileTriggering.Args.LauncherActorId] = evt.LauncherActorId;
                            args2[ProjectileTriggering.Args.RootActorId] = evt.RootActorId;
                            args2[ProjectileTriggering.Args.Frame] = evt.Frame;

                            args2[ProjectileTriggering.Args.HitCollider] = evt.HitCollider;
                            args2[ProjectileTriggering.Args.HitDistance] = evt.Distance;
                            args2[ProjectileTriggering.Args.HitPoint] = evt.Point;
                            args2[ProjectileTriggering.Args.HitNormal] = evt.Normal;
                            args2[ProjectileTriggering.Args.HitCount] = evt.HitCount;
                            args2["trigger.id"] = triggerId;

                            _effects.ExecuteTriggerId(triggerId, source: evt.OwnerId, target: hitActorId, payload: evt, args: args2);
                            args2.Dispose();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Exception(ex, "[MobaProjectileSyncSystem] Execute OnHitEffectId trigger failed.");
                    }
                }
            }
        }

        private int ResolveActorIdByCollider(ColliderId id)
        {
            if (_registry == null) return 0;
            if (id.Value <= 0) return 0;

            try
            {
                foreach (var kv in _registry.Entries)
                {
                    var e = kv.Value;
                    if (e == null || !e.hasActorId || !e.hasCollisionId) continue;
                    if (e.collisionId.Value.Equals(id))
                    {
                        return e.actorId.Value;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, "[MobaProjectileSyncSystem] ResolveActorIdByCollider failed");
            }

            return 0;
        }
    }
}

