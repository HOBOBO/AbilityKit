using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.World.Svelto;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public sealed class ShooterWorldModule : IWorldModule, IWorldModuleInfo
    {
        public string Id => "abilitykit.demo.shooter.world";
        public int Order => 0;
        public Type[] DependsOn => Array.Empty<Type>();
        public Type[] ConflictsWith => Array.Empty<Type>();

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.TryRegister<ShooterEnemyWaveOptions>(WorldLifetime.Singleton, _ => ShooterEnemyWaveOptions.DefaultEnabled);
            builder.TryRegister<ShooterRvoOptions>(WorldLifetime.Singleton, _ => ShooterRvoOptions.Default);
            builder.TryRegister<IShooterRvoNeighborAccelerationService>(
                WorldLifetime.Singleton,
                _ => ShooterNullRvoNeighborAccelerationService.Instance);
            builder.TryRegister<ShooterArenaGameplayOptions>(WorldLifetime.Singleton, _ => ShooterArenaGameplayOptions.Disabled);
            builder.TryRegister<ShooterMatchStateOptions>(WorldLifetime.Singleton, _ => ShooterMatchStateOptions.Default);
            builder.AddModule(new SveltoWorldModule());
            builder.AddModule(new ShooterServicesAutoModule());
        }
    }
}
