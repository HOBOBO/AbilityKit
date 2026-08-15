using System;

namespace AbilityKit.Network.Host
{
    /// <summary>
    /// One accepted, bidirectional peer. Implementations may use TCP, WebSocket,
    /// reliable UDP, an in-process queue, or a platform relay.
    /// </summary>
    public interface IServerChannel : IDisposable
    {
        string Id { get; }
        string RemoteEndpoint { get; }
        bool IsConnected { get; }

        event Action<ArraySegment<byte>> BytesReceived;
        event Action<IServerChannel> Closed;
        event Action<Exception> Error;

        void Send(ArraySegment<byte> bytes);
        void Close();
    }
}
