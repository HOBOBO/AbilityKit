namespace AbilityKit.Protocol.Moba.GatewayTimeSync
{
    public readonly struct WireTimeSyncReq
    {
        public readonly long ClientSendTicks;

        public WireTimeSyncReq(long clientSendTicks)
        {
            ClientSendTicks = clientSendTicks;
        }
    }

    public readonly struct WireTimeSyncRes
    {
        public readonly long ClientSendTicks;
        public readonly long ServerNowTicks;
        public readonly long ServerTickFrequency;

        public WireTimeSyncRes(long clientSendTicks, long serverNowTicks, long serverTickFrequency)
        {
            ClientSendTicks = clientSendTicks;
            ServerNowTicks = serverNowTicks;
            ServerTickFrequency = serverTickFrequency;
        }
    }
}
