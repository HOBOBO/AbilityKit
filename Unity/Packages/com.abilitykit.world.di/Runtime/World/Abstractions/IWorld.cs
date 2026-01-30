using System;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Abstractions
{
    public interface IWorld : IDisposable
    {
        WorldId Id { get; }
        string WorldType { get; }
        IWorldResolver Services { get; }

        void Initialize();
        void Tick(float deltaTime);
    }
}
