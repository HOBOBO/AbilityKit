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
            // 默认注册平台无关的并行加速（排序网格收集 + 并行 ORCA）；
            // Unity 上 Burst jobs 世界模块可用 Register 覆盖邻居收集部分。
            builder.TryRegister<IShooterRvoNeighborAccelerationService>(
                WorldLifetime.Singleton,
                _ => new ShooterParallelRvoAccelerationService());
            builder.TryRegister<ShooterArenaGameplayOptions>(WorldLifetime.Singleton, _ => ShooterArenaGameplayOptions.Disabled);
            builder.TryRegister<ShooterMatchStateOptions>(WorldLifetime.Singleton, _ => ShooterMatchStateOptions.Default);
            builder.AddModule(new SveltoWorldModule());
            builder.AddModule(new ShooterServicesAutoModule());
        }
    }
}
