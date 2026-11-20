using System.Text;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Systems.Motion;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Theory]
    [InlineData(MotionInputSpace.World, false, MotionStacking.Additive)]
    [InlineData(MotionInputSpace.World, true, MotionStacking.Additive)]
    [InlineData(MotionInputSpace.Local, false, MotionStacking.Additive)]
    [InlineData(MotionInputSpace.Local, true, MotionStacking.Additive)]
    [InlineData(MotionInputSpace.World, false, MotionStacking.ExclusiveHighestPriority)]
    [InlineData(MotionInputSpace.World, true, MotionStacking.ExclusiveHighestPriority)]
    [InlineData(MotionInputSpace.Local, false, MotionStacking.ExclusiveHighestPriority)]
    [InlineData(MotionInputSpace.Local, true, MotionStacking.ExclusiveHighestPriority)]
    public void Mixed_motion_restores_values_order_same_sources_and_next_system_input(
        MotionInputSpace space, bool zero, MotionStacking stacking)
    {
        var loco = OwnedLocomotion(1);
        loco.Configure(17.25f, space, 10);
        loco.SetInput(zero ? 0 : .3f, zero ? 0 : -.7f);
        var pipeline = AddTiedSources(keepExisting: true, stacking: stacking);
        var order = pipeline.CaptureSourceOrder();
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var expectedDelta = TickOrderedPipeline(pipeline);
        var events = new List<string>();
        var manager = Get<MobaContinuousManager>();
        manager.OnActivated += _ => events.Add("activated");
        manager.OnUnregistered += (_, _) => events.Add("unregistered");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            loco.Configure(-3, MotionInputSpace.World, -10);
            loco.SetInput(1, 2);
            pipeline.RestoreSourceOrder(order.Where(s => s != loco).Reverse().Append(loco).ToArray());
            Assert.True(coordinator.TryRestore(target, out var result), result.ToString());
            Assert.Same(loco, OwnedLocomotion(1));
            Assert.Equal(new Vec3(zero ? 0 : .3f, 0, zero ? 0 : -.7f), loco.Input);
            Assert.Equal(17.25f, loco.Speed);
            Assert.Equal(space, loco.Space);
            Assert.Equal(10, loco.Priority);
            Assert.Equal(order, pipeline.CaptureSourceOrder());
            Assert.Equal(WorldRollbackSnapshotCodec.Serialize(target), WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
            Assert.Equal(expectedDelta, TickOrderedPipeline(pipeline));
        }
        Assert.Empty(events);
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        actor.ReplaceMoveInput(-.2f, .8f);
        LocomotionSystem().Execute();
        Assert.Same(loco, OwnedLocomotion(1));
        Assert.Equal(new Vec3(-.2f, 0, .8f), loco.Input);
        Assert.Equal(new MobaAttrs(actor).MoveSpeed, loco.Speed);
    }

    [Fact]
    public void Restore_does_not_resample_movement_gate_but_next_system_frame_clears_blocked_input()
    {
        var loco = OwnedLocomotion(1);
        AddMotion(1, 91001, ExactSource(10));
        var buff = AddRetainedBuff(1, 2, 32040401);
        var buffContextId = buff.SourceContextId;
        Assert.False(Get<MobaCombatRulesService>().CanMove(1));
        loco.SetInput(.5f, -.5f);
        loco.Speed = 37.5f;
        var target = _preparation.Capture(_frame);
        loco.SetInput(0, 0);
        loco.Speed = 1;
        _preparation.Commit(_preparation.Prepare(_frame, target));
        Assert.Same(loco, OwnedLocomotion(1));
        Assert.Equal(new Vec3(.5f, 0, -.5f), loco.Input);
        Assert.Equal(37.5f, loco.Speed);
        LocomotionSystem().Execute();
        Assert.Equal(Vec3.Zero, loco.Input);
        Assert.Equal(37.5f, loco.Speed);
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        RemoveBuff(1, actor.buffs.Active.Single(b => b.SourceContextId == buffContextId));
        actor.ReplaceMoveInput(1, 0);
        LocomotionSystem().Execute();
        Assert.Same(loco, OwnedLocomotion(1));
        Assert.Equal(new Vec3(1, 0, 0), loco.Input);
        Assert.Equal(new MobaAttrs(actor).MoveSpeed, loco.Speed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Mixed_motion_failure_compensates_values_cache_order_and_fixed_delta(int stage)
    {
        var loco = OwnedLocomotion(1);
        loco.Configure(12, MotionInputSpace.Local, 10);
        loco.SetInput(.2f, .4f);
        var pipeline = AddTiedSources(keepExisting: true, stacking: MotionStacking.Additive);
        var target = _preparation.Capture(_frame);
        loco.Configure(29, MotionInputSpace.World, 10);
        loco.SetInput(-1, -2);
        var liveOrder = pipeline.CaptureSourceOrder().Reverse().ToArray();
        pipeline.RestoreSourceOrder(liveOrder);
        TickOrderedPipeline(pipeline);
        var live = _preparation.Capture(_frame);
        var states = liveOrder.OfType<FixedDeltaMotionSource>().Select(s => s.CaptureExactState()).ToArray();
        var reached = false;
        _preparation.RestoreStageCompleted = (value, compensating) =>
        {
            if (!compensating && (int)value == stage)
            {
                reached = true;
                throw new InvalidOperationException("注入普通移动共存恢复失败");
            }
        };
        Assert.Throws<InvalidOperationException>(() => _preparation.Commit(_preparation.Prepare(_frame, target)));
        Assert.True(reached);
        Assert.Same(loco, OwnedLocomotion(1));
        Assert.Equal(new Vec3(-1, 0, -2), loco.Input);
        Assert.Equal(29, loco.Speed);
        Assert.Equal(MotionInputSpace.World, loco.Space);
        Assert.Equal(10, loco.Priority);
        Assert.Equal(liveOrder, pipeline.CaptureSourceOrder());
        Assert.Equal(states, liveOrder.OfType<FixedDeltaMotionSource>().Select(s => s.CaptureExactState()));
        Assert.Equal(live, _preparation.Capture(_frame));
        _preparation.RestoreStageCompleted = null;
    }

    [Theory]
    [InlineData("missing-local")]
    [InlineData("duplicate-local")]
    [InlineData("owner")]
    [InlineData("version")]
    [InlineData("kind")]
    [InlineData("id")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("input-y")]
    [InlineData("space")]
    [InlineData("priority")]
    [InlineData("order-owner")]
    [InlineData("order-version")]
    [InlineData("order-kind")]
    [InlineData("order-id")]
    [InlineData("duplicate-order")]
    [InlineData("missing-live")]
    [InlineData("replacement-live")]
    [InlineData("duplicate-live")]
    [InlineData("wrong-pipeline")]
    [InlineData("wrong-live-version")]
    [InlineData("late-corrupt-entry")]
    [InlineData("path")]
    [InlineData("collision-dash")]
    public void Mixed_motion_invalid_checkpoint_or_binding_rejects_before_any_world_import(string defect)
    {
        var loco = OwnedLocomotion(1);
        var pipeline = AddTiedSources(keepExisting: true);
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        var envelope = Decode(_preparation.Capture(_frame));
        var localSection = Find(envelope, "locomotion-local");
        var local = JArray.Parse(Encoding.UTF8.GetString(localSection.Payload));
        var saved = (JObject)local[0]!;
        if (defect == "missing-local") local.Clear();
        if (defect == "duplicate-local") local.Add(saved.DeepClone());
        if (defect == "owner") saved["Identity"]!["ActorId"] = 3;
        if (defect == "version") saved["Identity"]!["EntityVersion"] = int.MaxValue;
        if (defect == "kind") saved["Identity"]!["SourceKind"] = 99;
        if (defect == "id") saved["Identity"]!["SourceId"] = 1;
        if (defect == "nan") saved["Speed"] = "NaN";
        if (defect == "infinity") saved["X"] = "Infinity";
        if (defect == "input-y") saved["Y"] = 1;
        if (defect == "space") saved["Space"] = 99;
        if (defect == "priority") saved["Priority"] = 99;
        if (defect == "late-corrupt-entry")
        {
            var invalid = (JObject)saved.DeepClone();
            invalid["Identity"]!["ActorId"] = 3;
            invalid["Y"] = 1;
            local.Add(invalid);
        }
        localSection.Payload = Encoding.UTF8.GetBytes(local.ToString(Newtonsoft.Json.Formatting.None));
        var continuousSection = Find(envelope, "motion-pipelines");
        var entries = JArray.Parse(Encoding.UTF8.GetString(continuousSection.Payload));
        var ids = (JArray)entries.First(e => ((JArray)e["Sources"]!).Count >= 2)["Sources"]!;
        var identity = (JObject)ids.Single(id => (int)id!["SourceKind"]! == (int)MobaMotionSourceKind.Locomotion)!;
        if (defect == "order-owner") identity["ActorId"] = 3;
        if (defect == "order-version") identity["EntityVersion"] = int.MaxValue;
        if (defect == "order-kind") identity["SourceKind"] = 99;
        if (defect == "order-id") identity["SourceId"] = 1;
        if (defect == "duplicate-order") ids[0] = identity.DeepClone();
        continuousSection.Payload = Encoding.UTF8.GetBytes(entries.ToString(Newtonsoft.Json.Formatting.None));
        if (defect is "missing-live" or "replacement-live") pipeline.RemoveSource(loco);
        if (defect == "replacement-live") pipeline.AddSource(new LocomotionMotionSource(4));
        if (defect == "duplicate-live") pipeline.AddSource(loco);
        if (defect == "path") pipeline.AddSource(new PathFollowerMotionSource(new[] { new Vec3(20, 0, 20) }, 4));
        if (defect == "collision-dash") pipeline.AddSource(new MobaCollisionDashMotionSource(ExactSource(20), default));
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        if (defect == "wrong-pipeline")
        {
            var m = actor.motion;
            var other = new MotionPipeline();
            foreach (var source in pipeline.CaptureSourceOrder()) other.AddSource(source);
            actor.ReplaceMotion(other, m.State, m.Output, m.Solver, m.Policy, m.Events, m.Initialized, m.HitTriggerRuntime);
        }
        if (defect == "wrong-live-version") Get<MobaActorRegistry>().RegisterRestored(1, actor, int.MaxValue);
        var live = WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame));
        var corrupt = ReplaceLifecycle(target, MobaSkillLifecycleCheckpointCodec.Encode(envelope));
        Assert.False(coordinator.TryRestore(corrupt, out var result));
        /* Actor 版本不符现由更早执行的 Brain 完整名单预检拒绝，其余错误仍属于联合生命周期。 */
        Assert.Equal(defect == "wrong-live-version" ? MobaBrainRollbackProvider.DefaultKey
            : MobaSkillLifecycleRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(live, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
    }

    [Fact]
    public void Empty_group_cleanup_detaches_only_owned_sources()
    {
        var actors = Get<MobaActorRegistry>();
        var owned = actors.Entries.Select(p => (Actor: p.Value, Source: OwnedLocomotion(p.Key), Pipeline: p.Value.motion.Pipeline)).ToArray();
        var external = new LocomotionMotionSource(1);
        owned[0].Pipeline.AddSource(external);
        foreach (var item in owned) item.Actor.RemoveMoveInput();
        LocomotionSystem().Execute();
        foreach (var item in owned) Assert.False(item.Pipeline.ContainsSource(item.Source));
        Assert.True(owned[0].Pipeline.ContainsSource(external));
        foreach (var item in owned) item.Actor.AddMoveInput(0, 0);
        LocomotionSystem().Execute();
        foreach (var item in owned)
            Assert.DoesNotContain(item.Source, item.Actor.motion.Pipeline.CaptureSourceOrder());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Normal_system_replaces_stale_pipeline_or_entity_version_binding(bool changedVersion)
    {
        var actors = Get<MobaActorRegistry>();
        Assert.True(actors.TryGet(1, out var actor));
        var source = OwnedLocomotion(1);
        var oldPipeline = actor.motion.Pipeline;
        if (changedVersion) actors.RegisterRestored(1, actor, actors.GetEntityVersion(1) + 1);
        else
        {
            var m = actor.motion;
            actor.ReplaceMotion(new MotionPipeline(), m.State, m.Output, m.Solver, m.Policy, m.Events, true, m.HitTriggerRuntime);
        }
        LocomotionSystem().Execute();
        Assert.False(oldPipeline.ContainsSource(source));
        Assert.DoesNotContain(source, actor.motion.Pipeline.CaptureSourceOrder());
        var created = Assert.Single(actor.motion.Pipeline.CaptureSourceOrder());
        Assert.IsType<LocomotionMotionSource>(created);
        Assert.Same(created, OwnedLocomotion(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recreated_source_restores_the_same_logical_slot_after_normal_system_refresh(bool leaveGroup)
    {
        var loco = OwnedLocomotion(1);
        AddMotion(1, 91001, ExactSource(10));
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        if (leaveGroup)
        {
            actor.RemoveMoveInput();
            LocomotionSystem().Execute();
            actor.AddMoveInput(0, 0);
        }
        else actor.motion.Pipeline.RemoveSource(loco);
        LocomotionSystem().Execute();
        Assert.DoesNotContain(loco, actor.motion.Pipeline.CaptureSourceOrder());
        Assert.Single(actor.motion.Pipeline.CaptureSourceOrder().OfType<LocomotionMotionSource>());
        var replacement = OwnedLocomotion(1);
        replacement.Speed = 99;
        Assert.True(coordinator.TryRestore(target, out var result), result.ToString());
        Assert.Same(replacement, OwnedLocomotion(1));
        Assert.Equal(WorldRollbackSnapshotCodec.Serialize(target), WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
    }

    private LocomotionMotionSource OwnedLocomotion(int id)
    {
        var actors = Get<MobaActorRegistry>();
        Assert.True(actors.TryGet(id, out var actor));
        Assert.True(Get<MobaLocomotionRuntimeService>().TryGetBinding(id, actor, actors.GetEntityVersion(id), actor.motion.Pipeline, out var source));
        return source;
    }

    private MobaMotionLocomotionInputSystem LocomotionSystem()
    {
        var system = new MobaMotionLocomotionInputSystem(((EntitasWorld)_world).Contexts, _world.Services);
        system.Initialize();
        return system;
    }
}
