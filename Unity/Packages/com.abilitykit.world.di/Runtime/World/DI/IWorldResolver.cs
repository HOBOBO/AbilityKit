using System;

namespace AbilityKit.Ability.World.DI
{
    public interface IWorldResolver
    {
        object Resolve(Type serviceType);
        T Resolve<T>();
        bool TryResolve(Type serviceType, out object instance);
        bool TryResolve<T>(out T instance);
    }
}
