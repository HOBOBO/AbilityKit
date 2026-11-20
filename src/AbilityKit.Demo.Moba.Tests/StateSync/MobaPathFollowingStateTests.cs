using System;
using System.Linq;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Events;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

/// <summary>
/// 验证 Path 局部充分状态、原绑定资格及拒绝时的零写入；不授予世界结构恢复资格。
/// </summary>
[Collection("Moba lifecycle preparation")]
public sealed class MobaPathFollowingStateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    [InlineData(20)]
    public void Same_binding_restores_full_values_and_replays_each_motion_frame_silently(int repathFrames)
    {
        using var f = new Fixture();
        var binding = f.Bind();
        binding.Data.FramesSinceRepath = repathFrames;
        var capture = f.Runtime.CaptureState(1);
        var independent = f.Runtime.CaptureState(1);
        Assert.NotSame(capture.State.Source.Value.Points, independent.State.Source.Value.Points);
        var motion = new MotionState(Vec3.Zero);
        var first = new (MotionState Motion, PathFollowerState Source)[8];
        for (var i = 0; i < first.Length; i++)
        {
            Tick(f.Pipeline, ref motion);
            first[i] = (motion, binding.Source.CaptureState());
        }
        binding.Data.LastTarget = new Vec3(99, 0, 99);
        binding.Data.FramesSinceRepath = 7;
        var revision = binding.Revision;
        var order = f.Pipeline.CaptureSourceOrder();
        f.Runtime.RestoreState(capture);
        Assert.Equal(0, f.Events.Count);
        Assert.Equal(revision, binding.Revision);
        Assert.Equal(order, f.Pipeline.CaptureSourceOrder());
        Assert.Equal(capture.State.LastTarget, binding.Data.LastTarget);
        Assert.Equal(repathFrames, binding.Data.FramesSinceRepath);
        Equal(capture.State.Source.Value, binding.Source.CaptureState());
        motion = new MotionState(Vec3.Zero);
        for (var i = 0; i < first.Length; i++)
        {
            Tick(f.Pipeline, ref motion);
            Assert.Equal(first[i].Motion, motion);
            Equal(first[i].Source, binding.Source.CaptureState());
        }
        capture.State.Source.Value.Points[0] = new Vec3(-999, 0, 0);
        Assert.Equal(Vec3.Zero, binding.Source.CaptureState().Points[0]);
        Assert.Equal(Vec3.Zero, binding.Data.Waypoints[0]);
        Assert.Equal(Vec3.Zero, independent.State.Source.Value.Points[0]);
        binding.Data.Waypoints[1] = Vec3.Zero;
        Assert.NotEqual(Vec3.Zero, binding.Source.CaptureState().Points[1]);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("cancelled")]
    [InlineData("inactive-member")]
    [InlineData("inactive-detached")]
    public void Empty_cancelled_and_finished_states_keep_their_distinct_structure(string kind)
    {
        using var f = new Fixture();
        MobaPathFollowingRuntimeService.Binding binding = null;
        if (kind != "absent") binding = f.Bind();
        if (kind == "cancelled") f.Runtime.Detach(binding);
        if (kind.StartsWith("inactive")) binding.Source.Cancel();
        if (kind == "inactive-detached") f.Pipeline.RemoveSource(binding.Source);
        var capture = f.Runtime.CaptureState(1);
        Assert.Equal(kind != "absent", capture.State.BindingExists);
        Assert.Equal(kind.StartsWith("inactive"), capture.State.SourceExists);
        Assert.Equal(kind == "inactive-member", capture.State.IsPipelineMember);
        if (binding != null)
        {
            binding.Data.LastTarget = new Vec3(12, 0, 4);
            binding.Data.FramesSinceRepath = 19;
        }
        f.Runtime.RestoreState(capture);
        Assert.Equal(0, f.Events.Count);
        if (binding != null)
        {
            Assert.Equal(capture.State.LastTarget, binding.Data.LastTarget);
            Assert.Equal(capture.State.FramesSinceRepath, binding.Data.FramesSinceRepath);
        }
        Assert.Equal(kind == "inactive-member" ? 1 : 0, f.Pipeline.SourceCount);
        if (kind.StartsWith("inactive")) Assert.False(binding.Source.IsActive);
    }

    [Theory]
    [InlineData("source-lease")]
    [InlineData("binding")]
    [InlineData("entity")]
    [InlineData("same-reference-new-version")]
    [InlineData("unregistered")]
    [InlineData("pipeline")]
    [InlineData("uninitialized")]
    public void Retired_binding_and_actor_dependencies_reject_without_mutation(string change)
    {
        using var f = new Fixture();
        var binding = f.Bind();
        var capture = f.Runtime.CaptureState(1);
        MotionPipeline replacement = null;
        if (change == "source-lease")
        {
            var old = binding.Source;
            f.Runtime.Detach(binding);
            f.Runtime.Attach(binding, new[] { new Vec3(8, 0, 8) }, 3, .1f, 10);
            Assert.Same(old, binding.Source);
        }
        if (change == "binding")
        {
            f.Runtime.Remove(1);
            binding = f.Bind();
        }
        if (change == "entity") f.Actors.Register(1, f.Context.CreateEntity());
        if (change == "same-reference-new-version")
            f.Actors.RegisterRestored(1, f.Actor, f.Actors.GetEntityVersion(1) + 1);
        if (change == "unregistered") f.Actors.Unregister(1);
        if (change == "pipeline") f.Actor.motion.Pipeline = replacement = new MotionPipeline();
        if (change == "uninitialized") f.Actor.motion.Initialized = false;
        try { RejectUnchanged(f, binding, capture); }
        finally { replacement?.Dispose(); }
    }

    [Theory]
    [InlineData("different-entity")]
    [InlineData("same-reference-new-version")]
    [InlineData("pipeline")]
    public void Normal_visit_cleans_only_old_owned_source_before_rebinding(string change)
    {
        using var f = new Fixture();
        var old = f.Bind();
        var source = old.Source;
        var sibling = new FixedDeltaMotionSource(Vec3.Right, 10, 1, MotionGroups.Ability, MotionStacking.Additive);
        f.Pipeline.AddSource(sibling);
        var actor = f.Actor;
        MotionPipeline replacement = null;
        if (change == "different-entity")
        {
            actor = f.Context.CreateEntity();
            actor.AddActorId(1);
            replacement = new MotionPipeline();
            actor.AddMotion(replacement, default, default, null, null, null, true, default);
            f.Actors.Register(1, actor);
        }
        if (change == "same-reference-new-version")
            f.Actors.RegisterRestored(1, actor, f.Actors.GetEntityVersion(1) + 1);
        if (change == "pipeline") actor.motion.Pipeline = replacement = new MotionPipeline();
        try
        {
            f.Runtime.BeginFrame();
            f.Runtime.Visit(actor, f.Actors.GetEntityVersion(1));
            Assert.False(f.Runtime.TryGet(1, out _));
            Assert.False(f.Pipeline.ContainsSource(source));
            Assert.Same(sibling, Assert.Single(f.Pipeline.CaptureSourceOrder()));
            var current = f.Runtime.Ensure(actor, f.Actors.GetEntityVersion(1));
            Assert.NotSame(old, current);
            Assert.Equal(Vec3.Zero, current.Data.LastTarget);
            Assert.Equal(0, current.Data.FramesSinceRepath);
            Assert.Null(current.Source);
            f.Runtime.EndFrame();
            Assert.Equal(1, f.Runtime.Count);
        }
        finally { replacement?.Dispose(); }
    }

    [Theory]
    [InlineData("removed-source")]
    [InlineData("extra-source")]
    [InlineData("reordered")]
    [InlineData("external-path")]
    [InlineData("duplicate-path")]
    public void Changed_membership_or_order_rejects_without_repairing_pipeline(string change)
    {
        using var f = new Fixture();
        var binding = f.Bind();
        var sibling = new FixedDeltaMotionSource(Vec3.Right, 10, 1, MotionGroups.Ability, MotionStacking.Additive);
        f.Pipeline.AddSource(sibling);
        var capture = f.Runtime.CaptureState(1);
        if (change == "removed-source") f.Pipeline.RemoveSource(binding.Source);
        if (change == "extra-source") f.Pipeline.AddSource(new FixedDeltaMotionSource(Vec3.Right, 10, 1,
            MotionGroups.Ability, MotionStacking.Additive));
        if (change == "reordered") f.Pipeline.RestoreSourceOrder(capture.Order.Reverse().ToArray());
        if (change == "external-path") f.Pipeline.AddSource(new PathFollowerMotionSource(new[] { Vec3.Right }, 1));
        if (change == "duplicate-path") f.Pipeline.AddSource(binding.Source);
        RejectUnchanged(f, binding, capture);
        if (change == "duplicate-path") f.Pipeline.RemoveSource(binding.Source);
    }

    [Theory]
    [InlineData("counter-negative")]
    [InlineData("counter-overflow")]
    [InlineData("target-nan")]
    [InlineData("target-infinity")]
    [InlineData("source-null")]
    [InlineData("source-existence")]
    [InlineData("binding-existence")]
    [InlineData("membership")]
    [InlineData("actor-id")]
    [InlineData("version")]
    [InlineData("path-nan")]
    [InlineData("path-null")]
    [InlineData("speed")]
    public void Invalid_data_is_rejected_before_writing_source_or_planning_values(string defect)
    {
        using var f = new Fixture();
        var binding = f.Bind();
        var capture = f.Runtime.CaptureState(1);
        var state = capture.State;
        if (defect == "counter-negative") state.FramesSinceRepath = -1;
        if (defect == "counter-overflow") state.FramesSinceRepath = 21;
        if (defect == "target-nan") state.LastTarget = new Vec3(0, float.NaN, 0);
        if (defect == "target-infinity") state.LastTarget = new Vec3(float.PositiveInfinity, 0, 0);
        if (defect == "source-null") state.Source = null;
        if (defect == "source-existence") state.SourceExists = false;
        if (defect == "binding-existence") state.BindingExists = false;
        if (defect == "membership") state.IsPipelineMember = false;
        if (defect == "actor-id") state.ActorId++;
        if (defect == "version") state.EntityVersion++;
        var source = state.Source.GetValueOrDefault();
        if (defect == "path-nan") source.Points[0] = new Vec3(0, 0, float.NaN);
        if (defect == "path-null") source.Points = null;
        if (defect == "speed") source.Speed = 0;
        if (defect.StartsWith("path-") || defect == "speed") state.Source = source;
        capture.State = state;
        RejectUnchanged(f, binding, capture);
    }

    [Fact]
    public void Capture_rejects_external_path_and_active_detached_source_without_silently_importing()
    {
        using var f = new Fixture();
        var binding = f.Bind();
        var source = binding.Source;
        f.Pipeline.RemoveSource(source);
        Assert.Throws<InvalidOperationException>(() => f.Runtime.CaptureState(1));
        Assert.True(source.IsActive);
        f.Pipeline.AddSource(source);
        f.Pipeline.AddSource(new PathFollowerMotionSource(new[] { Vec3.Right }, 1));
        Assert.Throws<InvalidOperationException>(() => f.Runtime.CaptureState(1));
        Assert.Equal(2, f.Pipeline.SourceCount);
    }

    [Fact]
    public void Restoring_finished_member_does_not_repeat_arrival_and_next_normal_tick_prunes_it()
    {
        using var f = new Fixture();
        var binding = f.Bind();
        binding.Source.Configure(new[] { Vec3.Zero }, 1, .25f);
        var motion = new MotionState(Vec3.Zero);
        Tick(f.Pipeline, ref motion);
        Assert.Equal(1, f.Events.Count);
        Assert.False(binding.Source.IsActive);
        Assert.True(f.Pipeline.ContainsSource(binding.Source));
        var capture = f.Runtime.CaptureState(1);
        f.Runtime.RestoreState(capture);
        Assert.Equal(1, f.Events.Count);
        Assert.True(f.Pipeline.ContainsSource(binding.Source));
        Tick(f.Pipeline, ref motion);
        Assert.Equal(1, f.Events.Count);
        Assert.False(f.Pipeline.ContainsSource(binding.Source));
        f.Runtime.RestoreState(f.Runtime.CaptureState(1));
        Assert.Equal(1, f.Events.Count);
    }

    [Fact]
    public void Revision_overflow_cannot_retire_or_attach_a_source_and_foreign_capture_is_rejected()
    {
        using var f = new Fixture();
        using var other = new Fixture();
        var binding = f.Bind();
        var capture = f.Runtime.CaptureState(1);
        Assert.Throws<InvalidOperationException>(() => other.Runtime.RestoreState(capture));
        var revision = binding.Revision;
        binding.Revision = long.MaxValue;
        var source = binding.Source;
        Assert.Throws<OverflowException>(() => f.Runtime.Detach(binding));
        Assert.Same(source, binding.Source);
        Assert.True(f.Pipeline.ContainsSource(source));
        binding.Revision = revision;
        f.Runtime.Detach(binding);
        binding.Revision = long.MaxValue;
        Assert.Throws<OverflowException>(() => f.Runtime.Attach(binding, new[] { Vec3.Right }, 1, .1f, 10));
        Assert.Null(binding.Source);
        Assert.Equal(0, f.Pipeline.SourceCount);
    }

    /// <summary>
    /// 对比拒绝前后的实际来源、规划值和完整管线次序，不以异常本身代替零写入证明。
    /// </summary>
    private static void RejectUnchanged(Fixture f, MobaPathFollowingRuntimeService.Binding binding,
        MobaPathFollowingRuntimeService.LocalCapture capture)
    {
        var source = binding.Source.CaptureState();
        var target = binding.Data.LastTarget;
        var frames = binding.Data.FramesSinceRepath;
        var mirror = binding.Data.Waypoints;
        var revision = binding.Revision;
        var order = f.Pipeline.CaptureSourceOrder();
        Assert.True(Record.Exception(() => f.Runtime.ValidateState(capture)) is ArgumentException or InvalidOperationException);
        Assert.True(Record.Exception(() => f.Runtime.RestoreState(capture)) is ArgumentException or InvalidOperationException);
        Equal(source, binding.Source.CaptureState());
        Assert.Equal(target, binding.Data.LastTarget);
        Assert.Equal(frames, binding.Data.FramesSinceRepath);
        Assert.Same(mirror, binding.Data.Waypoints);
        Assert.Equal(revision, binding.Revision);
        Assert.Equal(order, f.Pipeline.CaptureSourceOrder());
        Assert.Equal(0, f.Events.Count);
    }

    private static void Equal(PathFollowerState expected, PathFollowerState actual)
    {
        Assert.Equal(expected.Points, actual.Points);
        Assert.Equal(expected.Index, actual.Index);
        Assert.Equal(expected.Speed, actual.Speed);
        Assert.Equal(expected.ArriveEpsilon, actual.ArriveEpsilon);
        Assert.Equal(expected.GroupId, actual.GroupId);
        Assert.Equal(expected.Priority, actual.Priority);
        Assert.Equal(expected.Stacking, actual.Stacking);
        Assert.Equal(expected.IsActive, actual.IsActive);
    }

    private static void Tick(MotionPipeline pipeline, ref MotionState motion)
    {
        var output = new MotionOutput();
        pipeline.Tick(1, ref motion, .1f, ref output);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly ActorContext Context = new ActorContext();
        internal readonly MobaActorRegistry Actors = new MobaActorRegistry();
        internal readonly MotionPipeline Pipeline = new MotionPipeline();
        internal readonly CounterSink Events = new CounterSink();
        internal readonly ActorEntity Actor;
        internal readonly MobaPathFollowingRuntimeService Runtime;

        internal Fixture()
        {
            Actor = Context.CreateEntity();
            Actor.AddActorId(1);
            Pipeline.Events = Events;
            Actor.AddMotion(Pipeline, default, default, null, null, Events, true, default);
            Actors.Register(1, Actor);
            Runtime = new MobaPathFollowingRuntimeService(Actors);
        }

        internal MobaPathFollowingRuntimeService.Binding Bind()
        {
            var binding = Runtime.Ensure(Actor, Actors.GetEntityVersion(1));
            Runtime.Attach(binding, new[] { Vec3.Zero, new Vec3(20, 0, 0), new Vec3(20, 0, 20) }, 2, .25f, 10);
            binding.Data.LastTarget = new Vec3(20, 0, 20);
            return binding;
        }

        public void Dispose()
        {
            Runtime.Dispose();
            Pipeline.Dispose();
            Context.DestroyAllEntities();
            Actors.Dispose();
        }
    }

    private sealed class CounterSink : IMotionEventSink
    {
        internal int Count;
        public void OnHit(int id, in MotionState state, in MotionHit hit) => Count++;
        public void OnArrive(int id, in MotionState state) => Count++;
        public void OnExpired(int id, in MotionState state) => Count++;
    }
}
