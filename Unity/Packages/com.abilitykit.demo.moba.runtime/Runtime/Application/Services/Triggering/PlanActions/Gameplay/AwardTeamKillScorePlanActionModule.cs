using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.AwardTeamKillScore)]
    public sealed class AwardTeamKillScorePlanActionModule :
        MobaPlanActionModuleBase<AwardTeamKillScoreArgs, AwardTeamKillScorePlanActionModule>
    {
        protected override IActionSchema<AwardTeamKillScoreArgs, IWorldResolver> Schema =>
            AwardTeamKillScoreSchema.Instance;

        protected override void Execute(object triggerArgs, AwardTeamKillScoreArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!(triggerArgs is UnitDieEventPayload death) ||
                !TryResolveRequired(ctx, out TeamDeathmatchKillScoreService rules) ||
                !rules.TryAwardHeroKill(in death, args.KeyBaseId, args.Points, out var teamId, out var score))
            {
                LogRejected(ctx, "requires an enemy hero kill by a valid team during gameplay");
                return;
            }

            LogApplied(ctx, $"team={teamId} score={score}");
        }
    }
}
