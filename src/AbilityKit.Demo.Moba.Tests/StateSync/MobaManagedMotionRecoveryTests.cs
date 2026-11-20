using System.Text;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Theory]
    [InlineData(1, 91001)]
    [InlineData(3, 92002)]
    public void Managed_motion_restores_raw_time_same_source_and_final_step_without_activation(int actorId, int configId)
    {
        PrepareManagedOnlyMotionPipeline(actorId);
        var source = ExactSource(10);
        var runtime = AddMotion(actorId, configId, source);
        var initial = source.CaptureExactState();
        // Deliberately not representable by the legacy float TimeLeft roundtrip.
        source.RestoreExactState(WithTime(initial, 1234567897));
        var target = _preparation.Capture(_frame);
        var events = 0;
        Get<MobaContinuousManager>().OnActivated += _ => events++;
        var saved = source.CaptureExactState();
        TickSource(source, .016f);
        Assert.NotEqual(saved.TimeLeftRaw, source.CaptureExactState().TimeLeftRaw);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            _preparation.Commit(_preparation.Prepare(_frame, target));
            Assert.Equal(target, _preparation.Capture(_frame));
            Assert.Same(source, runtime.LocalMotionSource);
            Assert.Equal(saved, source.CaptureExactState());
            Assert.True(Get<MobaActorRegistry>().TryGet(actorId, out var actor));
            Assert.True(actor.motion.Pipeline.ContainsSource(source));
            // The final step clamps to exact remaining time and ends once, after restoration.
            var delta = TickSource(source, .5f);
            Assert.InRange(delta.X, 2.87f, 2.88f);
            Assert.False(source.IsActive);
            Assert.Equal(0, source.CaptureExactState().TimeLeftRaw);
            if (attempt == 0) source.RestoreExactState(in saved);
        }
        Assert.Equal(0, events);
    }

    [Fact]
    public void Managed_motion_recovery_preserves_arbitration_and_other_source_until_explicit_revoke()
    {
        PrepareManagedOnlyMotionPipeline(1);
        var low = ExactSource(1);
        var high = ExactSource(20);
        AddMotion(1, 91001, low);
        var runtime = AddMotion(1, 91002, high);
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var pipeline = actor.motion.Pipeline;
        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput();
        var target = _preparation.Capture(_frame);
        pipeline.Tick(1, ref state, .01f, ref output);
        var lowTime = low.CaptureExactState().TimeLeftRaw;
        var highTime = high.CaptureExactState().TimeLeftRaw;
        Assert.Equal(1L << 32, lowTime); // suppressed source never ticks
        Assert.True(highTime < lowTime);
        _preparation.Commit(_preparation.Prepare(_frame, target));
        Assert.Equal(1L << 32, high.CaptureExactState().TimeLeftRaw);
        Assert.True(pipeline.ContainsSource(low));
        Assert.True(pipeline.ContainsSource(high));
        pipeline.Tick(1, ref state, .01f, ref output);
        Assert.Equal(highTime, high.CaptureExactState().TimeLeftRaw);
        Assert.Equal(lowTime, low.CaptureExactState().TimeLeftRaw);
        Assert.True(Get<MobaContinuousManager>().TryEnd(runtime));
        Assert.False(pipeline.ContainsSource(high));
        Assert.True(pipeline.ContainsSource(low));
        pipeline.Tick(1, ref state, .01f, ref output);
        Assert.True(low.CaptureExactState().TimeLeftRaw < lowTime);
    }

    [Fact]
    public void Managed_motion_identity_allows_same_owner_and_config_with_distinct_source_ids()
    {
        PrepareManagedOnlyMotionPipeline(1);
        AddMotion(1, 91001, ExactSource(10));
        AddMotion(1, 91001, ExactSource(20));
        var target = _preparation.Capture(_frame);
        var envelope = Decode(target);
        var entries = JArray.Parse(Encoding.UTF8.GetString(Find(envelope, "continuous-local").Payload));
        var ids = entries.Where(e => e["Motion"]?.Type == JTokenType.Object)
            .Select(e => (int)e["ModifierSourceId"]!).ToArray();
        Assert.Equal(2, ids.Length);
        Assert.NotEqual(ids[0], ids[1]);
        _preparation.Commit(_preparation.Prepare(_frame, target));
        Assert.Equal(target, _preparation.Capture(_frame));
    }

    [Fact]
    public void Managed_motion_rejects_changed_frozen_source_configuration_without_mutation()
    {
        PrepareManagedOnlyMotionPipeline(1);
        var source = ExactSource(10);
        AddMotion(1, 91001, source);
        var target = _preparation.Capture(_frame);
        var saved = source.CaptureExactState();
        source.Configure(new Vec3(-4, 2, 1), 3f, 50, MotionGroups.Control,
            MotionStacking.Additive, hasCollisionPolicy: false, hasCompletionCollisionPolicy: false);
        var changed = _preparation.Capture(_frame);

        Assert.Throws<InvalidOperationException>(() => _preparation.Prepare(_frame, target));

        Assert.NotEqual(saved, source.CaptureExactState());
        Assert.Equal(changed, _preparation.Capture(_frame));
    }

    [Theory]
    [InlineData("v3")]
    [InlineData("missing-motion")]
    [InlineData("missing-raw")]
    [InlineData("missing-duration")]
    [InlineData("null-source")]
    [InlineData("source-type")]
    [InlineData("instance-source")]
    [InlineData("negative-time")]
    [InlineData("long-time")]
    [InlineData("inactive")]
    [InlineData("velocity")]
    [InlineData("priority")]
    [InlineData("group")]
    [InlineData("stacking")]
    [InlineData("collision")]
    [InlineData("endpoint")]
    [InlineData("bad-constraint")]
    [InlineData("owner")]
    [InlineData("duplicate")]
    public void Invalid_late_motion_entry_rejects_world_preflight_with_zero_imports(string defect)
    {
        PrepareManagedOnlyMotionPipeline(1);
        PrepareManagedOnlyMotionPipeline(3);
        AddMotion(1, 91001, ExactSource(10));
        AddMotion(3, 92002, ExactSource(20));
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var before = coordinator.Capture(_frame);
        var envelope = Decode(_preparation.Capture(_frame));
        var section = Find(envelope, "continuous-local");
        var array = JArray.Parse(Encoding.UTF8.GetString(section.Payload));
        var last = (JObject)array.Last(e => e["Motion"]?.Type == JTokenType.Object);
        var motion = (JObject)last["Motion"]!;
        var value = (JObject)motion["FixedDelta"]!;
        switch (defect)
        {
            case "v3": envelope.Version = 3; break;
            case "missing-motion": last.Remove("Motion"); break;
            case "missing-raw": value.Remove("TimeLeftRaw"); break;
            case "missing-duration": value.Remove("DurationRaw"); break;
            case "null-source": motion["FixedDelta"] = null; break;
            case "source-type": motion["SourceType"] = "unregistered"; break;
            case "instance-source": motion["ModifierSourceId"] = int.MaxValue; break;
            case "negative-time": value["TimeLeftRaw"] = -1; break;
            case "long-time": value["TimeLeftRaw"] = long.MaxValue; break;
            case "inactive": value["Active"] = false; break;
            case "velocity": value["X"] = 800; break;
            case "priority": value["Priority"] = 900; break;
            case "group": value["GroupId"] = 900; break;
            case "stacking": value["Stacking"] = 900; break;
            case "collision": value["CollisionPolicy"]!["IgnoreMask"] = 123; break;
            case "endpoint": value["CompletionCollisionPolicy"]!["EndOverlapPolicy"] = 1; break;
            case "bad-constraint": value["CollisionPolicy"]!["WallSlideSpeedRecovery"] = -1; break;
            case "owner": last["OwnerId"] = 2; break;
            case "duplicate": array.Add(last.DeepClone()); break;
        }
        section.Payload = Encoding.UTF8.GetBytes(array.ToString(Newtonsoft.Json.Formatting.None));
        var invalid = ReplaceLifecycle(before, MobaSkillLifecycleCheckpointCodec.Encode(envelope));
        Assert.False(coordinator.TryRestore(in invalid, out var result));
        /*
         * 联合生命周期现已参与整份快照预检，旧版本由载荷所有者提前拒绝。
         * 继续要求零导入和全世界字节不变，不能以错误归属变化放宽恢复保证。
         */
        Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(WorldRollbackSnapshotCodec.Serialize(in before), WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
        Assert.Equal(_frame.Value, _bootstrapper.Context.LastFrame);
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("ended")]
    [InlineData("replaced")]
    [InlineData("unsupported")]
    [InlineData("cancelled-source")]
    [InlineData("multiple-owners")]
    public void Missing_or_unsupported_motion_binding_rejects_before_import(string defect)
    {
        PrepareManagedOnlyMotionPipeline(1);
        PrepareManagedOnlyMotionPipeline(3);
        var source = ExactSource(10);
        IMotionSource actual = defect == "unsupported" ? new MobaCollisionDashMotionSource(source, default) : source;
        var runtime = AddMotion(1, 91001, actual);
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        if (defect == "removed") actor.motion.Pipeline.RemoveSource(source);
        if (defect == "ended" || defect == "replaced") Assert.True(Get<MobaContinuousManager>().TryEnd(runtime));
        if (defect == "replaced") AddMotion(1, 91001, ExactSource(10));
        if (defect == "cancelled-source") source.Cancel();
        if (defect == "multiple-owners") AddMotion(3, 92002, source);
        var live = coordinator.Capture(_frame);
        Assert.False(coordinator.TryRestore(in target, out var result));
        Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(WorldRollbackSnapshotCodec.Serialize(in live), WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Managed_motion_stage_failure_compensates_raw_time_activity_and_original_bindings(int stage)
    {
        PrepareManagedOnlyMotionPipeline(1);
        PrepareManagedOnlyMotionPipeline(3);
        var first = ExactSource(10);
        var second = ExactSource(20);
        var firstRuntime = AddMotion(1, 91001, first);
        var secondRuntime = AddMotion(3, 92002, second);
        var target = _preparation.Capture(_frame);
        TickSource(first, .016f);
        TickSource(second, .032f);
        var current = _preparation.Capture(_frame);
        var firstState = first.CaptureExactState();
        var secondState = second.CaptureExactState();
        var reached = false;
        _preparation.RestoreStageCompleted = (value, compensating) =>
        {
            if (!compensating && (int)value == stage)
            {
                reached = true;
                throw new InvalidOperationException("injected managed Motion failure");
            }
        };
        Assert.Throws<InvalidOperationException>(() => _preparation.Commit(_preparation.Prepare(_frame, target)));
        Assert.True(reached);
        Assert.Equal(current, _preparation.Capture(_frame));
        Assert.Equal(firstState, first.CaptureExactState());
        Assert.Equal(secondState, second.CaptureExactState());
        Assert.Same(first, firstRuntime.LocalMotionSource);
        Assert.Same(second, secondRuntime.LocalMotionSource);
        firstRuntime.ValidateLocalMotionBinding();
        secondRuntime.ValidateLocalMotionBinding();
        _preparation.RestoreStageCompleted = null;
    }

    private MotionPipeline PrepareManagedOnlyMotionPipeline(int actorId)
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(actorId, out var actor));
        var pipeline = actor.motion.Pipeline;
        /*
         * 此专项测试隔离全受管 FixedDelta 子集；普通角色默认的 Locomotion
         * 与受管来源共存在另一个专项和真实到达组合中保留并验收。
         * 经正式 System 退役默认槽位，同时清除业务绑定，不手工留下过期来源缓存。
         */
        var locomotion = Assert.Single(pipeline.CaptureSourceOrder());
        Assert.IsType<LocomotionMotionSource>(locomotion);
        actor.RemoveMoveInput();
        LocomotionSystem().Execute();
        Assert.False(pipeline.ContainsSource(locomotion));
        return pipeline;
    }

    private MobaMotionContinuousRuntime AddMotion(int actor, int config, IMotionSource source)
    {
        var identity = new MobaMotionContinuousIdentity("acceptance-motion", config, actor, actor, actor, 0, default);
        var resolved = new MobaMotionContinuousResolvedConfig(config, 1, null, null, null, 0, null);
        var dependencies = new MobaMotionContinuousDependencies(Get<MobaActorRegistry>(), source, default, default, null);
        var runtime = new MobaMotionContinuousRuntime(in identity, in resolved, in dependencies);
        Assert.True(Get<MobaContinuousManager>().TryActivate(runtime));
        return runtime;
    }

    private static FixedDeltaMotionSource ExactSource(int priority) => new(new Vec3(10, 0, 0), 1, priority,
        MotionGroups.Ability, MotionStacking.ExclusiveHighestPriority,
        new MotionCollisionConstraints(true, true, MotionEndOverlapPolicy.AllowInside, .4f, .01f, 3, 8,
            true, 3, .5f, 0), true,
        new MotionCollisionConstraints(true, false, MotionEndOverlapPolicy.ProjectAlongDirection, .4f, .02f, 3, 4,
            false, 2, .2f, 0), true);

    private static FixedDeltaMotionExactState WithTime(FixedDeltaMotionExactState value, long raw) => new(value.DurationRaw, raw,
        true, value.Velocity, value.GroupId, value.Priority, value.Stacking, value.HasCollisionPolicy, value.CollisionPolicy,
        value.HasCompletionCollisionPolicy, value.CompletionCollisionPolicy);

    private static Vec3 TickSource(FixedDeltaMotionSource source, float dt)
    {
        var state = new MotionState(Vec3.Zero);
        var delta = Vec3.Zero;
        source.Tick(1, ref state, dt, ref delta);
        return delta;
    }

    private static WorldRollbackSnapshot ReplaceLifecycle(WorldRollbackSnapshot before, byte[] payload) => new(
        before.Version, before.Frame, before.Entries.Select(entry => entry.Key == MobaSkillLifecycleRollbackProvider.DefaultKey
            ? new WorldRollbackSnapshotEntry(entry.Key, payload) : entry).ToArray());
}
