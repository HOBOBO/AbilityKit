using System;

namespace AbilityKit.Ability.World.DI
{
    public interface IWorldServices
    {
        object Resolve(Type serviceType);
        T Resolve<T>();
        bool TryResolve(Type serviceType, out object instance);
        bool TryResolve<T>(out T instance);

        T Get<T>();
        bool TryGet<T>(out T instance);
    }
}
