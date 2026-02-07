using AbilityKit.Ability.Host;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionNetAdapterSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            ISessionFramePacketTransformSubFeature<BattleSessionFeature>
        {
            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

            public FramePacket TransformFramePacket(in FeatureModuleContext<BattleSessionFeature> ctx, FramePacket packet)
            {
                var f = ctx.Feature;
                if (f == null) return packet;

                return f._net.TransformFramePacket(f._session, f._netAdapter, packet);
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}
