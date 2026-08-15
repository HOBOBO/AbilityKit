using System.Net;

namespace AbilityKit.Network.Host.Tcp
{
    public sealed class TcpChannelListenerOptions
    {
        public IPAddress Address { get; set; } = IPAddress.Any;
        public int Port { get; set; }
        public int Backlog { get; set; } = 128;
        public int ReceiveBufferSize { get; set; } = 64 * 1024;
        public bool NoDelay { get; set; } = true;
    }
}
