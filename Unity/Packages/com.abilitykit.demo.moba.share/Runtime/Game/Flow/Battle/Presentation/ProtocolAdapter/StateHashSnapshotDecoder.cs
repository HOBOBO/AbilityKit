using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    public static class StateHashSnapshotDecoder
    {
        public const int SupportedVersion = MobaStateHashSnapshotCodec.Version;

        public static StateHashData Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return default;
            }

            var wirePayload = MobaStateHashSnapshotCodec.Deserialize(payload);
            return StateHashSnapshotMapper.Map(in wirePayload);
        }
    }
}
