using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    public static class ShieldStateSnapshotDecoder
    {
        public static ShieldStateData[] Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return Array.Empty<ShieldStateData>();
            return ShieldStateSnapshotMapper.Map(MobaShieldStateSnapshotCodec.Deserialize(payload));
        }
    }
}
