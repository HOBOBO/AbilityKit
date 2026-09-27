using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    public static class AreaEventSnapshotDecoder
    {
        public static AreaEventData[] Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<AreaEventData>();
            }

            return AreaEventSnapshotMapper.Map(
                MobaAreaEventSnapshotCodec.Deserialize(payload));
        }
    }
}
