using System;

namespace AbilityKit.Network.Abstractions
{
    public interface IConnection : IDisposable
    {
        ConnectionState State { get; }
        bool IsConnected { get; }

        event Action Connected;
        event Action Disconnected;
        event Action<Exception> Error;

        event Action<uint, uint, ArraySegment<byte>> PacketReceived;

        event Action<uint, ArraySegment<byte>> ServerPushReceived;

        event Action<string, string> Kicked;

        void Open(string host, int port);
        void Close();

        void Tick(float deltaTime);

        void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0);
    }
}
