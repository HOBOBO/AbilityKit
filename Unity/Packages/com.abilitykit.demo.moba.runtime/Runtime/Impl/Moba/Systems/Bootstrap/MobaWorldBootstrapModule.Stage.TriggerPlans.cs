using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterTriggerPlans(WorldContainerBuilder builder)
        {
            builder.TryRegister<PlanActionModuleRegistry>(WorldLifetime.Singleton, _ => PlanActionModuleRegistry.CreateDefault());

            builder.TryRegister<TriggerPlanJsonDatabase>(WorldLifetime.Singleton, r =>
            {
                var loader = r.Resolve<ITextLoader>();
                var db = new TriggerPlanJsonDatabase();
                Log.Info("[MobaWorldBootstrapModule] TriggerPlanJsonDatabase.Load begin");
                db.Load(new PlanTextLoaderAdapter(loader), "ability/ability_trigger_plans");
                Log.Info($"[MobaWorldBootstrapModule] TriggerPlanJsonDatabase.Load end. records={db.Records?.Count ?? 0}");
                return db;
            });
        }
    }
}
