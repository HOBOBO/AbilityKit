using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World.Entitas
{
    public interface IEntitasWorld : IWorld
    {
        global::Entitas.IContexts Contexts { get; }
        global::Entitas.Systems Systems { get; }
    }
}
