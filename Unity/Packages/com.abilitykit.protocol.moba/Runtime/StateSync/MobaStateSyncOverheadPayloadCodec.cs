using System;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    /// <summary>
    /// 同帧状态投影与头顶完整基线的载体；保留原 HFSM 负载，不接管逻辑恢复。
    /// </summary>
    public static class MobaStateSyncOverheadPayloadCodec
    {
        /// <summary>
        /// 正式目录中头顶状态组合载荷的标识。
        /// </summary>
        public const int OpCode = 4021;
        /// <summary>
        /// 本载体版本；原逻辑载荷版本单独保留。
        /// </summary>
        public const int SchemaVersion = 1;

        /// <summary>
        /// 编码原投影和同批完整指示器、护盾集合，合法编码空集仍须携带。
        /// </summary>
        public static byte[] Encode(int opCode, int schemaVersion, byte[] projection, byte[] indicators, byte[] shields)
        {
            var payload = new MobaStateSyncOverheadPayload { ProjectionOpCode = opCode,
                ProjectionSchemaVersion = schemaVersion, Projection = projection ?? Array.Empty<byte>(),
                Indicators = indicators, Shields = shields };
            Validate(in payload);
            return MemoryPackSerializer.Serialize(payload);
        }

        /// <summary>
        /// 解码并拒绝嵌套自身、缺失完整集合或非法指示器，失败不伪装为空基线。
        /// </summary>
        public static MobaStateSyncOverheadPayload Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > 4194304) throw new ArgumentException("Invalid overhead carrier size.");
            var payload = MemoryPackSerializer.Deserialize<MobaStateSyncOverheadPayload>(bytes);
            Validate(in payload); return payload;
        }

        private static void Validate(in MobaStateSyncOverheadPayload payload)
        {
            if (payload.ProjectionOpCode == OpCode || payload.ProjectionSchemaVersion < 0 ||
                payload.Projection == null || payload.Indicators == null || payload.Indicators.Length == 0 ||
                payload.Shields == null || payload.Shields.Length == 0)
                throw new ArgumentException("Incomplete overhead carrier.");
            MobaActorIndicatorSnapshotCodec.Deserialize(payload.Indicators);
            MobaShieldStateSnapshotCodec.Deserialize(payload.Shields);
        }
    }
}
