using System.Collections.Generic;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
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
                if (!_sys.Links.TryGetActorId(evt.Projectile, out var actorId) || actorId <= 0) continue;
                SyncProjectileActorTransform(actorId, in evt);
            }

            ticks.Clear();
        }

        private void SyncProjectileActorTransform(int actorId, in ProjectileTickEvent evt)
        {
            if (_sys.Registry == null) return;
            if (!_sys.Registry.TryGet(actorId, out var actor) || actor == null) return;

            var newPosition = evt.Position;
            var current = actor.hasTransform ? actor.transform.Value : Transform3.Identity;
            var forward = ResolveForward(current.Position, in newPosition, current.Forward);
            var rotation = Quat.LookRotation(forward, Vec3.Up);
            actor.ReplaceTransform(new Transform3(in newPosition, in rotation, in current.Scale));
        }

        private static Vec3 ResolveForward(in Vec3 previousPosition, in Vec3 newPosition, in Vec3 fallback)
        {
            var delta = newPosition - previousPosition;
            if (delta.SqrMagnitude > 0.0001f) return DeterministicMathBridge.Normalize(in delta);
            if (fallback.SqrMagnitude > 0.0001f) return DeterministicMathBridge.Normalize(in fallback);
            return Vec3.Forward;
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
