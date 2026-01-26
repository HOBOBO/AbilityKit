using System;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Protocol
{
    public sealed class BodyOnlyFrameCodec : IFrameCodec
    {
        public static readonly BodyOnlyFrameCodec Instance = new BodyOnlyFrameCodec();

        private BodyOnlyFrameCodec()
        {
        }

        public IFrameDecoder CreateDecoder()
        {
            return new BodyOnlyFrameDecoder();
        }

        public ArraySegment<byte> Encode(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            if (payload.Array == null) payload = default;

            if (payload.Count != (int)header.PayloadLength)
            {
                header = new NetworkPacketHeader(header.Flags, header.OpCode, header.Seq, (uint)payload.Count);
            }

            checked
            {
                var total = NetworkPacketHeader.Size + payload.Count;
                var buffer = new byte[total];
                header.Write(buffer);

                if (payload.Count > 0)
                {
                    Buffer.BlockCopy(payload.Array, payload.Offset, buffer, NetworkPacketHeader.Size, payload.Count);
                }

                return new ArraySegment<byte>(buffer);
            }
        }

        private sealed class BodyOnlyFrameDecoder : IFrameDecoder
        {
            private ArraySegment<byte> _pending;
            private bool _hasPending;

            public void Reset()
            {
                _pending = default;
                _hasPending = false;
            }

            public void Append(ArraySegment<byte> bytes)
            {
                if (_hasPending) throw new InvalidOperationException("BodyOnlyFrameDecoder does not support buffering multiple frames.");
                _pending = bytes;
                _hasPending = true;
            }

            public bool TryRead(out NetworkPacketHeader header, out ArraySegment<byte> payload)
            {
                header = default;
                payload = default;

                if (!_hasPending) return false;
                _hasPending = false;

                if (_pending.Array == null) throw new InvalidOperationException("Invalid frame.");
                if (_pending.Count < NetworkPacketHeader.Size) throw new InvalidOperationException("Frame too small.");

                var span = new ReadOnlySpan<byte>(_pending.Array, _pending.Offset, _pending.Count);
                header = NetworkPacketHeader.Read(span.Slice(0, NetworkPacketHeader.Size));

                var payloadLen = _pending.Count - NetworkPacketHeader.Size;
                if (payloadLen != (int)header.PayloadLength) throw new InvalidOperationException("Payload length mismatch.");

                if (payloadLen == 0)
                {
                    payload = default;
                    return true;
                }

                var payloadCopy = new byte[payloadLen];
                span.Slice(NetworkPacketHeader.Size, payloadLen).CopyTo(payloadCopy);
                payload = new ArraySegment<byte>(payloadCopy);
                return true;
            }
        }
    }
}
