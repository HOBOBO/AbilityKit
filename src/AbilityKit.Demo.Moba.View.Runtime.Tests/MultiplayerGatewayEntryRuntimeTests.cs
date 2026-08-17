using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MultiplayerGatewayEntryRuntimeTests
{
    [Fact]
    public void Attach_ExposesCurrentGenerationAndLifetime()
    {
        using var runtime = new MultiplayerGatewayEntryRuntime();
        var generation = 0;

        runtime.Attach(attachment =>
        {
            generation = attachment.Generation;
            Assert.False(attachment.LifetimeToken.IsCancellationRequested);
        });

        Assert.True(runtime.IsAttached);
        Assert.Equal(generation, runtime.AttachmentGeneration);
        Assert.True(runtime.IsCurrent(generation));
        Assert.False(runtime.LifetimeToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Detach_CancelsLifetimeAndRunsTeardownInReverseOrder()
    {
        using var runtime = new MultiplayerGatewayEntryRuntime();
        var order = new List<string>();
        var cancellationObserved = false;
        var generation = 0;
        runtime.Attach(attachment =>
        {
            generation = attachment.Generation;
            attachment.Register(() =>
            {
                cancellationObserved = attachment.LifetimeToken.IsCancellationRequested;
                order.Add("resource");
            });
            attachment.Register(() => order.Add("subscription"));
            attachment.Register(() => order.Add("publication"));
        });

        await runtime.Detach();

        Assert.True(cancellationObserved);
        Assert.Equal(new[] { "publication", "subscription", "resource" }, order);
        Assert.False(runtime.IsAttached);
        Assert.False(runtime.IsCurrent(generation));
    }

    [Fact]
    public async Task Detach_WaitsForAsynchronousTeardown()
    {
        using var runtime = new MultiplayerGatewayEntryRuntime();
        var completion = NewCompletionSource();
        runtime.Attach(attachment => attachment.Register(() => completion.Task));

        var pending = runtime.Detach();

        Assert.Same(pending, runtime.PendingTask);
        Assert.False(pending.IsCompleted);
        completion.SetResult();
        await pending;
    }

    [Fact]
    public async Task Detach_AwaitsEachStepBeforeReleasingEarlierResource()
    {
        using var runtime = new MultiplayerGatewayEntryRuntime();
        var operation = NewCompletionSource();
        var resourceDisposed = false;
        runtime.Attach(attachment =>
        {
            attachment.Register(() => resourceDisposed = true);
            attachment.Register(() => operation.Task);
        });

        var pending = runtime.Detach();

        Assert.False(resourceDisposed);
        operation.SetResult();
        await pending;
        Assert.True(resourceDisposed);
    }

    [Fact]
    public async Task AttachFailure_RollsBackRegisteredResources()
    {
        using var runtime = new MultiplayerGatewayEntryRuntime();
        var order = new List<string>();
        var failure = Assert.Throws<InvalidOperationException>(() =>
            runtime.Attach(attachment =>
            {
                attachment.Register(() => order.Add("first"));
                attachment.Register(() => order.Add("second"));
                throw new InvalidOperationException("construction failed");
            }));

        await runtime.PendingTask;

        Assert.Equal("construction failed", failure.Message);
        Assert.Equal(new[] { "second", "first" }, order);
        Assert.False(runtime.IsAttached);
    }

    [Fact]
    public async Task TeardownFailure_DoesNotSkipEarlierResources()
    {
        using var runtime = new MultiplayerGatewayEntryRuntime();
        var order = new List<string>();
        runtime.Attach(attachment =>
        {
            attachment.Register(() => order.Add("resource"));
            attachment.Register(() => throw new InvalidOperationException("unsubscribe failed"));
            attachment.Register(() => order.Add("publication"));
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await runtime.Detach());

        Assert.Equal("unsubscribe failed", failure.Message);
        Assert.Equal(new[] { "publication", "resource" }, order);
    }

    [Fact]
    public async Task Attach_WhilePreviousTeardownIsRunning_IsRejected()
    {
        using var runtime = new MultiplayerGatewayEntryRuntime();
        var teardown = NewCompletionSource();
        runtime.Attach(attachment => attachment.Register(() => teardown.Task));
        var pending = runtime.Detach();

        var failure = Assert.Throws<InvalidOperationException>(
            () => runtime.Attach(_ => { }));

        Assert.Contains("still running", failure.Message);
        teardown.SetResult();
        await pending;
        runtime.Attach(_ => { });
        Assert.True(runtime.IsAttached);
    }

    [Fact]
    public async Task Reattach_InvalidatesPreviousGeneration()
    {
        using var runtime = new MultiplayerGatewayEntryRuntime();
        var previousGeneration = 0;
        runtime.Attach(attachment => previousGeneration = attachment.Generation);
        await runtime.Detach();

        var currentGeneration = 0;
        runtime.Attach(attachment => currentGeneration = attachment.Generation);

        Assert.False(runtime.IsCurrent(previousGeneration));
        Assert.True(runtime.IsCurrent(currentGeneration));
        Assert.NotEqual(previousGeneration, currentGeneration);
    }

    private static TaskCompletionSource NewCompletionSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
