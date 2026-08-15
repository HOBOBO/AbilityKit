using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Host
{
    public sealed class AsyncServerRequestRouter : IAsyncServerRequestHandler
    {
        private readonly Dictionary<uint, IAsyncServerRequestHandler> _handlers =
            new Dictionary<uint, IAsyncServerRequestHandler>();
        private IAsyncServerRequestHandler _fallback;

        public AsyncServerRequestRouter Register(uint opCode, IAsyncServerRequestHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handlers.ContainsKey(opCode))
                throw new InvalidOperationException($"A handler is already registered for op-code {opCode}.");
            _handlers.Add(opCode, handler);
            return this;
        }

        public AsyncServerRequestRouter Register(
            uint opCode,
            Func<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>, CancellationToken, Task> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return Register(opCode, new DelegateHandler(handler));
        }

        public AsyncServerRequestRouter SetFallback(IAsyncServerRequestHandler handler)
        {
            _fallback = handler;
            return this;
        }

        public bool Remove(uint opCode) => _handlers.Remove(opCode);

        public Task HandleAsync(
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            CancellationToken cancellationToken)
        {
            if (_handlers.TryGetValue(header.OpCode, out var handler))
                return handler.HandleAsync(session, header, payload, cancellationToken);
            return _fallback?.HandleAsync(session, header, payload, cancellationToken)
                ?? Task.CompletedTask;
        }

        private sealed class DelegateHandler : IAsyncServerRequestHandler
        {
            private readonly Func<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>, CancellationToken, Task> _handler;

            public DelegateHandler(
                Func<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>, CancellationToken, Task> handler)
            {
                _handler = handler;
            }

            public Task HandleAsync(
                IServerNetworkSession session,
                NetworkPacketHeader header,
                ArraySegment<byte> payload,
                CancellationToken cancellationToken)
            {
                return _handler(session, header, payload, cancellationToken);
            }
        }
    }
}
