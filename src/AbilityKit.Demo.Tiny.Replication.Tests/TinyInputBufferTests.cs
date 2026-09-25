using AbilityKit.Demo.Tiny.View;
using Xunit;

namespace AbilityKit.Demo.Tiny.Replication.Tests;

public sealed class TinyInputBufferTests
{
    [Fact]
    public void CapturesOneAttackWhileSubmissionIsPendingAndUsesLatestMovement()
    {
        var buffer = new TinyInputBuffer();
        buffer.Capture(new TinyInput(1, 0, false));
        buffer.Capture(new TinyInput(1, 0, true));
        buffer.Capture(new TinyInput(-1, 0, true));
        buffer.Capture(new TinyInput(0, 1, false));

        Assert.True(buffer.TryTake(out var submitted));
        Assert.Equal((sbyte)0, submitted.MoveX);
        Assert.Equal((sbyte)1, submitted.MoveY);
        Assert.True(submitted.Attack);

        Assert.True(buffer.TryTake(out var movementOnly));
        Assert.False(movementOnly.Attack);
        buffer.Capture(new TinyInput(0, 0, false));
        Assert.False(buffer.TryTake(out _));
    }

    [Fact]
    public void ClearDiscardsPendingAttackDuringSessionLoss()
    {
        var buffer = new TinyInputBuffer();
        buffer.Capture(new TinyInput(0, 0, true));
        buffer.Clear();
        Assert.False(buffer.TryTake(out _));
    }
}
