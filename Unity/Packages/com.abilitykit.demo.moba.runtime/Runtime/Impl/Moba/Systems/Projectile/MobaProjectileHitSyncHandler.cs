using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Projectile
{
    internal sealed class MobaProjectileHitSyncHandler : IProjectileSyncHandler
    {
        private readonly MobaProjectileSyncSystem _sys;

        public MobaProjectileHitSyncHandler(MobaProjectileSyncSystem sys)
        {
            _sys = sys;
        }

        public void HandleHits(List<ProjectileHitEvent> hits)
        {
            if (hits == null || hits.Count == 0) return;

            HashSet<(int Frame, int ProjectileId, int HitActorId)> hitActorOnce = null;
            if (hits.Count > 1)
            {
                hitActorOnce = new HashSet<(int, int, int)>();
            }
            for (int i = 0; i < hits.Count; i++)
            {
                var evt = hits[i];
                var hitActorId = _sys.ResolveActorIdByCollider(evt.HitCollider);

                if (hitActorOnce != null && hitActorId > 0 && !hitActorOnce.Add((evt.Frame, evt.Projectile.Value, hitActorId)))
                {
                    continue;
                }

                var eventBus = _sys.EventBus;
                if (eventBus != null)
                {
                    var eventId = ProjectileTriggering.Events.Hit;
                    var eid = global::AbilityKit.Ability.Share.Impl.Moba.Services.TriggeringIdUtil.GetEventEid(eventId);

                    eventBus.Publish(new EventKey<ProjectileHitEvent>(eid), in evt);
                    object boxed = evt;
                    eventBus.Publish(new EventKey<object>(eid), in boxed);
                }
            }
        }

        public void HandleSpawns(List<ProjectileSpawnEvent> spawns) { }
        public void HandleTicks(List<ProjectileTickEvent> ticks) { }
        public void HandleExits(List<ProjectileExitEvent> exits) { }
    }
}
