using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class ActionSchedulerManagerTests
{
    private static readonly ActionCallPlan Delayed = new(
        default, 0, default, default, null, EActionScheduleMode.Delayed, 100f, 1, true, EActionExecutionPolicy.Immediate);

    [Fact]
    public void Total_counts_across_schedulers_without_waiting_for_update()
    {
        var manager = new ActionSchedulerManager();
        var first = manager.GetOrCreateScheduler(10);
        var second = manager.GetOrCreateScheduler(20);
        Assert.Equal(0, manager.TotalActionCount);

        var calls = 0;
        first.Register(Delayed, (_, _) => calls++, null, null, null);
        second.RegisterOrReplace(0, Delayed, (_, _) => calls++, null, null, null);
        Assert.Equal(2, manager.TotalActionCount);

        second.RegisterOrReplace(0, Delayed, (_, _) => calls++, null, null, null);
        Assert.Equal(2, manager.TotalActionCount);

        manager.Update(1f, null);
        Assert.Equal(2, manager.TotalActionCount);
        manager.PauseAll();
        manager.Update(100f, null);
        Assert.Equal(2, manager.TotalActionCount);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false, EActionInstanceState.Completed)]
    [InlineData(true, EActionInstanceState.Interrupted)]
    public void Total_includes_terminal_action_until_cleanup(bool interrupt, EActionInstanceState terminalState)
    {
        var manager = new ActionSchedulerManager();
        var scheduler = manager.GetOrCreateScheduler(10);
        var plan = new ActionCallPlan(default);
        var instance = scheduler.Register(plan, (_, _) => { }, null, null, null);

        if (interrupt)
            manager.InterruptTriggerActions(10, "测试中断");
        else
            manager.Update(1f, null);

        Assert.Equal(terminalState, instance.State);
        Assert.Equal(1, manager.TotalActionCount);

        manager.Update(1f, null);

        Assert.Equal(0, manager.TotalActionCount);
        Assert.Null(manager.GetScheduler(10));
    }

    [Fact]
    public void Total_reflects_remove_and_clear_without_waiting_for_update()
    {
        var manager = new ActionSchedulerManager();
        manager.GetOrCreateScheduler(10).Register(Delayed, (_, _) => { }, null, null, null);
        manager.GetOrCreateScheduler(20).Register(Delayed, (_, _) => { }, null, null, null);
        manager.Update(1f, null);

        Assert.True(manager.RemoveScheduler(10));
        Assert.Equal(1, manager.TotalActionCount);

        manager.Clear();
        Assert.Equal(0, manager.TotalActionCount);
    }
}
