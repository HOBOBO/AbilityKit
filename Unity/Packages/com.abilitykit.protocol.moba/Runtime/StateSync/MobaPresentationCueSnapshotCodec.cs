using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    public enum PresentationCueStage : byte
    {
        None = 0,
        ConditionPassed = 1,
        ConditionFailed = 2,
        BeforeAction = 3,
        Executed = 4,
        Interrupted = 5,
        Skipped = 6,
        Started = 20,
        Ticked = 21,
        Refreshed = 22,
        StackChanged = 23,
        Expired = 24,
        Removed = 25,
        Completed = 26,
    }

    public enum MobaPresentationCueReplicationMode : byte
    {
        None = 0,
        ReliableForLifecycle = 1,
        UnreliableForTick = 2,
    }

    public enum MobaPresentationCuePredictionState : byte
    {
        None = 0,
        Predicted = 1,
        ServerConfirmed = 2,
        Corrected = 3,
        Rejected = 4,
    }

    public partial struct MobaPresentationCueSnapshotPayload
    {
        [MemoryPackConstructor]
        public MobaPresentationCueSnapshotPayload(MobaPresentationCueSnapshotEntry[] entries)
        {
            Entries = entries;
        }
    }

    public static class MobaPresentationCueSnapshotCodec
    {
        public static byte[] Serialize(MobaPresentationCueSnapshotEntry[] entries)
        {
            entries ??= Array.Empty<MobaPresentationCueSnapshotEntry>();
            var payload = new MobaPresentationCueSnapshotPayload { Entries = entries };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaPresentationCueSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaPresentationCueSnapshotEntry>();

            var p = MemoryPackSerializer.Deserialize<MobaPresentationCueSnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<MobaPresentationCueSnapshotEntry>();
        }
    }
}
