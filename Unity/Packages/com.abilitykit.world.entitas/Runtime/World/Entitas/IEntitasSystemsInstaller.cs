using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Entitas
{
    public interface IEntitasSystemsInstaller
    {
        void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver services);
    }
}
