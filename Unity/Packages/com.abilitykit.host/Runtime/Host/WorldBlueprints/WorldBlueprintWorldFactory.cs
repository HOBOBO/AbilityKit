using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Host.WorldBlueprints
{
    public sealed class WorldBlueprintWorldFactory : IWorldFactory
    {
        private readonly IWorldFactory _inner;
        private readonly WorldBlueprintRegistry _blueprints;

        public WorldBlueprintWorldFactory(IWorldFactory inner, WorldBlueprintRegistry blueprints)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _blueprints = blueprints ?? throw new ArgumentNullException(nameof(blueprints));
        }

        public IWorld Create(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _blueprints.Configure(options);
            return _inner.Create(options);
        }
    }
}
