using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    [MemoryPackable]
    public partial struct MobaStateHashSnapshotPayload
    {
        [MemoryPackOrder(0)] public int Version;
        [MemoryPackOrder(1)] public int Frame;
        [MemoryPackOrder(2)] public uint Hash;

        public MobaStateHashSnapshotPayload(int version, int frame, uint hash)
        {
            Version = version;
            Frame = frame;
            Hash = hash;
        }
    }

    public static class MobaStateHashSnapshotCodec
    {
        public const int Version = 1;

        public static byte[] Serialize(int frame, uint hash)
        {
            var payload = new MobaStateHashSnapshotPayload
            {
                Version = Version,
                Frame = frame,
                Hash = hash
            };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaStateHashSnapshotPayload Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return default;

            return MemoryPackSerializer.Deserialize<MobaStateHashSnapshotPayload>(payload);
        }
    }
}
