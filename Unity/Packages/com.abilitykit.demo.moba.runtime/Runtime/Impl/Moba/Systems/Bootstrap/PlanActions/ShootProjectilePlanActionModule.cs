using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    [PlanActionModule(order: 10)]
    public sealed class ShootProjectilePlanActionModule : PlanActionModuleBase
    {
        protected override string ActionName => "shoot_projectile";
        protected override bool HasAction2 => true;

        protected override void Execute2(object args, double a0, double a1, ExecCtx<IWorldResolver> ctx)
        {
            if (!PlanActionRegisterUtil.TryToIntId(a0, out var launcherId, logScope: "Plan")
                || !PlanActionRegisterUtil.TryToIntId(a1, out var projectileId, logScope: "Plan"))
            {
                Log.Warning($"[Plan] shoot_projectile invalid args. launcherIdRaw={a0} projectileIdRaw={a1}");
                return;
            }
            if (launcherId <= 0 || projectileId <= 0) return;

            if (!ctx.Context.TryResolve<MobaProjectileService>(out var projectileSvc) || projectileSvc == null) return;
            if (!ctx.Context.TryResolve<MobaConfigDatabase>(out var configs) || configs == null) return;

            if (!PlanContextValueResolver.TryGetCasterActorId(args, out var casterActorId)) return;
            PlanContextValueResolver.TryGetAim(args, out var aimPos, out var aimDir);

            ProjectileLauncherMO launcher = null;
            ProjectileMO projectile = null;
            if (!configs.TryGetProjectileLauncher(launcherId, out launcher)) return;
            if (!configs.TryGetProjectile(projectileId, out projectile)) return;
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

            if (!aimPos.Equals(Vec3.Zero))
            {
                var sqr = aimPos.SqrMagnitude;
                if (sqr > 1000f * 1000f)
                {
                    Log.Warning($"[Plan] shoot_projectile aimPos looks like world-space (will be treated as offset). casterActorId={casterActorId} aimPos={aimPos}");
                }
            }

            if (!aimPos.Equals(Vec3.Zero)) aimPos = casterPos + aimPos;
            if (!aimDir.Equals(Vec3.Zero)) aimDir = aimDir.Normalized;

            if (!projectileSvc.Launch(casterActorId, launcher, projectile, in aimPos, in aimDir))
            {
                Log.Warning($"[Plan] shoot_projectile launch failed. launcherId={launcherId} projectileId={projectileId}");
            }
        }
    }
}
