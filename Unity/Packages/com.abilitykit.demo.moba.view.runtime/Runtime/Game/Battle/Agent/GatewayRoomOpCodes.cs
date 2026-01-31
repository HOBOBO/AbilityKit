namespace AbilityKit.Game.Battle.Agent
{
    public readonly struct GatewayRoomOpCodes
    {
        public readonly uint CreateRoom;
        public readonly uint JoinRoom;

        public GatewayRoomOpCodes(uint createRoom, uint joinRoom)
        {
            CreateRoom = createRoom;
            JoinRoom = joinRoom;
        }
    }
}
