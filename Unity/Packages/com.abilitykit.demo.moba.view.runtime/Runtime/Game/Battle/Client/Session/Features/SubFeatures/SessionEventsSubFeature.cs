using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionEventsSubFeature :
        ISessionSubFeature<ISessionRuntimeFeature>, IGameModuleId, IGameModuleDependencies
    {
        public string Id => "session_events";

        public System.Collections.Generic.IEnumerable<string> Dependencies => null;

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx) =>
            BattleSessionFeatureRuntimeAccess.Require<ISessionEventsRuntime>(ctx, Id)
                .Events.OnAttach(ctx.Feature.EventsHost);

        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            if (BattleSessionFeatureRuntimeAccess.TryGet<ISessionEventsRuntime>(ctx, out var runtime))
                runtime.Events.OnDetach(ctx.Feature.EventsHost);
        }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }
        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }
    }
}
