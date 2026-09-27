using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionGatewayRoomSubFeature :
        ISessionSubFeature<BattleSessionFeature>,
        ISessionPreTickSubFeature<BattleSessionFeature>,
        IGameModuleId,
        IGameModuleDependencies
    {
        private Func<BattleStartPlan, bool> _planBuiltHandler;
        private Task _gatewayStartTask = Task.CompletedTask;
        private bool _sessionRequested;
        private bool _failureNotified;

        public string Id => "gateway_room";

        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
        {
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionGatewayRuntime>(ctx, out var runtime)) return;

            _sessionRequested = false;
            _failureNotified = false;
            _planBuiltHandler = plan =>
            {
                return TryStartGatewayRoomPreparation(runtime);
            };

            runtime.Hooks?.PlanBuilt.Add(_planBuiltHandler);
        }

        public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
        {
            BattleSessionFeatureRuntimeAccess.TryGet<ISessionGatewayRuntime>(ctx, out var runtime);
            if (_planBuiltHandler != null && runtime != null)
            {
                runtime.Hooks?.PlanBuilt.Remove(_planBuiltHandler);
            }
            _planBuiltHandler = null;
            _gatewayStartTask = Task.CompletedTask;
            _sessionRequested = false;
            _failureNotified = false;
        }

        public void PreTick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime)
        {
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionGatewayRuntime>(ctx, out var runtime)) return;
            if (!_gatewayStartTask.IsCompleted) return;
            if (_gatewayStartTask.IsCanceled) return;
            if (_gatewayStartTask.IsFaulted)
            {
                NotifyPreparationFailure(runtime, _gatewayStartTask);
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

            runtime.CompleteGatewayRoomPreparation();

            if (!_sessionRequested)
            {
                _sessionRequested = true;
                runtime.OnStartSessionRequested();
            }
        }

        public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }

        internal bool TryStartGatewayRoomPreparation(ISessionGatewayRuntime runtime)
        {
            if (runtime == null || !runtime.ShouldPrepareGatewayRoom()) return false;

            _sessionRequested = false;
            _failureNotified = false;
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
