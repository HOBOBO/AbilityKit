using System;
using AbilityKit.Timer;
using Xunit;

namespace AbilityKit.Timer.Tests;

public sealed class PeriodicTaskTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Non_positive_period_throws_at_construction(float period)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PeriodicTask(() => { }, period));
    }

    [Fact]
    public void Fires_at_fixed_cadence()
    {
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f);

        task.Update(0.5f);
        Assert.Equal(0, fired);

        task.Update(0.5f);
        Assert.Equal(1, fired);

        task.Update(0.5f);
        Assert.Equal(1, fired);

        task.Update(0.5f);
        Assert.Equal(2, fired);
        Assert.Equal(2, task.ExecutionCount);
    }

    [Fact]
    public void Large_delta_catches_up_multiple_fires()
    {
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f);

        task.Update(3.5f);

        Assert.Equal(3, fired);
        Assert.Equal(3, task.ExecutionCount);
        Assert.False(task.IsCompleted);
    }

    [Fact]
    public void Max_executions_limits_and_completes()
    {
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f, -1f, 3);

        task.Update(10f);

        Assert.Equal(3, fired);
        Assert.True(task.IsCompleted);
        Assert.Equal(TaskState.Completed, task.State);
    }

    [Fact]
    public void Max_executions_bounds_catch_up_loop()
    {
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f, -1f, 2);

        task.Update(100f);

        Assert.Equal(2, fired);
    }

    [Fact]
    public void Update_after_max_executions_is_noop()
    {
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f, -1f, 2);

        task.Update(2f);
        task.Update(5f);

        Assert.Equal(2, fired);
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void Cancel_stops_execution_and_reports_canceled()
    {
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f);

        task.Update(1f);
        task.RequestCancel("stop");
        task.Update(5f);

        Assert.Equal(1, fired);
        Assert.True(task.IsCanceled);
        Assert.Equal(TaskState.Canceled, task.State);
        // 当前契约：取消的周期任务 IsCompleted 为 true（调度器据此移除）。
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void Request_cancel_before_first_fire_keeps_count_zero()
    {
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f);

        task.RequestCancel();
        task.Update(2f);

        Assert.Equal(0, fired);
        Assert.True(task.IsCanceled);
    }

    [Fact]
    public void Duration_is_total_deadline_and_limits_fires()
    {
        // duration = 总时长截止：period=1、duration=2，触发时刻只能是 1 和 2，共 2 次。
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f, 2f);

        for (int i = 0; i < 5; i++)
            task.Update(0.5f);

        Assert.Equal(2, fired);
        Assert.Equal(2, task.ExecutionCount);
        Assert.True(task.IsCompleted);
        Assert.Equal(TaskState.Completed, task.State);
        Assert.Equal(2f, task.ElapsedTime, 2);
    }

    [Fact]
    public void Large_tick_beyond_duration_fires_only_within_window()
    {
        // 单帧大 delta 追赶到期：只触发 duration 窗口内的时刻（1、2），随后完成。
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f, 2f);

        task.Update(5f);

        Assert.Equal(2, fired);
        Assert.True(task.IsCompleted);
        Assert.Equal(TaskState.Completed, task.State);
    }

    [Fact]
    public void Duration_boundary_fire_at_exact_deadline_is_included()
    {
        // 触发时刻恰好等于 duration 也算一次（period=1、duration=2 → 触发 1、2）。
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f, 2f);

        task.Update(2f);

        Assert.Equal(2, fired);
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void Duration_reports_infinity_when_unbounded()
    {
        var bounded = new PeriodicTask(() => { }, 1f, 3f);
        var unbounded = new PeriodicTask(() => { }, 1f);

        Assert.Equal(3f, bounded.Duration, 3);
        Assert.Equal(float.MaxValue, unbounded.Duration);
    }

    [Fact]
    public void External_complete_stops_execution()
    {
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f);

        task.Update(1f);
        task.Complete();
        task.Update(1f);

        Assert.Equal(1, fired);
        Assert.True(task.IsCompleted);
    }
}
