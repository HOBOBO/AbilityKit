
using MemoryPack;

namespace AbilityKit.Protocol.Moba.GatewayTimeSync
{
    [MemoryPackable]
    public readonly partial struct WireTimeSyncReq
    {
        [MemoryPackOrder(0)] public readonly long ClientSendTicks;

        public WireTimeSyncReq(long clientSendTicks)
        {
            ClientSendTicks = clientSendTicks;
        }
    }

    [MemoryPackable]
    public readonly partial struct WireTimeSyncRes
    {
        [MemoryPackOrder(0)] public readonly long ClientSendTicks;
        [MemoryPackOrder(1)] public readonly long ServerNowTicks;
        [MemoryPackOrder(2)] public readonly long ServerTickFrequency;

        public WireTimeSyncRes(long clientSendTicks, long serverNowTicks, long serverTickFrequency)
        {
            ClientSendTicks = clientSendTicks;
            ServerNowTicks = serverNowTicks;
            ServerTickFrequency = serverTickFrequency;
        }
    }
}
