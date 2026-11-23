#nullable enable

using System;
using System.Buffers.Binary;

namespace AbilityKit.Protocol.Room
{
    /// <summary>
    /// 固定基线订阅使用独立入口，快照外壳保持原布局。
    /// </summary>
    public static class FixedBaselineStateSyncProtocol
    {
        /// <summary>
        /// 表示快照负载是固定基线信封，而不是玩法投影负载本身。
        /// </summary>
        public const int SchemaVersion = 4;

        /// <summary>
        /// 房间快照消息的最大序列化字节数，与 room 协议目录一致。
        /// </summary>
        public const int MaximumSnapshotBytes = 4 * 1024 * 1024;

        private const int HeaderBytes = 36;

        /// <summary>
        /// 编码嵌入旧外壳 Payload 的固定头及不变的玩法负载字节。
        /// </summary>
        public static byte[] Encode(Guid generation, long snapshotId, long baseSnapshotId,
            int projectionSchemaVersion, byte[]? projectionPayload)
        {
            var payload = projectionPayload ?? Array.Empty<byte>();
            if (payload.Length > MaximumSnapshotBytes - HeaderBytes)
                throw new ArgumentOutOfRangeException(nameof(projectionPayload));
            var buffer = new byte[HeaderBytes + payload.Length];
            generation.TryWriteBytes(buffer.AsSpan(0, 16));
            BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(16, 8), snapshotId);
            BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(24, 8), baseSnapshotId);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(32, 4), projectionSchemaVersion);
            payload.CopyTo(buffer, HeaderBytes);
            return buffer;
        }

        /// <summary>
        /// 校验信封身份、长度和内层版本，隔离复制保留的玩法负载。
        /// </summary>
        public static bool TryDecode(byte[]? buffer, out Guid generation, out long snapshotId,
            out long baseSnapshotId, out int projectionSchemaVersion, out byte[] projectionPayload)
        {
            generation = default;
            snapshotId = baseSnapshotId = 0;
            projectionSchemaVersion = 0;
            projectionPayload = Array.Empty<byte>();
            if (buffer == null || buffer.Length < HeaderBytes || buffer.Length > MaximumSnapshotBytes)
                return false;

            generation = new Guid(buffer.AsSpan(0, 16));
            snapshotId = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(16, 8));
            baseSnapshotId = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(24, 8));
            projectionSchemaVersion = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(32, 4));
            if (generation == Guid.Empty || snapshotId <= 0 || baseSnapshotId < 0 || baseSnapshotId >= snapshotId ||
                projectionSchemaVersion < 0 || projectionSchemaVersion >= SchemaVersion)
                return false;

            projectionPayload = buffer.AsSpan(HeaderBytes).ToArray();
            return true;
        }
    }
}
