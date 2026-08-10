using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.Heal)]
    public sealed class HealPlanActionModule : MobaPlanActionModuleBase<HealArgs, HealPlanActionModule>
    {
        protected override IActionSchema<HealArgs, IWorldResolver> Schema => HealSchema.Instance;

        protected override void Execute(object triggerArgs, HealArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.Amount <= 0f) return;
            if (!ctx.Context.TryResolve<MobaDamageService>(out var damage) || damage == null)
            {
                LogRejected(ctx, "cannot resolve MobaDamageService.");
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

            var effectInput = new MobaEffectActionInput(in coreInput);
            if (!effectInput.HasCasterActor)
            {
                LogRejected(ctx, "missing healer actor.");
                return;
            }

            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, TriggeringConstants.Actions.Heal, targets))
                {
                    return;
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    var targetActorId = targets[i];
                    var origin = effectInput.BuildOrigin(
                        effectInput.CasterActorId,
                        targetActorId,
                        MobaTraceKind.EffectExecution,
                        args.ReasonParam);
                    var result = damage.CommitHeal(
                        effectInput.CasterActorId,
                        targetActorId,
                        (int)args.HealType,
                        args.Amount,
                        args.ReasonKind,
                        args.ReasonParam,
                        origin);
                    if (result.Succeeded)
                    {
                        MobaPlanActionDiagnostics.Applied(ctx.Context, TriggeringConstants.Actions.Heal, $"healer={effectInput.CasterActorId}, target={targetActorId}, amount={result.AppliedValue:0.###}, reasonParam={args.ReasonParam}");
                    }
                }
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }
    }
}
