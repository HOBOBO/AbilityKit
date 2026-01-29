using AbilityKit.Ability.Share;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public readonly struct WorldRollbackSnapshotEntry
    {
        [BinaryMember(0)] public readonly int Key;
        [BinaryMember(1)] public readonly byte[] Payload;

        public WorldRollbackSnapshotEntry(int key, byte[] payload)
        {
            Key = key;
            Payload = payload;
        }
    }

    public readonly struct WorldRollbackSnapshot
    {
        [BinaryMember(0)] public readonly int Version;
        [BinaryMember(1)] public readonly FrameIndex Frame;
        [BinaryMember(2)] public readonly WorldRollbackSnapshotEntry[] Entries;

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
            return BinaryObjectCodec.Encode(snapshot);
        }

        public static WorldRollbackSnapshot Deserialize(byte[] payload)
        {
            return BinaryObjectCodec.Decode<WorldRollbackSnapshot>(payload);
        }
    }
}
