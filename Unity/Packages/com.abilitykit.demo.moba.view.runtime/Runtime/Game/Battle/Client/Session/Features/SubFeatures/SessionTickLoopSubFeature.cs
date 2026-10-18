using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionTickLoopSubFeature :
        ISessionSubFeature<ISessionRuntimeFeature>,
        ISessionMainTickSubFeature<ISessionRuntimeFeature>,
        IGameModuleId, IGameModuleDependencies
    {
        public string Id => "session_tick_loop";
        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx) =>
            BattleSessionFeatureRuntimeAccess.Require<ISessionTickLoopRuntime>(ctx, Id);
        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }

        public void MainTick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime)
        {
            BattleSessionFeatureRuntimeAccess.Require<ISessionTickLoopRuntime>(ctx, Id)
                .TickSession(deltaTime);
        }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }
        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }
    }
}
