using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class PresentationViewStateTests
{
    [Fact]
    public void PositionSamples_InterpolateAndCapExtrapolation()
    {
        var samples = new PresentationPositionSampleBuffer();
        var start = new PresentationPosition(0f, 0f, 0f);
        var end = new PresentationPosition(10f, 0f, 0f);
        samples.Add(0d, in start);
        samples.Add(1d, in end);

        Assert.True(samples.TryEvaluate(0.5d, out var middle));
        Assert.Equal(5f, middle.X);
        Assert.True(samples.TryEvaluate(100d, out var late));
        Assert.InRange(late.X, 10.66f, 10.67f);
    }

    [Fact]
    public void PositionSamples_KeepRecentSamplesInTimeOrder()
    {
        var samples = new PresentationPositionSampleBuffer();
        for (var i = 0; i < 8; i++)
        {
            var position = new PresentationPosition(i, 0f, 0f);
            samples.Add(i, in position);
        }

        var inserted = new PresentationPosition(35f, 0f, 0f);
        samples.Add(3.5d, in inserted);

        Assert.True(samples.TryEvaluate(3.5d, out var exact));
        Assert.Equal(35f, exact.X);
        Assert.True(samples.TryEvaluate(3.75d, out var interpolated));
        Assert.Equal(19.5f, interpolated.X);
    }

    [Fact]
    public void ActorIndex_StaleEntityCannotRemoveReplacement()
    {
        var index = new PresentationActorIndex<string>();
        index.Rebind(0, 1001, "old");
        index.Rebind(0, 1001, "new");

        index.Remove(1001, "old");

        Assert.True(index.TryResolve(1001, out var entity));
        Assert.Equal("new", entity);
        index.Rebind(1001, 2001, "new");
        Assert.False(index.TryResolve(1001, out _));
        Assert.True(index.TryResolve(2001, out entity));
        Assert.Equal("new", entity);
    }
}
