using AbilityKit.Ability.Host.Extensions.Time;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

public sealed class FixedStepTickRunnerTests
{
    [Fact]
    public void Step_PreservesUnfinishedTimeWithoutRepeatingCompletedFrames()
    {
        var runner = new FixedStepTickRunner(tickRate: 4);
        var failure = new InvalidOperationException("tick failed");
        var attempts = 0;

        var thrown = Assert.Throws<InvalidOperationException>(() => runner.Step(0.875f, delta =>
        {
            Assert.Equal(0.25f, delta);
            if (++attempts == 2) throw failure;
        }));

        Assert.Same(failure, thrown);
        Assert.Equal(1L, runner.Frame);
        Assert.Equal(0.25d, runner.Time);

        Assert.Equal(3, runner.Step(0.125f, delta => Assert.Equal(0.25f, delta)));
        Assert.Equal(4L, runner.Frame);
        Assert.Equal(1d, runner.Time);
        Assert.Equal(0, runner.Step(0.125f, _ => throw new InvalidOperationException("unexpected tick")));
    }

    [Fact]
    public void RunFrames_AdvancesFrameAndTimeWithoutConsumingElapsedBudget()
    {
        var runner = new FixedStepTickRunner(tickRate: 4);
        Assert.Equal(0, runner.Step(0.125f, _ => throw new InvalidOperationException("unexpected tick")));

        runner.RunFrames(2, delta => Assert.Equal(0.25f, delta));

        Assert.Equal(2L, runner.Frame);
        Assert.Equal(0.5d, runner.Time);
        Assert.Equal(1, runner.Step(0.125f, delta => Assert.Equal(0.25f, delta)));
        Assert.Equal(3L, runner.Frame);
        Assert.Equal(0.75d, runner.Time);
    }
}
