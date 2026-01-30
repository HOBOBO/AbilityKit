using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.WorldCapabilities
{
    public interface IWorldPlayerLifecycle
    {
        void OnPlayerJoined(PlayerId playerId);
        void OnPlayerLeft(PlayerId playerId);
    }
}
