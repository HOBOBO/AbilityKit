using System;
using System.Buffers.Binary;

namespace AbilityKit.Network.Runtime.TcpGateway
{
    public static class TcpGatewayResponseCodec
    {
        public static (TcpGatewayStatusCode StatusCode, ArraySegment<byte> Payload) Decode(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count < 4)
            {
                throw new ArgumentException("Invalid response bytes.", nameof(bytes));
            }

            var status = (TcpGatewayStatusCode)BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
            if (bytes.Count == 4)
            {
                return (status, default);
            }

            return (status, bytes.Slice(4));
        }

        private static ReadOnlySpan<byte> AsSpan(this ArraySegment<byte> seg, int offset, int count)
        {
            if (seg.Array == null) return ReadOnlySpan<byte>.Empty;
            return new ReadOnlySpan<byte>(seg.Array, seg.Offset + offset, count);
        }

        private static ArraySegment<byte> Slice(this ArraySegment<byte> seg, int offset)
        {
            if (seg.Array == null) return default;
            if (offset <= 0) return seg;
            if (offset >= seg.Count) return default;
            return new ArraySegment<byte>(seg.Array, seg.Offset + offset, seg.Count - offset);
        }
    }
}
