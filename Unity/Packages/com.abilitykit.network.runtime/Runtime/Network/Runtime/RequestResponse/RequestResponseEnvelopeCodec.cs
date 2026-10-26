using System;
using System.Buffers.Binary;
using System.Text;
using AbilityKit.Network.Runtime.TcpGateway;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// Decodes the payload carried by a response packet. The packet framing and request
    /// correlation stay in <see cref="RequestClient"/>; an envelope is an opt-in wire policy.
    /// </summary>
    public interface IRequestResponseEnvelopeCodec
    {
        ArraySegment<byte> Decode(uint opCode, uint seq, ArraySegment<byte> payload);
    }

    /// <summary>Encodes successful server response bodies for the selected wire envelope.</summary>
    public interface IRequestResponseEnvelopeEncoder
    {
        ArraySegment<byte> EncodeSuccess(uint opCode, uint seq, ArraySegment<byte> payload);
    }

    /// <summary>Passes response payloads through without imposing an application envelope.</summary>
    public sealed class RawRequestResponseEnvelopeCodec : IRequestResponseEnvelopeCodec, IRequestResponseEnvelopeEncoder
    {
        public static RawRequestResponseEnvelopeCodec Instance { get; } = new();

        private RawRequestResponseEnvelopeCodec()
        {
        }

        public ArraySegment<byte> Decode(uint opCode, uint seq, ArraySegment<byte> payload) => payload;

        public ArraySegment<byte> EncodeSuccess(uint opCode, uint seq, ArraySegment<byte> payload) => payload;
    }

    /// <summary>Decodes the four-byte status envelope used by the legacy TCP gateway.</summary>
    public sealed class TcpGatewayRequestResponseEnvelopeCodec : IRequestResponseEnvelopeCodec, IRequestResponseEnvelopeEncoder
    {
        public static TcpGatewayRequestResponseEnvelopeCodec Instance { get; } = new();

        private TcpGatewayRequestResponseEnvelopeCodec()
        {
        }

        public ArraySegment<byte> Decode(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            var decoded = TcpGatewayResponseCodec.Decode(payload);
            if (decoded.StatusCode != TcpGatewayStatusCode.Ok)
            {
                var message = DecodeErrorMessage(decoded.Payload);
                var detail = string.IsNullOrWhiteSpace(message) ? string.Empty : $" message={message}";
                throw new InvalidOperationException(
                    $"Gateway response error. statusCode={decoded.StatusCode} opCode={opCode} seq={seq}{detail}");
            }

            return decoded.Payload;
        }

        public ArraySegment<byte> EncodeSuccess(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            var bytes = new byte[sizeof(int) + payload.Count];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, (int)TcpGatewayStatusCode.Ok);
            if (payload.Array != null && payload.Count > 0)
                Buffer.BlockCopy(payload.Array, payload.Offset, bytes, sizeof(int), payload.Count);
            return new ArraySegment<byte>(bytes);
        }

        private static string DecodeErrorMessage(ArraySegment<byte> payload)
        {
            if (payload.Array == null || payload.Count <= 0) return string.Empty;
            var count = Math.Min(payload.Count, 1024);
            var message = Encoding.UTF8.GetString(payload.Array, payload.Offset, count);
            return message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
