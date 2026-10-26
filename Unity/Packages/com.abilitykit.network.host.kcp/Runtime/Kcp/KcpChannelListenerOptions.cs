using System.Net;

namespace AbilityKit.Network.Host.Kcp
{
    public sealed class KcpChannelListenerOptions
    {
        public uint ConversationId { get; set; } = 1;
        public IPAddress Address { get; set; } = IPAddress.Any;
        public int Port { get; set; }
        public int ReceiveBufferSize { get; set; } = 64 * 1024;
        public int UpdateIntervalMilliseconds { get; set; } = 10;
    }
}
