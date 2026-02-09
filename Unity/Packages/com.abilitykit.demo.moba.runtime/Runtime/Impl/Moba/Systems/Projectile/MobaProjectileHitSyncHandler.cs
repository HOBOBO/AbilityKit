using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;

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

                if (_sys.EventBus != null)
                {
                    var args = PooledTriggerArgs.Rent();
                    args[EffectTriggering.Args.Source] = evt.OwnerId;
                    args[EffectTriggering.Args.Target] = hitActorId;
                    args[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                    args[EffectTriggering.Args.OriginTarget] = hitActorId;

                    args[ProjectileTriggering.Args.ProjectileId] = evt.Projectile.Value;
                    args[ProjectileTriggering.Args.OwnerId] = evt.OwnerId;
                    args[ProjectileTriggering.Args.TemplateId] = evt.TemplateId;
                    args[ProjectileTriggering.Args.LauncherActorId] = evt.LauncherActorId;
                    args[ProjectileTriggering.Args.RootActorId] = evt.RootActorId;
                    args[ProjectileTriggering.Args.Frame] = evt.Frame;

                    args[ProjectileTriggering.Args.HitCollider] = evt.HitCollider;
                    args[ProjectileTriggering.Args.HitDistance] = evt.Distance;
                    args[ProjectileTriggering.Args.HitPoint] = evt.Point;
                    args[ProjectileTriggering.Args.HitNormal] = evt.Normal;

                    args[ProjectileTriggering.Args.HitCount] = evt.HitCount;

                    _sys.EventBus.Publish(new TriggerEvent(ProjectileTriggering.Events.Hit, payload: evt, args: args));
                }

                // Active trigger execution from projectile config (OnHitEffectId is a triggerId).
                if (_sys.Effects != null && _sys.Configs != null)
                {
                    try
                    {
                        var proj = _sys.Configs.GetProjectile(evt.TemplateId);
                        var triggerId = proj != null ? proj.OnHitEffectId : 0;
                        if (triggerId > 0)
                        {
                            var args2 = PooledTriggerArgs.Rent();
                            args2[EffectTriggering.Args.Source] = evt.OwnerId;
                            args2[EffectTriggering.Args.Target] = hitActorId;
                            args2[EffectTriggering.Args.OriginSource] = evt.OwnerId;
                            args2[EffectTriggering.Args.OriginTarget] = hitActorId;

                            args2[ProjectileTriggering.Args.ProjectileId] = evt.Projectile.Value;
                            args2[ProjectileTriggering.Args.OwnerId] = evt.OwnerId;
                            args2[ProjectileTriggering.Args.TemplateId] = evt.TemplateId;
                            args2[ProjectileTriggering.Args.LauncherActorId] = evt.LauncherActorId;
                            args2[ProjectileTriggering.Args.RootActorId] = evt.RootActorId;
                            args2[ProjectileTriggering.Args.Frame] = evt.Frame;

                            args2[ProjectileTriggering.Args.HitCollider] = evt.HitCollider;
                            args2[ProjectileTriggering.Args.HitDistance] = evt.Distance;
                            args2[ProjectileTriggering.Args.HitPoint] = evt.Point;
                            args2[ProjectileTriggering.Args.HitNormal] = evt.Normal;
                            args2[ProjectileTriggering.Args.HitCount] = evt.HitCount;
                            args2["trigger.id"] = triggerId;

                            _sys.Effects.ExecuteTriggerId(triggerId, source: evt.OwnerId, target: hitActorId, payload: evt, args: args2);
                            args2.Dispose();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Exception(ex, "[MobaProjectileSyncSystem] Execute OnHitEffectId trigger failed.");
                    }
                }
            }
        }

        public void HandleSpawns(List<ProjectileSpawnEvent> spawns) { }
        public void HandleTicks(List<ProjectileTickEvent> ticks) { }
        public void HandleExits(List<ProjectileExitEvent> exits) { }
    }
}
