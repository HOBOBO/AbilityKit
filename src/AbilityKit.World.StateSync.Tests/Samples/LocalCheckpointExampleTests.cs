using AbilityKit.Ability.StateSync.Samples;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class LocalCheckpointExampleTests
{
    [Fact]
    public void LocalCheckpointExample_RestoresAndEvictsWithoutTransport()
    {
        Assert.True(LocalCheckpointExample.Run());
    }
}
