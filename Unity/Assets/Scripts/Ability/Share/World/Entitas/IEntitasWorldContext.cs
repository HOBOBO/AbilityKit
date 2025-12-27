using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World.Entitas
{
    public interface IEntitasWorldContext : IWorldContext
    {
        global::Contexts Contexts { get; }
        global::Entitas.Systems Systems { get; }
    }
}
