using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Entitas
{
    public sealed class EntitasWorldContext : IEntitasWorldContext
    {
        public EntitasWorldContext(WorldId id, string worldType, global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldServices services)
        {
            Id = id;
            WorldType = worldType;
            Contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            Systems = systems ?? throw new ArgumentNullException(nameof(systems));
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public WorldId Id { get; }
        public string WorldType { get; }
        public IWorldServices Services { get; }

        public global::Entitas.IContexts Contexts { get; }
        public global::Entitas.Systems Systems { get; }
    }
}
