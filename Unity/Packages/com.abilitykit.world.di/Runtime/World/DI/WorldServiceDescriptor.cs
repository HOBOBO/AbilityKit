using System;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldServiceDescriptor
    {
        public readonly Type ServiceType;
        public readonly Type ImplType;
        public readonly WorldLifetime Lifetime;
        public readonly WorldServiceOwnership Ownership;
        public readonly Func<IWorldResolver, object> Factory;

        public WorldServiceDescriptor(
            Type serviceType,
            Type implType,
            WorldLifetime lifetime,
            Func<IWorldResolver, object> factory,
            WorldServiceOwnership ownership = WorldServiceOwnership.Container)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            ImplType = implType ?? serviceType; // ImplType 默认为 ServiceType（当使用 Register 而不是 RegisterType 时）
            Lifetime = lifetime;
            Ownership = ownership;
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// 方便的重载，implType 默认为 serviceType
        /// </summary>
        public WorldServiceDescriptor(
            Type serviceType,
            WorldLifetime lifetime,
            Func<IWorldResolver, object> factory,
            WorldServiceOwnership ownership = WorldServiceOwnership.Container)
            : this(serviceType, serviceType, lifetime, factory, ownership)
        {
        }
    }
}
