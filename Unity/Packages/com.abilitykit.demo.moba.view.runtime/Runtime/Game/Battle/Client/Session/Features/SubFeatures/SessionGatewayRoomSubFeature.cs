using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionGatewayRoomSubFeature :
        ISessionSubFeature<ISessionRuntimeFeature>,
        ISessionPreTickSubFeature<ISessionRuntimeFeature>,
        IGameModuleId,
        IGameModuleDependencies
    {
        private Func<BattleStartPlan, bool> _planBuiltHandler;
        private Task _gatewayStartTask = Task.CompletedTask;
        private Task _gatewayCompletionTask;
        private bool _sessionRequested;
        private bool _failureNotified;

        public string Id => "gateway_room";

        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            var runtime = BattleSessionFeatureRuntimeAccess.Require<ISessionGatewayRuntime>(ctx, Id);

            _sessionRequested = false;
            _failureNotified = false;
            _gatewayCompletionTask = null;
            _planBuiltHandler = plan =>
            {
                return TryStartGatewayRoomPreparation(runtime);
            };

            runtime.Hooks?.PlanBuilt.Add(_planBuiltHandler);
        }

        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            BattleSessionFeatureRuntimeAccess.TryGet<ISessionGatewayRuntime>(ctx, out var runtime);
            if (_planBuiltHandler != null && runtime != null)
            {
                runtime.Hooks?.PlanBuilt.Remove(_planBuiltHandler);
            }
            _planBuiltHandler = null;
            _gatewayStartTask = Task.CompletedTask;
            _gatewayCompletionTask = null;
            _sessionRequested = false;
            _failureNotified = false;
        }

        public void PreTick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime)
        {
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionGatewayRuntime>(ctx, out var runtime)) return;
            if (!_gatewayStartTask.IsCompleted) return;
            if (_gatewayStartTask.IsCanceled) return;
            if (_gatewayStartTask.IsFaulted)
            {
                NotifyPreparationFailure(runtime, _gatewayStartTask);
                return;
            }
            if (_gatewayCompletionTask != null)
            {
                if (!_gatewayCompletionTask.IsCompleted) return;
                if (_gatewayCompletionTask.IsFaulted)
                {
                    NotifyPreparationFailure(runtime, _gatewayCompletionTask);
                    return;
                }
                if (_gatewayCompletionTask.IsCanceled) return;
                if (!_sessionRequested)
                {
                    _sessionRequested = true;
                    runtime.OnStartSessionRequested();
                }
                return;
            }
            if (!runtime.HasGatewayRoomConnection) return;

            runtime.TickGatewayRoomConnection(deltaTime);

            var task = runtime.GatewayRoomPreparationTask;
            if (task == null || !task.IsCompleted) return;

            if (task.IsFaulted)
            {
                NotifyPreparationFailure(runtime, task);
                return;
            }

            _gatewayCompletionTask = runtime.CompleteGatewayRoomPreparationAsync() ?? Task.CompletedTask;
        }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }

        internal bool TryStartGatewayRoomPreparation(ISessionGatewayRuntime runtime)
        {
            if (runtime == null || !runtime.ShouldPrepareGatewayRoom()) return false;

            _sessionRequested = false;
            _failureNotified = false;
            _gatewayCompletionTask = null;
            _gatewayStartTask = runtime.StartGatewayRoomPreparation() ?? Task.CompletedTask;
            return true;
        }

        private void NotifyPreparationFailure(
            ISessionGatewayRuntime runtime,
            Task failedTask)
        {
            if (_failureNotified) return;
            _failureNotified = true;

            var wrapped = GatewaySessionFailurePolicy.WrapPreparationFailure(failedTask);
            GatewaySessionFailurePolicy.LogPreparationFailure(wrapped);
            _ = ObserveStopFailureAsync(runtime.StopGatewayRoomPreparationAsync());
            runtime.NotifySessionFailed(wrapped);
        }

        private static async Task ObserveStopFailureAsync(Task stopTask)
        {
            try
            {
                await (stopTask ?? Task.CompletedTask);
            }
            catch (Exception exception)
            {
                Log.Exception(exception, "[BattleSessionFeature] Gateway room cleanup failed");
            }
        }
    }
}
