using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.Server;

namespace AbilityKit.Game.Battle.Requests
{
    public readonly struct LeaveWorldRequest
    {
        public readonly WorldId WorldId;
        public readonly PlayerId PlayerId;

        public readonly int OpCode;
        public readonly byte[] Payload;

        public LeaveWorldRequest(WorldId worldId, PlayerId playerId, int opCode = 0, byte[] payload = null)
        {
            WorldId = worldId;
            PlayerId = playerId;
            OpCode = opCode;
            Payload = payload;
        }
    }
}
