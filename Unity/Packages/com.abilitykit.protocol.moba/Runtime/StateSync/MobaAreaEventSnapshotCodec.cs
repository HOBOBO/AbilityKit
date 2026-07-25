using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    public enum AreaEventKind : byte
    {
        Spawn = 1,
        Expire = 2,
    }

    [MemoryPackable]
    public partial struct MobaAreaEventSnapshotEntry
    {
        [MemoryPackOrder(0)] public int Kind;
        [MemoryPackOrder(1)] public int AreaId;
        [MemoryPackOrder(2)] public int OwnerActorId;
        [MemoryPackOrder(3)] public int TemplateId;
        [MemoryPackOrder(4)] public float X;
        [MemoryPackOrder(5)] public float Y;
        [MemoryPackOrder(6)] public float Z;
        [MemoryPackOrder(7)] public float Radius;

        public MobaAreaEventSnapshotEntry(int kind, int areaId, int ownerActorId, int templateId, float x, float y, float z, float radius)
        {
            Kind = kind;
            AreaId = areaId;
            OwnerActorId = ownerActorId;
            TemplateId = templateId;
            X = x;
            Y = y;
            Z = z;
            Radius = radius;
        }
    }

    [MemoryPackable]
    public partial struct MobaAreaEventSnapshotPayload
    {
        [MemoryPackOrder(0)] public MobaAreaEventSnapshotEntry[] Entries;

        [MemoryPackConstructor]
        public MobaAreaEventSnapshotPayload(MobaAreaEventSnapshotEntry[] entries)
        {
            Entries = entries;
        }
    }

    public static class MobaAreaEventSnapshotCodec
    {
        public static byte[] Serialize(MobaAreaEventSnapshotEntry[] entries)
        {
            entries ??= Array.Empty<MobaAreaEventSnapshotEntry>();
            var payload = new MobaAreaEventSnapshotPayload { Entries = entries };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaAreaEventSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaAreaEventSnapshotEntry>();

            var p = MemoryPackSerializer.Deserialize<MobaAreaEventSnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<MobaAreaEventSnapshotEntry>();
        }
    }
}
