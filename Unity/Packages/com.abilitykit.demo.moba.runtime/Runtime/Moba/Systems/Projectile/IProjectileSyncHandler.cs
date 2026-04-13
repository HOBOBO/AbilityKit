using System.Collections.Generic;
using AbilityKit.Core.Common.Projectile;

namespace AbilityKit.Demo.Moba.Systems.Projectile
{
    internal interface IProjectileSyncHandler
    {
        void HandleSpawns(List<ProjectileSpawnEvent> spawns);
        void HandleTicks(List<ProjectileTickEvent> ticks);
        void HandleExits(List<ProjectileExitEvent> exits);
        void HandleHits(List<ProjectileHitEvent> hits);
    }
}
