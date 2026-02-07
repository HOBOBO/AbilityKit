using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionLifecycleSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            ISessionLifecycleNotifySubFeature<BattleSessionFeature>,
            IGameModuleId,
            IGameModuleDependencies
        {
            public string Id => "session_lifecycle";

            public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

            public void NotifySessionStarting(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f.Events?.Publish(new SessionStartingEvent(f._plan));
                f.Events?.Flush();
                f.Hooks?.SessionStarting.Invoke();
            }

            public void NotifySessionStopping(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f.Events?.Publish(new SessionStoppingEvent(f._plan));
                f.Events?.Flush();
                f.Hooks?.SessionStopping.Invoke();
            }

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}
