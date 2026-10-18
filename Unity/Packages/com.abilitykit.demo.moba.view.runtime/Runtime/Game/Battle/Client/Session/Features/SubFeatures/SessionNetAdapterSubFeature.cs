using AbilityKit.Ability.Host;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionNetAdapterSubFeature :
        ISessionSubFeature<ISessionRuntimeFeature>,
        ISessionFramePacketTransformSubFeature<ISessionRuntimeFeature>,
        IGameModuleId, IGameModuleDependencies
    {
        public string Id => "session_net_adapter";
        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx) =>
            BattleSessionFeatureRuntimeAccess.Require<ISessionNetAdapterRuntime>(ctx, Id);
        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }

        public FramePacket TransformFramePacket(in FeatureModuleContext<ISessionRuntimeFeature> ctx, FramePacket packet)
        {
            return BattleSessionFeatureRuntimeAccess.Require<ISessionNetAdapterRuntime>(ctx, Id)
                .TransformFramePacket(packet);
        }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }
        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }
    }
}
