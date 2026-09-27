using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Decodes MOBA wire payloads directly into platform-neutral presentation contracts.
    /// </summary>
    public static class PresentationCueSnapshotDecoder
    {
        public static PresentationCueData[] Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<PresentationCueData>();
            }

            return PresentationCueSnapshotMapper.Map(
                MobaPresentationCueSnapshotCodec.Deserialize(payload));
        }
    }
}
