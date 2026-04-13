using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Util.Generator;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.MotionSystem.Core;
using AbilityKit.Core.Common.MotionSystem.Trajectory;
using AbilityKit.Core.Common.Projectile;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Math;
// [REMOVED] using AbilityKit.Demo.Moba.Effects.Runtime;
// [REMOVED] using AbilityKit.Effects.Core;

namespace AbilityKit.Demo.Moba.Systems.Projectile
{
    internal sealed class MobaProjectileSpawnSyncHandler : IProjectileSyncHandler
    {
        private readonly MobaProjectileSyncSystem _sys;

        public MobaProjectileSpawnSyncHandler(MobaProjectileSyncSystem sys)
        {
            _sys = sys;
        }

        public void HandleSpawns(List<ProjectileSpawnEvent> spawns)
        {
            if (spawns == null || spawns.Count == 0) return;
            if (_sys.Links == null || _sys.Registry == null) return;
            if (_sys.ActorIds == null) return;
            if (_sys.Entities == null) return;

            for (int i = 0; i < spawns.Count; i++)
            {
                var evt = spawns[i];

                // MobaProjectileService immediate-shoot path already creates ActorEntity and links it.
                if (_sys.Links.TryGetActorId(evt.Projectile, out var existingActorId) && existingActorId > 0) continue;

                if (!_sys.Entities.TryGetActorEntity(evt.OwnerId, out var caster) || caster == null || !caster.hasTransform)
                {
                    continue;
                }

                var projectileActorId = _sys.ActorIds.Next();

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

                var bullet = ActorArchetypeFactory.Create(_sys.ActorContext, in info);
                if (bullet == null) continue;

                bullet.isFlyingProjectileTag = true;

                // [REMOVED] Effects 包已删除，弹丸效果快照功能待重构
                // if (_sys.EffectRegistry != null)
                // {
                //     try
                //     {
                //         var launcherId = 0;
                //         if (evt.LauncherActorId > 0
                //             && _sys.Registry.TryGet(evt.LauncherActorId, out var launcherEntityForEffect)
                //             && launcherEntityForEffect != null
                //             && launcherEntityForEffect.hasProjectileLauncher)
                //         {
                //             launcherId = launcherEntityForEffect.projectileLauncher.LauncherId;
                //         }
                //
                //         var ctx = MobaEffectQueryContext.CreateForProjectile(
                //             actorId: evt.RootActorId > 0 ? evt.RootActorId : evt.OwnerId,
                //             skillId: 0,
                //             launcherId: launcherId,
                //             projectileId: evt.TemplateId);
                //
                //         var snapshot = MobaEffectSnapshotBuilder.BuildProjectileSnapshot(_sys.EffectRegistry, in ctx);
                //         if (bullet.hasProjectileEffectSnapshot)
                //         {
                //             bullet.ReplaceProjectileEffectSnapshot(snapshot.DamageMul, snapshot.SpeedMul, snapshot.Pierce);
                //         }
                //         else
                //         {
                //             bullet.AddProjectileEffectSnapshot(snapshot.DamageMul, snapshot.SpeedMul, snapshot.Pierce);
                //         }
                //     }
                //     catch (System.Exception ex)
                //     {
                //         Log.Exception(ex, "[MobaProjectileSyncSystem] init projectile effect snapshot failed");
                //     }
                // }

                // Use MotionSystem to drive projectile movement (scheme B simplified).
                // Trajectory duration is derived from projectile config (lifetime/maxDistance/speed).
                try
                {
                    float speed = 0f;
                    float maxDistance = 0f;
                    float lifetimeSec = 0f;
                    var useMotion = true;
                    if (_sys.Configs != null)
                    {
                        var proj = _sys.Configs.GetProjectile(evt.TemplateId);
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
                _sys.Registry.Register(projectileActorId, bullet);

                if (_sys.SpawnSnapshots != null)
                {
                    _sys.SpawnSnapshots.Enqueue(new MobaActorSpawnSnapshotCodec.Entry(
                        netId: projectileActorId,
                        kind: (int)SpawnEntityKind.Projectile,
                        code: evt.TemplateId,
                        ownerNetId: evt.OwnerId,
                        x: evt.Position.X,
                        y: evt.Position.Y,
                        z: evt.Position.Z));
                }

                try { _sys.Entities.TryRegisterFromEntity(bullet); }
                catch (System.Exception ex) { Log.Exception(ex, "[MobaProjectileSyncSystem] TryRegisterFromEntity failed"); }

                _sys.Links.Link(evt.Projectile, projectileActorId);

                if (evt.LauncherActorId > 0 && _sys.Registry.TryGet(evt.LauncherActorId, out var launcherEntity) && launcherEntity != null && launcherEntity.hasProjectileLauncher)
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
        }

        public void HandleTicks(List<ProjectileTickEvent> ticks) { }
        public void HandleExits(List<ProjectileExitEvent> exits) { }
        public void HandleHits(List<ProjectileHitEvent> hits) { }
    }
}
