using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Theory]
    [InlineData("absent", true)]
    [InlineData("cancelled", false)]
    [InlineData("ended-detached", false)]
    [InlineData("ended-member", false)]
    [InlineData("active", false)]
    [InlineData("active", true)]
    public void Prediction_path_slot_restores_existence_values_and_mixed_order(string kind, bool managed)
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var path = Get<MobaPathFollowingRuntimeService>();
        var pipeline = managed ? AddTiedSources(true, MotionStacking.Additive) : actor.motion.Pipeline;
        var locomotion = OwnedLocomotion(1);
        locomotion.Configure(12, MotionInputSpace.World, 10);
        PathFollowerMotionSource? targetSource = null;
        if (kind != "absent")
        {
            var binding = BindTestPath(1);
            targetSource = binding.Source;
            if (kind == "cancelled") path.Detach(binding);
            if (kind.StartsWith("ended")) targetSource.Cancel();
            if (kind == "ended-detached") pipeline.RemoveSource(targetSource);
            if (kind is "active" or "ended-member")
            {
                var tied = pipeline.CaptureSourceOrder().Where(source => !ReferenceEquals(source, targetSource)).ToList();
                tied.Insert(managed ? 9 : 0, targetSource);
                pipeline.RestoreSourceOrder(tied);
            }
        }
        var targetOrder = pipeline.CaptureSourceOrder();
        var targetPath = path.CaptureState(1).State;
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.CreateForPrediction(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var targetBytes = WorldRollbackSnapshotCodec.Serialize(target);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            path.Remove(1);
            if (!targetPath.SourceExists || attempt == 1) BindTestPath(1, alternate: true);
            var before = path.CaptureState(1);
            Assert.True(coordinator.TryRestore(target, out var result), result.ToString());
            Assert.Equal(targetBytes, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
            var restored = path.CaptureState(1);
            Assert.Equal(targetPath.BindingExists, restored.State.BindingExists);
            Assert.Equal(targetPath.SourceExists, restored.State.SourceExists);
            Assert.Equal(targetPath.IsPipelineMember, restored.State.IsPipelineMember);
            Assert.Equal(targetPath.LastTarget, restored.State.LastTarget);
            Assert.Equal(targetPath.FramesSinceRepath, restored.State.FramesSinceRepath);
            Assert.Equal(targetPath.Source?.Points, restored.State.Source?.Points);
            Assert.Equal(targetPath.Source?.Index, restored.State.Source?.Index);
            Assert.Equal(targetPath.Source?.IsActive, restored.State.Source?.IsActive);
            Assert.Equal(targetOrder.Select(source => ReferenceEquals(source, targetSource) ? restored.Source : source), restored.Order);
            if (before.Source != null && !restored.State.SourceExists)
            {
                var reused = PathFollowerMotionSource.Rent(new[] { Vec3.Right }, 1f);
                try { Assert.Same(before.Source, reused); }
                finally { PathFollowerMotionSource.Release(reused); }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Path_commit_failure_preserves_original_references_and_returns_only_temporary_leases(bool removeOnCommit)
    {
        var path = Get<MobaPathFollowingRuntimeService>();
        var registry = MobaRollbackRegistryBuilder.CreateForPrediction(_world);
        Assert.True(registry.TryGet(MobaSkillLifecycleRollbackProvider.DefaultKey, out var provider));
        var preparation = new MobaSkillLifecycleRollbackPreparation(_world,
            Assert.IsType<MobaSkillLifecycleRollbackProvider>(provider).OwnerBlackboards, true);
        if (!removeOnCommit) BindTestPath(1);
        var target = preparation.Capture(_frame);
        path.Remove(1);
        if (removeOnCommit) BindTestPath(1, alternate: true);
        var before = preparation.Capture(_frame);
        var original = path.CaptureState(1);
        PathFollowerMotionSource? temporary = null;
        preparation.RestoreStageCompleted = (stage, compensating) =>
        {
            if (compensating || stage != MobaLifecycleRestoreStage.Reconciled) return;
            if (path.TryGet(1, out var binding)) temporary = binding.Source;
            if (removeOnCommit) Assert.True(original.Source.IsActive);
            throw new InvalidOperationException("inject after Path membership restore");
        };

        var failure = Assert.Throws<InvalidOperationException>(() => preparation.Commit(preparation.Prepare(_frame, target)));
        Assert.Contains("inject after Path membership restore", failure.ToString());
        var after = path.CaptureState(1);
        Assert.Same(original.Binding, after.Binding);
        Assert.Same(original.Source, after.Source);
        Assert.Equal(original.Order, after.Order);
        Assert.Equal(before, preparation.Capture(_frame));
        if (!removeOnCommit)
        {
            Assert.NotNull(temporary);
            var reused = PathFollowerMotionSource.Rent(new[] { Vec3.Right }, 1f);
            var second = PathFollowerMotionSource.Rent(new[] { Vec3.Right }, 1f);
            try
            {
                Assert.Same(temporary, reused);
                Assert.NotSame(reused, second);
            }
            finally
            {
                PathFollowerMotionSource.Release(second);
                PathFollowerMotionSource.Release(reused);
            }
        }
    }

    [Theory]
    [InlineData("orphan-member")]
    [InlineData("active-detached")]
    [InlineData("duplicate-member")]
    [InlineData("wrong-index")]
    [InlineData("wrong-version")]
    [InlineData("non-finite")]
    public void Invalid_path_payload_rejects_world_before_any_import(string defect)
    {
        BindTestPath(1);
        var prediction = new RollbackCoordinator(MobaRollbackRegistryBuilder.CreateForPrediction(_world), new RollbackSnapshotRingBuffer(2));
        var ordinary = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        Assert.False(ordinary.TryCaptureAndStore(_frame, out var rejected));
        Assert.Contains("prediction navigation", rejected.ToString());
        var target = prediction.Capture(_frame);
        var envelope = Decode(target.Entries.Single(entry => entry.Key == MobaSkillLifecycleRollbackProvider.DefaultKey).Payload);
        var section = Find(envelope, "motion-pipelines");
        var roster = MobaSkillLifecycleCheckpointCodec.Decode<MobaMotionPipelineCheckpoint[]>(section.Payload);
        var row = roster.Single(entry => entry.ActorId == 1);
        var path = row.Sources.Single(entry => entry.SourceKind == MobaMotionSourceKind.Path);
        switch (defect)
        {
            case "orphan-member": row.Path.Source = null; break;
            case "active-detached": row.Sources = row.Sources.Where(entry => entry != path).ToArray(); break;
            case "duplicate-member": row.Sources = row.Sources.Append(path).ToArray(); break;
            case "wrong-index": row.Path.Source.Index = int.MaxValue; break;
            case "wrong-version": path.EntityVersion++; break;
            case "non-finite": row.Path.LastTarget[0] = float.PositiveInfinity; break;
        }
        section.Payload = MobaSkillLifecycleCheckpointCodec.Encode(roster);
        var invalid = ReplaceLifecycle(target, MobaSkillLifecycleCheckpointCodec.Encode(envelope));
        Assert.False(prediction.TryRestore(invalid, out var result));
        Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(WorldRollbackSnapshotCodec.Serialize(target), WorldRollbackSnapshotCodec.Serialize(prediction.Capture(_frame)));
    }

    private MobaPathFollowingRuntimeService.Binding BindTestPath(int actorId, bool alternate = false)
    {
        var actors = Get<MobaActorRegistry>();
        Assert.True(actors.TryGet(actorId, out var actor));
        var path = Get<MobaPathFollowingRuntimeService>();
        var binding = path.Ensure(actor, actors.GetEntityVersion(actorId));
        binding.Data.LastTarget = alternate ? new Vec3(25, 0, 10) : new Vec3(15, 0, -7);
        binding.Data.FramesSinceRepath = alternate ? 2 : 13;
        path.Attach(binding, new[] { new Vec3(5, 0, 4), binding.Data.LastTarget }, alternate ? 7 : 4, .125f, 10);
        var state = binding.Source.CaptureState();
        state.Index = alternate ? 0 : 1;
        binding.Source.RestoreState(in state);
        return binding;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Battle_capture_rejects_unowned_path_source_without_storing_or_mutating(bool prediction)
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var pipeline = actor.motion.Pipeline;
        var source = new PathFollowerMotionSource(new[] { Vec3.Right }, 1f);
        pipeline.AddSource(source);
        var before = source.CaptureState();
        var order = pipeline.CaptureSourceOrder();
        var snapshots = new RollbackSnapshotRingBuffer(2);
        var registry = prediction ? MobaRollbackRegistryBuilder.CreateForPrediction(_world) : MobaRollbackRegistryBuilder.Create(_world);
        var coordinator = new RollbackCoordinator(registry, snapshots);

        try
        {
            Assert.False(coordinator.TryCaptureAndStore(_frame, out var result));
            Assert.Contains("outside declared recovery ownership", result.ToString());
            Assert.False(snapshots.TryGet(_frame, out _));
            Assert.Equal(order, pipeline.CaptureSourceOrder());
            var after = source.CaptureState();
            Assert.Equal(before.Points, after.Points);
            before.Points = after.Points;
            Assert.Equal(before, after);
        }
        finally
        {
            pipeline.RemoveSource(source);
        }
    }
}
