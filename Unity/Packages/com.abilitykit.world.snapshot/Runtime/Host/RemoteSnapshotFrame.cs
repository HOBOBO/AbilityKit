using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Host
{
    public readonly struct RemoteSnapshotFrame
    {
        public RemoteSnapshotFrame(FrameIndex frame, ISnapshotEnvelope[] envelopes)
        {
            Frame = frame;
            Envelopes = envelopes;
        }

        public FrameIndex Frame { get; }

        public ISnapshotEnvelope[] Envelopes { get; }
    }
}
