using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.RemoveProjectile)]
    public sealed class RemoveProjectilePlanActionModule : MobaPlanActionModuleBase<RemoveProjectileArgs, RemoveProjectilePlanActionModule>
    {
        protected override IActionSchema<RemoveProjectileArgs, IWorldResolver> Schema => RemoveProjectileSchema.Instance;

        protected override void Execute(object triggerArgs, RemoveProjectileArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (ctx.Context == null)
            {
                LogRejected(ctx, "missing world context.");
                return;
            }

            if (!MobaPlanActionInputResolver.TryResolve(
                    triggerArgs,
                    ctx,
                    out var input))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            if (!input.HasTargetActor)
            {
                LogRejected(ctx, "requires target actor from trigger payload.");
                return;
            }

            if (!ctx.Context.TryResolve<MobaActorLookupService>(out var actors)
                || actors == null
                || !actors.TryGetActorEntity(input.TargetActorId, out var target)
                || target == null
                || !target.isFlyingProjectileTag)
            {
                LogRejected(ctx, $"target is not a flying projectile. target={input.TargetActorId}");
                return;
            }

            if (!ctx.Context.TryResolve<MobaProjectileLinkService>(out var links)
                || links == null
                || !links.TryGetProjectileId(input.TargetActorId, out var projectileId))
            {
                LogRejected(ctx, $"cannot resolve projectile link. target={input.TargetActorId}");
                return;
            }

            if (!ctx.Context.TryResolve<IProjectileService>(out var projectiles) || projectiles == null)
            {
                LogRejected(ctx, "cannot resolve IProjectileService.");
                return;
            }

            if (!ctx.Context.TryResolve<IFrameTime>(out var frameTime) || frameTime == null)
            {
                LogRejected(ctx, "cannot resolve IFrameTime.");
                return;
            }

            var frame = frameTime.Frame.Value;
            var removed = projectiles.Despawn(projectileId, frame, ProjectileExitReason.Manual);
            if (!removed)
            {
                LogRejected(ctx, $"projectile is no longer active. target={input.TargetActorId} projectile={projectileId.Value}");
                return;
            }

            LogApplied(ctx, $"target={input.TargetActorId} projectile={projectileId.Value} frame={frame}");
        }
    }
}
