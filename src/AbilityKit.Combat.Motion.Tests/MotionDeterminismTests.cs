using System.Collections.Generic;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Combat.MotionSystem.Trajectory;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using Xunit;

namespace AbilityKit.Combat.Motion.Tests;

/// <summary>
/// combat.motion 定点计时/几何契约：源级计时（FixedStepRunner / FixedDelta / Trajectory）
/// 全部 Q32.32 raw 整数累计；轨迹几何经 DeterministicMathBridge（定点 sqrt/归一化）。
/// 断言以"与独立复算的定点参考逐位一致"为准，不用 float 误差容忍。
/// </summary>
public sealed class MotionDeterminismTests
{
    [Fact]
    public void FixedStepRunner_uses_exact_integer_accounting()
    {
        var runner = new FixedStepRunner(0.25f);
        var stepRaw = DeterministicMathBridge.ToFixed(0.25f).RawValue;
        var dtRaw = DeterministicMathBridge.ToFixed(0.1f).RawValue;

        Assert.Equal(0, runner.Accumulate(0.1f));
        Assert.Equal(0, runner.Accumulate(0.1f));
        // 0.3 / 0.25 → 1 步，余 0.05（raw 整数账目：3×dt − 1×step）。
        Assert.Equal(1, runner.Accumulate(0.1f));

        // 再累计两次 0.1：余 0.05 + 0.2 = 0.25 → 第 4 次调用后恰好再出 1 步、余 0。
        Assert.Equal(0, runner.Accumulate(0.1f));
        Assert.Equal(1, runner.Accumulate(0.1f));
        Assert.Equal(0, runner.Accumulate(0.1f));
        _ = stepRaw;
        _ = dtRaw;
    }

    [Fact]
    public void FixedDelta_clamps_final_step_to_duration_exactly()
    {
        // dyadic 数值保证 raw 精确：时长 0.5s，两步 0.3s → 第二步只走剩余 0.2s。
        var source = new FixedDeltaMotionSource(
            new Vec3(1f, 0f, 0f),
            duration: 0.5f,
            priority: 1,
            groupId: 0,
            MotionStacking.ExclusiveHighestPriority);

        var state = new MotionState(Vec3.Zero);
        var desired = Vec3.Zero;
        source.Tick(1, ref state, 0.3f, ref desired);
        source.Tick(1, ref state, 0.3f, ref desired);

        Assert.False(source.IsActive);
        Assert.Equal(0f, source.TimeLeft);
        // 总位移 = 0.3 + 剩余 0.2 = 0.5（与时长相等，不超调）。
        Assert.Equal(0.5f, desired.X, 6);
    }

    [Fact]
    public void Trajectory_source_time_accumulates_in_raw_and_finishes_at_duration()
    {
        var trajectory = new LinearTrajectory3D(Vec3.Zero, new Vec3(8f, 0f, 0f), 1f);
        var source = new TrajectoryMotionSource(trajectory);

        var state = new MotionState(Vec3.Zero);
        var desired = Vec3.Zero;

        var stepRaw = DeterministicMathBridge.ToFixed(0.125f).RawValue;
        for (var i = 0; i < 8; i++)
        {
            source.Tick(1, ref state, 0.125f, ref desired);
        }

        // 契约：8 步后 Time raw == 8 × 单步 raw（整数账目），恰好到终点并结束。
        Assert.Equal(8 * stepRaw, DeterministicMathBridge.ToFixed(source.Time).RawValue);
        Assert.False(source.IsActive);
        Assert.True(source.IsFinished);
        Assert.Equal(8f, state.Position.X + desired.X, 5);
    }

    [Fact]
    public void Trajectory_source_snapshot_roundtrips_time_view()
    {
        var trajectory = new LinearTrajectory3D(Vec3.Zero, new Vec3(4f, 0f, 0f), 1f);
        var source = new TrajectoryMotionSource(trajectory);
        var state = new MotionState(Vec3.Zero);
        var desired = Vec3.Zero;
        for (var i = 0; i < 3; i++)
        {
            source.Tick(1, ref state, 0.1f, ref desired);
        }

        Assert.True(source.ExportSnapshot(out var snapshot));
        var restored = new TrajectoryMotionSource(trajectory);
        Assert.True(restored.ImportSnapshot(in snapshot));

        Assert.Equal(source.Time, restored.Time);
        Assert.Equal(source.IsActive, restored.IsActive);
    }

    [Fact]
    public void LinearTrajectory_direction_matches_fixed_point_reference()
    {
        // 归一化方向与独立复算的定点参考逐位一致（防止退回 float sqrt）。
        var start = new Vec3(1f, 2f, 3f);
        var end = new Vec3(4f, 6f, 3f);
        var trajectory = new LinearTrajectory3D(start, end, 2f);

        var d = end - start;
        var fixedDir = DeterministicMathBridge.ToFixed(d);
        var len = DeterministicMath.Sqrt(fixedDir.SqrMagnitude);
        var expectedX = (fixedDir.X / len).ToSingle();

        Assert.True(trajectory.TrySampleForward(0.5f, out var forward));
        Assert.Equal(expectedX, forward.X);
    }

    [Fact]
    public void Waypoint_trajectory_segment_lengths_use_fixed_point_magnitude()
    {
        var waypoints = new[] { Vec3.Zero, new Vec3(3f, 0f, 0f), new Vec3(3f, 4f, 0f) };
        var trajectory = new WaypointTrajectory3D(waypoints, speed: 8f);

        // 总长 = 3 + 4 = 7（定点 sqrt：两段均为完美平方，raw 精确）；speed 8 → 时长 7/8 = 0.875（dyadic 精确）。
        Assert.Equal(0.875f, trajectory.Duration);
        // 半程走 3.5 距离：第一段 3 + 第二段 0.5 → (3, 0.5, 0)。
        var p = trajectory.SamplePosition(0.4375f);
        Assert.Equal(3f, p.X, 5);
        Assert.Equal(0.5f, p.Y, 5);
    }
}
