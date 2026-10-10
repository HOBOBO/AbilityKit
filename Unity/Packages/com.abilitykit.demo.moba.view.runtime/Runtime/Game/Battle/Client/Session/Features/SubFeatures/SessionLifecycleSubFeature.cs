using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionLifecycleSubFeature :
        ISessionSubFeature<ISessionRuntimeFeature>,
        ISessionLifecycleNotifySubFeature<ISessionRuntimeFeature>,
        IGameModuleId,
        IGameModuleDependencies
    {
        public string Id => "session_lifecycle";

        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void NotifySessionStarting(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            var runtime = BattleSessionFeatureRuntimeAccess.Require<ISessionLifecycleRuntime>(ctx, Id);

            runtime.Hooks?.SessionStarting.Invoke();
        }

        public void NotifySessionStopping(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            var runtime = BattleSessionFeatureRuntimeAccess.Require<ISessionLifecycleRuntime>(ctx, Id);

            runtime.Hooks?.SessionStopping.Invoke();
        }

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx) =>
            BattleSessionFeatureRuntimeAccess.Require<ISessionLifecycleRuntime>(ctx, Id);

        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }
    }
}
