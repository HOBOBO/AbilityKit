using AbilityKit.Game.Flow.Battle.Modules;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class ReplaySeekModule : IBattleSessionModule, IBattleSessionModuleId, IBattleSessionModuleDependencies
        {
            private readonly BattleSessionFeature _feature;

            public ReplaySeekModule(BattleSessionFeature feature)
            {
                _feature = feature;
            }

            public string Id => "replay_seek";

            public IEnumerable<string> Dependencies => null;

            public void OnAttach(in BattleSessionModuleContext ctx)
            {
            }

            public void OnDetach(in BattleSessionModuleContext ctx)
            {
            }

            public void Tick(in BattleSessionModuleContext ctx, float deltaTime)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _feature.HandleReplayDebugInput();
#endif
            }
        }
    }
}
