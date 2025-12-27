using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.World.DI
{
    public interface IWorldServiceContainer
    {
        IReadOnlyCollection<Type> RegisteredServiceTypes { get; }
        bool IsRegistered(Type serviceType);
    }
}
