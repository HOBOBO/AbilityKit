using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.RemoveSharedShield)]
    public sealed class RemoveSharedShieldPlanActionModule : MobaPlanActionModuleBase<RemoveSharedShieldArgs, RemoveSharedShieldPlanActionModule>
    {
        protected override IActionSchema<RemoveSharedShieldArgs, AbilityKit.Ability.World.DI.IWorldResolver> Schema => RemoveSharedShieldSchema.Instance;
        protected override void Execute(object triggerArgs, RemoveSharedShieldArgs args, ExecCtx<AbilityKit.Ability.World.DI.IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaShieldService>(out var shields) || shields == null)
            {
                LogRejected(ctx, "cannot resolve MobaShieldService.");
                return;
            }
            if (args.PoolId <= 0 || !shields.RemoveSharedPool(args.PoolId))
            {
                LogRejected(ctx, $"shared pool was not found or could not be removed. poolId={args.PoolId}");
                return;
            }
            LogApplied(ctx, $"poolId={args.PoolId} removed=true");
        }
    }
}
