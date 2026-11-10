using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.AdjustDamageNumber)]
    public sealed class SetAttackCriticalPlanActionModule : MobaPlanActionModuleBase<SetAttackCriticalArgs, SetAttackCriticalPlanActionModule>
    {
        protected override IActionSchema<SetAttackCriticalArgs, IWorldResolver> Schema => SetAttackCriticalSchema.Instance;

        protected override void Execute(object triggerArgs, SetAttackCriticalArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!(triggerArgs is AttackInfo attack))
            {
                LogRejected(ctx, "payload is not AttackInfo.");
                return;
            }

            if (args.ReasonKind != 0 && (int)attack.ReasonKind != args.ReasonKind) return;
            if (args.ReasonParam != 0 && attack.ReasonParam != args.ReasonParam) return;
            attack.CritType = CritType.Critical;
            LogApplied(ctx, $"attacker={attack.AttackerActorId} target={attack.TargetActorId} reason={attack.ReasonKind}:{attack.ReasonParam}");
        }
    }
}
