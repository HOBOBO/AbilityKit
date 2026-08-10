using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 发射投射物的 Plan Action 模块，使用强类型参数 Schema API。
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.ShootProjectile)]
    public sealed class ShootProjectilePlanActionModule : MobaPlanActionModuleBase<ShootProjectileArgs, ShootProjectilePlanActionModule>
    {
        protected override IActionSchema<ShootProjectileArgs, IWorldResolver> Schema => ShootProjectileSchema.Instance;

        protected override void Execute(object triggerArgs, ShootProjectileArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaProjectileService>(out var projectileSvc) || projectileSvc == null)
            {
                LogRejected(ctx, "cannot resolve MobaProjectileService.");
                return;
            }

            if (!ctx.Context.TryResolve<MobaConfigDatabase>(out var configs) || configs == null)
            {
                LogRejected(ctx, "cannot resolve MobaConfigDatabase.");
                return;
            }

            if (!MobaPlanActionInputResolver.TryResolve(
                    triggerArgs,
                    ctx,
                    out var coreInput))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            var input = MobaPlanActionInputAssembler.AssembleProjectile(in coreInput);
            if (!input.HasCasterActor)
            {
                LogRejected(ctx, "caster actor not found");
                return;
            }

            var casterActorId = input.CasterActorId;
            var launcherId = args.LauncherId;
            var projectileId = args.ProjectileId;
            var paramResolver = ctx.Context.TryResolve<MobaSkillParamModifierService>(out var resolvedParamResolver)
                ? resolvedParamResolver
                : null;

            if (paramResolver != null)
            {
                launcherId = paramResolver.Projectile.ResolveLauncherId(casterActorId, launcherId);
                projectileId = paramResolver.Projectile.ResolveProjectileId(casterActorId, projectileId);
            }

            if (launcherId <= 0 || projectileId <= 0)
            {
                LogRejected(ctx, $"invalid args. launcherId={launcherId} projectileId={projectileId}");
                return;
            }

            var aimPos = input.HasAimPosition ? input.AimPosition : Vec3.Zero;
            var aimDir = input.HasAimDirection ? input.AimDirection : Vec3.Zero;
            var targetActorId = 0;

            ProjectileLauncherMO launcher = null;
            ProjectileMO projectile = null;
            if (!configs.TryGetProjectileLauncher(launcherId, out launcher) || launcher == null)
            {
                LogRejected(ctx, $"launcher config not found. launcherId={launcherId}");
                return;
            }

            if (!configs.TryGetProjectile(projectileId, out projectile) || projectile == null)
            {
                LogRejected(ctx, $"projectile config not found. projectileId={projectileId}");
                return;
            }

            var countPerShot = paramResolver != null
                ? paramResolver.Projectile.ResolveCountPerShot(casterActorId, launcher.CountPerShot)
                : launcher.CountPerShot;
            var fanAngleDeg = paramResolver != null
                ? paramResolver.Projectile.ResolveFanAngleDeg(casterActorId, launcher.FanAngleDeg)
                : launcher.FanAngleDeg;
            var durationMs = paramResolver != null
                ? paramResolver.Projectile.ResolveDurationMs(casterActorId, launcher.DurationMs)
                : launcher.DurationMs;
            MobaResolvedShootProjectileParams launchParams;
            try
            {
                launchParams = new MobaResolvedShootProjectileParams(launcherId, projectileId, countPerShot, fanAngleDeg, durationMs);
            }
            catch (System.ArgumentOutOfRangeException ex)
            {
                LogRejected(ctx, ex.Message);
                return;
            }
 
            var casterPos = Vec3.Zero;
            if (ctx.Context.TryResolve<MobaActorRegistry>(out var actorRegistry)
                && actorRegistry != null
                && actorRegistry.TryGet(casterActorId, out var casterEntity)
                && casterEntity != null
                && casterEntity.hasTransform)
            {
                casterPos = casterEntity.transform.Value.Position;
            }

            var requiresTargetResolution = args.TrackTarget
                || args.TargetRequest.UsesTemplate
                || args.TargetRequest.TargetActorId > 0
                || args.TargetRequest.TargetPayloadActorId > 0;
            if (requiresTargetResolution)
            {
                var effectInput = new MobaEffectActionInput(in coreInput);
                var targets = PooledMobaPlanActionLists.GetIntList();
                try
                {
                    if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, ActionName, targets)
                        || targets.Count == 0)
                    {
                        LogRejected(ctx, "target query found no target");
                        return;
                    }

                    targetActorId = targets[0];
                }
                finally
                {
                    PooledMobaPlanActionLists.Release(targets);
                }

                if (actorRegistry == null
                    || !actorRegistry.TryGet(targetActorId, out var targetEntity)
                    || targetEntity == null
                    || !targetEntity.hasTransform)
                {
                    LogRejected(ctx, $"target actor is missing transform. targetActorId={targetActorId}");
                    return;
                }

                aimPos = targetEntity.transform.Value.Position;
                var targetDelta = new Vec3(aimPos.X - casterPos.X, 0f, aimPos.Z - casterPos.Z);
                if (!targetDelta.Equals(Vec3.Zero)) aimDir = targetDelta.Normalized;
            }
            else if (!aimDir.Equals(Vec3.Zero))
            {
                aimDir = aimDir.Normalized;
            }
            else if (!aimPos.Equals(Vec3.Zero))
            {
                var delta = new Vec3(aimPos.X - casterPos.X, 0f, aimPos.Z - casterPos.Z);
                if (!delta.Equals(Vec3.Zero)) aimDir = delta.Normalized;
            }

            aimPos = casterPos;

            var sourceContext = input.CreateSourceContext(casterActorId, targetActorId, projectile.Id);
            if (!projectileSvc.Launch(casterActorId, launcher, projectile, launchParams.CountPerShot, launchParams.FanAngleDeg, launchParams.DurationMs, args.ContinuousProcessId, args.TrackTarget, in aimPos, in aimDir, in sourceContext))
            {
                LogRejected(ctx, $"launch failed. launcherId={launchParams.LauncherId} projectileId={launchParams.ProjectileId}");
                return;
            }

            LogApplied(ctx, $"launch requested. casterActorId={casterActorId} launcherId={launchParams.LauncherId} projectileId={launchParams.ProjectileId} targetActorId={targetActorId} trackTarget={args.TrackTarget} countPerShot={launchParams.CountPerShot} fanAngleDeg={launchParams.FanAngleDeg:0.###} durationMs={launchParams.DurationMs} continuousProcessId={args.ContinuousProcessId} hasAimPos={input.HasAimPosition} hasAimDir={input.HasAimDirection}");
        }
    }
}
