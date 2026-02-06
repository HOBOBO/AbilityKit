using AbilityKit.Game.Flow.Battle.Modules;
using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SnapshotRoutingModule : IBattleSessionModule, IBattleSessionModuleId, IBattleSessionModuleDependencies
        {
            private readonly BattleSessionFeature _feature;

            private IDisposable _sessionStartingSub;
            private IDisposable _sessionStoppingSub;

            public SnapshotRoutingModule(BattleSessionFeature feature)
            {
                _feature = feature;
            }

            public string Id => "snapshot_routing";

            public IEnumerable<string> Dependencies => null;

            public void OnAttach(in BattleSessionModuleContext ctx)
            {
                _sessionStartingSub = ctx.Events?.Subscribe<SessionStartingEvent>(_ => _feature.BuildSnapshotRouting());
                _sessionStoppingSub = ctx.Events?.Subscribe<SessionStoppingEvent>(_ => _feature.DisposeSnapshotRouting());
            }

            public void OnDetach(in BattleSessionModuleContext ctx)
            {
                _sessionStartingSub?.Dispose();
                _sessionStartingSub = null;

                _sessionStoppingSub?.Dispose();
                _sessionStoppingSub = null;
            }

            public void Tick(in BattleSessionModuleContext ctx, float deltaTime)
            {
            }
        }
    }
}
