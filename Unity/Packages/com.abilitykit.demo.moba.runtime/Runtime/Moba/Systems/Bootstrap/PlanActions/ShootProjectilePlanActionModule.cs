using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Core.Common.Log;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Core.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// 发射投射物的Plan Action模块
    /// 使用强类型参数 Schema API
    /// </summary>
    [PlanActionModule(order: 10)]
    public sealed class ShootProjectilePlanActionModule : NamedArgsPlanActionModuleBase<ShootProjectileArgs, IWorldResolver, ShootProjectilePlanActionModule>
    {
        protected override ActionId ActionId => TriggeringConstants.ShootProjectileId;
        protected override IActionSchema<ShootProjectileArgs, IWorldResolver> Schema => ShootProjectileSchema.Instance;

        protected override void Execute(object triggerArgs, ShootProjectileArgs args, ExecCtx<IWorldResolver> ctx)
        {
            var launcherId = args.LauncherId;
            var projectileId = args.ProjectileId;

            if (launcherId <= 0 || projectileId <= 0)
            {
                Log.Warning($"[Plan] shoot_projectile invalid args. launcherId={launcherId} projectileId={projectileId}");
                return;
            }

            if (!ctx.Context.TryResolve<MobaProjectileService>(out var projectileSvc) || projectileSvc == null) return;
            if (!ctx.Context.TryResolve<MobaConfigDatabase>(out var configs) || configs == null) return;

            if (!PlanContextValueResolver.TryGetCasterActorId(triggerArgs, out var casterActorId)) return;
            PlanContextValueResolver.TryGetAim(triggerArgs, out var aimPos, out var aimDir);

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
