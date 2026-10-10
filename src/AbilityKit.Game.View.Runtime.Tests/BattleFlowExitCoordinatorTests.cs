using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class BattleFlowExitCoordinatorTests
{
    [Fact]
    public void End_WaitsForCleanupBeforeEnteringEnd()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<string>();
        var coordinator = new BattleFlowExitCoordinator(
            () => completion.Task,
            () => events.Add("end"),
            () => events.Add("lobby"));

        coordinator.RequestEnd();
        coordinator.RequestEnd();
        Assert.True(coordinator.Poll());
        Assert.Empty(events);

        completion.SetResult(true);
        Assert.False(coordinator.Poll());
        Assert.Equal(new[] { "end" }, events);
        Assert.False(coordinator.IsPending);
    }

    [Fact]
    public void ReturnLobby_SupersedesEndButUsesTheSameCleanup()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;
        var events = new List<string>();
        var coordinator = new BattleFlowExitCoordinator(
            () => { starts++; return completion.Task; },
            () => events.Add("end"),
            () => events.Add("lobby"));

        coordinator.RequestEnd();
        coordinator.RequestReturnLobby();
        Assert.True(coordinator.Poll());
        completion.SetResult(true);
        Assert.False(coordinator.Poll());

        Assert.Equal(1, starts);
        Assert.Equal(new[] { "lobby" }, events);
    }

    [Fact]
    public void CleanupFailure_ReportsErrorAndRetainsBattleScope()
    {
        var events = new List<string>();
        var coordinator = new BattleFlowExitCoordinator(
            () => Task.FromException(new InvalidOperationException("stop failed")),
            () => events.Add("end"),
            () => events.Add("lobby"),
            ex => events.Add("failure:" + ex.Message));

        coordinator.RequestEnd();
        Assert.True(coordinator.Poll());
        Assert.Equal(new[] { "failure:stop failed" }, events);
        Assert.True(coordinator.Poll());
        Assert.Single(events);
        Assert.True(coordinator.IsPending);
    }

    [Fact]
    public void SynchronousCleanupFailure_ReportsErrorAndRetainsBattleScope()
    {
        var events = new List<string>();
        var coordinator = new BattleFlowExitCoordinator(
            () => throw new InvalidOperationException("cannot begin cleanup"),
            () => events.Add("end"),
            () => events.Add("lobby"),
            ex => events.Add("failure:" + ex.Message));

        coordinator.RequestEnd();
        Assert.True(coordinator.Poll());
        Assert.Equal(new[] { "failure:cannot begin cleanup" }, events);
        Assert.True(coordinator.IsPending);
    }
}
