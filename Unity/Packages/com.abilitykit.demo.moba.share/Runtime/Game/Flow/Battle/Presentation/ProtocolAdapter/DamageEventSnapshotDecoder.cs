using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Decodes MOBA wire payloads directly into platform-neutral damage contracts.
    /// </summary>
    public static class DamageEventSnapshotDecoder
    {
        public static DamageEventData[] Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<DamageEventData>();
            }

            return DamageEventSnapshotMapper.Map(
                MobaDamageEventSnapshotCodec.Deserialize(payload));
        }
    }
}

