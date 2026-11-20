using System.Text;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.MotionSystem.Collision;
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
    [InlineData(false)]
    [InlineData(true)]
    public void World_restore_preserves_consumed_or_available_motion_hit_qualification(bool available)
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var scope = new MobaEffectExecutionScopeSnapshot(1, 91001, 1, 1, 2, true, 0, 0, 0);
        var qualification = new MobaMotionHitTriggerRuntime(1, 1, 91001, scope);
        SetQualification(available ? qualification : default);
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        SetQualification(available ? default : qualification);
        Assert.False(WorldRollbackSnapshotCodec.Serialize(target).SequenceEqual(
            WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame))));
        var activations = 0;
        Get<MobaContinuousManager>().OnActivated += _ => activations++;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.True(coordinator.TryRestore(in target, out var result), result.ToString());
            Assert.Equal(available ? qualification : default, actor.motion.HitTriggerRuntime);
            Assert.Equal(WorldRollbackSnapshotCodec.Serialize(target), WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
        }
        Assert.Equal(0, activations);

        void SetQualification(MobaMotionHitTriggerRuntime value)
        {
            var motion = actor.motion;
            actor.ReplaceMotion(motion.Pipeline, motion.State, motion.Output, motion.Solver,
                motion.Policy, motion.Events, motion.Initialized, value);
        }
    }

    [Theory]
    [InlineData(MotionStacking.ExclusiveHighestPriority)]
    [InlineData(MotionStacking.Additive)]
    public void World_restore_reinstates_equal_priority_source_order_and_next_frame_winner(MotionStacking stacking)
    {
        var pipeline = AddTiedSources(stacking: stacking);
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var order = pipeline.CaptureSourceOrder();
        var expected = TickOrderedPipeline(pipeline);
        var transient = AddMotion(1, 92000, new FixedDeltaMotionSource(new Vec3(99, 0, 0), 1, 10,
            MotionGroups.Ability, MotionStacking.ExclusiveHighestPriority));
        Assert.True(Get<MobaContinuousManager>().TryEnd(transient));
        Assert.False(order.SequenceEqual(pipeline.CaptureSourceOrder()));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.True(coordinator.TryRestore(in target, out var result), result.ToString());
            Assert.Equal(order, pipeline.CaptureSourceOrder());
            Assert.Equal(WorldRollbackSnapshotCodec.Serialize(target), WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
            Assert.Equal(expected, TickOrderedPipeline(pipeline));
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("owner")]
    [InlineData("unmanaged")]
    [InlineData("v5")]
    [InlineData("v6")]
    [InlineData("v9")]
    public void Invalid_pipeline_order_rejects_complete_world_before_import(string defect)
    {
        var pipeline = AddTiedSources(keepExisting: defect == "unmanaged");
        if (defect == "unmanaged") pipeline.AddSource(new LocomotionMotionSource(4));
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var envelope = Decode(_preparation.Capture(_frame));
        var section = Find(envelope, "motion-pipelines");
        var entries = JArray.Parse(Encoding.UTF8.GetString(section.Payload));
        var order = (JObject)entries.First(e => ((JArray)e["Sources"]!).Count >= 2);
        var ids = (JArray)order["Sources"]!;
        if (defect == "missing") order.Remove();
        if (defect == "duplicate") ids[1] = ids[0]!.DeepClone();
        if (defect == "unknown") ids[ids.Count - 1]!["SourceId"] = int.MaxValue;
        if (defect == "owner") order["ActorId"] = 3;
        if (defect == "v5") envelope.Version = 5;
        if (defect == "v6") envelope.Version = 6;
        if (defect == "v9") envelope.Version = 9;
        section.Payload = Encoding.UTF8.GetBytes(entries.ToString(Newtonsoft.Json.Formatting.None));
        var invalid = ReplaceLifecycle(target, MobaSkillLifecycleCheckpointCodec.Encode(envelope));
        Assert.False(coordinator.TryRestore(in invalid, out var result));
        Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(WorldRollbackSnapshotCodec.Serialize(target), WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Lifecycle_failure_compensates_pipeline_order_and_raw_source_time(int stage)
    {
        var pipeline = AddTiedSources();
        var target = _preparation.Capture(_frame);
        var reversed = pipeline.CaptureSourceOrder().Reverse().ToArray();
        pipeline.RestoreSourceOrder(reversed);
        TickOrderedPipeline(pipeline);
        var live = _preparation.Capture(_frame);
        var states = reversed.Cast<FixedDeltaMotionSource>().Select(source => source.CaptureExactState()).ToArray();
        var reached = false;
        _preparation.RestoreStageCompleted = (value, compensating) =>
        {
            if (!compensating && (int)value == stage)
            {
                reached = true;
                throw new InvalidOperationException("注入 Pipeline 顺序恢复故障");
            }
        };
        Assert.Throws<InvalidOperationException>(() => _preparation.Commit(_preparation.Prepare(_frame, target)));
        Assert.True(reached);
        Assert.Equal(reversed, pipeline.CaptureSourceOrder());
        Assert.Equal(states, reversed.Cast<FixedDeltaMotionSource>().Select(source => source.CaptureExactState()));
        Assert.Equal(live, _preparation.Capture(_frame));
        _preparation.RestoreStageCompleted = null;
    }

    private MotionPipeline AddTiedSources(bool keepExisting = false,
        MotionStacking stacking = MotionStacking.ExclusiveHighestPriority)
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var pipeline = actor.motion.Pipeline;
        /*
         * 正常角色带有普通移动来源；支持范围限定为全受管 FixedDelta。
         * 在测试装配阶段移除普通来源构造该受支持组合，混用真实来源另测预检拒绝。
         */
        if (!keepExisting) PrepareManagedOnlyMotionPipeline(1);
        pipeline.Solver = NoMotionSolver.Instance;
        for (var i = 1; i <= 24; i++)
            AddMotion(1, 91000 + i, new FixedDeltaMotionSource(new Vec3(i, 0, 0), 1, 10,
                MotionGroups.Ability, stacking));
        return pipeline;
    }

    private static Vec3 TickOrderedPipeline(MotionPipeline pipeline)
    {
        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput();
        pipeline.Tick(1, ref state, .01f, ref output);
        return output.DesiredDelta;
    }
}
