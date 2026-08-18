using AbilityKit.Timer;
using Xunit;

namespace AbilityKit.Timer.Tests;

public sealed class DefaultSchedulerTests
{
    [Fact]
    public void Count_tracks_scheduled_tasks()
    {
        var scheduler = new DefaultScheduler();

        scheduler.ScheduleDelay(() => { }, 1f);
        scheduler.ScheduleDelay(() => { }, 2f);

        Assert.Equal(2, scheduler.Count);
    }

    [Fact]
    public void Completed_tasks_are_removed_after_tick()
    {
        var scheduler = new DefaultScheduler();
        scheduler.ScheduleDelay(() => { }, 1f);
        scheduler.ScheduleDelay(() => { }, 5f);

        scheduler.Tick(2f);

        Assert.Equal(1, scheduler.Count);
    }

    [Fact]
    public void Canceled_tasks_are_removed_after_tick()
    {
        var scheduler = new DefaultScheduler();
        var task = scheduler.ScheduleDelay(() => { }, 5f);

        task.RequestCancel();
        scheduler.Tick(0.1f);

        Assert.Equal(0, scheduler.Count);
    }

    [Fact]
    public void Cancel_all_marks_every_task_and_defers_removal_to_tick()
    {
        var scheduler = new DefaultScheduler();
        var a = scheduler.ScheduleDelay(() => { }, 1f);
        var b = scheduler.ScheduleDelay(() => { }, 1f);

        scheduler.CancelAll();

        Assert.True(a.IsCanceled);
        Assert.True(b.IsCanceled);
        Assert.Equal("Canceled by CancelAll", a.CancelReason);
        Assert.Equal(2, scheduler.Count);

        scheduler.Tick(0.1f);
        Assert.Equal(0, scheduler.Count);
    }

    [Fact]
    public void Cancel_by_name_matches_exact_name_only()
    {
        var scheduler = new DefaultScheduler();
        var named = scheduler.ScheduleDelay(() => { }, 1f);
        named.Name = "special";
        var unnamed = scheduler.ScheduleDelay(() => { }, 1f);

        scheduler.CancelByName("special");

        Assert.True(named.IsCanceled);
        Assert.Equal("Canceled by name: special", named.CancelReason);
        Assert.False(unnamed.IsCanceled);
    }

    [Fact]
    public void Tasks_fire_exactly_once_in_time_order()
    {
        var order = new System.Collections.Generic.List<string>();
        var scheduler = new DefaultScheduler();
        scheduler.ScheduleDelay(() => order.Add("slow"), 3f);
        scheduler.ScheduleDelay(() => order.Add("fast"), 1f);
        scheduler.ScheduleDelay(() => order.Add("mid"), 2f);

        for (int i = 0; i < 8; i++)
            scheduler.Tick(0.5f);

        Assert.Equal(new[] { "fast", "mid", "slow" }, order);
        Assert.Equal(0, scheduler.Count);
    }

    [Fact]
    public void Removing_middle_task_does_not_break_other_tasks()
    {
        // 中间任务先完成触发 swap-remove，其余任务必须不受影响、各恰好执行一次。
        int aFired = 0, bFired = 0, cFired = 0;
        var scheduler = new DefaultScheduler();
        scheduler.ScheduleDelay(() => aFired++, 5f);
        scheduler.ScheduleDelay(() => bFired++, 1f);
        scheduler.ScheduleDelay(() => cFired++, 3f);

        for (int i = 0; i < 12; i++)
            scheduler.Tick(0.5f);

        Assert.Equal(1, aFired);
        Assert.Equal(1, bFired);
        Assert.Equal(1, cFired);
        Assert.Equal(0, scheduler.Count);
    }

    [Fact]
    public void Scheduling_from_callback_is_safe()
    {
        int secondFired = 0;
        var scheduler = new DefaultScheduler();

        scheduler.ScheduleDelay(
            () => scheduler.ScheduleDelay(() => secondFired++, 0.5f),
            1f);

        for (int i = 0; i < 6; i++)
            scheduler.Tick(0.5f);

        Assert.Equal(1, secondFired);
        Assert.Equal(0, scheduler.Count);
    }

    [Fact]
    public void Periodic_task_completes_via_scheduler_removal()
    {
        int fired = 0;
        var scheduler = new DefaultScheduler();
        scheduler.SchedulePeriodic(() => fired++, 1f, -1f, 3);

        for (int i = 0; i < 8; i++)
            scheduler.Tick(0.5f);

        Assert.Equal(3, fired);
        Assert.Equal(0, scheduler.Count);
    }

    [Fact]
    public void Continuous_task_ticks_through_scheduler()
    {
        var deltas = new System.Collections.Generic.List<float>();
        var scheduler = new DefaultScheduler();
        scheduler.ScheduleContinuous(deltas.Add, null, 1f);

        scheduler.Tick(0.5f);
        scheduler.Tick(0.5f);

        Assert.Equal(new[] { 0.5f, 0.5f }, deltas);
        Assert.Equal(0, scheduler.Count);
    }

    [Fact]
    public void Tick_with_no_tasks_is_noop()
    {
        var scheduler = new DefaultScheduler();

        scheduler.Tick(1f);

        Assert.Equal(0, scheduler.Count);
    }

    [Fact]
    public void Non_positive_period_rejected_through_scheduler()
    {
        var scheduler = new DefaultScheduler();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => scheduler.SchedulePeriodic(() => { }, 0f));
        Assert.Equal(0, scheduler.Count);
    }
}
