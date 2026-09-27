using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class BattleSessionLifecycleControllerTests
{
    [Fact]
    public async Task DetachAsync_ConcurrentCallersShareTheSameTeardown()
    {
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var controller = new BattleSessionLifecycleController<int>(_ =>
            new[]
            {
                new AsyncSessionTeardownStep("pending", async () =>
                {
                    calls++;
                    await release.Task;
                }),
            });
        controller.Attach();

        var first = controller.DetachAsync(1);
        var second = controller.DetachAsync(2);

        Assert.Same(first, second);
        Assert.Equal(1, calls);
        Assert.Equal(
            BattleSessionFeatureLifecycleState.Detaching,
            controller.State);

        release.SetResult(true);
        await Task.WhenAll(first, second);

        Assert.Equal(
            BattleSessionFeatureLifecycleState.Detached,
            controller.State);
    }

    [Fact]
    public async Task DetachAsync_RunsEveryStepAndKeepsStepNamesOnFailure()
    {
        var calls = new List<string>();
        var controller = new BattleSessionLifecycleController<int>(_ =>
            new[]
            {
                new AsyncSessionTeardownStep("first", () =>
                {
                    calls.Add("first");
                    throw new InvalidOperationException("first failed");
                }),
                new AsyncSessionTeardownStep(
                    "second",
                    () => calls.Add("second")),
                new AsyncSessionTeardownStep("third", () =>
                {
                    calls.Add("third");
                    throw new InvalidOperationException("third failed");
                }),
            });
        controller.Attach();

        var exception = await Assert.ThrowsAsync<SessionTeardownException>(
            () => controller.DetachAsync(0));

        Assert.Equal(new[] { "first", "second", "third" }, calls);
        Assert.Collection(
            exception.InnerExceptions,
            first => Assert.Contains("first", first.Message),
            third => Assert.Contains("third", third.Message));
        Assert.Equal(new[] { "first", "third" }, exception.FailedStepNames);
        Assert.Equal(
            BattleSessionFeatureLifecycleState.DetachFailed,
            controller.State);
    }

    [Fact]
    public async Task DetachAsync_RetryRunsOnlyPreviouslyFailedSteps()
    {
        var completedCalls = 0;
        var retryCalls = 0;
        var controller = new BattleSessionLifecycleController<int>(_ =>
            new[]
            {
                new AsyncSessionTeardownStep(
                    "completed",
                    () => completedCalls++),
                new AsyncSessionTeardownStep("retry", () =>
                {
                    retryCalls++;
                    if (retryCalls == 1)
                    {
                        throw new InvalidOperationException("transient failure");
                    }
                }),
            });
        controller.Attach();

        var failedDetach = controller.DetachAsync(0);
        await Assert.ThrowsAsync<SessionTeardownException>(
            () => failedDetach);
        Assert.Equal(
            BattleSessionFeatureLifecycleState.DetachFailed,
            controller.State);
        Assert.Throws<InvalidOperationException>(controller.Attach);

        var retry = controller.DetachAsync(1);

        Assert.NotSame(failedDetach, retry);
        await retry;
        Assert.Equal(1, completedCalls);
        Assert.Equal(2, retryCalls);
        Assert.Equal(
            BattleSessionFeatureLifecycleState.Detached,
            controller.State);
    }

    [Fact]
    public async Task Attach_DuringDetachIsRejected_AndSucceedsAfterCompletion()
    {
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new BattleSessionLifecycleController<int>(_ =>
            new[]
            {
                new AsyncSessionTeardownStep("pending", () => release.Task),
            });
        controller.Attach();
        var firstGeneration = controller.Generation;
        var detach = controller.DetachAsync(0);

        var exception = Assert.Throws<InvalidOperationException>(
            controller.Attach);
        Assert.Contains("teardown", exception.Message);

        release.SetResult(true);
        await detach;
        controller.Attach();

        Assert.Equal(firstGeneration + 1, controller.Generation);
        Assert.Equal(
            BattleSessionFeatureLifecycleState.Attached,
            controller.State);
    }
}
