using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Battle.DemoLegacy.Requests
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
