using System.Collections.Generic;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Abstractions
{
    public sealed class WorldCreateOptions
    {
        public WorldId Id;
        public string WorldType;
        public WorldContainerBuilder ServiceBuilder;
        public readonly Dictionary<System.Type, object> Extensions = new Dictionary<System.Type, object>();
        public readonly List<IWorldModule> Modules = new List<IWorldModule>();

        public WorldCreateOptions() { }

        public WorldCreateOptions(WorldId id, string worldType)
        {
            Id = id;
            WorldType = worldType;
        }
    }
}
