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
                if (f == null || f._netAdapter == null || f._session == null) return packet;

                var frame = packet.Frame.Value;
                if (f._session.RemoteInputFrames != null
                    && f._session.RemoteSnapshotFrames != null
                    && f._session.RemoteInputFrames.TryGet(frame, out var inputFrame)
                    && f._session.RemoteSnapshotFrames.TryGet(frame, out var snapshotFrame))
                {
                    return f._netAdapter.ProcessAndFeed(packet.WorldId, inputFrame, snapshotFrame);
                }

                return f._netAdapter.ProcessAndFeed(packet);
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}
