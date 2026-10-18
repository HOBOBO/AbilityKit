using System;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionSnapshotRoutingSubFeature :
        ISessionSubFeature<ISessionRuntimeFeature>, IGameModuleId, IGameModuleDependencies
    {
        private Action _onSessionStarting;
        private Action _onSessionStopping;

        public string Id => "snapshot_routing";
        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            var runtime = BattleSessionFeatureRuntimeAccess.Require<ISessionSnapshotRoutingRuntime>(ctx, Id);
            _onSessionStarting = runtime.EnsureSnapshotRoutingBuilt;
            _onSessionStopping = runtime.DisposeSnapshotRoutingIfAny;

            runtime.Hooks?.SessionStarting.Add(_onSessionStarting);
            runtime.Hooks?.SessionStopping.Add(_onSessionStopping);
        }

        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            BattleSessionFeatureRuntimeAccess.TryGet<ISessionSnapshotRoutingRuntime>(ctx, out var runtime);
            Exception firstFailure = null;
            try
            {
                if (_onSessionStarting != null && runtime != null)
                    runtime.Hooks?.SessionStarting.Remove(_onSessionStarting);
            }
            catch (Exception exception) { firstFailure = exception; }
            try
            {
                if (_onSessionStopping != null && runtime != null)
                    runtime.Hooks?.SessionStopping.Remove(_onSessionStopping);
            }
            catch (Exception exception)
            {
                if (firstFailure != null) throw new AggregateException(firstFailure, exception);
                throw;
            }
            if (firstFailure != null) throw firstFailure;
            _onSessionStarting = null;
            _onSessionStopping = null;
        }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }
        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }
    }
}
