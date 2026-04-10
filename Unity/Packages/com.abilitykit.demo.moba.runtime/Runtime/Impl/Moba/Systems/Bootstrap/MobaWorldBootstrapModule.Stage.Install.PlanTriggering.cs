using System;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void InstallPlanTriggering(IWorldResolver services)
        {
            try
            {
                Log.Info("[MobaWorldBootstrapModule] InstallPlanTriggering: starting...");
                if (services.TryResolve<TriggerPlanJsonDatabase>(out var db) && db != null
                    && services.TryResolve<AbilityKit.Ability.Share.Impl.Moba.Services.MobaEffectExecutionService>(out var effects) && effects != null)
                {
                    Log.Info("[MobaWorldBootstrapModule] InstallPlanTriggering: initializing plan actions...");
                    effects.InitializePlanActions();

                    if (services.TryResolve<AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver>>(out var runner) && runner != null)
                    {
                        db.RegisterAll(runner);
                    }
                    Log.Info($"[MobaWorldBootstrapModule] PlanTriggering initialized. records={db.Records?.Count ?? 0}");
                }
                else
                {
                    Log.Warning("[MobaWorldBootstrapModule] InstallPlanTriggering init skipped (missing deps: db or effects)");
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaWorldBootstrapModule] PlanTriggering init exception");
            }
        }
    }
}
