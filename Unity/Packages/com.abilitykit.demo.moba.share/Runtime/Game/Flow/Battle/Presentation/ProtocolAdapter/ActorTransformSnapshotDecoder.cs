using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Decodes MOBA wire payloads directly into platform-neutral actor transforms.
    /// </summary>
    public static class ActorTransformSnapshotDecoder
    {
        public static ActorTransformData[] Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<ActorTransformData>();
            }

            return ActorTransformSnapshotMapper.Map(
                MobaActorTransformSnapshotCodec.Deserialize(payload));
        }
    }
}
