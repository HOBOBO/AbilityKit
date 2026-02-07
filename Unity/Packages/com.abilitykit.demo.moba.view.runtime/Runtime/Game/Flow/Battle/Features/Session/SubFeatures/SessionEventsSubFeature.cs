using System;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionEventsSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            IGameModuleId,
            IGameModuleDependencies
        {
            public string Id => "session_events";

            public System.Collections.Generic.IEnumerable<string> Dependencies => null;

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                var provider = ctx.Phase.Entry != null ? ctx.Phase.Entry.Get<IBattleSessionEventsProvider>() : null;
                f._eventsCtrl.OnAttach(provider, (ISessionEventsHost)f);
            }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._eventsCtrl.OnDetach((ISessionEventsHost)f);
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}
