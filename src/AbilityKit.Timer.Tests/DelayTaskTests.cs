using AbilityKit.Timer;
using Xunit;

namespace AbilityKit.Timer.Tests;

public sealed class DelayTaskTests
{
    [Fact]
    public void Does_not_fire_before_delay_is_reached()
    {
        int fired = 0;
        var task = new DelayTask(() => fired++, 1f);

        task.Update(0.5f);

        Assert.Equal(0, fired);
        Assert.Equal(TaskState.Running, task.State);
        Assert.False(task.IsCompleted);
    }

    [Fact]
    public void Fires_once_when_accumulated_time_reaches_delay()
    {
        int fired = 0;
        var task = new DelayTask(() => fired++, 1f);

        task.Update(0.5f);
        task.Update(0.5f);

        Assert.Equal(1, fired);
        Assert.Equal(TaskState.Completed, task.State);
        Assert.True(task.IsCompleted);
        Assert.Equal(1f, task.ElapsedTime, 3);
        Assert.Equal(1f, task.Duration, 3);
    }

    [Fact]
    public void Zero_delay_fires_on_first_update()
    {
        int fired = 0;
        var task = new DelayTask(() => fired++, 0f);

        task.Update(0.016f);

        Assert.Equal(1, fired);
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void Large_delta_fires_exactly_once()
    {
        int fired = 0;
        var task = new DelayTask(() => fired++, 1f);

        task.Update(10f);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void Cancel_before_due_suppresses_callback()
    {
        int fired = 0;
        var task = new DelayTask(() => fired++, 1f);

        task.Update(0.5f);
        task.RequestCancel("unit-test");
        task.Update(1f);

        Assert.Equal(0, fired);
        Assert.True(task.IsCanceled);
        Assert.Equal(TaskState.Canceled, task.State);
        Assert.Equal("unit-test", task.CancelReason);
    }

    [Fact]
    public void External_complete_skips_callback()
    {
        int fired = 0;
        var task = new DelayTask(() => fired++, 1f);

        task.Update(0.5f);
        task.Complete();
        task.Update(0.5f);

        Assert.Equal(0, fired);
        Assert.True(task.IsCompleted);
        Assert.Equal(TaskState.Completed, task.State);
    }

    [Fact]
    public void Update_after_completion_is_noop()
    {
        int fired = 0;
        var task = new DelayTask(() => fired++, 1f);

        task.Update(1f);
        task.Update(1f);
        task.Update(1f);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void Update_after_cancel_is_noop()
    {
        int fired = 0;
        var task = new DelayTask(() => fired++, 1f);

        task.RequestCancel();
        task.Update(5f);

        Assert.Equal(0, fired);
        Assert.True(task.IsCanceled);
    }

    [Fact]
    public void Default_name_is_null_and_settable()
    {
        var task = new DelayTask(() => { }, 1f);
        Assert.Null(task.Name);

        task.Name = "delay-a";
        Assert.Equal("delay-a", task.Name);
    }
}
