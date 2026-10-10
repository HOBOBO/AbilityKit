using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionPlanSubFeature :
        ISessionSubFeature<ISessionRuntimeFeature>,
        IGameModuleId,
        IGameModuleDependencies
    {
        private Task _attachTask = Task.CompletedTask;

        public string Id => "session_plan";

        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            var runtime = BattleSessionFeatureRuntimeAccess.Require<ISessionPlanRuntime>(ctx, Id);
            var host = ctx.Feature.PlanHost;

            _attachTask = runtime.StartupTasks.TrackPlanAttach(async generation =>
            {
                try
                {
                    await runtime.AttachPlanAsync(
                        () => runtime.StartupTasks.IsCurrent(generation));
                }
                catch (Exception exception)
                {
                    if (runtime.StartupTasks.IsCurrent(generation)) host.NotifySessionFailed(exception);
                    throw;
                }
            });
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

        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
        }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }

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
