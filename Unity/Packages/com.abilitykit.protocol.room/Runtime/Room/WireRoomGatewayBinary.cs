using System;
using System.Buffers;
using MemoryPack;

namespace AbilityKit.Protocol.Room
{
    public static class WireRoomGatewayBinary
    {
        public static ArraySegment<byte> Serialize<T>(in T value)
        {
            var bytes = MemoryPackSerializer.Serialize(value);
            return new ArraySegment<byte>(bytes);
        }

        /// <summary>
        /// Serializes into a caller-owned reusable buffer. Consume the returned segment before
        /// using the buffer again.
        /// </summary>
        public static ArraySegment<byte> SerializeTransient<T>(
            in T value,
            ReusableMemoryPackSerializationBuffer buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            return new ArraySegment<byte>(buffer.SerializeTransient(in value));
        }

        public static T Deserialize<T>(ArraySegment<byte> payload)
        {
            if (payload.Array == null || payload.Count == 0)
                return default;

            var span = new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count);
            return MemoryPackSerializer.Deserialize<T>(span);
        }

        public static T Deserialize<T>(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
                return default;

            return MemoryPackSerializer.Deserialize<T>(payload);
        }
    }

    /// <summary>
    /// Reusable, non-thread-safe MemoryPack output buffer for synchronous serialization paths.
    /// Returned arrays remain valid only until the next serialization on this instance.
    /// </summary>
    public sealed class ReusableMemoryPackSerializationBuffer : IBufferWriter<byte>
    {
        private byte[] _writeBuffer = Array.Empty<byte>();
        private byte[] _exactBuffer = Array.Empty<byte>();
        private int _writtenCount;

        public int WrittenCount => _writtenCount;

        public byte[] SerializeTransient<T>(in T value)
        {
            _writtenCount = 0;
            MemoryPackSerializer.Serialize(this, value);
            if (_writtenCount == 0)
            {
                _exactBuffer = Array.Empty<byte>();
                return _exactBuffer;
            }

            if (_exactBuffer.Length != _writtenCount)
            {
                _exactBuffer = new byte[_writtenCount];
            }

            Buffer.BlockCopy(_writeBuffer, 0, _exactBuffer, 0, _writtenCount);
            return _exactBuffer;
        }

        public void Advance(int count)
        {
            if (count < 0 || _writtenCount > _writeBuffer.Length - count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _writtenCount += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _writeBuffer.AsMemory(_writtenCount);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _writeBuffer.AsSpan(_writtenCount);
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
            if (sizeHint == 0) sizeHint = 1;

            var required = checked(_writtenCount + sizeHint);
            if (required <= _writeBuffer.Length)
            {
                return;
            }

            var doubled = _writeBuffer.Length == 0 ? 256 : checked(_writeBuffer.Length * 2);
            var nextLength = Math.Max(required, doubled);
            var next = new byte[nextLength];
            if (_writtenCount > 0)
            {
                Buffer.BlockCopy(_writeBuffer, 0, next, 0, _writtenCount);
            }

            _writeBuffer = next;
        }
    }
}
