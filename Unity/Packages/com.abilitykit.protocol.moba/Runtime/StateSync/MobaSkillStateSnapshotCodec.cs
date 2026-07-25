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

    [MemoryPackable]
    public partial struct MobaSkillStateSnapshotEntry
    {
        [MemoryPackOrder(0)] public int ActorId;
        [MemoryPackOrder(1)] public int Slot;
        [MemoryPackOrder(2)] public int SkillId;
        [MemoryPackOrder(3)] public int Level;
        [MemoryPackOrder(4)] public int CooldownTotalMs;
        [MemoryPackOrder(5)] public int CooldownRemainingMs;
        [MemoryPackOrder(6)] public long CooldownEndTimeMs;
        [MemoryPackOrder(7)] public long ServerTimeMs;
        [MemoryPackOrder(8)] public MobaSkillAvailabilityState Availability;
        [MemoryPackOrder(9)] public int DisableReason;
    }

    [MemoryPackable]
    public partial struct MobaSkillStateSnapshotPayload
    {
        [MemoryPackOrder(0)] public MobaSkillStateSnapshotEntry[] Entries;

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
