using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public interface IFrameSyncInputHub
    {
        bool SubmitInput(ServerClientId clientId, WorldId worldId, PlayerInputCommand input);
    }
}
