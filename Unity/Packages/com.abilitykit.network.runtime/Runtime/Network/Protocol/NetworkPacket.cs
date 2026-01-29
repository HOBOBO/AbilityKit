using System;

namespace AbilityKit.Network.Protocol
{
    public readonly struct NetworkPacket
    {
        public readonly NetworkPacketHeader Header;
        public readonly ArraySegment<byte> Payload;

        public NetworkPacket(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            Header = header;
            Payload = payload;
        }
    }
}
