using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.JoinSharedShield)]
    public sealed class JoinSharedShieldPlanActionModule : MobaPlanActionModuleBase<JoinSharedShieldArgs, JoinSharedShieldPlanActionModule>
    {
        protected override IActionSchema<JoinSharedShieldArgs, IWorldResolver> Schema => JoinSharedShieldSchema.Instance;

        protected override void Execute(object triggerArgs, JoinSharedShieldArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaShieldService>(out var shields) || shields == null) { LogRejected(ctx, "cannot resolve MobaShieldService."); return; }
            if (!ctx.Context.TryResolve<IFrameTime>(out var frameTime) || frameTime == null) { LogRejected(ctx, "requires IFrameTime."); return; }
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var coreInput)) { LogRejected(ctx, "requires combat execution context."); return; }
            var effectInput = new MobaEffectActionInput(in coreInput);
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, ActionName, targets)) return;
                var joined = 0;
                for (var i = 0; i < targets.Count; i++) if (shields.JoinSharedPool(args.PoolId, targets[i], frameTime.Frame.Value, args.Weight, args.MaxConsumeValue)) joined++;
                LogApplied(ctx, $"poolId={args.PoolId} targets={targets.Count} joined={joined}");
            }
            finally { PooledMobaPlanActionLists.Release(targets); }
        }
    }
}
