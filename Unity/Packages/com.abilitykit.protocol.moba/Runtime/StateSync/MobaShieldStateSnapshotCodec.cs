using System;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    public partial struct MobaShieldStateSnapshotPayload
    {
        [MemoryPackConstructor]
        public MobaShieldStateSnapshotPayload(MobaShieldStateSnapshotEntry[] entries)
        {
            Entries = entries;
        }
    }

    public static class MobaShieldStateSnapshotCodec
    {
        public static byte[] Serialize(MobaShieldStateSnapshotEntry[] entries)
        {
            entries ??= Array.Empty<MobaShieldStateSnapshotEntry>();
            return MemoryPackSerializer.Serialize(new MobaShieldStateSnapshotPayload { Entries = entries });
        }

        public static MobaShieldStateSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return Array.Empty<MobaShieldStateSnapshotEntry>();
            var decoded = MemoryPackSerializer.Deserialize<MobaShieldStateSnapshotPayload>(payload);
            return decoded.Entries ?? Array.Empty<MobaShieldStateSnapshotEntry>();
        }
    }
}
