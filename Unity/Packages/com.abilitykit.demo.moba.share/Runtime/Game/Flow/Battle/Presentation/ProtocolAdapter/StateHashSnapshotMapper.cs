using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    public static class StateHashSnapshotMapper
    {
        public static StateHashData Map(in MobaStateHashSnapshotPayload payload)
        {
            return new StateHashData(
                payload.Version,
                payload.Frame,
                payload.Hash);
        }
    }
}
