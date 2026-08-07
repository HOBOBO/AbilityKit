using AbilityKit.Ability.StateSync.Diff;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class StateDiffProviderTests
{
    [Fact]
    public void IncrementalDiffRoundTripsChangesBeyondByteIndexRange()
    {
        var provider = new StateDiffProvider(DiffCompressionLevel.None);
        var previous = new LargeState { Payload = Enumerable.Repeat((byte)1, 512).ToArray() };
        var current = new LargeState { Payload = (byte[])previous.Payload.Clone() };
        current.Payload[400] = 9;

        var diff = provider.ComputeDiff(current, previous);
        var restored = provider.ApplyDiff(previous, diff);

        Assert.Equal(current.Payload, restored.Payload);
        Assert.Equal(1, restored.Payload[144]);
        Assert.Equal(9, restored.Payload[400]);
    }

    public sealed class LargeState
    {
        public byte[] Payload = Array.Empty<byte>();
    }
}
