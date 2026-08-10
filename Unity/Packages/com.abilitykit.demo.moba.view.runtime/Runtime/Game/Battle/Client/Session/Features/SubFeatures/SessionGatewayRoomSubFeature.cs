using System;
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
        private bool _sessionRequested;

        public string Id => "gateway_room";

        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
        {
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionGatewayRuntime>(ctx, out var runtime)) return;

            _sessionRequested = false;
            _planBuiltHandler = plan =>
            {
                if (!runtime.ShouldPrepareGatewayRoom()) return false;
                runtime.StartGatewayRoomPreparation();
                return false;
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
            _sessionRequested = false;

            runtime?.StopGatewayRoomPreparation();
        }

        public void PreTick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime)
        {
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionGatewayRuntime>(ctx, out var runtime)) return;
            if (!runtime.HasGatewayRoomConnection) return;

            runtime.TickGatewayRoomConnection(deltaTime);

            var task = runtime.GatewayRoomPreparationTask;
            if (task == null || !task.IsCompleted) return;

            if (task.IsFaulted)
            {
                var wrapped = GatewaySessionFailurePolicy.WrapPreparationFailure(task);
                GatewaySessionFailurePolicy.LogPreparationFailure(wrapped);
                runtime.StopGatewayRoomPreparation();
                runtime.NotifySessionFailed(wrapped);
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
    }
}
