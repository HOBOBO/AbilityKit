using AbilityKit.Ability.StateSync.Samples;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class LocalSnapshotHistoryExampleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Offline_sample_preserves_snapshot_history_with_either_storage(bool useRingStorage)
    {
        Assert.True(LocalSnapshotHistoryExample.Run(useRingStorage));
    }
}
