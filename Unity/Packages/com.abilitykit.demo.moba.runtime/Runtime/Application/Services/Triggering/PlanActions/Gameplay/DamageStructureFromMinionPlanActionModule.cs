using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Minions;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.DamageStructureFromMinion)]
    public sealed class DamageStructureFromMinionPlanActionModule :
        MobaPlanActionModuleBase<DamageStructureFromMinionArgs, DamageStructureFromMinionPlanActionModule>
    {
        protected override IActionSchema<DamageStructureFromMinionArgs, IWorldResolver> Schema =>
            DamageStructureFromMinionSchema.Instance;

        protected override void Execute(object triggerArgs, DamageStructureFromMinionArgs args,
            ExecCtx<IWorldResolver> ctx)
        {
            if (triggerArgs is not MobaMinionBaseDamageEvent request)
            {
                LogRejected(ctx, "requires a minion base-damage event");
                return;
            }
            if (!TryResolveRequired(ctx, out MobaStructureService structures)) return;
            if (!structures.ApplyMinionBaseDamage(in request))
            {
                LogRejected(ctx, "no eligible structure for this minion push");
                return;
            }
            LogApplied(ctx, $"sourceTeam={request.SourceTeamId}, targetTeam={request.TargetTeamId}");
        }
    }
}
