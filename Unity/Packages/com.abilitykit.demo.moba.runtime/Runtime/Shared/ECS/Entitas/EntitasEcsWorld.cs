using System;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.ECS.Entitas
{
    public sealed class EntitasEcsWorld : IEcsWorld
    {
        private readonly EntitasActorIdLookup _lookup;

        public EntitasEcsWorld(IWorldResolver services, EntitasActorIdLookup lookup, IUnitResolver units)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            Units = units ?? throw new ArgumentNullException(nameof(units));
        }

        public IWorldResolver Services { get; }

        public IUnitResolver Units { get; }

        public bool Exists(EcsEntityId id)
        {
            return id.IsValid && _lookup.TryGet(id.ActorId, out _);
        }
    }
}
