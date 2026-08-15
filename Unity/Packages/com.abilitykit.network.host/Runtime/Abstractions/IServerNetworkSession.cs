using System;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Host
{
    public interface IServerNetworkSession : IDisposable
    {
        string Id { get; }
        IServerChannel Channel { get; }
        ServerSessionContext Context { get; }
        bool IsConnected { get; }
        long OpenedTimestamp { get; }
        long LastActivityTimestamp { get; }
        long BytesReceivedCount { get; }
        long BytesSentCount { get; }
        long PacketsReceivedCount { get; }
        long PacketsSentCount { get; }

        event Action<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>> PacketReceived;
        event Action<IServerNetworkSession> Closed;
        event Action<IServerNetworkSession, Exception> Error;

        void Start();
        void Stop();
        void Send(NetworkPacketHeader header, ArraySegment<byte> payload);
        void SendResponse(uint opCode, uint seq, ArraySegment<byte> payload);
        void SendPush(uint opCode, ArraySegment<byte> payload);
        void Close();
    }
}
