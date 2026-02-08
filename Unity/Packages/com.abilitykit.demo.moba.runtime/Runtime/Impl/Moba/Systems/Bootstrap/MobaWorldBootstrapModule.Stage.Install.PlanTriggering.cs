using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void InstallPlanTriggering(IWorldResolver services)
        {
            try
            {
                if (services.TryResolve<TriggerPlanJsonDatabase>(out var db) && db != null
                    && services.TryResolve<ActionRegistry>(out var acts) && acts != null
                    && services.TryResolve<AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver>>(out var runner) && runner != null)
                {
                    RegisterStubActionsFromPlans(db, acts);

                    if (services.TryResolve<PlanActionModuleRegistry>(out var registry) && registry != null && registry.Modules != null)
                    {
                        var modules = registry.Modules;
                        for (int i = 0; i < modules.Length; i++)
                        {
                            var m = modules[i];
                            if (m == null) continue;
                            try { m.Register(acts, services); }
                            catch (Exception ex) { Log.Exception(ex, $"[MobaWorldBootstrapModule] PlanActionModule register failed. module={m.GetType().Name}"); }
                        }
                    }

                    db.RegisterAll(runner);
                    Log.Info($"[MobaWorldBootstrapModule] PlanTriggering initialized. records={db.Records?.Count ?? 0}");
                }
                else
                {
                    Log.Info("[MobaWorldBootstrapModule] PlanTriggering init skipped (missing deps)");
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaWorldBootstrapModule] PlanTriggering init exception");
            }
        }
    }
}
