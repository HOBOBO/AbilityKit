using System;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionSnapshotRoutingSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            IGameModuleId,
            IGameModuleDependencies
        {
            private IDisposable _sessionStartingSub;
            private IDisposable _sessionStoppingSub;

            public string Id => "snapshot_routing";

            public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                var events = f.Events;
                _sessionStartingSub = events?.Subscribe<SessionStartingEvent>(_ => f.EnsureSnapshotRoutingBuilt());
                _sessionStoppingSub = events?.Subscribe<SessionStoppingEvent>(_ => f.DisposeSnapshotRoutingIfAny());
            }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                _sessionStartingSub?.Dispose();
                _sessionStartingSub = null;

                _sessionStoppingSub?.Dispose();
                _sessionStoppingSub = null;
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}
