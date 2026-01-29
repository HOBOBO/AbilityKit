using System;

namespace AbilityKit.Network.Protocol
{
    public sealed class NetworkFrameReader
    {
        private const int MinBufferSize = 4 + NetworkPacketHeader.Size;

        private byte[] _buffer;
        private int _count;

        public int MaxFrameLength { get; set; } = 4 * 1024 * 1024;

        public NetworkFrameReader(int initialCapacity = 8 * 1024)
        {
            if (initialCapacity < MinBufferSize) initialCapacity = MinBufferSize;
            _buffer = new byte[initialCapacity];
        }

        public void Reset()
        {
            _count = 0;
        }

        public void Append(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null) throw new ArgumentException("Invalid bytes.", nameof(bytes));
            if (bytes.Count == 0) return;

            EnsureCapacity(_count + bytes.Count);
            Buffer.BlockCopy(bytes.Array, bytes.Offset, _buffer, _count, bytes.Count);
            _count += bytes.Count;
        }

        public bool TryRead(out NetworkPacketHeader header, out ArraySegment<byte> payload)
        {
            header = default;
            payload = default;

            if (_count < 4) return false;

            if (!NetworkFrameCodec.TryReadLengthPrefix(new ReadOnlySpan<byte>(_buffer, 0, _count), out var frameLength))
            {
                return false;
            }

            if (frameLength > (uint)MaxFrameLength) throw new InvalidOperationException($"Frame too large: {frameLength}.");

            var totalSize = 4 + (int)frameLength;
            if (_count < totalSize) return false;

            var frameSpan = new ReadOnlySpan<byte>(_buffer, 0, totalSize);
            if (!NetworkFrameCodec.TryParseFrame(frameSpan, out header, out var payloadSpan))
            {
                throw new InvalidOperationException("Invalid frame.");
            }

            var payloadCopy = new byte[payloadSpan.Length];
            payloadSpan.CopyTo(payloadCopy);
            payload = new ArraySegment<byte>(payloadCopy);

            Consume(totalSize);
            return true;
        }

        private void Consume(int bytes)
        {
            if (bytes <= 0) return;
            if (bytes > _count) throw new ArgumentOutOfRangeException(nameof(bytes));

            var remain = _count - bytes;
            if (remain > 0)
            {
                Buffer.BlockCopy(_buffer, bytes, _buffer, 0, remain);
            }
            _count = remain;
        }

        private void EnsureCapacity(int needed)
        {
            if (needed <= _buffer.Length) return;

            var newSize = _buffer.Length;
            while (newSize < needed) newSize *= 2;
            var newBuf = new byte[newSize];
            if (_count > 0)
            {
                Buffer.BlockCopy(_buffer, 0, newBuf, 0, _count);
            }
            _buffer = newBuf;
        }
    }
}
