using System;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldServiceDescriptor
    {
        public readonly Type ServiceType;
        public readonly WorldLifetime Lifetime;
        public readonly Func<IWorldResolver, object> Factory;

        public WorldServiceDescriptor(Type serviceType, WorldLifetime lifetime, Func<IWorldResolver, object> factory)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            Lifetime = lifetime;
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
    }
}
