using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    public partial struct MobaActorDespawnSnapshotEntry
    {
        public MobaActorDespawnSnapshotEntry(int actorId, byte reason)
        {
            ActorId = actorId;
            Reason = reason;
        }
    }

    public partial struct MobaActorDespawnSnapshotPayload
    {
        [MemoryPackConstructor]
        public MobaActorDespawnSnapshotPayload(MobaActorDespawnSnapshotEntry[] entries)
        {
            Entries = entries;
        }
    }

    public static class MobaActorDespawnSnapshotCodec
    {
        public static byte[] Serialize(MobaActorDespawnSnapshotEntry[] entries)
        {
            entries ??= Array.Empty<MobaActorDespawnSnapshotEntry>();
            var payload = new MobaActorDespawnSnapshotPayload { Entries = entries };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaActorDespawnSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaActorDespawnSnapshotEntry>();

            var p = MemoryPackSerializer.Deserialize<MobaActorDespawnSnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<MobaActorDespawnSnapshotEntry>();
        }
    }
}
