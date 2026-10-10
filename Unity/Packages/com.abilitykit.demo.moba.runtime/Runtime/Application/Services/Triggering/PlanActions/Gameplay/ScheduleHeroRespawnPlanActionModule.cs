using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.ScheduleHeroRespawn)]
    public sealed class ScheduleHeroRespawnPlanActionModule :
        MobaPlanActionModuleBase<ScheduleHeroRespawnArgs, ScheduleHeroRespawnPlanActionModule>
    {
        protected override IActionSchema<ScheduleHeroRespawnArgs, IWorldResolver> Schema =>
            ScheduleHeroRespawnSchema.Instance;

        protected override void Execute(object triggerArgs, ScheduleHeroRespawnArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!(triggerArgs is UnitDieEventPayload death) ||
                !TryResolveRequired(ctx, out MobaGameplayRespawnService respawns) ||
                !respawns.TrySchedule(in death, args.DelayMilliseconds, args.SpawnIndex,
                    args.HealthRatio, args.MaxRespawns))
            {
                LogRejected(ctx, "requires a dead hero and valid respawn settings during gameplay");
                return;
            }

            LogApplied(ctx, $"actor={death.ActorId} delay_ms={args.DelayMilliseconds}");
        }
    }
}
