using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.ModifyResource)]
    public sealed class RefundSkillCostOnKillPlanActionModule : MobaPlanActionModuleBase<RefundSkillCostOnKillArgs, RefundSkillCostOnKillPlanActionModule>
    {
        protected override IActionSchema<RefundSkillCostOnKillArgs, IWorldResolver> Schema => RefundSkillCostOnKillSchema.Instance;
        protected override void Execute(object triggerArgs, RefundSkillCostOnKillArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!(triggerArgs is UnitDieEventPayload death) || death.KillerActorId <= 0 ||
                !death.TryGetOrigin(out var origin) || !origin.SkillRuntimeHandle.IsValid ||
                !TryResolveRequired(ctx, out MobaSkillEconomyService economy) ||
                !TryRefund(economy, in origin, death.KillerActorId, args.SkillId, out var amount))
            { LogRejected(ctx, "requires a qualifying skill-attributed kill with a retained resource cost"); return; }
            LogApplied(ctx, $"killer={death.KillerActorId} skill={args.SkillId} refunded={amount}");
        }

        private static bool TryRefund(MobaSkillEconomyService economy, in MobaGameplayOrigin origin,
            int killerActorId, int skillId, out AbilityKit.Deterministic.Fixed64 amount)
        {
            var handle = origin.SkillRuntimeHandle;
            return economy.TryRefundKillCost(in handle, killerActorId, skillId, out amount);
        }
    }
}
