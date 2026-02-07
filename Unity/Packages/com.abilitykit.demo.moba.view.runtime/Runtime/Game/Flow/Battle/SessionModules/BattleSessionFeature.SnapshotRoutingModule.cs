using AbilityKit.Game.Flow.Battle.Modules;
using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private interface ISnapshotRoutingModuleHost
        {
            void BuildSnapshotRouting();
            void DisposeSnapshotRouting();
        }

        private sealed class SnapshotRoutingModule : IBattleSessionModule, IBattleSessionModuleId, IBattleSessionModuleDependencies
        {
            private readonly ISnapshotRoutingModuleHost _host;

            private IDisposable _sessionStartingSub;
            private IDisposable _sessionStoppingSub;

            public SnapshotRoutingModule(ISnapshotRoutingModuleHost host)
            {
                _host = host;
            }

            public string Id => "snapshot_routing";

            public IEnumerable<string> Dependencies => null;

            public void OnAttach(in BattleSessionModuleContext ctx)
            {
                _sessionStartingSub = ctx.Events?.Subscribe<SessionStartingEvent>(_ => _host?.BuildSnapshotRouting());
                _sessionStoppingSub = ctx.Events?.Subscribe<SessionStoppingEvent>(_ => _host?.DisposeSnapshotRouting());
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
