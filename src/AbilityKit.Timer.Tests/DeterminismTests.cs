using AbilityKit.Timer;
using Xunit;

namespace AbilityKit.Timer.Tests;

/// <summary>定点时间路径契约：对外 float 无感、内部 raw 累加不漂移、同输入同结果。</summary>
public sealed class DeterminismTests
{
    [Fact]
    public void Long_accumulation_fires_exactly_once_without_drift()
    {
        // dyadic 步长 0.125f（精确可表示）累加 8000 帧，恰好到 1000，无浮点累加漂移。
        int fired = 0;
        var task = new DelayTask(() => fired++, 1000f);

        for (int i = 0; i < 8_000; i++)
            task.Update(0.125f);

        Assert.Equal(1, fired);
        Assert.True(task.IsCompleted);
        Assert.Equal(1000f, task.ElapsedTime, 2);
    }

    [Fact]
    public void Non_dyadic_delta_truncates_toward_zero()
    {
        // 定点换算用 (long) 截断（向零），非 dyadic 的 0.0001f 向下截断，
        // 10000 次累加略小于 1.0，故到期前不会触发。钉住该已知边界（非 dyadic 步长定点 floor）。
        int fired = 0;
        var task = new DelayTask(() => fired++, 1f);

        for (int i = 0; i < 10_000; i++)
            task.Update(0.0001f);

        Assert.Equal(0, fired);
        Assert.False(task.IsCompleted);

        // 再来一帧则超过阈值并触发。
        task.Update(0.0001f);
        Assert.Equal(1, fired);
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void Periodic_task_fires_exact_count_over_many_frames()
    {
        int fired = 0;
        var task = new PeriodicTask(() => fired++, 1f, -1f, 1000);

        for (int i = 0; i < 1000; i++)
            task.Update(1f);

        Assert.Equal(1000, fired);
        Assert.Equal(1000, task.ExecutionCount);
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void Identical_input_sequences_produce_bit_identical_results()
    {
        var a = new DelayTask(() => { }, 10f);
        var b = new DelayTask(() => { }, 10f);

        float[] ticks = { 0.1f, 0.25f, 0.05f, 1.5f, 0.333f, 0.001f };
        foreach (var dt in ticks)
        {
            a.Update(dt);
            b.Update(dt);
        }

        // 同输入 → raw 累加逐位一致 → float 视图与完成状态逐位一致。
        Assert.Equal(a.ElapsedTime, b.ElapsedTime);
        Assert.Equal(a.IsCompleted, b.IsCompleted);
        Assert.Equal(a.State, b.State);
    }

    [Fact]
    public void Elapsed_time_is_monotonic_across_ticks()
    {
        var task = new ContinuousTask(_ => { }, null, -1f);

        float previous = -1f;
        for (int i = 0; i < 100; i++)
        {
            task.Update(0.016f);
            var current = task.ElapsedTime;
            Assert.True(current >= previous, $"elapsed regressed at tick {i}: {previous} -> {current}");
            previous = current;
        }
    }
}
