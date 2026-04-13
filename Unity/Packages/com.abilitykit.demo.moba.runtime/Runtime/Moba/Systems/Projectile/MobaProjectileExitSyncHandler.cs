using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.Projectile;

namespace AbilityKit.Demo.Moba.Systems.Projectile
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
            if (exits == null || exits.Count == 0) return;
            if (_sys.Links == null || _sys.Registry == null) return;

            for (int i = 0; i < exits.Count; i++)
            {
                var evt = exits[i];
                if (!_sys.Links.TryGetActorId(evt.Projectile, out var actorId) || actorId <= 0) continue;

                _sys.DespawnSnapshots?.Enqueue(actorId, reason: 0);

                if (_sys.Registry.TryGet(actorId, out var e) && e != null)
                {
                    try { e.Destroy(); }
                    catch (System.Exception ex) { Log.Exception(ex, "[MobaProjectileSyncSystem] destroy projectile entity failed"); }
                }

                if (evt.LauncherActorId > 0 && _sys.Registry.TryGet(evt.LauncherActorId, out var launcherEntity) && launcherEntity != null && launcherEntity.hasProjectileLauncher)
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

                _sys.Registry.Unregister(actorId);
                _sys.Links.UnlinkByProjectileId(evt.Projectile);
            }
        }

        public void HandleSpawns(List<ProjectileSpawnEvent> spawns) { }
        public void HandleTicks(List<ProjectileTickEvent> ticks) { }
        public void HandleHits(List<ProjectileHitEvent> hits) { }
    }
}
