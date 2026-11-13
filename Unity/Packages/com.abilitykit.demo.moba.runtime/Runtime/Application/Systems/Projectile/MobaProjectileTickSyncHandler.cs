using System.Collections.Generic;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;

namespace AbilityKit.Demo.Moba.Runtime.Application.Systems.Projectile
{
    internal sealed class MobaProjectileTickSyncHandler : IProjectileSyncHandler
    {
        private readonly MobaProjectileSyncSystem _sys;

        public MobaProjectileTickSyncHandler(MobaProjectileSyncSystem sys)
        {
            _sys = sys;
        }

        public void HandleTicks(List<ProjectileTickEvent> ticks)
        {
            var count = ticks.Count;
            if (count <= 0) return;

            for (var i = 0; i < count; i++)
            {
                var evt = ticks[i];
                _sys.StageTriggers?.ExecuteProjectileTick(evt);
            }

            ticks.Clear();
        }

        public void HandleSpawns(List<ProjectileSpawnEvent> spawns)
        {
        }

        public void HandleExits(List<ProjectileExitEvent> exits)
        {
        }

        public void HandleHits(List<ProjectileHitEvent> hits)
        {
        }
    }
}
