using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.TransferShield)]
    public sealed class TransferShieldPlanActionModule : MobaPlanActionModuleBase<TransferShieldArgs, TransferShieldPlanActionModule>
    {
        protected override IActionSchema<TransferShieldArgs, IWorldResolver> Schema => TransferShieldSchema.Instance;
        protected override void Execute(object triggerArgs, TransferShieldArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaShieldService>(out var shields) || shields == null) { LogRejected(ctx, "cannot resolve MobaShieldService."); return; }
            if (!ctx.Context.TryResolve<IFrameTime>(out var frameTime) || frameTime == null) { LogRejected(ctx, "requires IFrameTime."); return; }
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var coreInput)) { LogRejected(ctx, "requires combat execution context."); return; }
            var effectInput = new MobaEffectActionInput(in coreInput);
            var source = args.SourceActorId > 0 ? args.SourceActorId : effectInput.CasterActorId;
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, ActionName, targets)) return;
                var transferred = 0;
                for (var i = 0; i < targets.Count; i++)
                    if (shields.TransferShield(source, args.InstanceId, targets[i], frameTime.Frame.Value, args.Policy, args.Ratio)) transferred++;
                LogApplied(ctx, $"source={source} instance={args.InstanceId} targets={targets.Count} transferred={transferred} policy={args.Policy}");
            }
            finally { PooledMobaPlanActionLists.Release(targets); }
        }
    }
}
