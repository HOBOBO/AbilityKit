using System;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Services.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class WorldServiceAttribute : Attribute
    {
        public Type ServiceType { get; }
        public WorldLifetime Lifetime { get; }
        public bool IsDefault { get; }
        public WorldServiceProfile Profile { get; }

        public WorldServiceAttribute(Type serviceType, WorldLifetime lifetime = WorldLifetime.Scoped, bool isDefault = true, WorldServiceProfile profile = WorldServiceProfile.All)
        {
            ServiceType = serviceType;
            Lifetime = lifetime;
            IsDefault = isDefault;
            Profile = profile;
        }
    }
}
