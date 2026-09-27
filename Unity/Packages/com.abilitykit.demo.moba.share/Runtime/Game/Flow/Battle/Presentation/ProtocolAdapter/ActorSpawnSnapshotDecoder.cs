using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Decodes MOBA wire payloads directly into platform-neutral actor spawns.
    /// </summary>
    public static class ActorSpawnSnapshotDecoder
    {
        public static ActorSpawnData[] Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<ActorSpawnData>();
            }

            return ActorSpawnSnapshotMapper.Map(
                MobaActorSpawnSnapshotCodec.Deserialize(payload));
        }
    }
}
