using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionPlanSubFeature :
        ISessionSubFeature<BattleSessionFeature>,
        IGameModuleId,
        IGameModuleDependencies
    {
        private Task _attachTask = Task.CompletedTask;

        public string Id => "session_plan";

        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
        {
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionPlanRuntime>(ctx, out var runtime)) return;

            _attachTask = runtime.PlanController.OnAttachAsync(
                host: (ISessionPlanHost)ctx.Feature,
                bootstrapper: runtime.Bootstrapper,
                state: runtime.State,
                handles: runtime.Handles,
                hooks: runtime.Hooks,
                ctx: runtime.Context);
            if (_attachTask.IsCompleted)
            {
                SessionAsyncOperation.RequireCompleted(
                    _attachTask,
                    "Immediate battle session plan attach");
            }
            else
            {
                _ = ObserveAttachFailureAsync(_attachTask);
            }
        }

        public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
        {
        }

        public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }

        private static async Task ObserveAttachFailureAsync(Task attachTask)
        {
            try
            {
                await (attachTask ?? Task.CompletedTask);
            }
            catch (Exception exception)
            {
                Log.Exception(exception, "[BattleSessionFeature] Asynchronous plan attach failed");
            }
        }
    }
}
