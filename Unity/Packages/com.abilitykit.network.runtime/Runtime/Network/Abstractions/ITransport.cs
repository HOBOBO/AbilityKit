using System;

namespace AbilityKit.Network.Abstractions
{
    public interface ITransport : IDisposable
    {
        bool IsConnected { get; }

        event Action Connected;
        event Action Disconnected;
        event Action<Exception> Error;
        event Action<ArraySegment<byte>> BytesReceived;

        void Connect(string host, int port);
        void Close();

        void Send(ArraySegment<byte> bytes);
    }
}
