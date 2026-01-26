using System;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Abstractions
{
    public interface ISessionContext
    {
        ISession Session { get; }

        IDispatcher Dispatcher { get; }

        void Send(NetworkPacketHeader header, ArraySegment<byte> payload);
    }
}
