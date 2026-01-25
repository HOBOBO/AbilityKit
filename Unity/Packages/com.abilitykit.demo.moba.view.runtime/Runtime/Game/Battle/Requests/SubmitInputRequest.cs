using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Battle.Requests
{
    public readonly struct SubmitInputRequest
    {
        public readonly WorldId WorldId;
        public readonly PlayerInputCommand Input;

        public SubmitInputRequest(WorldId worldId, PlayerInputCommand input)
        {
            WorldId = worldId;
            Input = input;
        }
    }
}
