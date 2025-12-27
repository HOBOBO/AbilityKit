using System;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World.Management
{
    public sealed class RegistryWorldFactory : IWorldFactory
    {
        private readonly WorldTypeRegistry _registry;

        public RegistryWorldFactory(WorldTypeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public IWorld Create(WorldCreateOptions options)
        {
            return _registry.Create(options);
        }
    }
}
