using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// Canonical pattern-driven Area group action. The legacy batch action and
    /// this action share the same executor so lifecycle and rollback semantics
    /// cannot diverge between old and new content.
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.SpawnAreaPattern)]
    public sealed class SpawnAreaPatternPlanActionModule : MobaPlanActionModuleBase<SpawnAreaBatchArgs, SpawnAreaPatternPlanActionModule>
    {
        protected override IActionSchema<SpawnAreaBatchArgs, IWorldResolver> Schema => SpawnAreaPatternSchema.Instance;

        protected override void Execute(object triggerArgs, SpawnAreaBatchArgs args, ExecCtx<IWorldResolver> ctx)
        {
            new SpawnAreaBatchPlanActionModule().ExecutePattern(triggerArgs, args, ctx, ActionName);
        }
    }
}
