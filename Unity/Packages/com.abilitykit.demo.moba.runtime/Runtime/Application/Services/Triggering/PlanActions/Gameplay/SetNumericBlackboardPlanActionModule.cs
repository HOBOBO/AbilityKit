using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.SetNumericBlackboard)]
    public sealed class SetNumericBlackboardPlanActionModule : MobaPlanActionModuleBase<SetNumericBlackboardArgs, SetNumericBlackboardPlanActionModule>
    {
        protected override IActionSchema<SetNumericBlackboardArgs, IWorldResolver> Schema => SetNumericBlackboardSchema.Instance;

        protected override void Execute(object triggerArgs, SetNumericBlackboardArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!BlackboardMutation.TrySetNumeric(ctx.Blackboards, in args.Target, args.Value, out var error))
            {
                LogRejected(ctx, error);
            }
        }
    }
}
