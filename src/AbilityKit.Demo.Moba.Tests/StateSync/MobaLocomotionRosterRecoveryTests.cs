using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Locomotion_slot_existence_restores_both_directions_with_exact_mixed_order(bool exists, bool managed)
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var original = OwnedLocomotion(1);
        original.Configure(23.75f, MotionInputSpace.Local, 10);
        original.SetInput(.25f, -.75f);
        var pipeline = managed ? AddTiedSources(true, MotionStacking.Additive) : actor.motion.Pipeline;
        if (!exists) RetireLocomotion(actor);
        else if (managed)
        {
            var tied = pipeline.CaptureSourceOrder().Where(s => s != original).ToList();
            tied.Insert(7, original);
            pipeline.RestoreSourceOrder(tied);
        }
        var targetOrder = pipeline.CaptureSourceOrder();
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var targetBytes = WorldRollbackSnapshotCodec.Serialize(target);
        var manager = Get<MobaContinuousManager>();
        var events = new List<string>();
        manager.OnRegistered += _ => events.Add("registered");
        manager.OnActivated += _ => events.Add("activated");
        manager.OnUnregistered += (_, _) => events.Add("unregistered");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            LocomotionMotionSource? divergent = null;
            if (exists) RetireLocomotion(actor);
            else
            {
                actor.AddMoveInput(1, -.5f);
                LocomotionSystem().Execute();
                divergent = OwnedLocomotion(1);
                divergent.Configure(91, MotionInputSpace.World, 10);
            }
            var beforePrepare = WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame));
            _preparation.Prepare(_frame, target.Entries.Single(e => e.Key == MobaSkillLifecycleRollbackProvider.DefaultKey).Payload);
            Assert.Equal(beforePrepare, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
            Assert.True(coordinator.TryRestore(target, out var result), result.ToString());
            Assert.Empty(events);
            Assert.Same(pipeline, actor.motion.Pipeline);
            Assert.Equal(exists, actor.hasMoveInput);
            Assert.Equal(targetBytes, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
            var order = pipeline.CaptureSourceOrder();
            Assert.Equal(targetOrder.Where(s => s != original), order.Where(s => s is not LocomotionMotionSource));
            if (exists)
            {
                var source = OwnedLocomotion(1);
                Assert.NotSame(original, source);
                Assert.Equal(Array.IndexOf(targetOrder, original), Array.IndexOf(order, source));
                Assert.Equal(new Vec3(.25f, 0, -.75f), source.Input);
                Assert.Equal(23.75f, source.Speed);
                Assert.Equal(MotionInputSpace.Local, source.Space);
                Assert.Equal(10, source.Priority);
            }
            else
            {
                Assert.DoesNotContain(order, s => s is LocomotionMotionSource);
                Assert.False(pipeline.ContainsSource(Assert.IsType<LocomotionMotionSource>(divergent)));
                Assert.False(Get<MobaLocomotionRuntimeService>().TryGetBinding(1, actor,
                    Get<MobaActorRegistry>().GetEntityVersion(1), pipeline, out _));
            }
        }
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(false, 3)]
    [InlineData(false, 4)] [InlineData(false, 5)] [InlineData(false, 6)] [InlineData(false, 7)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)]
    [InlineData(true, 4)] [InlineData(true, 5)] [InlineData(true, 6)] [InlineData(true, 7)]
    public void Locomotion_slot_failure_restores_original_references_values_binding_and_order(bool targetExists, int stage)
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var pipeline = AddTiedSources(true, MotionStacking.Additive);
        OwnedLocomotion(1).Configure(13, MotionInputSpace.Local, 10);
        if (!targetExists) RetireLocomotion(actor);
        var target = _preparation.Capture(_frame);
        if (targetExists) RetireLocomotion(actor);
        else
        {
            actor.AddMoveInput(-.2f, .6f);
            LocomotionSystem().Execute();
            var source = OwnedLocomotion(1);
            source.Configure(47, MotionInputSpace.World, 10);
            source.SetInput(-.2f, .6f);
        }
        var originalOrder = pipeline.CaptureSourceOrder().Reverse().ToArray();
        pipeline.RestoreSourceOrder(originalOrder);
        var original = originalOrder.OfType<LocomotionMotionSource>().SingleOrDefault();
        var live = _preparation.Capture(_frame);
        var reached = false;
        _preparation.RestoreStageCompleted = (value, compensating) =>
        {
            if (!compensating && (int)value == stage)
            {
                reached = true;
                if (value == MobaLifecycleRestoreStage.MotionOrdered)
                    Assert.Equal(targetExists, pipeline.CaptureSourceOrder().Any(s => s is LocomotionMotionSource));
                throw new InvalidOperationException("注入槽位存在性恢复失败");
            }
        };
        Assert.Throws<InvalidOperationException>(() => _preparation.Commit(_preparation.Prepare(_frame, target)));
        Assert.True(reached);
        Assert.Equal(originalOrder, pipeline.CaptureSourceOrder());
        if (original != null) Assert.Same(original, OwnedLocomotion(1));
        else Assert.False(Get<MobaLocomotionRuntimeService>().TryGetBinding(1, actor,
            Get<MobaActorRegistry>().GetEntityVersion(1), pipeline, out _));
        Assert.Equal(live, _preparation.Capture(_frame));
        _preparation.RestoreStageCompleted = null!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Formal_move_input_replays_each_system_frame_after_bidirectional_slot_restore(bool targetExists)
    {
        const float delta = 1f / 30f;
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        if (!targetExists)
        {
            actor.RemoveMoveInput();
            _bootstrapper.Tick(delta);
            Assert.DoesNotContain(actor.motion.Pipeline.CaptureSourceOrder(), s => s is LocomotionMotionSource);
        }
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var frame = new FrameIndex(_bootstrapper.Context.LastFrame);
        Assert.True(coordinator.CaptureAndStore(frame));
        var target = WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(frame));
        var start = actor.transform.Value.Position;
        var input = Get<IMobaInputCoordinator>();
        var replay = Get<IWorldInputReplaySink>();
        var records = Enumerable.Range(1, 6).Select(i => new PlayerInputCommand(new FrameIndex(frame.Value + i),
            new PlayerId("player_1"), MobaOpCodes.Input.Move, MobaMoveCodec.Serialize(i % 2 == 0 ? .3f : .6f, .8f))).ToArray();
        var baseline = new List<(byte[] World, Vec3 Position, Vec3 Velocity, Vec3 Input)>();
        foreach (var command in records)
        {
            var submitted = input.TrySubmit(command.Frame, new[] { command });
            Assert.True(submitted.Succeeded && submitted.HandledCount == 1, submitted.ToString());
            _bootstrapper.Tick(delta);
            baseline.Add((WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(command.Frame)), actor.transform.Value.Position,
                actor.motion.State.Velocity, OwnedLocomotion(1).Input));
        }
        Assert.NotEqual(start, actor.transform.Value.Position);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (targetExists)
            {
                actor.RemoveMoveInput();
                _bootstrapper.Tick(delta);
                Assert.DoesNotContain(actor.motion.Pipeline.CaptureSourceOrder(), s => s is LocomotionMotionSource);
            }
            Assert.True(_bootstrapper.TryRestoreSimulationFrame(coordinator, frame, out var restored), restored.ToString());
            Assert.Equal(targetExists, actor.hasMoveInput);
            Assert.Equal(targetExists, actor.motion.Pipeline.CaptureSourceOrder().Any(s => s is LocomotionMotionSource));
            Assert.Equal(target, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(frame)));
            replay.BeginReplay(frame, new FrameIndex(frame.Value + records.Length));
            try
            {
                for (var i = 0; i < records.Length; i++)
                {
                    replay.Replay(records[i].Frame, new[] { records[i] });
                    _bootstrapper.Tick(delta);
                    Assert.Equal(records[i].Frame.Value, _bootstrapper.Context.LastFrame);
                    Assert.Equal(baseline[i].World, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(records[i].Frame)));
                    Assert.Equal(baseline[i].Position, actor.transform.Value.Position);
                    Assert.Equal(baseline[i].Velocity, actor.motion.State.Velocity);
                    Assert.Equal(baseline[i].Input, OwnedLocomotion(1).Input);
                }
            }
            finally { replay.EndReplay(); }
        }
    }

    [Fact]
    public void Missing_slot_rebuild_does_not_resample_speed_or_blocked_movement_gate()
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var original = OwnedLocomotion(1);
        original.Configure(37.5f, MotionInputSpace.Local, 17);
        original.SetInput(.4f, -.6f);
        AddRetainedBuff(1, 2, 32040401);
        Assert.False(Get<MobaCombatRulesService>().CanMove(1));
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        RetireLocomotion(actor);
        Assert.True(coordinator.TryRestore(target, out var result), result.ToString());
        var source = OwnedLocomotion(1);
        Assert.NotSame(original, source);
        Assert.Equal(new Vec3(.4f, 0, -.6f), source.Input);
        Assert.Equal(37.5f, source.Speed);
        Assert.Equal(MotionInputSpace.Local, source.Space);
        Assert.Equal(17, source.Priority);
        LocomotionSystem().Execute();
        Assert.Same(source, OwnedLocomotion(1));
        Assert.Equal(Vec3.Zero, source.Input);
        Assert.Equal(37.5f, source.Speed);
    }

    [Theory]
    [InlineData("v7")] [InlineData("missing-empty")] [InlineData("missing-existence")]
    [InlineData("unsorted")] [InlineData("empty-values")] [InlineData("pipeline")]
    [InlineData("uninitialized")] [InlineData("structure-order")]
    [InlineData("pipeline-refreshed")] [InlineData("roster-command-order")]
    public void Invalid_empty_slot_or_structural_order_rejects_before_world_import(string defect)
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        RetireLocomotion(actor);
        var registry = MobaRollbackRegistryBuilder.Create(_world);
        /*
         * 未配置显式顺序时仍必须拒绝不合法的有效执行计划；
         * 正式 Battle 对输入条目乱序的兼容由显式顺序专项覆盖。
         */
        if (defect is "structure-order" or "roster-command-order")
            registry = CopyRegistryWithoutStructureOrder(registry);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var checkpoint = Decode(_preparation.Capture(_frame));
        var section = Find(checkpoint, "locomotion-local");
        var slots = JArray.Parse(Encoding.UTF8.GetString(section.Payload));
        Assert.False((bool)slots[0]!["Exists"]!);
        if (defect == "v7") checkpoint.Version = 7;
        if (defect == "missing-empty") slots.RemoveAt(0);
        if (defect == "missing-existence") ((JObject)slots[0]!).Remove("Exists");
        if (defect == "unsorted") slots = new JArray(slots.Reverse());
        if (defect == "empty-values") slots[0]!["Speed"] = 1;
        section.Payload = Encoding.UTF8.GetBytes(slots.ToString(Newtonsoft.Json.Formatting.None));
        var corrupt = ReplaceLifecycle(target, MobaSkillLifecycleCheckpointCodec.Encode(checkpoint));
        if (defect is "pipeline" or "pipeline-refreshed" or "uninitialized")
        {
            var m = actor.motion;
            actor.ReplaceMotion(defect != "uninitialized" ? new MotionPipeline() : m.Pipeline,
                m.State, m.Output, m.Solver, m.Policy, m.Events, defect != "uninitialized", m.HitTriggerRuntime);
            if (defect == "pipeline-refreshed")
            {
                actor.AddMoveInput(.5f, .5f);
                LocomotionSystem().Execute();
                Assert.NotNull(OwnedLocomotion(1));
            }
        }
        if (defect is "structure-order" or "roster-command-order")
        {
            var entries = corrupt.Entries.ToArray();
            var a = Array.FindIndex(entries, e => e.Key == MobaActorRollbackCommandRuntime.RollbackProviderKey);
            var b = Array.FindIndex(entries, e => e.Key == (defect == "structure-order"
                ? MobaSkillLifecycleRollbackProvider.DefaultKey : MobaEntitasEntityRollbackProvider.DefaultKey));
            (entries[a], entries[b]) = (entries[b], entries[a]);
            corrupt = new WorldRollbackSnapshot(corrupt.Version, corrupt.Frame, entries);
        }
        /*
         * 独立运行依赖捕获现在也拒绝无效初始化布局，不能再用完整捕获读取该损坏状态。
         * 仍须证明恢复在零导入阶段拒绝，且不会借初始化入口修复损坏的组件。
         */
        if (defect == "uninitialized")
        {
            var pipeline = actor.motion.Pipeline;
            var state = actor.motion.State;
            Assert.Throws<InvalidOperationException>(() => coordinator.Capture(_frame));
            Assert.False(coordinator.TryRestore(corrupt, out var rejected));
            Assert.Equal(0, rejected.ProviderCount);
            Assert.False(actor.motion.Initialized);
            Assert.Same(pipeline, actor.motion.Pipeline);
            Assert.Equal(state, actor.motion.State);
            return;
        }
        var before = WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame));
        Assert.False(coordinator.TryRestore(corrupt, out var result));
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(before, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
    }

    private void RetireLocomotion(global::ActorEntity actor)
    {
        if (actor.hasMoveInput) actor.RemoveMoveInput();
        LocomotionSystem().Execute();
        Assert.DoesNotContain(actor.motion.Pipeline.CaptureSourceOrder(), source => source is LocomotionMotionSource);
    }
}
