using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.WorldBlueprints
{
    public sealed class WorldBlueprintRegistry
    {
        private readonly Dictionary<string, IWorldBlueprint> _map = new Dictionary<string, IWorldBlueprint>(StringComparer.Ordinal);

        public WorldBlueprintRegistry Register(IWorldBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (string.IsNullOrEmpty(blueprint.WorldType)) throw new ArgumentException("Blueprint.WorldType is required", nameof(blueprint));
            _map[blueprint.WorldType] = blueprint;
            return this;
        }

        public bool TryGet(string worldType, out IWorldBlueprint blueprint)
        {
            if (string.IsNullOrEmpty(worldType))
            {
                blueprint = null;
                return false;
            }

            return _map.TryGetValue(worldType, out blueprint);
        }

        public void Configure(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (TryGet(options.WorldType, out var blueprint) && blueprint != null)
            {
                blueprint.Configure(options);
            }
        }
    }
}
