using AbilityKit.Ability.FrameSync;
using Xunit;

namespace AbilityKit.World.FrameSync.Tests;

/// <summary>
/// world.framesync 包帧时间(FrameTime)的直接契约测试（脱离 demo）。
/// 锁步/回滚核心是确定性模拟的基础；本测试覆盖帧推进、状态重置与帧↔时间换算。
/// 注意：客户端预测栈（ClientPredictionRunner 等）已按 D1 标记废弃，不在承诺范围。
/// </summary>
public sealed class FrameTimeTests
{
    [Fact]
    public void StepTo_advances_frame_delta_and_time()
    {
        var ft = new FrameTime();
        ft.StepTo(new FrameIndex(5), 0.1f);
        Assert.Equal(5, ft.Frame.Value);
        Assert.Equal(0.1f, ft.DeltaTime);
        Assert.Equal(0.1f, ft.Time);

        ft.StepTo(new FrameIndex(6), 0.1f);
        Assert.Equal(6, ft.Frame.Value);
        Assert.Equal(0.2f, ft.Time);
    }

    [Fact]
    public void Reset_sets_frame_and_time()
    {
        var ft = new FrameTime();
        ft.Reset(new FrameIndex(3), 0.3f, 0.1f);
        Assert.Equal(3, ft.Frame.Value);
        Assert.Equal(0.3f, ft.Time);
    }

    [Fact]
    public void FrameToTime_returns_zero_before_fixed_delta_set()
    {
        var ft = new FrameTime();
        Assert.Equal(0f, ft.FrameToTime(new FrameIndex(5)));
    }

    [Fact]
    public void FrameToTime_uses_fixed_delta_after_step()
    {
        var ft = new FrameTime();
        ft.StepTo(new FrameIndex(0), 0.1f);   // 固定 _fixedDelta = 0.1
        // frame.Value * fixedDelta：第 10 帧 ≈ 1.0s（用区间容忍浮点）
        Assert.InRange(ft.FrameToTime(new FrameIndex(10)), 0.99f, 1.01f);
    }

    [Fact]
    public void Time_accumulates_exactly_in_raw_fixed_point()
    {
        // 定点累加契约：N 步后的 TimeRaw 恒等于 N × 单步 raw（整数加法，无漂移）。
        var ft = new FrameTime();
        var stepRaw = AbilityKit.Deterministic.Fixed64.FromSingle(1f / 30f).RawValue;
        for (var i = 1; i <= 300; i++)
        {
            ft.StepTo(new FrameIndex(i), 1f / 30f);
        }

        Assert.Equal(300 * stepRaw, ft.TimeRaw);
    }

    [Fact]
    public void RestoreRaw_roundtrips_exact_accumulated_time()
    {
        var ft = new FrameTime();
        for (var i = 1; i <= 7; i++)
        {
            ft.StepTo(new FrameIndex(i), 1f / 30f);
        }

        var provider = new AbilityKit.Ability.FrameSync.Rollback.FrameTimeRollbackStateProvider(ft);
        var payload = provider.Export(default);
        ft.StepTo(new FrameIndex(8), 1f / 30f);
        provider.Import(default, payload);

        Assert.Equal(7 * AbilityKit.Deterministic.Fixed64.FromSingle(1f / 30f).RawValue, ft.TimeRaw);
    }

    [Fact]
    public void AlignTo_matches_step_accumulation_bit_exactly()
    {
        // 逐帧累加 120 步的对齐基准。
        var stepped = new FrameTime();
        for (var i = 1; i <= 120; i++)
        {
            stepped.StepTo(new FrameIndex(i), 1f / 30f);
        }

        // 客户端预测对齐路径：一步整数重建必须与累加位一致。
        var aligned = new FrameTime();
        aligned.StepTo(new FrameIndex(1), 1f / 30f);
        Assert.True(aligned.AlignTo(new FrameIndex(120), 1f / 30f));

        Assert.Equal(stepped.TimeRaw, aligned.TimeRaw);
        Assert.Equal(stepped.TimeMilliseconds, aligned.TimeMilliseconds);
    }

    [Fact]
    public void TimeMilliseconds_is_pure_integer_floor_of_raw()
    {
        var ft = new FrameTime();
        for (var i = 1; i <= 10; i++)
        {
            ft.StepTo(new FrameIndex(i), 1f / 30f);
        }

        Assert.Equal((ft.TimeRaw * 1000L) >> 32, ft.TimeMilliseconds);
        Assert.Equal(333L, ft.TimeMilliseconds);
    }

    [Fact]
    public void FrameAfterSeconds_uses_fixed_division_from_current_frame()
    {
        var ft = new FrameTime();
        ft.StepTo(new FrameIndex(8), 0.125f);

        // dyadic 步长下精确：0.5s @ 0.125s/帧 = 4 帧，从当前帧起算。
        Assert.Equal(12, ft.FrameAfterSeconds(0.5f).Value);
    }
}
