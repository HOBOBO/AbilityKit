using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionDispatchersSubFeature : ISessionSubFeature<BattleSessionFeature>
        {
            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._unityDispatcher = AbilityKit.Network.Abstractions.UnityMainThreadDispatcher.CaptureCurrent();
                f._networkIoDispatcher ??= new AbilityKit.Network.Abstractions.DedicatedThreadDispatcher("GatewayNetworkThread");
            }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._unityDispatcher = null;
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}
