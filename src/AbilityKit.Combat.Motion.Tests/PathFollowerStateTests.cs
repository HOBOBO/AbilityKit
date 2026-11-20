using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Motion.Tests;

/// <summary>
/// 从独立消费程序集验证完整领域状态的所有权、静默恢复与充分性。
/// </summary>
public sealed class PathFollowerStateTests
{
    [Fact]
    public void Capture_contains_the_full_route_and_all_live_fields()
    {
        var points = new[] { Vec3.Zero, new Vec3(2, 0, 0), new Vec3(2, 3, 4) };
        var source = new PathFollowerMotionSource(points, 4, .125f, -12, -91, MotionStacking.Additive);
        var motion = new MotionState(Vec3.Zero);
        var delta = Vec3.Zero;
        source.Tick(1, ref motion, .125f, ref delta);
        var state = source.CaptureState();
        Assert.Equal(points, state.Points);
        Assert.NotSame(points, state.Points);
        Assert.Equal(4, state.Speed);
        Assert.Equal(.125f, state.ArriveEpsilon);
        Assert.Equal(-12, state.Priority);
        Assert.Equal(-91, state.GroupId);
        Assert.Equal(MotionStacking.Additive, state.Stacking);
        Assert.Equal(1, state.Index);
        Assert.True(state.IsActive);
        Assert.Equal(new Vec3(.5f, 0, 0), delta);
        PathFollowerMotionSource.ValidateState(in state);
    }

    [Fact]
    public void Captures_configure_input_and_live_source_do_not_share_mutable_arrays()
    {
        var points = new[] { new Vec3(4, 0, 0) };
        var source = new PathFollowerMotionSource(points, 2);
        var first = source.CaptureState();
        var second = source.CaptureState();
        Assert.NotSame(first.Points, second.Points);
        points[0] = new Vec3(100, 0, 0);
        first.Points[0] = new Vec3(200, 0, 0);
        first.Speed = 999;
        source.Configure(new[] { new Vec3(0, 4, 0) }, 7);
        Assert.Equal(new Vec3(4, 0, 0), second.Points[0]);
        Assert.Equal(2, second.Speed);
        Assert.Equal(new Vec3(0, 4, 0), source.CaptureState().Points[0]);
        Assert.Equal(7, source.CaptureState().Speed);
    }

    [Fact]
    public void Restore_owns_its_array_and_is_independent_of_future_input_changes()
    {
        var state = ValidState();
        var expected = Copy(state);
        var source = new PathFollowerMotionSource(Array.Empty<Vec3>(), 100);
        source.RestoreState(in state);
        AssertState(expected, source.CaptureState());
        state.Points[0] = new Vec3(-999, -999, -999);
        state.Index = 2;
        state.Speed = 999;
        AssertState(expected, source.CaptureState());
        var captured = source.CaptureState();
        captured.Points[1] = Vec3.Zero;
        AssertState(expected, source.CaptureState());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(12, false)]
    [InlineData(48, false)]
    [InlineData(3, true)]
    public void Full_state_replays_on_unrelated_sources_from_start_middle_finish_and_cancel(int checkpointFrame, bool cancel)
    {
        var source = new PathFollowerMotionSource(
            new[] { Vec3.Zero, new Vec3(2, 0, 0), new Vec3(2, 2, 0), new Vec3(2, 2, 2) },
            2, .05f, -17, 123, MotionStacking.OverrideLowerPriority);
        var motion = new MotionState(Vec3.Zero);
        for (var i = 0; i < checkpointFrame; i++) Advance(source, ref motion, .125f);
        if (cancel) source.Cancel();
        var checkpoint = source.CaptureState();
        var motionCheckpoint = motion;
        var expected = new List<(PathFollowerState Path, MotionState Motion, Vec3 Delta)>();
        for (var i = 0; i < 40; i++)
        {
            var delta = Advance(source, ref motion, .125f);
            expected.Add((source.CaptureState(), motion, delta));
        }

        for (var replay = 0; replay < 2; replay++)
        {
            var restored = new PathFollowerMotionSource(new[] { new Vec3(-1000, 0, 0) }, 999);
            restored.RestoreState(in checkpoint);
            var replayMotion = motionCheckpoint;
            AssertState(checkpoint, restored.CaptureState());
            foreach (var frame in expected)
            {
                Assert.Equal(frame.Delta, Advance(restored, ref replayMotion, .125f));
                Assert.Equal(frame.Motion, replayMotion);
                AssertState(frame.Path, restored.CaptureState());
            }
        }
    }

