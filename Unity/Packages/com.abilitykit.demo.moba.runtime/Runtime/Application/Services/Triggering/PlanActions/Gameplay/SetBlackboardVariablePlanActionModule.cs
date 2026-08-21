using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.SetBlackboardVariable)]
    public sealed class SetBlackboardVariablePlanActionModule : MobaPlanActionModuleBase<SetBlackboardVariableArgs, SetBlackboardVariablePlanActionModule>
    {
        protected override IActionSchema<SetBlackboardVariableArgs, IWorldResolver> Schema => SetBlackboardVariableSchema.Instance;

        protected override void Execute(object triggerArgs, SetBlackboardVariableArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!BlackboardMutation.TrySetValue(ctx.Blackboards, in args.Target, in args.Value, out var error))
                LogRejected(ctx, error);
        }
    }
}
