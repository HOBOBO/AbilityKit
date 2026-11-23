using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Samples;
using Xunit;

namespace AbilityKit.World.FrameSync.Tests;

public sealed class LocalFrameCommandExampleTests
{
    [Fact]
    public void Offline_sample_requires_ready_frame_and_applies_commands_in_order()
    {
        Assert.True(LocalFrameCommandExample.Run());
    }

    [Fact]
    public void A_missing_frame_cannot_poison_other_readers_or_buffers()
    {
        var first = new FrameCommandBuffer<int, int>();
        var second = new FrameCommandBuffer<int, int>();

        var missing = first.GetFrameCommandsOrEmpty(9);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<int, int>)missing).Add(7, 11));
        Assert.False(first.TryGetFrameCommands(9, out var sameMissingFrame));
        Assert.Empty(sameMissingFrame);
        Assert.Empty(second.GetFrameCommandsOrEmpty(10));

        first.SubmitCommand(9, 7, 12);
        Assert.Equal(12, first.GetFrameCommandsOrEmpty(9)[7]);
        Assert.Empty(missing);
    }
}
