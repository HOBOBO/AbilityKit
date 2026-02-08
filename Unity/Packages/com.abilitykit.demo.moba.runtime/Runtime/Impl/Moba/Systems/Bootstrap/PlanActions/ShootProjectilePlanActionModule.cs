using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    [PlanActionModule(order: 10)]
    public sealed class ShootProjectilePlanActionModule : IPlanActionModule
    {
        public void Register(ActionRegistry actions, IWorldResolver services)
        {
            if (actions == null) return;

            var shootProjectileId = new ActionId(AbilityKit.Triggering.Eventing.StableStringId.Get("action:shoot_projectile"));
            actions.Register<PlannedTrigger<object, IWorldResolver>.Action2>(
                shootProjectileId,
                static (args, a0, a1, ctx) =>
                {
                    try
                    {
                        if (ctx.Context == null) return;

                        var launcherId = (int)a0;
                        var projectileId = (int)a1;
                        if (launcherId <= 0 || projectileId <= 0) return;

                        if (!ctx.Context.TryResolve<MobaProjectileService>(out var projectileSvc) || projectileSvc == null) return;
                        if (!ctx.Context.TryResolve<MobaConfigDatabase>(out var configs) || configs == null) return;

                        var casterActorId = 0;
                        var aimPos = Vec3.Zero;
                        var aimDir = Vec3.Zero;
                        if (args is SkillCastContext scc)
                        {
                            casterActorId = scc.CasterActorId;
                            aimPos = scc.AimPos;
                            aimDir = scc.AimDir;
                        }

                        if (casterActorId <= 0) return;

                        ProjectileLauncherMO launcher = null;
                        ProjectileMO projectile = null;
                        if (launcherId > 0) configs.TryGetProjectileLauncher(launcherId, out launcher);
                        if (projectileId > 0) configs.TryGetProjectile(projectileId, out projectile);
                        if (launcher == null || projectile == null) return;

                        var casterPos = Vec3.Zero;
                        if (ctx.Context.TryResolve<MobaActorRegistry>(out var actorRegistry)
                            && actorRegistry != null
                            && actorRegistry.TryGet(casterActorId, out var casterEntity)
                            && casterEntity != null
                            && casterEntity.hasTransform)
                        {
                            casterPos = casterEntity.transform.Value.Position;
                        }

                        if (!aimPos.Equals(Vec3.Zero)) aimPos = casterPos + aimPos;
                        if (!aimDir.Equals(Vec3.Zero)) aimDir = aimDir.Normalized;

                        if (!projectileSvc.Launch(casterActorId, launcher, projectile, in aimPos, in aimDir))
                        {
                            Log.Warning($"[Plan] shoot_projectile launch failed. launcherId={launcherId} projectileId={projectileId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[Plan] shoot_projectile executed failed");
                    }
                },
                isDeterministic: true);
        }
    }
}
