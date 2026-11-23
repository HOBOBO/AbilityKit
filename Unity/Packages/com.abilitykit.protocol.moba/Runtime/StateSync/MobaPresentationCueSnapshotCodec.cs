using System;
using System.Buffers.Binary;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    /// <summary>
    /// MOBA 状态快照内层负载：保留原投影负载，并附带同一快照帧的完整活动 Cue 集。
    /// </summary>
    public static class MobaStateSyncCuePayloadCodec
    {
        /// <summary>
        /// 仅标识状态快照的内层组合负载，不是 Cue 快照路由或同步模式标识。
        /// </summary>
        public const int OpCode = 11018;
        /// <summary>
        /// 外层投影版本保持在固定基线信封的保留版本之下；原版本随负载搬运。
        /// </summary>
        public const int CarrierSchemaVersion = 3;
        private const int HeaderBytes = 12;

        /// <summary>
        /// 编码原投影 OpCode、版本、长度、原投影负载与非空 Cue 集负载。
        /// </summary>
        public static byte[] Encode(int projectionOpCode, int schemaVersion, byte[] projection, byte[] cueBaseline)
        {
            if (cueBaseline == null || cueBaseline.Length == 0)
                throw new ArgumentException("A complete Cue baseline is required.", nameof(cueBaseline));
            var projectionLength = projection?.Length ?? 0;
            var result = new byte[checked(HeaderBytes + projectionLength + cueBaseline.Length)];
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(0, 4), projectionOpCode);
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4, 4), schemaVersion);
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(8, 4), projectionLength);
            if (projectionLength > 0) projection.AsSpan().CopyTo(result.AsSpan(HeaderBytes));
            cueBaseline.AsSpan().CopyTo(result.AsSpan(HeaderBytes + projectionLength));
            return result;
        }

        /// <summary>
        /// 解包内层负载；截断、越界长度或空 Cue 负载不予解包。
        /// </summary>
        public static bool TryDecode(byte[] payload, out int projectionOpCode, out int schemaVersion,
            out byte[] projection, out byte[] cueBaseline)
        {
            projectionOpCode = 0;
            schemaVersion = 0;
            projection = Array.Empty<byte>();
            cueBaseline = Array.Empty<byte>();
            if (payload == null || payload.Length <= HeaderBytes) return false;
            var projectionLength = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(8, 4));
            if (projectionLength < 0 || projectionLength >= payload.Length - HeaderBytes) return false;
            projectionOpCode = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4));
            schemaVersion = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));
            if (projectionOpCode == OpCode || schemaVersion < 0) return false;
            projection = payload.AsSpan(HeaderBytes, projectionLength).ToArray();
            cueBaseline = payload.AsSpan(HeaderBytes + projectionLength).ToArray();
            return true;
        }
    }

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
