using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.World.Entitas
{
    public static class EntitasWorldTypeRegistration
    {
        public static WorldTypeRegistry RegisterEntitasWorld(this WorldTypeRegistry registry, string worldType)
        {
            return registry.Register(worldType, opts => new EntitasWorld(opts));
        }
    }
}
