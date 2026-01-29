using System;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Protocol
{
    public sealed class LengthPrefixedFrameCodec : IFrameCodec
    {
        public static readonly LengthPrefixedFrameCodec Instance = new LengthPrefixedFrameCodec();

        private LengthPrefixedFrameCodec()
        {
        }

        public IFrameDecoder CreateDecoder()
        {
            return new LengthPrefixedFrameDecoder();
        }

        public ArraySegment<byte> Encode(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            if (payload.Array == null) payload = default;

            if (payload.Count != (int)header.PayloadLength)
            {
                header = new NetworkPacketHeader(header.Flags, header.OpCode, header.Seq, (uint)payload.Count);
            }

            var frameSize = NetworkFrameCodec.GetFrameSize(payload.Count);
            var buffer = new byte[frameSize];

            var payloadSpan = payload.Array == null
                ? ReadOnlySpan<byte>.Empty
                : new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count);

            NetworkFrameCodec.WriteFrame(buffer, header, payloadSpan);
            return new ArraySegment<byte>(buffer);
        }

        private sealed class LengthPrefixedFrameDecoder : IFrameDecoder
        {
            private readonly NetworkFrameReader _reader = new NetworkFrameReader();

            public void Reset() => _reader.Reset();

            public void Append(ArraySegment<byte> bytes) => _reader.Append(bytes);

            public bool TryRead(out NetworkPacketHeader header, out ArraySegment<byte> payload)
            {
                return _reader.TryRead(out header, out payload);
            }
        }
    }
}
