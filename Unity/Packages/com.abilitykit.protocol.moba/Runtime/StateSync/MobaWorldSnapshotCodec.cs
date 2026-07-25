using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    [MemoryPackable]
    public partial struct MobaActorSnapshotEntry
    {
        [MemoryPackOrder(0)] public int ActorId;
        [MemoryPackOrder(1)] public float PositionX;
        [MemoryPackOrder(2)] public float PositionY;
        [MemoryPackOrder(3)] public float PositionZ;
        [MemoryPackOrder(4)] public float Rotation;
        [MemoryPackOrder(5)] public float VelocityX;
        [MemoryPackOrder(6)] public float VelocityZ;
        [MemoryPackOrder(7)] public float Hp;
        [MemoryPackOrder(8)] public float HpMax;
        [MemoryPackOrder(9)] public int TeamId;

        public MobaActorSnapshotEntry(int actorId, float positionX, float positionY, float positionZ, float rotation, float velocityX, float velocityZ, float hp, float hpMax, int teamId)
        {
            ActorId = actorId;
            PositionX = positionX;
            PositionY = positionY;
            PositionZ = positionZ;
            Rotation = rotation;
            VelocityX = velocityX;
            VelocityZ = velocityZ;
            Hp = hp;
            HpMax = hpMax;
            TeamId = teamId;
        }
    }

    [MemoryPackable]
    public partial struct MobaWorldSnapshotPayload
    {
        [MemoryPackOrder(0)] public ulong WorldId;
        [MemoryPackOrder(1)] public int Frame;
        [MemoryPackOrder(2)] public long Timestamp;
        [MemoryPackOrder(3)] public bool IsFullSnapshot;
        [MemoryPackOrder(4)] public MobaActorSnapshotEntry[] Actors;

        [MemoryPackConstructor]
        public MobaWorldSnapshotPayload(ulong worldId, int frame, long timestamp, bool isFullSnapshot, MobaActorSnapshotEntry[] actors)
        {
            WorldId = worldId;
            Frame = frame;
            Timestamp = timestamp;
            IsFullSnapshot = isFullSnapshot;
            Actors = actors;
        }
    }

    public static class MobaWorldSnapshotCodec
    {
        public static byte[] Serialize(in MobaWorldSnapshotPayload payload)
        {
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaWorldSnapshotPayload Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return new MobaWorldSnapshotPayload(0, 0, 0, false, Array.Empty<MobaActorSnapshotEntry>());
            }

            var snapshot = MemoryPackSerializer.Deserialize<MobaWorldSnapshotPayload>(payload);
            snapshot.Actors ??= Array.Empty<MobaActorSnapshotEntry>();
            return snapshot;
        }
    }
}
