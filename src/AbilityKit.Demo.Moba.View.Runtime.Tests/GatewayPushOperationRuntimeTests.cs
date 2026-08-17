using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class GatewayPushOperationRuntimeTests
{
    [Fact]
    public void TryStart_WhenDetached_ReturnsFalse()
    {
        using var runtime = new GatewayPushOperationRuntime();

        Assert.False(runtime.TryStart(1u, default));
        Assert.True(runtime.PendingTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task PendingTask_WaitsForAllStartedOperations()
    {
        using var runtime = new GatewayPushOperationRuntime();
        var first = NewCompletionSource();
        var second = NewCompletionSource();
        runtime.Attach((opCode, _, _) => opCode == 1u ? first.Task : second.Task);

        Assert.True(runtime.TryStart(1u, default));
        Assert.True(runtime.TryStart(2u, default));
        var pending = runtime.PendingTask;

        first.SetResult();
        Assert.False(pending.IsCompleted);
        second.SetResult();
        await pending;

        Assert.True(runtime.PendingTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Detach_CancelsLifetimeAndReturnsPendingSnapshot()
    {
        using var runtime = new GatewayPushOperationRuntime();
        var cancellationObserved = NewCompletionSource();
        runtime.Attach(async (_, _, cancellationToken) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                cancellationObserved.TrySetResult();
            }
        });
        Assert.True(runtime.TryStart(1u, default));

        var pending = runtime.Detach();
        await pending;
        await cancellationObserved.Task;

        Assert.False(runtime.IsAttached);
        Assert.False(runtime.TryStart(2u, default));
    }

    [Fact]
    public async Task Detach_SuppressesLateFailureFromPreviousAttachment()
    {
        using var runtime = new GatewayPushOperationRuntime();
        var operation = NewCompletionSource();
        Exception? reported = null;
        runtime.Attach((_, _, _) => operation.Task, exception => reported = exception);
        Assert.True(runtime.TryStart(1u, default));

        var pending = runtime.Detach();
        operation.SetException(new InvalidOperationException("late failure"));
        await pending;

        Assert.Null(reported);
    }

    [Fact]
    public async Task Reattach_ReportsOnlyCurrentAttachmentFailure()
    {
        using var runtime = new GatewayPushOperationRuntime();
        var previous = NewCompletionSource();
        Exception? reported = null;
        runtime.Attach((_, _, _) => previous.Task, exception => reported = exception);
        Assert.True(runtime.TryStart(1u, default));

        runtime.Attach(
            (_, _, _) => Task.FromException(new ApplicationException("current failure")),
            exception => reported = exception);
        previous.SetException(new InvalidOperationException("previous failure"));
        Assert.True(runtime.TryStart(2u, default));
        await runtime.PendingTask;

        var currentFailure = Assert.IsType<ApplicationException>(reported);
        Assert.Equal("current failure", currentFailure.Message);
    }

    private static TaskCompletionSource NewCompletionSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
