using System;
using System.Collections.Generic;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Runtime.Application.Systems.Projectile
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
            var count = spawns.Count;
            if (count <= 0) return;

            for (var i = 0; i < count; i++)
            {
                var evt = spawns[i];
                BindProjectileSource(evt);
                EnsureProjectileActor(evt);
                IncrementLauncherActiveBullets(evt.LauncherActorId);
                _sys.ProjectileSnapshots?.RecordSpawn(evt);
                _sys.StageTriggers?.ExecuteProjectileSpawn(evt);
            }

            spawns.Clear();
        }

        public void HandleTicks(List<ProjectileTickEvent> ticks)
        {
        }

        public void HandleExits(List<ProjectileExitEvent> exits)
        {
        }

        public void HandleHits(List<ProjectileHitEvent> hits)
        {
        }

        private void BindProjectileSource(in ProjectileSpawnEvent evt)
        {
            var links = _sys.Links;
            if (links == null) return;

            if (links.TryGetSource(evt.Projectile, out _))
            {
                return;
            }

            if (evt.LauncherActorId <= 0
                || !links.TryGetLauncherSource(evt.LauncherActorId, out var launcherSource))
            {
                return;
            }

            var trace = _sys.Trace;
            if (trace == null)
            {
                throw new InvalidOperationException(
                    $"Scheduled projectile requires trace service. projectile={evt.Projectile} templateId={evt.TemplateId} launcherActorId={evt.LauncherActorId}");
            }

            var sourceActorId = launcherSource.SourceActorId > 0
                ? launcherSource.SourceActorId
                : evt.OwnerId;
            var targetActorId = launcherSource.InitialTargetActorId;
            var projectileContextId = trace.CreateChildContext(
                launcherSource.SourceContextId,
                MobaTraceKind.ProjectileLaunch,
                evt.TemplateId,
                sourceActorId,
                targetActorId);
            if (projectileContextId == 0L)
            {
                throw new InvalidOperationException(
                    $"Scheduled projectile trace creation failed. projectile={evt.Projectile} templateId={evt.TemplateId} launcherActorId={evt.LauncherActorId} launcherContextId={launcherSource.SourceContextId}");
            }

            var projectileSource = launcherSource.WithLaunchContext(projectileContextId);
            links.BindSource(evt.Projectile, in projectileSource);
        }

        private void EnsureProjectileActor(in ProjectileSpawnEvent evt)
        {
            var links = _sys.Links;
            if (links == null) return;
            if (links.TryGetActorId(evt.Projectile, out var existingActorId) && existingActorId > 0) return;

            if (_sys.ActorSpawn == null || _sys.ActorIds == null || _sys.Registry == null)
            {
                Log.Warning($"[MobaProjectileSpawnSyncHandler] cannot create scheduled projectile actor because required services are missing. projectile={evt.Projectile} templateId={evt.TemplateId} launcherActorId={evt.LauncherActorId} rootActorId={evt.RootActorId}");
                return;
            }

            var ownerActorId = evt.RootActorId > 0 ? evt.RootActorId : evt.OwnerId;
            if (ownerActorId <= 0) ownerActorId = evt.LauncherActorId;
            if (ownerActorId <= 0 || !_sys.Registry.TryGet(ownerActorId, out var ownerEntity) || ownerEntity == null)
            {
                Log.Warning($"[MobaProjectileSpawnSyncHandler] cannot create scheduled projectile actor because owner actor is missing. projectile={evt.Projectile} templateId={evt.TemplateId} ownerActorId={ownerActorId} launcherActorId={evt.LauncherActorId} rootActorId={evt.RootActorId}");
                return;
            }

            var projectileActorId = _sys.ActorIds.Next();
            var spec = MobaConverter.ToProjectileActorBuildSpec(projectileActorId, evt.TemplateId, ownerEntity, evt.Position, evt.Direction);
            var request = MobaActorSpawnRequest.FromSpec(in spec);
            request.PostSetup = new MobaActorSpawnPostSetup
            {
                SetFlyingProjectileTag = true,
            };

            if (!_sys.ActorSpawn.TrySpawn(in request, out var spawnResult) || !spawnResult.Success)
            {
                Log.Warning($"[MobaProjectileSpawnSyncHandler] scheduled projectile actor spawn failed. projectile={evt.Projectile} templateId={evt.TemplateId} actorId={projectileActorId} ownerActorId={ownerActorId} error={spawnResult.Error}");
                return;
            }

            _sys.SpawnSnapshots?.Enqueue(new MobaActorSpawnSnapshotEntry
            {
                NetId = projectileActorId,
                Kind = (int)SpawnEntityKind.Projectile,
                Code = evt.TemplateId,
                OwnerNetId = ownerActorId,
                X = evt.Position.X,
                Y = evt.Position.Y,
                Z = evt.Position.Z
            });

            links.Link(evt.Projectile, projectileActorId);
            AttachConfiguredStateMachine(evt.TemplateId, spawnResult.Entity);
        }

        private void AttachConfiguredStateMachine(int templateId, global::ActorEntity actor)
        {
            if (actor == null || _sys.Configs == null) return;
            if (!_sys.Configs.TryGetProjectile(templateId, out ProjectileMO config) || config == null) return;
            if (string.IsNullOrWhiteSpace(config.StateMachineProfileId)) return;

            var factory = _sys.StateMachineFactory
                          ?? throw new InvalidOperationException(
                              $"Projectile '{templateId}' requires HFSM profile '{config.StateMachineProfileId}', but the state-machine factory is unavailable.");
            if (!factory.TryCreate(actor, config.StateMachineProfileId, out var runtime) || runtime == null)
            {
                throw new InvalidOperationException(
                    $"Projectile '{templateId}' references missing HFSM profile '{config.StateMachineProfileId}'.");
            }

            actor.AddActorStateMachine(config.StateMachineProfileId, runtime);
        }

        private void IncrementLauncherActiveBullets(int launcherActorId)
        {
            if (launcherActorId <= 0) return;
            if (_sys.Registry == null || !_sys.Registry.TryGet(launcherActorId, out var launcherEntity) || launcherEntity == null) return;
            if (!launcherEntity.hasProjectileLauncher) return;

            var plc = launcherEntity.projectileLauncher;
            var nextActiveBullets = Math.Max(0, plc.ActiveBullets) + 1;
            launcherEntity.ReplaceProjectileLauncher(
                newLauncherId: plc.LauncherId,
                newProjectileId: plc.ProjectileId,
                newRootActorId: plc.RootActorId,
                newEndTimeMs: plc.EndTimeMs,
                newActiveBullets: nextActiveBullets,
                newScheduleId: plc.ScheduleId,
                newIntervalFrames: plc.IntervalFrames,
                newTotalCount: plc.TotalCount);
        }
    }
}
