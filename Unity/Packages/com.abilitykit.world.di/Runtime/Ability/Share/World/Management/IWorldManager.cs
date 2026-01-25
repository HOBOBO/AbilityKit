using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World.Management
{
    public interface IWorldManager
    {
        IReadOnlyDictionary<WorldId, IWorld> Worlds { get; }

        IWorld Create(WorldCreateOptions options);
        bool TryGet(WorldId id, out IWorld world);
        bool Destroy(WorldId id);

        void Tick(float deltaTime);
        void DisposeAll();
    }
}
