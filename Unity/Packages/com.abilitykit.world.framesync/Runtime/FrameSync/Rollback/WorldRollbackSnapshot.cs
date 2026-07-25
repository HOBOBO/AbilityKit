using MemoryPack;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public readonly partial struct WorldRollbackSnapshotEntry
    {
        [MemoryPackOrder(0)] public readonly int Key;
        [MemoryPackOrder(1)] public readonly byte[] Payload;

        [MemoryPackConstructor]
        public WorldRollbackSnapshotEntry(int key, byte[] payload)
        {
            Key = key;
            Payload = payload;
        }
    }

    [MemoryPackable]
    public readonly partial struct WorldRollbackSnapshot
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly FrameIndex Frame;
        [MemoryPackOrder(2)] public readonly WorldRollbackSnapshotEntry[] Entries;

        [MemoryPackConstructor]
        public WorldRollbackSnapshot(int version, FrameIndex frame, WorldRollbackSnapshotEntry[] entries)
        {
            Version = version;
            Frame = frame;
            Entries = entries;
        }
    }

    public static class WorldRollbackSnapshotCodec
    {
        public const int CurrentVersion = 1;

        public static byte[] Serialize(in WorldRollbackSnapshot snapshot)
        {
            return MemoryPackSerializer.Serialize(snapshot);
        }

        public static WorldRollbackSnapshot Deserialize(byte[] payload)
        {
            return MemoryPackSerializer.Deserialize<WorldRollbackSnapshot>(payload);
        }
    }
}
