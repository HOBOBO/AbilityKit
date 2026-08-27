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

            // 2026-08-26 编辑器 2048 实测（ShooterLocalEditorPerfBenchCommand）：
            // 共享并行收集（排序网格 + Parallel.For，ShooterWorldModule 默认注册）
            // 邻居收集 3.8ms/帧、GC 23MB/8s；本模块的 Burst hashmap 收集为 10.8ms/帧、
            // GC 235MB/8s（每帧托管↔Native 拷贝与预扫描把 Burst 数学优势吃掉了）。
            // 因此默认世界不再覆盖注册 jobs 收集；显式组合本模块仍可选用。
            builder.Register<IShooterRvoNeighborAccelerationService>(
                WorldLifetime.Singleton,
                _ => new ShooterUnityJobsRvoNeighborAccelerationService(
                    _minimumAgentCount,
                    _innerLoopBatchCount));
        }
    }
}
