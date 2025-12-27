using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Impl.World.Examples
{
    public static class WorldExampleUsage
    {
        public static IWorldManager CreateOneBattleWorld()
        {
            var registry = new WorldTypeRegistry()
                .RegisterEntitasWorld("battle");

            var manager = new WorldManager(new RegistryWorldFactory(registry));

            var envBuilder = new WorldContainerBuilder();

            manager.Create(new WorldCreateOptions(new WorldId("room_1"), "battle")
            {
                ServiceBuilder = envBuilder,
                Modules =
                {
                    new BattleWorldModule()
                }
            });

            return manager;
        }

        public static IWorldManager CreateTwoRooms()
        {
            var registry = new WorldTypeRegistry()
                .RegisterEntitasWorld("battle")
                .RegisterEntitasWorld("town");

            var manager = new WorldManager(new RegistryWorldFactory(registry));

            var envBuilder = new WorldContainerBuilder();

            manager.Create(new WorldCreateOptions(new WorldId("room_1"), "battle") { ServiceBuilder = envBuilder });
            manager.Create(new WorldCreateOptions(new WorldId("room_2"), "town") { ServiceBuilder = envBuilder });

            return manager;
        }
    }
}
