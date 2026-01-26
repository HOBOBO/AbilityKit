using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Battle.DemoLegacy.Requests
{
    public readonly struct JoinWorldRequest
    {
        public readonly WorldId WorldId;
        public readonly PlayerId PlayerId;
        public readonly int OpCode;
        public readonly byte[] Payload;

        public JoinWorldRequest(WorldId worldId, PlayerId playerId, int opCode = 0, byte[] payload = null)
        {
            WorldId = worldId;
            PlayerId = playerId;
            OpCode = opCode;
            Payload = payload;
        }
    }
}
