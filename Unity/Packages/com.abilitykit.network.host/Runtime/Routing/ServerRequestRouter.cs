using System;
using System.Collections.Generic;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Host
{
    /// <summary>Explicit op-code router with no reflection or global registration.</summary>
    public sealed class ServerRequestRouter : IServerRequestHandler
    {
        private readonly Dictionary<uint, IServerRequestHandler> _handlers =
            new Dictionary<uint, IServerRequestHandler>();
        private IServerRequestHandler _fallback;

        public ServerRequestRouter Register(uint opCode, IServerRequestHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handlers.ContainsKey(opCode))
                throw new InvalidOperationException($"A handler is already registered for op-code {opCode}.");
            _handlers.Add(opCode, handler);
            return this;
        }

        public ServerRequestRouter Register(
            uint opCode,
            Action<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return Register(opCode, new DelegateHandler(handler));
        }

        public ServerRequestRouter SetFallback(IServerRequestHandler handler)
        {
            _fallback = handler;
            return this;
        }

        public bool Remove(uint opCode)
        {
            return _handlers.Remove(opCode);
        }

        public void Handle(
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload)
        {
            if (_handlers.TryGetValue(header.OpCode, out var handler))
            {
                handler.Handle(session, header, payload);
                return;
            }
            _fallback?.Handle(session, header, payload);
        }

        private sealed class DelegateHandler : IServerRequestHandler
        {
            private readonly Action<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>> _handler;

            public DelegateHandler(Action<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>> handler)
            {
                _handler = handler;
            }

            public void Handle(
                IServerNetworkSession session,
                NetworkPacketHeader header,
                ArraySegment<byte> payload)
            {
                _handler(session, header, payload);
            }
        }
    }
}
