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

        public GatewayJoinRoomResult(ulong numericRoomId, string snapshotJson)
        {
            NumericRoomId = numericRoomId;
            SnapshotJson = snapshotJson;
        }
    }
}
