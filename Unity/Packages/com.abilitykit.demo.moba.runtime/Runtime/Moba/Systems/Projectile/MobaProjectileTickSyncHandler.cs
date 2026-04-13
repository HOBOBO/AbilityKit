using System.Collections.Generic;
using AbilityKit.Core.Common.Projectile;
using AbilityKit.Core.Math;

namespace AbilityKit.Demo.Moba.Systems.Projectile
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
            if (ticks == null || ticks.Count == 0) return;
            if (_sys.Links == null || _sys.Registry == null) return;

            for (int i = 0; i < ticks.Count; i++)
            {
                var evt = ticks[i];
                if (!_sys.Links.TryGetActorId(evt.Projectile, out var actorId) || actorId <= 0) continue;
                if (!_sys.Registry.TryGet(actorId, out var e) || e == null) continue;
                if (!e.hasTransform) continue;

                // Movement is driven by MotionSystem for bullets; do not override it.
                if (e.hasMotion) continue;

                var t = e.transform.Value;
                var nt = new Transform3(evt.Position, t.Rotation, t.Scale);
                e.ReplaceTransform(nt);
            }
        }

        public void HandleSpawns(List<ProjectileSpawnEvent> spawns) { }
        public void HandleExits(List<ProjectileExitEvent> exits) { }
        public void HandleHits(List<ProjectileHitEvent> hits) { }
    }
}
