using System;
using AbilityKit.Core.Common.Log;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionPlanSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            IGameModuleId,
            IGameModuleDependencies
        {
            public string Id => "session_plan";

            public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._planCtrl.OnAttach(
                    host: (ISessionPlanHost)f,
                    bootstrapper: f._bootstrapper,
                    state: f._state,
                    handles: f._handles,
                    events: f.Events,
                    hooks: f.Hooks,
                    ctx: f._ctx);
            }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}
