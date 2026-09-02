using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    public enum MobaSkillAvailabilityState
    {
        Available = 0,
        CoolingDown = 1,
        Disabled = 2,
    }

    public partial struct MobaSkillStateSnapshotPayload
    {
        [MemoryPackConstructor]
        public MobaSkillStateSnapshotPayload(MobaSkillStateSnapshotEntry[] entries)
        {
            Entries = entries;
        }
    }

    public static class MobaSkillStateSnapshotCodec
    {
        public static byte[] Serialize(MobaSkillStateSnapshotEntry[] entries)
        {
            entries ??= Array.Empty<MobaSkillStateSnapshotEntry>();
            var payload = new MobaSkillStateSnapshotPayload { Entries = entries };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaSkillStateSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaSkillStateSnapshotEntry>();

            var p = MemoryPackSerializer.Deserialize<MobaSkillStateSnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<MobaSkillStateSnapshotEntry>();
        }
    }
}
