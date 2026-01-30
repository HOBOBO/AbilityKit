using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host
{
    public interface IFrameSyncSessionHost
    {
        bool TryGetFrameSyncWorldSession(WorldId worldId, out IFrameSyncWorldSession session);
    }
}
