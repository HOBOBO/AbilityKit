using System;
using AbilityKit.Ability.StateSync.Aoi;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class AoiInterestSetAllocationTests
{
    private const int AllocationIterations = 1_000;

    private static readonly AoiInterestScope Scope = new AoiInterestScope(
        centerX: 0f,
        centerY: 0f,
        visibleRadius: 20f,
        boundaryRadius: 24f,
        maxEntities: 0);

    private static readonly AoiEntitySample[] Samples =
    {
        new AoiEntitySample(new AoiEntityKey(1, 1), 0f, 0f),
        new AoiEntitySample(new AoiEntityKey(1, 2), 2f, 1f),
        new AoiEntitySample(new AoiEntityKey(2, 3), 4f, 2f)
    };

    [Fact]
    public void TransientEvaluationReusesResultAndChangesList()
    {
        var interestSet = new AoiInterestSet();

        var first = interestSet.EvaluateTransient(Samples, Scope);
        var firstChanges = first.Changes;
        var second = interestSet.EvaluateTransient(Samples, Scope);

        Assert.Same(first, second);
        Assert.Same(firstChanges, second.Changes);
        Assert.Equal(Samples.Length, second.VisibleCount);
        Assert.All(second.Changes, change => Assert.Equal(AoiInterestTransition.Stay, change.Transition));
        Assert.Equal(new[] { 0, 1, 2 }, second.Changes.Select(change => change.SourceIndex));
    }

    [Fact]
    public void OwnedEvaluationRemainsIndependentAcrossCalls()
    {
        var interestSet = new AoiInterestSet();

        var first = interestSet.Evaluate(Samples, Scope);
        var second = interestSet.Evaluate(Samples, Scope);

        Assert.NotSame(first, second);
        Assert.NotSame(first.Changes, second.Changes);
        Assert.All(first.Changes, change => Assert.Equal(AoiInterestTransition.Enter, change.Transition));
        Assert.All(second.Changes, change => Assert.Equal(AoiInterestTransition.Stay, change.Transition));
    }

    [Fact]
    public void WarmTransientEvaluationDoesNotAllocatePerFrame()
    {
        var interestSet = new AoiInterestSet();
        AoiInterestEvaluation last = interestSet.EvaluateTransient(Samples, Scope);
        interestSet.EvaluateTransient(Samples, Scope);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < AllocationIterations; i++)
        {
            last = interestSet.EvaluateTransient(Samples, Scope);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Samples.Length, last.Changes.Count);
        Assert.True(allocated <= 1_024L, $"Expected allocation-free steady state, allocated {allocated} bytes.");
    }

    [Fact]
    public void TransientEvaluationOrdersMultipleLeavesByKindThenId()
    {
        var interestSet = new AoiInterestSet();
        var samples = new[]
        {
            new AoiEntitySample(new AoiEntityKey(2, 8), 0f, 0f),
            new AoiEntitySample(new AoiEntityKey(1, 9), 0f, 0f),
            new AoiEntitySample(new AoiEntityKey(1, 3), 0f, 0f)
        };

        interestSet.EvaluateTransient(samples, Scope);
        var leaves = interestSet.EvaluateTransient(Array.Empty<AoiEntitySample>(), Scope);

        Assert.Collection(
            leaves.Changes,
            change => Assert.Equal(new AoiEntityKey(1, 3), change.Key),
            change => Assert.Equal(new AoiEntityKey(1, 9), change.Key),
            change => Assert.Equal(new AoiEntityKey(2, 8), change.Key));
        Assert.All(leaves.Changes, change => Assert.Equal(AoiInterestTransition.Leave, change.Transition));
        Assert.All(leaves.Changes, change => Assert.Equal(-1, change.SourceIndex));
    }
}
