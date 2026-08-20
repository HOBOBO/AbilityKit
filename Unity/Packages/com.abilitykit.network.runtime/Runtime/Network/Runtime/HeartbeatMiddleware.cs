using System;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    /// <summary>Middleware contract used by the connection runtime to observe heartbeat traffic.</summary>
    public interface INetworkHeartbeatMiddleware : INetworkMiddleware
    {
        event Action HeartbeatReceived;
    }

    public sealed class HeartbeatMiddleware : INetworkHeartbeatMiddleware
    {
        private readonly uint _heartbeatOpCode;

        public HeartbeatMiddleware(uint heartbeatOpCode)
        {
            _heartbeatOpCode = heartbeatOpCode;
        }

        public event Action HeartbeatReceived;

        public void OnInbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next)
        {
            if ((header.Flags & NetworkPacketFlags.Heartbeat) != 0 || header.OpCode == _heartbeatOpCode)
            {
                context.Dispatcher.Post(() => HeartbeatReceived?.Invoke());
                return;
            }

            next(header, payload);
        }

        public void OnOutbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next)
        {
            next(header, payload);
        }
    }
}
