using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Projectile;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Projectile
{
    internal interface IProjectileSyncHandler
    {
        void HandleSpawns(List<ProjectileSpawnEvent> spawns);
        void HandleTicks(List<ProjectileTickEvent> ticks);
        void HandleExits(List<ProjectileExitEvent> exits);
        void HandleHits(List<ProjectileHitEvent> hits);
    }
}
