using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.GameModes
{
    public interface IGameMode
    {
        IGameModeSession CreateSession(IWorldHost host, WorldCreateOptions options);
    }
}
