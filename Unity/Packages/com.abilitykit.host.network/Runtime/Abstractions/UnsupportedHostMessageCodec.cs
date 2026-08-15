using System;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Ability.Host.Network
{
    /// <summary>
    /// Codec for receive-only host compositions. Sending a <see cref="ServerMessage"/>
    /// remains an explicit error in <see cref="HostNetworkServerConnection"/>.
    /// </summary>
    public sealed class UnsupportedHostMessageCodec : IHostMessageCodec
    {
        public static readonly UnsupportedHostMessageCodec Instance = new UnsupportedHostMessageCodec();

        private UnsupportedHostMessageCodec()
        {
        }

        public bool TryEncode(
            ServerMessage message,
            out NetworkPacketHeader header,
            out ArraySegment<byte> payload)
        {
            header = default;
            payload = default;
            return false;
        }
    }
}
