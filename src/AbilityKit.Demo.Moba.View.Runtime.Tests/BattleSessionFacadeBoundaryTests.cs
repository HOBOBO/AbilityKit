using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class BattleSessionFacadeBoundaryTests
{
    [Fact]
    public void TeardownPolicy_RunsEveryStepInDeclaredOrderAndReportsFailures()
    {
        var calls = new List<string>();
        var failures = new List<(string Name, Exception Exception)>();
        var expected = new InvalidOperationException("stop failed");

        SessionTeardownPolicy.Execute(
            (name, exception) => failures.Add((name, exception)),
            new SessionTeardownStep("sub-features", () => calls.Add("sub-features")),
            new SessionTeardownStep("session", () =>
            {
                calls.Add("session");
                throw expected;
            }),
            new SessionTeardownStep("assets", () => calls.Add("assets")));

        Assert.Equal(new[] { "sub-features", "session", "assets" }, calls);
        var failure = Assert.Single(failures);
        Assert.Equal("session", failure.Name);
        Assert.Same(expected, failure.Exception);
    }

    [Fact]
    public void TeardownPolicy_ReportsMultipleFailuresInDeclaredOrder()
    {
        var failures = new List<string>();

        SessionTeardownPolicy.Execute(
            (name, _) => failures.Add(name),
            new SessionTeardownStep("first", () => throw new InvalidOperationException()),
            new SessionTeardownStep("middle", () => { }),
            new SessionTeardownStep("last", () => throw new InvalidOperationException()));

        Assert.Equal(new[] { "first", "last" }, failures);
    }

    [Fact]
    public void TickProjector_ProjectsFrameAndAccumulatorToLogicTime()
    {
        var projection = BattleSessionTickProjector.Create(
            lastFrame: 90,
            tickAccumulator: 0.0125f,
            fixedDeltaSeconds: 1f / 30f);

        Assert.Equal(90, projection.LastFrame);
        Assert.Equal(
            90d * (double)(1f / 30f) + (double)0.0125f,
            projection.LogicTimeSeconds,
            precision: 12);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.1f)]
    public void TickProjector_InvalidFixedDeltaProjectsZeroLogicTime(
        float fixedDeltaSeconds)
    {
        var projection = BattleSessionTickProjector.Create(
            lastFrame: 90,
            tickAccumulator: 0.5f,
            fixedDeltaSeconds);

        Assert.Equal(90, projection.LastFrame);
        Assert.Equal(0d, projection.LogicTimeSeconds);
    }
}
