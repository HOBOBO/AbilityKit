using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.LeaveSharedShield)]
    public sealed class LeaveSharedShieldPlanActionModule : MobaPlanActionModuleBase<LeaveSharedShieldArgs, LeaveSharedShieldPlanActionModule>
    {
        protected override IActionSchema<LeaveSharedShieldArgs, IWorldResolver> Schema => LeaveSharedShieldSchema.Instance;
        protected override void Execute(object triggerArgs, LeaveSharedShieldArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaShieldService>(out var shields) || shields == null) { LogRejected(ctx, "cannot resolve MobaShieldService."); return; }
            if (!ctx.Context.TryResolve<IFrameTime>(out var frameTime) || frameTime == null) { LogRejected(ctx, "requires IFrameTime."); return; }
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var coreInput)) { LogRejected(ctx, "requires combat execution context."); return; }
            var effectInput = new MobaEffectActionInput(in coreInput);
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, ActionName, targets)) return;
                var left = 0;
                for (var i = 0; i < targets.Count; i++) if (shields.LeaveSharedPool(args.PoolId, targets[i], frameTime.Frame.Value)) left++;
                LogApplied(ctx, $"poolId={args.PoolId} targets={targets.Count} left={left}");
            }
            finally { PooledMobaPlanActionLists.Release(targets); }
        }
    }
}
