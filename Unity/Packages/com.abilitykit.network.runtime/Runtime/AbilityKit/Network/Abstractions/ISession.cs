using System;

namespace AbilityKit.Network.Abstractions
{
    public interface ISession : IDisposable
    {
        bool IsConnected { get; }

        event Action Connected;
        event Action Disconnected;
        event Action<Exception> Error;

        event Action<uint, uint, ArraySegment<byte>> PacketReceived;

        void Start();
        void Stop();

        void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0);
    }
}
