using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Host
{
    public interface IWorldHost
    {
        IWorldManager Worlds { get; }

        IWorld CreateWorld(WorldCreateOptions options);
        bool DestroyWorld(WorldId id);
        bool TryGetWorld(WorldId id, out IWorld world);

        void Tick(float deltaTime);
    }
}
