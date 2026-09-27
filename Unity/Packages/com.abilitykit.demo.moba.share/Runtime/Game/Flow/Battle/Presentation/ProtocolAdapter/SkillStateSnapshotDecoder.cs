using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Decodes MOBA wire payloads directly into platform-neutral skill states.
    /// </summary>
    public static class SkillStateSnapshotDecoder
    {
        public static SkillStateData[] Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<SkillStateData>();
            }

            return SkillStateSnapshotMapper.Map(
                MobaSkillStateSnapshotCodec.Deserialize(payload));
        }
    }
}
