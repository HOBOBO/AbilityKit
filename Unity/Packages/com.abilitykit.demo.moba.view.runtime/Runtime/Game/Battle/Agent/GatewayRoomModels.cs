namespace AbilityKit.Game.Battle.Agent
{
    public readonly struct GatewayCreateRoomResult
    {
        public readonly string RoomId;
        public readonly ulong NumericRoomId;

        public GatewayCreateRoomResult(string roomId, ulong numericRoomId)
        {
            RoomId = roomId;
            NumericRoomId = numericRoomId;
        }
    }

    public readonly struct GatewayJoinRoomResult
    {
        public readonly ulong NumericRoomId;
        public readonly string SnapshotJson;
        public readonly GatewayWorldStartAnchor WorldStartAnchor;

        public GatewayJoinRoomResult(ulong numericRoomId, string snapshotJson, in GatewayWorldStartAnchor worldStartAnchor)
        {
            NumericRoomId = numericRoomId;
            SnapshotJson = snapshotJson;
            WorldStartAnchor = worldStartAnchor;
        }
    }

    public readonly struct GatewayWorldStartAnchor
    {
        public readonly long StartServerTicks;
        public readonly long ServerTickFrequency;
        public readonly int StartFrame;
        public readonly double FixedDeltaSeconds;

        public GatewayWorldStartAnchor(long startServerTicks, long serverTickFrequency, int startFrame, double fixedDeltaSeconds)
        {
            StartServerTicks = startServerTicks;
            ServerTickFrequency = serverTickFrequency;
            StartFrame = startFrame;
            FixedDeltaSeconds = fixedDeltaSeconds;
        }
    }

    public readonly struct GatewayTimeSyncResult
    {
        public readonly long ClientSendTicks;
        public readonly long ServerNowTicks;
        public readonly long ServerTickFrequency;

        public GatewayTimeSyncResult(long clientSendTicks, long serverNowTicks, long serverTickFrequency)
        {
            ClientSendTicks = clientSendTicks;
            ServerNowTicks = serverNowTicks;
            ServerTickFrequency = serverTickFrequency;
        }
    }
}
