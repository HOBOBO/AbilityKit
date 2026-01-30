using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host
{
    public interface IPlayerSessionHost
    {
        bool JoinWorld(ServerClientId clientId, WorldId worldId, PlayerId playerId);
        bool LeaveWorld(ServerClientId clientId, WorldId worldId, PlayerId playerId);

        bool SubmitInput(ServerClientId clientId, WorldId worldId, PlayerInputCommand input);
    }
}
