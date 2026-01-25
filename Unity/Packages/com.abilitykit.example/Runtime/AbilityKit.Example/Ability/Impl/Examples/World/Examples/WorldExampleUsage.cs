using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Entitas.Systems;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Ability.Impl.World.Examples
{
    public static class WorldExampleUsage
    {
        public static WorldManagerFrameDriver CreateOneBattleWorldDriver()
        {
            var registry = new WorldTypeRegistry()
                .RegisterEntitasWorld("battle");

            var manager = new WorldManager(new RegistryWorldFactory(registry));

            var envBuilder = WorldServiceContainerFactory.CreateWithAttributes(
                WorldServiceProfile.Client,
                new[] { typeof(WorldExampleUsage).Assembly },
                new[] { "AbilityKit" }
            );

            envBuilder.AddModule(new TickCounterWorldModule());

            manager.Create(new WorldCreateOptions(new WorldId("room_1"), "battle")
            {
                ServiceBuilder = envBuilder
            });

            return new WorldManagerFrameDriver(manager);
        }

        public static WorldManagerFrameDriver CreateTwoRoomsDriver()
        {
            var registry = new WorldTypeRegistry()
                .RegisterEntitasWorld("battle")
                .RegisterEntitasWorld("town");

            var manager = new WorldManager(new RegistryWorldFactory(registry));

            var battleBuilder = WorldServiceContainerFactory.CreateDefaultOnly();
            var townBuilder = WorldServiceContainerFactory.CreateDefaultOnly();

            battleBuilder.AddModule(new TickCounterWorldModule());
            townBuilder.AddModule(new TickCounterWorldModule());

            manager.Create(new WorldCreateOptions(new WorldId("room_1"), "battle") { ServiceBuilder = battleBuilder });
            manager.Create(new WorldCreateOptions(new WorldId("room_2"), "town") { ServiceBuilder = townBuilder });

            return new WorldManagerFrameDriver(manager);
        }
    }
}
