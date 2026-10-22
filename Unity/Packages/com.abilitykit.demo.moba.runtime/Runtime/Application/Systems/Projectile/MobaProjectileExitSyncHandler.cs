using System;
using System.Collections.Generic;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Core.Logging;

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
                // Exit triggers may execute remove_projectile and unlink the
                // runtime mapping. Capture the actor/source before dispatching
                // those triggers so the presentation actor is still cleaned up.
                var hadActorLink = _sys.Links.TryGetActorId(evt.Projectile, out var actorId) && actorId > 0;
                var hadSource = _sys.Links.TryGetSource(evt.Projectile, out var source);
                var projectileEntity = hadActorLink ? ResolveProjectileActor(actorId) : null;
                _sys.ProjectileSnapshots?.RecordExit(evt);
                _sys.StageTriggers?.ExecuteProjectileExit(evt);
                DecrementLauncherActiveBullets(evt.LauncherActorId);
                if (hadActorLink)
                {
                    RequestProjectileActorDespawn(evt, actorId, projectileEntity, hadSource ? source : default);
                }
                else
                {
                    CleanupUnlinkedProjectile(evt.Projectile, evt.Frame);
                }
            }

            exits.Clear();
        }

        private global::ActorEntity ResolveProjectileActor(int actorId)
        {
            global::ActorEntity projectileEntity = null;
            if (_sys.Registry != null) _sys.Registry.TryGet(actorId, out projectileEntity);
            if (projectileEntity == null && _sys.Entities != null) _sys.Entities.TryGetActorEntity(actorId, out projectileEntity);
            if (projectileEntity == null && _sys.ActorContext != null)
            {
                var entities = _sys.ActorContext.GetGroup(ActorMatcher.ActorId).GetEntities();
                for (var i = 0; i < entities.Length; i++)
                {
                    var candidate = entities[i];
                    if (candidate != null && candidate.isEnabled && candidate.hasActorId && candidate.actorId.Value == actorId)
                    {
                        projectileEntity = candidate;
                        break;
                    }
                }
            }

            return projectileEntity;
        }

        private void RequestProjectileActorDespawn(in ProjectileExitEvent evt, int actorId, global::ActorEntity projectileEntity, in ProjectileSourceContext capturedSource)
        {
            if (projectileEntity == null)
            {
                return;
            }

            var sourceActorId = evt.OwnerId;
            var sourceContextId = 0L;
            if (capturedSource.IsValid)
            {
                if (capturedSource.SourceActorId > 0) sourceActorId = capturedSource.SourceActorId;
                sourceContextId = capturedSource.SourceContextId;
            }

            _sys.CleanupProjectileActorOnExit(evt.Projectile, projectileEntity, ActorDespawnReason.ProjectileHitOrExit, sourceActorId, sourceContextId);
        }

        private void CleanupUnlinkedProjectile(ProjectileId projectileId, int frame)
        {
            var links = _sys.Links;
            if (links == null) return;

            if (links.TryGetSource(projectileId, out var source)
                && source.SourceContextId != 0L
                && _sys.ExecutionContexts != null)
            {
                try
                {
                    _sys.ExecutionContexts.End(
                        source.SourceContextId,
                        (int)MobaExecutionEndReason.Completed,
                        frame);
                }
                catch (Exception ex)
                {
                    AbilityKit.Core.Logging.Log.Exception(
                        ex,
                        $"[MobaProjectileExitSyncHandler] end unlinked projectile context failed (projectileId={projectileId.Value}, sourceContextId={source.SourceContextId})");
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
