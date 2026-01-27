using System;

namespace AbilityKit.Network.Protocol
{
    [Flags]
    public enum NetworkPacketFlags : ushort
    {
        None = 0,
        Compressed = 1 << 0,
        Encrypted = 1 << 1,
        Heartbeat = 1 << 2,
        Request = 1 << 3,
        Response = 1 << 4,
        ServerPush = 1 << 5
    }
}
