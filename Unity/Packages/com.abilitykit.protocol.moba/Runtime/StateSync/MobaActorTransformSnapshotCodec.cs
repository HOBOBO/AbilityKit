using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    [MemoryPackable]
    public partial struct MobaActorTransformSnapshotEntry
    {
        [MemoryPackOrder(0)] public int ActorId;
        [MemoryPackOrder(1)] public float X;
        [MemoryPackOrder(2)] public float Y;
        [MemoryPackOrder(3)] public float Z;
        [MemoryPackOrder(4)] public float ForwardX;
        [MemoryPackOrder(5)] public float ForwardY;
        [MemoryPackOrder(6)] public float ForwardZ;

        public MobaActorTransformSnapshotEntry(int actorId, float x, float y, float z)
            : this(actorId, x, y, z, 0f, 0f, 1f)
        {
        }

        public MobaActorTransformSnapshotEntry(int actorId, float x, float y, float z, float forwardX, float forwardY, float forwardZ)
        {
            ActorId = actorId;
            X = x;
            Y = y;
            Z = z;
            ForwardX = forwardX;
            ForwardY = forwardY;
            ForwardZ = forwardZ;
        }
    }

    [MemoryPackable]
    public partial struct MobaActorTransformSnapshotPayload
    {
        [MemoryPackOrder(0)] public MobaActorTransformSnapshotEntry[] Entries;

        [MemoryPackConstructor]
        public MobaActorTransformSnapshotPayload(MobaActorTransformSnapshotEntry[] entries)
        {
            Entries = entries;
        }
    }

    public static class MobaActorTransformSnapshotCodec
    {
        public static byte[] Serialize(MobaActorTransformSnapshotEntry[] entries)
        {
            entries ??= Array.Empty<MobaActorTransformSnapshotEntry>();
            var payload = new MobaActorTransformSnapshotPayload { Entries = entries };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaActorTransformSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaActorTransformSnapshotEntry>();

            var p = MemoryPackSerializer.Deserialize<MobaActorTransformSnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<MobaActorTransformSnapshotEntry>();
        }
    }
}
