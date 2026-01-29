using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Entitas.Systems;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.FlowExamples
{
    public static class BattleDungeonBootstrap
    {
        public static IWorldManager CreateWorlds(WorldId id)
        {
            var registry = new WorldTypeRegistry()
                .RegisterEntitasWorld("battle");

            var manager = new WorldManager(new RegistryWorldFactory(registry));

            var builder = WorldServiceContainerFactory.CreateDefaultOnly();
            builder.AddModule(new TickCounterWorldModule());

            manager.Create(new WorldCreateOptions(id, "battle")
            {
                ServiceBuilder = builder
            });

            return manager;
        }
    }
}
