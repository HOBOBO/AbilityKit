#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;

namespace AbilityKit.Network.Sdk
{
    public interface INetworkProtocolTransport
    {
        event Action<uint, ArraySegment<byte>>? ServerPushReceived;

        Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }

    public interface INetworkProtocolCodec
    {
        ArraySegment<byte> Serialize<T>(in T value);
        T Deserialize<T>(ArraySegment<byte> payload);
    }

    /// <summary>Typed protocol access scoped to one transport and codec.</summary>
    public sealed class NetworkProtocolAgent : IDisposable
    {
        private readonly INetworkProtocolTransport _transport;
        private readonly INetworkProtocolCodec _codec;
        private readonly object _gate = new object();
        private readonly Dictionary<uint, Action<ArraySegment<byte>>[]> _handlers =
            new Dictionary<uint, Action<ArraySegment<byte>>[]>();
        private bool _disposed;

        public NetworkProtocolAgent(INetworkProtocolTransport transport, INetworkProtocolCodec codec)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _transport.ServerPushReceived += OnServerPushReceived;
        }

        public event Action<uint, Exception>? PushDispatchFailed;

        public Task<TResponse> RequestAsync<TRequest, TResponse>(
            ProtocolCatalogRegistry catalog,
            string catalogId,
            string requestMessageId,
            TRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var message = Resolve<TRequest>(catalog, catalogId, requestMessageId,
                ProtocolDirection.ClientToServer, ProtocolPacketKind.Request);
            if (string.IsNullOrEmpty(message.ResponseId))
                throw new InvalidOperationException($"Protocol request '{requestMessageId}' has no response mapping.");
            var response = Resolve<TResponse>(catalog, catalogId, message.ResponseId,
                ProtocolDirection.ServerToClient, ProtocolPacketKind.Response);
            if (response.OpCode != message.OpCode)
                throw new InvalidOperationException($"Protocol response '{message.ResponseId}' has a different OpCode.");
            return RequestAsync<TRequest, TResponse>(message.OpCode, request, timeout, cancellationToken);
        }

        public async Task<TResponse> RequestAsync<TRequest, TResponse>(
            uint opCode,
            TRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            var payload = _codec.Serialize(in request);
            var response = await _transport.SendRequestAsync(
                opCode, payload, timeout, cancellationToken).ConfigureAwait(false);
            return _codec.Deserialize<TResponse>(response);
        }

        public IDisposable Subscribe<TPush>(uint opCode, Action<TPush> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Action<ArraySegment<byte>> dispatch = payload => handler(_codec.Deserialize<TPush>(payload));
            lock (_gate)
            {
                ThrowIfDisposed();
                if (!_handlers.TryGetValue(opCode, out var handlers))
                    handlers = Array.Empty<Action<ArraySegment<byte>>>();
                var updated = new Action<ArraySegment<byte>>[handlers.Length + 1];
                Array.Copy(handlers, updated, handlers.Length);
                updated[handlers.Length] = dispatch;
                _handlers[opCode] = updated;
            }
            return new Subscription(this, opCode, dispatch);
        }

        public IDisposable Subscribe<TPush>(
            ProtocolCatalogRegistry catalog,
            string catalogId,
            string messageId,
            Action<TPush> handler)
        {
            var message = Resolve<TPush>(catalog, catalogId, messageId,
                ProtocolDirection.ServerToClient, ProtocolPacketKind.Push);
            return Subscribe(message.OpCode, handler);
        }

        private static ProtocolMessageDefinition Resolve<T>(
            ProtocolCatalogRegistry catalog,
            string catalogId,
            string messageId,
            ProtocolDirection direction,
            ProtocolPacketKind kind)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!catalog.TryGetMessage(catalogId, messageId, out var message) || message == null ||
                (message.Direction != direction && message.Direction != ProtocolDirection.Bidirectional) ||
                message.Kind != kind ||
                !string.Equals(message.PayloadType, typeof(T).FullName, StringComparison.Ordinal))
                throw new InvalidOperationException($"Protocol message '{catalogId}/{messageId}' does not match {typeof(T).FullName}.");
            return message;
        }

        private void OnServerPushReceived(uint opCode, ArraySegment<byte> payload)
        {
            Action<ArraySegment<byte>>[] handlers;
            lock (_gate)
            {
                if (_disposed || !_handlers.TryGetValue(opCode, out var registered)) return;
                handlers = registered;
            }

            foreach (var handler in handlers)
            {
                try { handler(payload); }
                catch (Exception exception)
                {
                    try { PushDispatchFailed?.Invoke(opCode, exception); }
                    catch { }
                }
            }
        }

        private void Unsubscribe(uint opCode, Action<ArraySegment<byte>> handler)
        {
            lock (_gate)
            {
                if (!_handlers.TryGetValue(opCode, out var handlers)) return;
                var index = Array.IndexOf(handlers, handler);
                if (index < 0) return;
                if (handlers.Length == 1)
                {
                    _handlers.Remove(opCode);
                    return;
                }
                var updated = new Action<ArraySegment<byte>>[handlers.Length - 1];
                Array.Copy(handlers, 0, updated, 0, index);
                Array.Copy(handlers, index + 1, updated, index, handlers.Length - index - 1);
                _handlers[opCode] = updated;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _handlers.Clear();
            }
            _transport.ServerPushReceived -= OnServerPushReceived;
            PushDispatchFailed = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NetworkProtocolAgent));
        }

        private sealed class Subscription : IDisposable
        {
            private NetworkProtocolAgent? _owner;
            private readonly uint _opCode;
            private readonly Action<ArraySegment<byte>> _handler;

            public Subscription(NetworkProtocolAgent owner, uint opCode, Action<ArraySegment<byte>> handler)
            {
                _owner = owner;
                _opCode = opCode;
                _handler = handler;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _owner, null)?.Unsubscribe(_opCode, _handler);
            }
        }
    }
}
