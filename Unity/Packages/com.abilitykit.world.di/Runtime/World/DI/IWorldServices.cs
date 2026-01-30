using System;

namespace AbilityKit.Ability.World.DI
{
    public interface IWorldServices : IWorldResolver
    {
        T Get<T>();
        bool TryGet<T>(out T instance);
    }
}
