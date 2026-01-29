using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.HotReload
{
    public interface IHotfixEntry
    {
        string Name { get; }

        void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldServices services);

        void Uninstall(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldServices services);
    }
}
