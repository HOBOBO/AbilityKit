using System;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionEventsSubFeature :
        ISessionSubFeature<ISessionRuntimeFeature>,
        IGameModuleId,
        IGameModuleDependencies
    {
        public string Id => "session_events";

        public System.Collections.Generic.IEnumerable<string> Dependencies => null;

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            var runtime = BattleSessionFeatureRuntimeAccess.Require<ISessionEventsRuntime>(ctx, Id);

            runtime.Events.OnAttach(ctx.Feature.EventsHost);
        }

        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionEventsRuntime>(ctx, out var runtime)) return;

            runtime.Events.OnDetach(ctx.Feature.EventsHost);
        }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }
    }
}
