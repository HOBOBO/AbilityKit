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
}
