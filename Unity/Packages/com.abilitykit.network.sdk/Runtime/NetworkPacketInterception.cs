#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Network.Sdk.Observability
{
    /// <summary>A copy of an outbound packet waiting for an explicit editor decision.</summary>
    public sealed class PendingNetworkPacket
    {
        internal PendingNetworkPacket(long id, string connectionId, string catalogId, int generation,
            NetworkPacketHeader header, byte[] payload)
        {
            Id = id;
            ConnectionId = connectionId;
            CatalogId = catalogId;
            Generation = generation;
            Header = header;
            Payload = payload;
            QueuedAtUtc = DateTimeOffset.UtcNow;
        }

        public long Id { get; }
        public string ConnectionId { get; }
        public string CatalogId { get; }
        public int Generation { get; }
        public NetworkPacketHeader Header { get; }
        public byte[] Payload { get; }
        public DateTimeOffset QueuedAtUtc { get; }
    }

    /// <summary>Gates every outbound packet in an attached connection's middleware pipeline.</summary>
    public sealed class NetworkPacketInterception
    {
        private const int MaxPendingPackets = 8192;
        private static readonly Lazy<NetworkPacketInterception> Shared =
            new Lazy<NetworkPacketInterception>(() => new NetworkPacketInterception());
        private readonly object _gate = new object();
        private readonly Dictionary<long, QueuedPacket> _pending = new Dictionary<long, QueuedPacket>();
        private long _nextId;
        private bool _enabled;

        public static NetworkPacketInterception Default => Shared.Value;

        public bool Enabled
        {
            get { lock (_gate) return _enabled; }
            set { lock (_gate) _enabled = value; }
        }

        public int PendingCount
        {
            get { lock (_gate) return _pending.Count; }
        }

        public IDisposable Attach(NetworkSdkClient client, string catalogId = "")
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (!(client.Connection is ConnectionManager connection))
                throw new NotSupportedException("Packet interception requires a ConnectionManager pipeline.");
            return Attach(connection, catalogId);
        }

        public IDisposable Attach(ConnectionManager connection, string catalogId = "") =>
            new Binding(this, connection ?? throw new ArgumentNullException(nameof(connection)), catalogId);

        public IReadOnlyList<PendingNetworkPacket> Snapshot()
        {
            lock (_gate)
            {
                var result = new List<PendingNetworkPacket>(_pending.Count);
                foreach (var queued in _pending.Values)
                {
                    var packet = queued.Packet;
                    result.Add(new PendingNetworkPacket(packet.Id, packet.ConnectionId, packet.CatalogId,
                        packet.Generation, packet.Header, (byte[])packet.Payload.Clone()));
                }
                result.Sort((a, b) => a.Id.CompareTo(b.Id));
                return result;
            }
        }

        public bool Release(long id, byte[]? replacementPayload = null)
        {
            QueuedPacket queued;
            lock (_gate)
            {
                if (!_pending.TryGetValue(id, out queued!)) return false;
                _pending.Remove(id);
            }

            if (!queued.Binding.IsCurrent(queued.Generation)) return false;
            var payload = replacementPayload == null
                ? queued.Packet.Payload : (byte[])replacementPayload.Clone();
            var original = queued.Packet.Header;
            var header = new NetworkPacketHeader(original.Flags, original.OpCode,
                original.Seq, checked((uint)payload.Length));
            queued.Next(header, new ArraySegment<byte>(payload));
            return true;
        }

        public bool Discard(long id)
        {
            lock (_gate) return _pending.Remove(id);
        }

        public int DiscardAll()
        {
            lock (_gate)
            {
                var count = _pending.Count;
                _pending.Clear();
                return count;
            }
        }

        private bool Enqueue(Binding binding, int generation, NetworkPacketHeader header,
            ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next)
        {
            lock (_gate)
            {
                if (!_enabled)
                {
                    return false;
                }
                if (_pending.Count >= MaxPendingPackets)
                    throw new InvalidOperationException("Network interception queue is full.");
                var bytes = payload.Array == null ? Array.Empty<byte>() : payload.ToArray();
                var packet = new PendingNetworkPacket(++_nextId, binding.ConnectionId, binding.CatalogId,
                    generation, header, bytes);
                _pending.Add(packet.Id, new QueuedPacket(packet, binding, generation, next));
                return true;
            }
        }

        private void RemoveBinding(Binding binding)
        {
            lock (_gate)
            {
                var ids = new List<long>();
                foreach (var pair in _pending)
                    if (ReferenceEquals(pair.Value.Binding, binding)) ids.Add(pair.Key);
                foreach (var id in ids) _pending.Remove(id);
            }
        }

        private sealed class QueuedPacket
        {
            public QueuedPacket(PendingNetworkPacket packet, Binding binding, int generation,
                Action<NetworkPacketHeader, ArraySegment<byte>> next)
            {
                Packet = packet;
                Binding = binding;
                Generation = generation;
                Next = next;
            }
            public PendingNetworkPacket Packet { get; }
            public Binding Binding { get; }
            public int Generation { get; }
            public Action<NetworkPacketHeader, ArraySegment<byte>> Next { get; }
        }

        private sealed class Binding : IDisposable
        {
            private readonly NetworkPacketInterception _owner;
            private readonly ConnectionManager _connection;
            private readonly string _catalogId;
            private int _generation;
            private bool _disposed;

            public Binding(NetworkPacketInterception owner, ConnectionManager connection, string catalogId)
            {
                _owner = owner;
                _connection = connection;
                _catalogId = catalogId ?? string.Empty;
                _connection.PipelineCreated += OnPipelineCreated;
                _connection.Disconnected += OnDisconnected;
                if (_connection.Pipeline != null) OnPipelineCreated(_connection.Pipeline);
            }

            public string ConnectionId => _connection.GetDiagnosticsSnapshot().ConnectionId;
            public string CatalogId => _catalogId;
            public bool IsDisposed => _disposed;
            public bool IsCurrent(int generation) => !_disposed &&
                _generation == generation && _connection.Pipeline != null &&
                _connection.IsConnected;

            private void OnPipelineCreated(NetworkPipeline pipeline)
            {
                _owner.RemoveBinding(this);
                _generation = _connection.GetDiagnosticsSnapshot().Generation;
                pipeline.AddFirst(new Middleware(_owner, this, _generation));
            }

            private void OnDisconnected() => _owner.RemoveBinding(this);

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _connection.PipelineCreated -= OnPipelineCreated;
                _connection.Disconnected -= OnDisconnected;
                _owner.RemoveBinding(this);
            }
        }

        private sealed class Middleware : INetworkMiddleware
        {
            private readonly NetworkPacketInterception _owner;
            private readonly Binding _binding;
            private readonly int _generation;

            public Middleware(NetworkPacketInterception owner, Binding binding, int generation)
            {
                _owner = owner;
                _binding = binding;
                _generation = generation;
            }

            public void OnInbound(ISessionContext context, NetworkPacketHeader header,
                ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next) =>
                next(header, payload);

            public void OnOutbound(ISessionContext context, NetworkPacketHeader header,
                ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next)
            {
                if (_binding.IsDisposed || !_owner.Enabled)
                {
                    next(header, payload);
                    return;
                }
                if (!_owner.Enqueue(_binding, _generation, header, payload, next))
                    next(header, payload);
            }
        }
    }
}
