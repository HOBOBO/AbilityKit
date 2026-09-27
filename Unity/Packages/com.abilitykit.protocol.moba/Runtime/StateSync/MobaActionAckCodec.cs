using System;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    public enum MobaActionAckReason
    {
        None = 0,
        InvalidInput = 1,
        ResourceInsufficient = 2,
        Cooldown = 3,
        Controlled = 4,
        TargetInvalid = 5,
        Interrupted = 6,
        OwnershipChanged = 7,
        EntityVersionChanged = 8,
        ServerPolicy = 9,
    }

    public partial struct MobaActionAckPayload
    {
        [MemoryPackConstructor]
        public MobaActionAckPayload(MobaActionAckEntry[] entries)
        {
            Entries = entries;
        }
    }

    public static class MobaActionAckCodec
    {
        public static byte[] Serialize(MobaActionAckEntry[] entries)
        {
            entries ??= Array.Empty<MobaActionAckEntry>();
            return MemoryPackSerializer.Serialize(new MobaActionAckPayload { Entries = entries });
        }

        public static MobaActionAckEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaActionAckEntry>();

            var decoded = MemoryPackSerializer.Deserialize<MobaActionAckPayload>(payload);
            return decoded.Entries ?? Array.Empty<MobaActionAckEntry>();
        }
    }
}
