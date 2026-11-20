using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Services;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Fact]
    public void Motion_dependencies_restore_policy_from_values_even_after_initialization_config_changes()
    {
        var registry = MobaRollbackRegistryBuilder.Create(_world);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(4));
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var pipeline = actor.motion.Pipeline;
        var policy = pipeline.Policy;
        var solver = actor.motion.Solver;
        var sources = pipeline.CaptureSourceOrder();
        policy.SetSuppressedGroups(-200, -1, 0, int.MaxValue);
        var target = coordinator.Capture(_frame);
        var group = Get<MobaConfigDatabase>().GetAllMotionGroups().First(g => g.SuppressedGroupIds.Length > 0);
        var previous = group.SuppressedGroupIds[0];
        try
        {
            group.SuppressedGroupIds[0] = 991234;
            policy.SetSuppressedGroups(-200);
            policy.SetSuppressedGroups(999123, 123);
            Assert.True(coordinator.TryRestore(target, out var result), result.ToString());
            Assert.Same(pipeline, actor.motion.Pipeline);
            Assert.Same(policy, pipeline.Policy);
            Assert.Same(solver, actor.motion.Solver);
            Assert.Equal(sources, pipeline.CaptureSourceOrder());
            Assert.True(policy.TryGetSuppressedGroups(-200, out var groups));
            Assert.Equal(new[] { -1, 0, int.MaxValue }, groups);
            Assert.False(policy.TryGetSuppressedGroups(999123, out _));
            Assert.Equal(WorldRollbackSnapshotCodec.Serialize(target), WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
        }
        finally { group.SuppressedGroupIds[0] = previous; }
    }

    [Fact]
    public void Motion_dependencies_missing_payload_is_rejected_before_any_world_import()
    {
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var old = new WorldRollbackSnapshot(target.Version, target.Frame,
            target.Entries.Where(e => e.Key != MobaMotionRuntimeDependencyRollbackProvider.DefaultKey).ToArray());
        Assert.False(coordinator.TryRestore(old, out var result));
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(MobaMotionRuntimeDependencyRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.Equal(WorldRollbackSnapshotCodec.Serialize(target), WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
    }

    [Theory]
    [InlineData("shared-policy")]
    [InlineData("component-policy")]
    [InlineData("bad-payload")]
    public void Motion_dependencies_full_snapshot_preflight_rejects_unsupported_layout_with_zero_imports(string defect)
    {
        var registry = MobaRollbackRegistryBuilder.Create(_world);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        Assert.True(Get<MobaActorRegistry>().TryGet(2, out var second));
        actor.motion.Pipeline.Policy.SetSuppressedGroups(987321, 5);
        if (defect == "shared-policy") second.motion.Pipeline.Policy = actor.motion.Pipeline.Policy;
        if (defect == "component-policy") second.motion.Policy = new MotionPipelinePolicy();
        if (defect == "bad-payload")
        {
            var entries = target.Entries.Select(e => e.Key == MobaMotionRuntimeDependencyRollbackProvider.DefaultKey
                ? new WorldRollbackSnapshotEntry(e.Key, e.Payload.Append((byte)0).ToArray()) : e).ToArray();
            target = new WorldRollbackSnapshot(target.Version, target.Frame, entries);
        }
        var other = registry.Providers.Where(p => p.Key != MobaMotionRuntimeDependencyRollbackProvider.DefaultKey).ToArray();
        var before = other.Select(p => p.Export(_frame)).ToArray();
        Assert.False(coordinator.TryRestore(target, out var result));
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(MobaMotionRuntimeDependencyRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.True(actor.motion.Pipeline.Policy.TryGetSuppressedGroups(987321, out var groups));
        Assert.Equal(new[] { 5 }, groups);
        for (var i = 0; i < other.Length; i++) Assert.Equal(before[i], other[i].Export(_frame));
    }

    [Fact]
    public void Motion_dependencies_restore_then_formal_system_tree_replays_identical_frame_state()
    {
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        Assert.True(coordinator.CaptureAndStore(_frame));
        var target = coordinator.Capture(_frame);
        var frames = new List<byte[]>();
        for (var i = 0; i < 6; i++)
        {
            _bootstrapper.Tick(1f / 30f);
            frames.Add(WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(new FrameIndex(_bootstrapper.Context.LastFrame))));
        }
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        actor.motion.Pipeline.Policy.SetSuppressedGroups(912345, MotionGroups.Locomotion);
        Assert.True(_bootstrapper.TryRestoreSimulationFrame(coordinator, _frame, out var result), result.ToString());
        Assert.Equal(WorldRollbackSnapshotCodec.Serialize(target), WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
        for (var i = 0; i < frames.Count; i++)
        {
            _bootstrapper.Tick(1f / 30f);
            Assert.Equal(frames[i], WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(new FrameIndex(_bootstrapper.Context.LastFrame))));
        }
    }
}
