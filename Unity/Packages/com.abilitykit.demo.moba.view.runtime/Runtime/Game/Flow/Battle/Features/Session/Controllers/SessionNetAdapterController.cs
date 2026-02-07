using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionNetAdapterController
        {
            public FramePacket TransformFramePacket(BattleLogicSession session, BattleSessionNetAdapter adapter, FramePacket packet)
            {
                if (adapter == null || session == null) return packet;

                var frame = packet.Frame.Value;
                if (session.RemoteInputFrames != null
                    && session.RemoteSnapshotFrames != null
                    && session.RemoteInputFrames.TryGet(frame, out var inputFrame)
                    && session.RemoteSnapshotFrames.TryGet(frame, out var snapshotFrame))
                {
                    return adapter.ProcessAndFeed(packet.WorldId, inputFrame, snapshotFrame);
                }

                return adapter.ProcessAndFeed(packet);
            }
        }
    }
}
