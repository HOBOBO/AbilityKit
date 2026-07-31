using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Shooter.Runtime;

namespace AbilityKit.Demo.Shooter.Jobs
{
    public sealed class ShooterUnityJobsWorldModule : IWorldModule, IWorldModuleInfo
    {
        private readonly int _minimumAgentCount;
        private readonly int _innerLoopBatchCount;

        public ShooterUnityJobsWorldModule(
            int minimumAgentCount = ShooterUnityJobsRvoNeighborAccelerationService.DefaultMinimumAgentCount,
            int innerLoopBatchCount = ShooterUnityJobsRvoNeighborAccelerationService.DefaultInnerLoopBatchCount)
        {
            if (minimumAgentCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAgentCount));
            }

            if (innerLoopBatchCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(innerLoopBatchCount));
            }

            _minimumAgentCount = minimumAgentCount;
            _innerLoopBatchCount = innerLoopBatchCount;
        }

        public string Id => "abilitykit.demo.shooter.jobs";
        public int Order => 100;
        public Type[] DependsOn => new[] { typeof(ShooterWorldModule) };
        public Type[] ConflictsWith => Array.Empty<Type>();

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Register<IShooterRvoNeighborAccelerationService>(
                WorldLifetime.Singleton,
                _ => new ShooterUnityJobsRvoNeighborAccelerationService(
                    _minimumAgentCount,
                    _innerLoopBatchCount));
        }
    }
}
