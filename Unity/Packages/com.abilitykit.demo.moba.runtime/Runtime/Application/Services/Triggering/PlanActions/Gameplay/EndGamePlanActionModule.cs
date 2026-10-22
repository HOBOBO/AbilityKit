using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Gameplay.Structures;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.EndGame)]
    public sealed class EndGamePlanActionModule : MobaPlanActionModuleBase<EndGameArgs, EndGamePlanActionModule>
    {
        protected override IActionSchema<EndGameArgs, IWorldResolver> Schema => EndGameSchema.Instance;

        protected override void Execute(object triggerArgs, EndGameArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (ctx.Context == null)
            {
                LogRejected("missing world context");
                return;
            }

            if (!TryResolveRequired(ctx, out MobaGameplayService gameplay))
            {
                return;
            }

            var reason = string.Empty;
            if (args.ReasonId > 0 && ctx.Context.TryResolve<TriggerPlanJsonDatabase>(out var db) && db != null)
            {
                db.TryGetString(args.ReasonId, out reason);
            }

            if (string.IsNullOrEmpty(reason))
            {
                reason = triggerArgs is MobaStructureEventArgs structureEvent &&
                         structureEvent.Kind == MobaStructureKind.Base
                    ? "base_destroyed"
                    : "trigger_end_game";
            }

            var winTeamId = args.WinTeamId;
            if (args.WinTeamFromEvent)
            {
                if (triggerArgs is MobaStructureEventArgs structureEvent)
                {
                    winTeamId = structureEvent.WinnerTeamId;
                }
                else if (triggerArgs is GameplayLifecycleEventArgs gameplayEvent &&
                         (gameplayEvent.TeamId == (int)Team.Team1 || gameplayEvent.TeamId == (int)Team.Team2))
                {
                    winTeamId = gameplayEvent.TeamId;
                }
                else
                {
                    LogRejected("win_team_from_event requires a gameplay score or structure event");
                    return;
                }
            }

            if (winTeamId != 0 && winTeamId != (int)Team.Team1 && winTeamId != (int)Team.Team2)
            {
                LogRejected("win_team_id must be a team in this mode");
                return;
            }

            if (!gameplay.End(reason, winTeamId))
            {
                LogRejected($"ignored phase={gameplay.Phase}, reason={reason}, winTeamId={winTeamId}");
                return;
            }

            LogApplied($"reason={reason}, winTeamId={winTeamId}");
        }
    }
}
