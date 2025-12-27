using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Entitas
{
    public interface IEntitasSystemsInstaller
    {
        void Install(global::Contexts contexts, global::Entitas.Systems systems, IWorldResolver resolver);
    }
}
