using System;

namespace AbilityKit.Network.Host
{
    /// <summary>
    /// Accepts server channels. Endpoint configuration belongs to the concrete
    /// implementation so this contract does not assume an IP socket transport.
    /// Ownership transfers to the event subscriber after ChannelAccepted returns.
    /// Stop ends acceptance but must not close transferred channels; Dispose may
    /// release channels that were never transferred.
    /// </summary>
    public interface IChannelListener : IDisposable
    {
        bool IsListening { get; }
        string Endpoint { get; }

        event Action<IServerChannel> ChannelAccepted;
        event Action<Exception> Error;

        void Start();
        void Stop();
    }
}
