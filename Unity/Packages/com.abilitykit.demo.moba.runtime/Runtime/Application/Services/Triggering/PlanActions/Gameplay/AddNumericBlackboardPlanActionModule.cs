using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.AddNumericBlackboard)]
    public sealed class AddNumericBlackboardPlanActionModule : MobaPlanActionModuleBase<AddNumericBlackboardArgs, AddNumericBlackboardPlanActionModule>
    {
        protected override IActionSchema<AddNumericBlackboardArgs, IWorldResolver> Schema => AddNumericBlackboardSchema.Instance;

        protected override void Execute(object triggerArgs, AddNumericBlackboardArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!BlackboardMutation.TryAddNumeric(ctx.Blackboards, in args.Target, args.Value, out var error))
            {
                LogRejected(ctx, error);
            }
        }
    }
}