    [Fact]
    public void Two_sources_interleave_without_hidden_progress_or_restore_interference()
    {
        var a = new PathFollowerMotionSource(new[] { new Vec3(4, 0, 0), new Vec3(4, 2, 0) }, 2);
        var b = new PathFollowerMotionSource(new[] { new Vec3(0, 0, 5), new Vec3(1, 2, 5) }, 3);
        var stateA = a.CaptureState();
        var stateB = b.CaptureState();
        var expectedA = RunAlone(stateA, .125f);
        var expectedB = RunAlone(stateB, .25f);
        var motionA = new MotionState(Vec3.Zero);
        var motionB = new MotionState(Vec3.Zero);
        for (var i = 0; i < expectedA.Count; i++)
        {
            Advance(b, ref motionB, .25f);
            var savedB = b.CaptureState();
            Advance(a, ref motionA, .125f);
            var savedA = a.CaptureState();
            a.RestoreState(in savedA);
            b.RestoreState(in savedB);
            Assert.Equal(expectedA[i], motionA);
            Assert.Equal(expectedB[i], motionB);
        }
    }

    [Theory]
    [InlineData("null-points", "Points")]
    [InlineData("nan-x", "Points[0]")]
    [InlineData("positive-infinity-y", "Points[1]")]
    [InlineData("negative-infinity-z", "Points[2]")]
    [InlineData("zero-speed", "Speed")]
    [InlineData("negative-speed", "Speed")]
    [InlineData("nan-speed", "Speed")]
    [InlineData("positive-infinity-speed", "Speed")]
    [InlineData("negative-infinity-speed", "Speed")]
    [InlineData("zero-epsilon", "ArriveEpsilon")]
    [InlineData("negative-epsilon", "ArriveEpsilon")]
    [InlineData("nan-epsilon", "ArriveEpsilon")]
    [InlineData("positive-infinity-epsilon", "ArriveEpsilon")]
    [InlineData("negative-infinity-epsilon", "ArriveEpsilon")]
    [InlineData("undefined-stacking", "Stacking")]
    [InlineData("negative-index", "Index")]
    [InlineData("overrun-index", "Index")]
    [InlineData("empty-active", "IsActive")]
    [InlineData("empty-nonzero-index", "Index")]
    [InlineData("default", "Points")]
    public void Invalid_complete_state_is_rejected_readonly_and_restore_writes_nothing(string kind, string diagnosticField)
    {
        var invalid = ValidState();
        switch (kind)
        {
            case "null-points": invalid.Points = null!; break;
            case "nan-x": invalid.Points[0] = new Vec3(float.NaN, 0, 0); break;
            case "positive-infinity-y": invalid.Points[1] = new Vec3(0, float.PositiveInfinity, 0); break;
            case "negative-infinity-z": invalid.Points[2] = new Vec3(0, 0, float.NegativeInfinity); break;
            case "zero-speed": invalid.Speed = 0; break;
            case "negative-speed": invalid.Speed = -1; break;
            case "nan-speed": invalid.Speed = float.NaN; break;
            case "positive-infinity-speed": invalid.Speed = float.PositiveInfinity; break;
            case "negative-infinity-speed": invalid.Speed = float.NegativeInfinity; break;
            case "zero-epsilon": invalid.ArriveEpsilon = 0; break;
            case "negative-epsilon": invalid.ArriveEpsilon = -1; break;
            case "nan-epsilon": invalid.ArriveEpsilon = float.NaN; break;
            case "positive-infinity-epsilon": invalid.ArriveEpsilon = float.PositiveInfinity; break;
            case "negative-infinity-epsilon": invalid.ArriveEpsilon = float.NegativeInfinity; break;
            case "undefined-stacking": invalid.Stacking = (MotionStacking)999; break;
            case "negative-index": invalid.Index = -1; break;
            case "overrun-index": invalid.Index = 4; break;
            case "empty-active": invalid.Points = Array.Empty<Vec3>(); invalid.Index = 0; break;
            case "empty-nonzero-index": invalid.Points = Array.Empty<Vec3>(); invalid.IsActive = false; break;
            case "default": invalid = default; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        var invalidBefore = Copy(invalid);
        var source = new PathFollowerMotionSource(new[] { new Vec3(3, 4, 5), new Vec3(6, 7, 8) }, 7);
        var motion = new MotionState(Vec3.Zero);
        Advance(source, ref motion, .125f);
        var before = source.CaptureState();
        var validationError = Assert.Throws<ArgumentException>(() => PathFollowerMotionSource.ValidateState(in invalid));
        Assert.Contains(diagnosticField, validationError.Message);
        AssertState(invalidBefore, invalid);
        AssertState(before, source.CaptureState());
        var restoreError = Assert.Throws<ArgumentException>(() => source.RestoreState(in invalid));
        Assert.Contains(diagnosticField, restoreError.Message);
        AssertState(invalidBefore, invalid);
        AssertState(before, source.CaptureState());
    }

    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 1)]
    [InlineData(false, 3)]
    public void Legal_end_or_cancelled_states_restore_without_normalization(bool active, int index)
    {
        var state = ValidState();
        state.IsActive = active;
        state.Index = index;
        state.Priority = int.MinValue;
        state.GroupId = int.MaxValue;
        var source = new PathFollowerMotionSource(Array.Empty<Vec3>(), 1);
        source.RestoreState(in state);
        AssertState(state, source.CaptureState());
        var motion = new MotionState(Vec3.Zero);
        Advance(source, ref motion, 0);
        AssertState(state, source.CaptureState());
        Advance(source, ref motion, .25f);
        Assert.False(source.IsActive);
        Assert.Equal(index, source.CurrentIndex);
    }

    [Fact]
    public void Reset_empty_and_null_configurations_produce_valid_complete_states()
    {
        var source = new PathFollowerMotionSource(null!, 0, -1);
        var empty = source.CaptureState();
        PathFollowerMotionSource.ValidateState(in empty);
        Assert.Empty(empty.Points);
        Assert.Equal(.0001f, empty.Speed);
        Assert.Equal(.01f, empty.ArriveEpsilon);
        Assert.False(empty.IsActive);
        source.Reset();
        var reset = source.CaptureState();
        PathFollowerMotionSource.ValidateState(in reset);
        Assert.Empty(reset.Points);
        Assert.Equal(.05f, reset.ArriveEpsilon);
        Assert.Equal(MotionGroups.Path, reset.GroupId);
        Assert.Equal(10, reset.Priority);
        Assert.Equal(MotionStacking.ExclusiveHighestPriority, reset.Stacking);
        source.RestoreState(in empty);
        AssertState(empty, source.CaptureState());
    }

    [Fact]
    public void Snapshot_survives_reset_pool_release_and_reconfiguration()
    {
        var source = PathFollowerMotionSource.Rent(new[] { new Vec3(2, 3, 4), new Vec3(4, 3, 2) }, 2);
        var motion = new MotionState(Vec3.Zero);
        Advance(source, ref motion, .25f);
        var checkpoint = source.CaptureState();
        var expected = Copy(checkpoint);
        PathFollowerMotionSource.Release(source);
        var reused = PathFollowerMotionSource.Rent(new[] { new Vec3(999, 0, 0) }, 100);
        try
        {
            Assert.Same(source, reused);
            AssertState(expected, checkpoint);
            Assert.Equal(0, reused.CurrentIndex);
            Assert.Equal(100, reused.CaptureState().Speed);
            reused.RestoreState(in checkpoint);
            AssertState(expected, reused.CaptureState());
            checkpoint.Points[0] = Vec3.Zero;
            AssertState(expected, reused.CaptureState());
        }
        finally
        {
            PathFollowerMotionSource.Release(reused);
        }
    }

    [Fact]
    public void Restore_is_silent_keeps_membership_order_and_does_not_repeat_finished_notifications()
    {
        using var pipeline = new MotionPipeline();
        var events = new PathFollowerCompatibilityTests.EventLog();
        pipeline.Events = events;
        var source = new PathFollowerMotionSource(new[] { new Vec3(1, 0, 0) }, 4);
        var other = new PathFollowerMotionSource(new[] { new Vec3(10000, 0, 0) }, 1, priority: 0,
            groupId: MotionGroups.Locomotion, stacking: MotionStacking.Additive);
        pipeline.AddSource(source);
        pipeline.AddSource(other);
        var order = pipeline.CaptureSourceOrder();
        var original = source.CaptureState();
        var changed = Copy(original);
        changed.Priority = -999;
        source.RestoreState(in changed);
        Assert.Empty(events.Notifications);
        Assert.Equal(order, pipeline.CaptureSourceOrder());
        Assert.Equal(0, source.CurrentIndex);
        source.RestoreState(in original);
        var motion = new MotionState(Vec3.Zero);
        var output = new MotionOutput();
        events.Frame = 1;
        pipeline.Tick(1, ref motion, .25f, ref output);
        Assert.False(source.IsActive);
        Assert.True(pipeline.ContainsSource(source));
        Assert.Equal(2, pipeline.SourceCount);
        Assert.Single(events.Notifications);
        var finished = source.CaptureState();
        source.RestoreState(in finished);
        Assert.Single(events.Notifications);
        Assert.Equal(order, pipeline.CaptureSourceOrder());
        events.Frame = 2;
        pipeline.Tick(1, ref motion, .25f, ref output);
        Assert.False(pipeline.ContainsSource(source));
        Assert.Equal(1, pipeline.SourceCount);
        Assert.Single(events.Notifications);
    }

    [Theory]
    [InlineData("speed")]
    [InlineData("epsilon")]
    [InlineData("points")]
    [InlineData("stacking")]
    public void Ordinary_configuration_keeps_legacy_special_values_until_explicit_full_validation(string field)
    {
        var source = new PathFollowerMotionSource(
            new[] { field == "points" ? new Vec3(float.NaN, 0, 0) : new Vec3(4, 0, 0) },
            field == "speed" ? float.NaN : 2,
            field == "epsilon" ? float.PositiveInfinity : .05f,
            stacking: field == "stacking" ? (MotionStacking)99 : MotionStacking.Additive);
        var captured = source.CaptureState();
        Assert.Throws<ArgumentException>(() => PathFollowerMotionSource.ValidateState(in captured));
        AssertState(captured, source.CaptureState());
        var legacy = PathFollowerCompatibilityTests.Snapshot(source);
        Assert.True(source.ImportSnapshot(in legacy));
        AssertState(captured, source.CaptureState());
    }

    [Fact]
    public void Warm_normal_ticks_allocate_no_managed_state_or_route_objects()
    {
        var source = new PathFollowerMotionSource(new[] { new Vec3(1000, 0, 0) }, 1);
        var motion = new MotionState(Vec3.Zero);
        var delta = Vec3.Zero;
        for (var i = 0; i < 256; i++) source.Tick(1, ref motion, .125f, ref delta);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++) source.Tick(1, ref motion, .125f, ref delta);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(0L, allocated);
        Assert.True(source.IsActive);
    }

    private static PathFollowerState ValidState() => new()
    {
        Points = new[] { new Vec3(1, 2, 3), new Vec3(4, 5, 6), new Vec3(7, 8, 9) },
        Speed = 2.5f, ArriveEpsilon = .125f, GroupId = -19, Priority = 27,
        Stacking = MotionStacking.OverrideLowerPriority, Index = 1, IsActive = true,
    };

    private static PathFollowerState Copy(PathFollowerState state)
    {
        state.Points = state.Points == null ? null! : (Vec3[])state.Points.Clone();
        return state;
    }

    private static void AssertState(PathFollowerState expected, PathFollowerState actual)
    {
        Assert.Equal(expected.Points, actual.Points);
        Assert.Equal(expected.Speed, actual.Speed);
        Assert.Equal(expected.ArriveEpsilon, actual.ArriveEpsilon);
        Assert.Equal(expected.GroupId, actual.GroupId);
        Assert.Equal(expected.Priority, actual.Priority);
        Assert.Equal(expected.Stacking, actual.Stacking);
        Assert.Equal(expected.Index, actual.Index);
        Assert.Equal(expected.IsActive, actual.IsActive);
    }

    private static Vec3 Advance(PathFollowerMotionSource source, ref MotionState motion, float dt)
    {
        var delta = Vec3.Zero;
        source.Tick(1, ref motion, dt, ref delta);
        motion.Position += delta;
        return delta;
    }

    private static List<MotionState> RunAlone(PathFollowerState checkpoint, float dt)
    {
        var source = new PathFollowerMotionSource(Array.Empty<Vec3>(), 1);
        source.RestoreState(in checkpoint);
        var motion = new MotionState(Vec3.Zero);
        var frames = new List<MotionState>();
        for (var i = 0; i < 32; i++)
        {
            Advance(source, ref motion, dt);
            frames.Add(motion);
        }
        return frames;
    }
}
