using AbilityKit.Demo.Shooter.Runtime;

namespace AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;

/// <summary>
/// 服务端别名：实现已下沉到共享 runtime 包（ShooterParallelRvoAccelerationService），
/// 服务端与 Unity 客户端共用同一份排序网格 + Parallel.For 加速实现。
/// </summary>
internal sealed class ShooterServerRvoNeighborAccelerationService : ShooterParallelRvoAccelerationService
{
    internal const int DefaultMinimumParallelAgentCount = ShooterParallelRvoAccelerationService.DefaultMinimumParallelAgentCount;
    internal const int DefaultMaximumDegreeOfParallelism = ShooterParallelRvoAccelerationService.DefaultMaximumDegreeOfParallelism;

    public ShooterServerRvoNeighborAccelerationService(
        int minimumParallelAgentCount = ShooterParallelRvoAccelerationService.DefaultMinimumParallelAgentCount,
        int maximumDegreeOfParallelism = ShooterParallelRvoAccelerationService.DefaultMaximumDegreeOfParallelism)
        : base(minimumParallelAgentCount, maximumDegreeOfParallelism)
    {
    }
}
