using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.Projectile;
using AbilityKit.Effect;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Systems.Projectile
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
                    var eid = AbilityKit.Demo.Moba.Services.TriggeringIdUtil.GetEventEid(eventId);

                    eventBus.Publish(new EventKey<ProjectileHitEvent>(eid), in evt);
                    object boxed = evt;
                    eventBus.Publish(new EventKey<object>(eid), in boxed);
                }

                var effects = _sys.Effects;
                var cfgs = _sys.Configs;
                if (effects != null && cfgs != null)
                {
                    try
                    {
                        var proj = cfgs.GetProjectile(evt.TemplateId);
                        var onHitTriggerId = proj != null ? proj.OnHitEffectId : 0;
                        if (onHitTriggerId > 0)
                        {
                            var payload = new ProjectileHitArgs
                            {
                                CasterActorId = evt.OwnerId,
                                TargetActorId = hitActorId,
                                Frame = evt.Frame,
                                ProjectileTemplateId = evt.TemplateId,
                                ProjectileId = evt.Projectile,
                                Point = evt.Point,
                                Normal = evt.Normal,
                                HitCollider = evt.HitCollider,
                                Raw = evt,
                            };

                            effects.ExecuteTriggerId(onHitTriggerId, payload);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Exception(ex, "[MobaProjectileHitSyncHandler] Execute projectile OnHitEffectId failed");
                    }
                }
            }
        }

        public void HandleSpawns(List<ProjectileSpawnEvent> spawns) { }
        public void HandleTicks(List<ProjectileTickEvent> ticks) { }
        public void HandleExits(List<ProjectileExitEvent> exits) { }
    }
}
