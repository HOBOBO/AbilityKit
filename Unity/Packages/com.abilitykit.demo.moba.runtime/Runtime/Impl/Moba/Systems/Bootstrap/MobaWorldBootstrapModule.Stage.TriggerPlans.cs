using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterTriggerPlans(WorldContainerBuilder builder)
        {
            // [RESTORED] ITextLoader 原本在 Effects Stage 注册，但 Effects 包已删除
            // 需要保留此服务供 TriggerPlanJsonDatabase 使用
            builder.TryRegister<ITextLoader>(WorldLifetime.Singleton, _ => new UnityResourcesTextLoader());

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
