using System;
using System.Collections.Generic;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;

namespace AbilityKit.Demo.Moba.Runtime.Application.Systems.Projectile
{
    internal sealed class MobaProjectileExitSyncHandler : IProjectileSyncHandler
    {
        private readonly MobaProjectileSyncSystem _sys;

        public MobaProjectileExitSyncHandler(MobaProjectileSyncSystem sys)
        {
            _sys = sys;
        }

        public void HandleExits(List<ProjectileExitEvent> exits)
        {
            var count = exits.Count;
            if (count <= 0) return;

            for (var i = 0; i < count; i++)
            {
                var evt = exits[i];
                _sys.ProjectileSnapshots?.RecordExit(evt);
                _sys.StageTriggers?.ExecuteProjectileExit(evt);
                DecrementLauncherActiveBullets(evt.LauncherActorId);
                if (_sys.Links.TryGetActorId(evt.Projectile, out var actorId) && actorId > 0)
                {
                    RequestProjectileActorDespawn(evt, actorId);
                }
                else
                {
                    CleanupUnlinkedProjectile(evt.Projectile);
                }
            }

            exits.Clear();
        }

        private void RequestProjectileActorDespawn(in ProjectileExitEvent evt, int actorId)
        {
            global::ActorEntity projectileEntity = null;
            if (_sys.Registry != null) _sys.Registry.TryGet(actorId, out projectileEntity);
            if (projectileEntity == null && _sys.Entities != null) _sys.Entities.TryGetActorEntity(actorId, out projectileEntity);
            if (projectileEntity == null) return;

            var sourceActorId = evt.OwnerId;
            var sourceContextId = 0L;
            if (_sys.Links != null && _sys.Links.TryGetSource(evt.Projectile, out var source))
            {
                if (source.SourceActorId > 0) sourceActorId = source.SourceActorId;
                sourceContextId = source.SourceContextId;
            }

            _sys.CleanupProjectileActorOnExit(evt.Projectile, projectileEntity, ActorDespawnReason.ProjectileHitOrExit, sourceActorId, sourceContextId);
        }

        private void CleanupUnlinkedProjectile(ProjectileId projectileId)
        {
            var links = _sys.Links;
            if (links == null) return;

            if (links.TryGetSource(projectileId, out var source)
                && source.SourceContextId != 0L
                && _sys.Trace != null)
            {
                try
                {
                    _sys.Trace.EndContext(
                        source.SourceContextId,
                        AbilityKit.Trace.TraceLifecycleReason.Completed);
                }
                catch (Exception ex)
                {
                    AbilityKit.Core.Logging.Log.Exception(
                        ex,
                        $"[MobaProjectileExitSyncHandler] end unlinked projectile trace failed (projectileId={projectileId.Value}, sourceContextId={source.SourceContextId})");
                }
            }

            if (_sys.SkillRuntimes != null
                && links.TryConsumeRetain(projectileId, out var retainHandle))
            {
                try
                {
                    _sys.SkillRuntimes.ReleaseChild(in retainHandle);
                }
                catch (Exception ex)
                {
                    AbilityKit.Core.Logging.Log.Exception(
                        ex,
                        $"[MobaProjectileExitSyncHandler] release unlinked projectile retain failed (projectileId={projectileId.Value})");
                }
            }

            links.UnlinkByProjectileId(projectileId);
        }

        private void DecrementLauncherActiveBullets(int launcherActorId)
        {
            if (launcherActorId <= 0) return;
            if (_sys.Registry == null || !_sys.Registry.TryGet(launcherActorId, out var launcherEntity) || launcherEntity == null) return;
            if (!launcherEntity.hasProjectileLauncher) return;

            var plc = launcherEntity.projectileLauncher;
            var nextActiveBullets = Math.Max(0, plc.ActiveBullets - 1);
            if (nextActiveBullets == plc.ActiveBullets) return;

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

        public void HandleSpawns(List<ProjectileSpawnEvent> spawns)
        {
        }

        public void HandleTicks(List<ProjectileTickEvent> ticks)
        {
        }

        public void HandleHits(List<ProjectileHitEvent> hits)
        {
        }
    }
}
