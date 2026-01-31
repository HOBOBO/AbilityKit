using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host
{
    public interface IFrameSyncWorldSession
    {
        WorldId WorldId { get; }

        bool Join(IServerConnection connection, PlayerId playerId);
        bool Leave(IServerConnection connection, PlayerId playerId);

        bool SubmitInput(IServerConnection connection, PlayerInputCommand input);
    }
}
