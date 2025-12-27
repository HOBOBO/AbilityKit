using System;

namespace AbilityKit.Ability.World.DI
{
    public interface IWorldScope : IWorldServices, IDisposable
    {
        IWorldServices Root { get; }
    }
}
