using System;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    public sealed class NetworkPipeline
    {
        private readonly List<INetworkMiddleware> _middlewares = new List<INetworkMiddleware>();

        public void Add(INetworkMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            _middlewares.Add(middleware);
        }

        public void ProcessInbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (terminal == null) throw new ArgumentNullException(nameof(terminal));

            InvokeInbound(0, context, header, payload, terminal);
        }

        public void ProcessOutbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (terminal == null) throw new ArgumentNullException(nameof(terminal));

            InvokeOutbound(0, context, header, payload, terminal);
        }

        private void InvokeInbound(int index, ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (index >= _middlewares.Count)
            {
                terminal(header, payload);
                return;
            }

            _middlewares[index].OnInbound(context, header, payload, (h, p) => InvokeInbound(index + 1, context, h, p, terminal));
        }

        private void InvokeOutbound(int index, ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (index >= _middlewares.Count)
            {
                terminal(header, payload);
                return;
            }

            _middlewares[index].OnOutbound(context, header, payload, (h, p) => InvokeOutbound(index + 1, context, h, p, terminal));
        }
    }
}
