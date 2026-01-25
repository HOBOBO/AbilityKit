using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.World.Abstractions
{
    public sealed class WorldCreateOptions
    {
        public WorldId Id;
        public string WorldType;
        public WorldContainerBuilder ServiceBuilder;
        public IEntitasContextsFactory EntitasContextsFactory;
        public readonly List<IWorldModule> Modules = new List<IWorldModule>();

        public WorldCreateOptions() { }

        public WorldCreateOptions(WorldId id, string worldType)
        {
            Id = id;
            WorldType = worldType;
        }
    }
}
