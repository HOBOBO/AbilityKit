using System;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.SettledFrames
{
    /// <summary>
    /// MOBA 权威结算流版本及独立网关操作码，与 MOBA 协议 catalog 对应。
    /// </summary>
    public static class SettledFrameProtocol
    {
        public const int Version = 2;
        public const string CapabilityProfile = "Moba.SettledFrameSyncV2";
        public const int Subscribe = 2005;
        public const int CatchUp = 2006;
        public const int FramePushed = 9020;
        public const int MaxPayloadBytes = 4 * 1024 * 1024;

        /// <summary>
        /// 将结算协议 DTO 编码成请求载荷。
        /// </summary>
        public static byte[] Encode<T>(T value) => MemoryPackSerializer.Serialize(value);

        /// <summary>
        /// 解码有界的结算协议响应或推送载荷。
        /// </summary>
        public static T Decode<T>(ArraySegment<byte> payload)
        {
            if (payload.Count == 0 || payload.Count > MaxPayloadBytes)
                throw new FormatException("Invalid settled frame payload size.");
            return MemoryPackSerializer.Deserialize<T>(payload.AsSpan());
        }
    }
}
