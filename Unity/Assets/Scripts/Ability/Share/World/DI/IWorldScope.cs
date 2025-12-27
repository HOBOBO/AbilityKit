using System;

namespace AbilityKit.Ability.World.DI
{
    public interface IWorldScope : IWorldResolver, IDisposable
    {
        IWorldResolver Root { get; }
    }
}
