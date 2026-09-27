using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    public static class ProjectileEventSnapshotDecoder
    {
        public static ProjectileEventData[] Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<ProjectileEventData>();
            }

            return ProjectileEventSnapshotMapper.Map(
                MobaProjectileEventSnapshotCodec.Deserialize(payload));
        }
    }
}
