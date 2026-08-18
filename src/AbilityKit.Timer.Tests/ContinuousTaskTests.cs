using AbilityKit.Timer;
using Xunit;

namespace AbilityKit.Timer.Tests;

public sealed class ContinuousTaskTests
{
    [Fact]
    public void OnTick_receives_delta_time_each_update()
    {
        var deltas = new System.Collections.Generic.List<float>();
        var task = new ContinuousTask(deltas.Add, null, -1f);

        task.Update(0.1f);
        task.Update(0.2f);

        Assert.Equal(new[] { 0.1f, 0.2f }, deltas);
    }

    [Fact]
    public void Completes_at_duration_and_invokes_onComplete_once()
    {
        int ticks = 0;
        int completed = 0;
        var task = new ContinuousTask(_ => ticks++, () => completed++, 2f);

        for (int i = 0; i < 4; i++)
            task.Update(0.5f);

        Assert.Equal(4, ticks);
        Assert.Equal(1, completed);
        Assert.True(task.IsCompleted);
        Assert.Equal(TaskState.Completed, task.State);
    }

    [Fact]
    public void Boundary_tick_still_fires_onTick_before_completing()
    {
        var order = new System.Collections.Generic.List<string>();
        var task = new ContinuousTask(
            _ => order.Add("tick"),
            () => order.Add("complete"),
            1f);

        task.Update(1f);

        Assert.Equal(new[] { "tick", "complete" }, order);
    }

    [Fact]
    public void Unbounded_task_never_completes_on_its_own()
    {
        int ticks = 0;
        var task = new ContinuousTask(_ => ticks++, null, -1f);

        for (int i = 0; i < 100; i++)
            task.Update(0.5f);

        Assert.Equal(100, ticks);
        Assert.False(task.IsCompleted);
        Assert.Equal(TaskState.Running, task.State);
        Assert.Equal(float.MaxValue, task.Duration);
    }

    [Fact]
    public void External_complete_does_not_invoke_onComplete()
    {
        // 当前契约：Complete() 只置完成标志，不走 onComplete 回调路径。
        int completed = 0;
        var task = new ContinuousTask(_ => { }, () => completed++, -1f);

        task.Update(0.5f);
        task.Complete();
        task.Update(0.5f);

        Assert.Equal(0, completed);
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void Cancel_stops_ticking()
    {
        int ticks = 0;
        var task = new ContinuousTask(_ => ticks++, null, -1f);

        task.Update(0.5f);
        task.RequestCancel("done");
        task.Update(0.5f);

        Assert.Equal(1, ticks);
        Assert.True(task.IsCanceled);
        Assert.Equal(TaskState.Canceled, task.State);
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void Update_after_completion_is_noop()
    {
        int ticks = 0;
        var task = new ContinuousTask(_ => ticks++, null, 1f);

        task.Update(1f);
        task.Update(1f);

        Assert.Equal(1, ticks);
    }

    [Fact]
    public void Elapsed_time_accumulates()
    {
        var task = new ContinuousTask(_ => { }, null, 10f);

        task.Update(0.3f);
        task.Update(0.4f);

        Assert.Equal(0.7f, task.ElapsedTime, 5);
    }
}
