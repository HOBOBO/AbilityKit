using System;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Abstractions
{
    public interface IWorld : IDisposable
    {
        WorldId Id { get; }
        string WorldType { get; }
        IWorldServices Services { get; }

        void Initialize();
        void Tick(float deltaTime);
    }
}
