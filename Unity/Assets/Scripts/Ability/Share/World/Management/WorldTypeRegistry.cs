using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World.Management
{
    public sealed class WorldTypeRegistry
    {
        private readonly Dictionary<string, Func<WorldCreateOptions, IWorld>> _factories = new Dictionary<string, Func<WorldCreateOptions, IWorld>>(StringComparer.Ordinal);

        public WorldTypeRegistry Register(string worldType, Func<WorldCreateOptions, IWorld> factory)
        {
            if (string.IsNullOrEmpty(worldType)) throw new ArgumentException(nameof(worldType));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _factories[worldType] = factory;
            return this;
        }

        public IWorld Create(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (!_factories.TryGetValue(options.WorldType, out var f))
            {
                throw new InvalidOperationException($"No world factory registered for type: {options.WorldType}");
            }
            return f(options);
        }
    }
}
