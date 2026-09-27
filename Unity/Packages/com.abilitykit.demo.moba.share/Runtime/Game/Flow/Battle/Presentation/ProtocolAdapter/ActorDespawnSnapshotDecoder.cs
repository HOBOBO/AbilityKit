using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Decodes MOBA wire payloads directly into platform-neutral actor despawns.
    /// </summary>
    public static class ActorDespawnSnapshotDecoder
    {
        public static ActorDespawnData[] Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<ActorDespawnData>();
            }

            return ActorDespawnSnapshotMapper.Map(
                MobaActorDespawnSnapshotCodec.Deserialize(payload));
        }
    }
}
