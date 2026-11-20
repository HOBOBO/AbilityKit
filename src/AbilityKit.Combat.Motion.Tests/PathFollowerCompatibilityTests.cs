using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Events;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Motion.Tests;

/// <summary>
/// 以拆分前独立保存的算法逐帧比较来源输出，锁定现有步进和管线通知边界。
/// </summary>
public sealed class PathFollowerCompatibilityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Source_matches_original_algorithm_step_by_step(int scenario)
    {
        var points = scenario switch
        {
            0 => Array.Empty<Vec3>(),
            1 => new[] { new Vec3(1, 0, 0) },
            2 => new[] { Vec3.Zero, Vec3.Zero, new Vec3(.01f, 0, 0), new Vec3(1, 1, 1) },
            3 => new[] { new Vec3(.25f, 0, 0), new Vec3(.5f, .5f, .5f) },
            4 => new[] { new Vec3(.000001f, 0, 0) },
            _ => new[] { new Vec3(1, 0, 0), new Vec3(2, 1, 0), new Vec3(3, 1, 1) },
        };
        var speed = scenario == 5 ? 100f : 2f;
        var epsilon = scenario == 4 ? 1e-8f : .25f;
        var source = new PathFollowerMotionSource(points, speed, epsilon, 17, -7, MotionStacking.Additive);
        var reference = new OriginalPathFollower(points, speed, epsilon, 17, -7, MotionStacking.Additive);
        if (points.Length > 0) points[0] = new Vec3(999, 999, 999);
        var actual = new MotionState(Vec3.Zero);
        var expected = actual;
        var times = new[] { 0f, -.25f, .125f, .5f, 2f, .125f, .25f, .5f };
        for (var frame = 0; frame < 32; frame++)
        {
            if (scenario == 6 && frame == 4)
            {
                source.Cancel();
                reference.Cancel();
            }
            if (scenario == 6 && frame == 7)
            {
                var next = new[] { new Vec3(0, 2, 0), new Vec3(1, 2, 3) };
                source.Configure(next, 3f, .05f);
                reference.Configure(next, 3f, .05f);
            }
            var initialDelta = new Vec3(.0625f, -.03125f, .125f);
            var actualDelta = initialDelta;
            var expectedDelta = initialDelta;
            source.Tick(23, ref actual, times[frame % times.Length], ref actualDelta);
            reference.Tick(23, ref expected, times[frame % times.Length], ref expectedDelta);
            Assert.Equal(expectedDelta, actualDelta);
            Assert.Equal(expected, actual);
            Assert.Equal(reference.CurrentIndex, source.CurrentIndex);
            Assert.Equal(reference.IsActive, source.IsActive);
            Assert.Equal(!reference.IsActive, source.IsFinished);
            Assert.True(source.ExportSnapshot(out var snapshot));
            Assert.Equal(reference.Snapshot(), snapshot);
            actual.Position += actualDelta;
            expected.Position += expectedDelta;
        }
    }

    [Fact]
    public void Tiny_final_segment_keeps_active_until_the_next_tick()
    {
        var source = new PathFollowerMotionSource(new[] { new Vec3(.000001f, 0, 0) }, 1f, 1e-8f);
        var motion = new MotionState(Vec3.Zero);
        var delta = Vec3.Zero;
        source.Tick(1, ref motion, .25f, ref delta);
        Assert.Equal(1, source.CurrentIndex);
        Assert.True(source.IsActive);
        Assert.Equal(Vec3.Zero, delta);
        source.Tick(1, ref motion, .25f, ref delta);
        Assert.False(source.IsActive);
    }

    [Fact]
    public void Large_step_stops_at_one_waypoint_and_does_not_write_position()
    {
        var source = new PathFollowerMotionSource(new[] { new Vec3(1, 0, 0), new Vec3(2, 0, 0) }, 100);
        var state = new MotionState(Vec3.Zero);
        var delta = new Vec3(0, 0, 1);
        source.Tick(1, ref state, 1f, ref delta);
        Assert.Equal(new Vec3(1, 0, 1), delta);
        Assert.Equal(Vec3.Zero, state.Position);
        Assert.Equal(new Vec3(1, 0, 0), state.Forward);
        Assert.Equal(1, source.CurrentIndex);
        Assert.True(source.IsActive);
    }

    [Fact]
    public void Legacy_snapshot_keeps_the_destination_route_and_its_clamp_rules()
    {
        var source = new PathFollowerMotionSource(new[] { new Vec3(100, 0, 0), new Vec3(200, 0, 0) }, -1f, -1f);
        Assert.True(source.ExportSnapshot(out var initial));
        Assert.Equal(.0001f, initial.Float0);
        Assert.Equal(.01f, initial.Float1);
        var partial = new MotionSourceSnapshot
        {
            GroupId = -8, Priority = -4, Stacking = (MotionStacking)77,
            Index = -3, IsActive = true, Float0 = -2f, Float1 = 0f,
        };
        Assert.True(source.ImportSnapshot(in partial));
        Assert.True(source.ExportSnapshot(out var restored));
        Assert.Equal(0, restored.Index);
        Assert.Equal(initial.Float0, restored.Float0);
        Assert.Equal(initial.Float1, restored.Float1);
        Assert.Equal(partial.Stacking, restored.Stacking);
        var motion = new MotionState(Vec3.Zero);
        var delta = Vec3.Zero;
        source.Tick(1, ref motion, 1f, ref delta);
        Assert.Equal(new Vec3(.0001f, 0, 0), delta);

        partial.Index = 99;
        source.ImportSnapshot(in partial);
        Assert.Equal(2, source.CurrentIndex);
        Assert.True(source.IsActive);
        source.Reset();
        source.ExportSnapshot(out var reset);
        Assert.Equal(.05f, reset.Float1);
        source.ImportSnapshot(in partial);
        Assert.False(source.IsActive);
        Assert.Equal(0, source.CurrentIndex);
    }

    [Fact]
    public void Pipeline_suppression_ties_arrival_and_removal_match_original_sources()
    {
        using var actualPipeline = new MotionPipeline { Policy = MotionPipelinePolicy.CreateDefault() };
        using var expectedPipeline = new MotionPipeline { Policy = MotionPipelinePolicy.CreateDefault() };
        var actualEvents = new EventLog();
        var expectedEvents = new EventLog();
        actualPipeline.Events = actualEvents;
        expectedPipeline.Events = expectedEvents;
        var firstPoints = new[] { new Vec3(1, 0, 0) };
        var secondPoints = new[] { new Vec3(0, 0, 1) };
        var first = new PathFollowerMotionSource(firstPoints, 4, .05f, 10, MotionGroups.Path, MotionStacking.OverrideLowerPriority);
        var second = new PathFollowerMotionSource(secondPoints, 4, .05f, 10, MotionGroups.Path, MotionStacking.OverrideLowerPriority);
        var referenceFirst = new OriginalPathFollower(firstPoints, 4, .05f, 10, MotionGroups.Path, MotionStacking.OverrideLowerPriority);
        var referenceSecond = new OriginalPathFollower(secondPoints, 4, .05f, 10, MotionGroups.Path, MotionStacking.OverrideLowerPriority);
        actualPipeline.AddSource(first);
        actualPipeline.AddSource(second);
        expectedPipeline.AddSource(referenceFirst);
        expectedPipeline.AddSource(referenceSecond);
        actualPipeline.AddSource(Control());
        expectedPipeline.AddSource(Control());
        var actual = new MotionState(Vec3.Zero);
        var expected = actual;
        for (var frame = 1; frame <= 12; frame++)
        {
            actualEvents.Frame = expectedEvents.Frame = frame;
            var actualOutput = new MotionOutput();
            var expectedOutput = new MotionOutput();
            actualPipeline.Tick(42, ref actual, .25f, ref actualOutput);
            expectedPipeline.Tick(42, ref expected, .25f, ref expectedOutput);
            Assert.Equal(expected, actual);
            Assert.Equal(expectedOutput.DesiredDelta, actualOutput.DesiredDelta);
            Assert.Equal(referenceFirst.Snapshot(), Snapshot(first));
            Assert.Equal(referenceSecond.Snapshot(), Snapshot(second));
            Assert.Equal(expectedPipeline.SourceCount, actualPipeline.SourceCount);
            Assert.Equal(expectedEvents.Notifications, actualEvents.Notifications);
            if (frame <= 2)
            {
                Assert.Equal(0, first.CurrentIndex);
                Assert.Equal(0, second.CurrentIndex);
            }
        }
        Assert.Equal(2, actualEvents.Notifications.Count(n => n.Kind == "arrive"));
        Assert.Equal(0, actualPipeline.SourceCount);

        static FixedDeltaMotionSource Control() => new(Vec3.Zero, .5f, 100, MotionGroups.Control, MotionStacking.OverrideLowerPriority);
    }

    internal static MotionSourceSnapshot Snapshot(PathFollowerMotionSource source)
    {
        Assert.True(source.ExportSnapshot(out var snapshot));
        return snapshot;
    }

    /// <summary>
    /// 保存本批重构前的字段和推进分支，不调用新的 State 或 Stepper。
    /// </summary>
    private sealed class OriginalPathFollower : IMotionSource, IMotionFinishEventSource
    {
        private Vec3[] _points = Array.Empty<Vec3>();
        private float _speed;
        private float _epsilon;
        public int GroupId { get; private set; }
        public MotionStacking Stacking { get; private set; }
        public int Priority { get; private set; }
        public bool IsActive { get; private set; }
        public int CurrentIndex { get; private set; }
        public MotionFinishEvent FinishEvent => MotionFinishEvent.Arrive;

        public OriginalPathFollower(Vec3[] points, float speed, float epsilon, int priority, int group, MotionStacking stacking)
            => Configure(points, speed, epsilon, priority, group, stacking);

        public void Configure(Vec3[] points, float speed, float epsilon, int priority = 10,
            int group = MotionGroups.Path, MotionStacking stacking = MotionStacking.ExclusiveHighestPriority)
        {
            _points = (Vec3[])points.Clone();
            _speed = speed <= 0 ? .0001f : speed;
            _epsilon = epsilon <= 0 ? .01f : epsilon;
            Priority = priority;
            GroupId = group;
            Stacking = stacking;
            CurrentIndex = 0;
            IsActive = points.Length > 0;
        }

        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            if (!IsActive) return;
            if (dt <= 0f) return;
            if (_points.Length == 0) { IsActive = false; return; }
            while (CurrentIndex < _points.Length)
            {
                var to = _points[CurrentIndex] - state.Position;
                if (to.SqrMagnitude > _epsilon * _epsilon) break;
                CurrentIndex++;
            }
            if (CurrentIndex >= _points.Length) { IsActive = false; return; }
            var target = _points[CurrentIndex];
            var deltaTo = target - state.Position;
            var dist = DeterministicMathBridge.Magnitude(in deltaTo);
            if (dist <= .00001f) { CurrentIndex++; return; }
            var maxStep = _speed * dt;
            if (maxStep <= 0f) return;
            var step = maxStep;
            if (step > dist) step = dist;
            var dir = deltaTo / dist;
            outDesiredDelta += dir * step;
            state.Forward = dir;
            if (step >= dist - .00001f)
            {
                CurrentIndex++;
                if (CurrentIndex >= _points.Length) IsActive = false;
            }
        }

        public void Cancel() => IsActive = false;

        public MotionSourceSnapshot Snapshot() => new()
        {
            GroupId = GroupId, Priority = Priority, Stacking = Stacking, IsActive = IsActive,
            Index = CurrentIndex, Float0 = _speed, Float1 = _epsilon,
        };
    }

    /// <summary>
    /// 只记录实际管线通知的帧和位置，用于比较到达与过期边界。
    /// </summary>
    internal sealed class EventLog : IMotionEventSink
    {
        public int Frame;
        public readonly List<(int Frame, string Kind, int Id, Vec3 Position)> Notifications = new();
        public void OnHit(int id, in MotionState state, in MotionHit hit) => Notifications.Add((Frame, "hit", id, state.Position));
        public void OnArrive(int id, in MotionState state) => Notifications.Add((Frame, "arrive", id, state.Position));
        public void OnExpired(int id, in MotionState state) => Notifications.Add((Frame, "expired", id, state.Position));
    }
}
