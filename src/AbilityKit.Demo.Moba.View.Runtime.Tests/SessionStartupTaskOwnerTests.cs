using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class SessionStartupTaskOwnerTests
{
    [Fact]
    public async Task DetachWaitsForStartupAndInvalidatesItsContinuation()
    {
        var owner = new SessionStartupTaskOwner();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.Attach();

        var start = owner.RequestStart(async generation =>
        {
            Assert.True(owner.IsCurrent(generation));
            await release.Task;
            Assert.False(owner.IsCurrent(generation));
        });
        var detach = owner.BeginDetachAsync();

        Assert.False(detach.IsCompleted);
        Assert.False(detach.IsCompleted);
        release.SetResult(true);
        await detach;
        owner.Attach();
        Assert.True(owner.IsCurrent(3));
    }

    [Fact]
    public async Task DetachWaitsForPlanAttachAsWellAsGatewayStartup()
    {
        var owner = new SessionStartupTaskOwner();
        var releasePlan = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGateway = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.Attach();
        var plan = owner.TrackPlanAttach(_ => releasePlan.Task);
        var gateway = owner.RequestStart(_ => releaseGateway.Task);

        var detach = owner.BeginDetachAsync();
        releaseGateway.SetResult(true);
        await gateway;
        Assert.False(detach.IsCompleted);
        releasePlan.SetResult(true);
        await Task.WhenAll(plan, detach);
    }

    [Fact]
    public async Task RepeatedStartRequestRunsOnlyOncePerAttachment()
    {
        var owner = new SessionStartupTaskOwner();
        owner.Attach();
        var calls = 0;
        var first = owner.RequestStart(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });
        var second = owner.RequestStart(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Assert.Same(first, second);
        await first;
        Assert.Equal(1, calls);
        await owner.BeginDetachAsync();
        await owner.RequestStart(_ => throw new InvalidOperationException());
        Assert.Equal(1, calls);
    }
}
