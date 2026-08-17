using System;
using System.Buffers;
using System.Collections.Generic;
using MemoryPack;

namespace AbilityKit.Protocol.Room
{
    [MemoryPackable]
    internal partial class ReusableWireStateSyncSnapshotPush
    {
        [MemoryPackOrder(0)] public ulong WorldId;
        [MemoryPackOrder(1)] public int Frame;
        [MemoryPackOrder(2)] public double Timestamp;
        [MemoryPackOrder(3)] public bool IsFullSnapshot;
        [MemoryPackOrder(4)] public List<WireStateSyncActorSnapshot>? Actors;
        [MemoryPackOrder(5)] public int PayloadOpCode;
        [MemoryPackOrder(6)] public byte[]? Payload = Array.Empty<byte>();
        [MemoryPackOrder(7)] public long ServerTicks;
        [MemoryPackOrder(8)] public long EventWatermark;
        [MemoryPackOrder(9)] public int SchemaVersion;
        [MemoryPackOrder(10)] public List<int>? RemovedActorIds;
        [MemoryPackOrder(11)] public string EventEpoch = string.Empty;
    }

    public sealed class WireStateSyncSnapshotPushDecodeBuffer
    {
        private ReusableWireStateSyncSnapshotPush? _buffer = new ReusableWireStateSyncSnapshotPush();

        public WireStateSyncSnapshotPush Decode(ArraySegment<byte> payload)
        {
            if (payload.Array == null || payload.Count == 0)
            {
                return default;
            }

            return Decode(new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
        }

        public WireStateSyncSnapshotPush Decode(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
            {
                return default;
            }

            MemoryPackSerializer.Deserialize(payload, ref _buffer);
            var value = _buffer ?? new ReusableWireStateSyncSnapshotPush();
            _buffer = value;
            return new WireStateSyncSnapshotPush
            {
                WorldId = value.WorldId,
                Frame = value.Frame,
                Timestamp = value.Timestamp,
                IsFullSnapshot = value.IsFullSnapshot,
                Actors = value.Actors,
                PayloadOpCode = value.PayloadOpCode,
                Payload = value.Payload,
                ServerTicks = value.ServerTicks,
                EventWatermark = value.EventWatermark,
                SchemaVersion = value.SchemaVersion,
                RemovedActorIds = value.RemovedActorIds,
                EventEpoch = value.EventEpoch
            };
        }
    }

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

        /// <summary>
        /// Serializes a state-sync push whose payload is a slice of a reusable buffer. The wire
        /// layout remains identical to <see cref="WireStateSyncSnapshotPush"/>.
        /// </summary>
        public static ArraySegment<byte> SerializeTransient(
            in WireStateSyncSnapshotPush value,
            ArraySegment<byte> payload,
            ReusableMemoryPackSerializationBuffer buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
#if UNITY_5_3_OR_NEWER
            var compatibleValue = value;
            compatibleValue.Payload = CopySegment(payload);
            return SerializeTransient(in compatibleValue, buffer);
#else
            return buffer.SerializeTransientSegment(new WireStateSyncSnapshotPushTransient(in value, payload));
#endif
        }

#if UNITY_5_3_OR_NEWER
        private static byte[] CopySegment(ArraySegment<byte> payload)
        {
            if (payload.Array == null || payload.Count == 0)
            {
                return Array.Empty<byte>();
            }

            if (payload.Offset == 0 && payload.Count == payload.Array.Length)
            {
                return payload.Array;
            }

            var copy = new byte[payload.Count];
            Buffer.BlockCopy(payload.Array, payload.Offset, copy, 0, payload.Count);
            return copy;
        }
#endif

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
            SerializeTransientSegment(in value);
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

        /// <summary>
        /// Serializes into the reusable backing buffer without copying to an exact-length array.
        /// Consume the returned segment before the next serialization on this instance.
        /// </summary>
        public ArraySegment<byte> SerializeTransientSegment<T>(in T value)
        {
            _writtenCount = 0;
            MemoryPackSerializer.Serialize(this, value);
            return new ArraySegment<byte>(_writeBuffer, 0, _writtenCount);
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

#if !UNITY_5_3_OR_NEWER
    internal readonly struct WireStateSyncSnapshotPushTransient : IMemoryPackable<WireStateSyncSnapshotPushTransient>
    {
        private readonly WireStateSyncSnapshotPush _value;
        private readonly ArraySegment<byte> _payload;

        public WireStateSyncSnapshotPushTransient(in WireStateSyncSnapshotPush value, ArraySegment<byte> payload)
        {
            _value = value;
            _payload = payload;
        }

        public static void RegisterFormatter()
        {
            if (!MemoryPackFormatterProvider.IsRegistered<WireStateSyncSnapshotPushTransient>())
            {
                MemoryPackFormatterProvider.Register(
                    new MemoryPack.Formatters.MemoryPackableFormatter<WireStateSyncSnapshotPushTransient>());
            }
        }

        static void IMemoryPackable<WireStateSyncSnapshotPushTransient>.Serialize<TBufferWriter>(
            ref MemoryPackWriter<TBufferWriter> writer,
            scoped ref WireStateSyncSnapshotPushTransient value)
        {
            ref readonly var push = ref value._value;
            writer.WriteUnmanagedWithObjectHeader(12, push.WorldId, push.Frame, push.Timestamp, push.IsFullSnapshot);
            MemoryPack.Formatters.ListFormatter.SerializePackable(ref writer, push.Actors);
            writer.WriteUnmanaged(push.PayloadOpCode);
            writer.WriteUnmanagedSpan(value._payload.AsSpan());
            writer.WriteUnmanaged(push.ServerTicks, push.EventWatermark, push.SchemaVersion);
            writer.WriteValue(push.RemovedActorIds);
            writer.WriteString(push.EventEpoch);
        }

        static void IMemoryPackable<WireStateSyncSnapshotPushTransient>.Deserialize(
            ref MemoryPackReader reader,
            scoped ref WireStateSyncSnapshotPushTransient value)
        {
            throw new NotSupportedException("Transient wire views are serialization-only.");
        }
    }
#endif
}
