using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionDispatchersSubFeature :
        ISessionSubFeature<ISessionRuntimeFeature>,
        IGameModuleId,
        IGameModuleDependencies
    {
        public string Id => "session_dispatchers";

        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            var runtime = BattleSessionFeatureRuntimeAccess.Require<ISessionDispatchersRuntime>(ctx, Id);

            runtime.AttachDispatchers();
        }

        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionDispatchersRuntime>(ctx, out var runtime)) return;

            runtime.DetachDispatchers();
        }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }
    }
}
