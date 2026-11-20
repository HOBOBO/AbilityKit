using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projection;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.Systems.Motion;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Theory]
    [InlineData("time-negative")]
    [InlineData("time-nan")]
    [InlineData("time-infinity")]
    [InlineData("velocity-x")]
    [InlineData("velocity-y")]
    [InlineData("velocity-z")]
    public void Motion_capture_rejects_invalid_authoritative_values_without_changing_business_state(string defect)
    {
        Assert.True(Get<MobaActorRegistry>().TryGet(1, out var actor));
        var motion = actor.motion;
        var state = motion.State;
        switch (defect)
        {
            case "time-negative": state.Time = -1; break;
            case "time-nan": state.Time = float.NaN; break;
            case "time-infinity": state.Time = float.PositiveInfinity; break;
            case "velocity-x": state.Velocity = new Vec3(float.NaN, 2, 3); break;
            case "velocity-y": state.Velocity = new Vec3(1, float.PositiveInfinity, 3); break;
            case "velocity-z": state.Velocity = new Vec3(1, 2, float.NegativeInfinity); break;
        }
        actor.ReplaceMotion(motion.Pipeline, state, motion.Output, motion.Solver, motion.Policy,
            motion.Events, motion.Initialized, motion.HitTriggerRuntime);
        var before = actor.motion;
        var provider = new MobaActorMotionRollbackProvider(Get<MobaActorRegistry>());
        var captureError = Assert.Throws<InvalidOperationException>(() => provider.Export(_frame));
        Assert.Contains("ActorMotion", captureError.Message);
        var hash = new MobaStateHashBuilder(2166136261u);
        Assert.Throws<InvalidOperationException>(() => provider.AddStateHash(_frame, ref hash));
        Assert.Equal(2166136261u, hash.Value);
        Assert.Same(before, actor.motion);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Motion_restore_rebuilds_projected_velocity_before_next_tick(bool wholeWorld, bool stopped)
    {
        var actors = Get<MobaActorRegistry>();
        Assert.True(actors.TryGet(1, out var actor));
        var velocity = stopped ? Vec3.Zero : new Vec3(2.5f, 0, -3.75f);
        SetVelocity(velocity);
        var producer = new MobaActorProjectionProducer(actors);
        var expected = producer.ExtractFull(1);
        var motionProvider = new MobaActorMotionRollbackProvider(actors);
        var checkpoint = motionProvider.Export(_frame);
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        var worldCheckpoint = coordinator.Capture(_frame);
        var events = new List<string>();
        Get<MobaContinuousManager>().OnActivated += _ => events.Add("activated");
        Get<MobaContinuousManager>().OnUnregistered += (_, _) => events.Add("unregistered");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            SetVelocity(new Vec3(-9, 0, 13));
            Assert.NotEqual(expected.VelX, producer.ExtractFull(1).VelX);
            if (wholeWorld)
                Assert.True(coordinator.TryRestore(worldCheckpoint, out var result), result.ToString());
            else
                motionProvider.Import(_frame, checkpoint);

            AssertVelocity();
            Assert.Equal(checkpoint, motionProvider.Export(_frame));
            if (wholeWorld)
                Assert.Equal(WorldRollbackSnapshotCodec.Serialize(worldCheckpoint),
                    WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));

            /*
             * 零时间步会在进入 Pipeline 前返回，不能靠下一次 Tick 清缓存掩盖恢复遗漏。
             */
            Get<IWorldClock>().Tick(0);
            var system = new MobaMotionTickSystem(((EntitasWorld)_world).Contexts, _world.Services);
            system.Initialize();
            system.Execute();
            AssertVelocity();
            Assert.Equal(checkpoint, motionProvider.Export(_frame));
        }
        Assert.Empty(events);

        void SetVelocity(Vec3 value)
        {
            var motion = actor.motion;
            var state = motion.State;
            state.Velocity = value;
            var output = motion.Output;
            output.NewVelocity = value;
            actor.ReplaceMotion(motion.Pipeline, state, output, motion.Solver, motion.Policy,
                motion.Events, motion.Initialized, motion.HitTriggerRuntime);
        }

        void AssertVelocity()
        {
            var actual = producer.ExtractFull(1);
            Assert.Equal(velocity, actor.motion.State.Velocity);
            Assert.Equal(velocity, actor.motion.Output.NewVelocity);
            Assert.Equal(expected.VelX, actual.VelX);
            Assert.Equal(expected.VelZ, actual.VelZ);
            Assert.Equal(expected.PosX, actual.PosX);
            Assert.Equal(expected.PosY, actual.PosY);
            Assert.Equal(expected.PosZ, actual.PosZ);
        }
    }

    [Fact]
    public void Motion_velocity_projection_replays_each_formal_frame_after_restore()
    {
        const float delta = 1f / 30f;
        var actors = Get<MobaActorRegistry>();
        Assert.True(actors.TryGet(1, out var actor));
        actor.ReplaceMoveInput(.6f, .8f);
        _bootstrapper.Tick(delta);
        var frame = new FrameIndex(_bootstrapper.Context.LastFrame);
        var producer = new MobaActorProjectionProducer(actors);
        var initial = producer.ExtractFull(1);
        Assert.NotEqual(0f, initial.VelX);
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(_world), new RollbackSnapshotRingBuffer(2));
        Assert.True(coordinator.CaptureAndStore(frame));
        var expected = new List<(byte[] World, Vec3 Position, Vec3 Velocity)>();
        for (var i = 0; i < 4; i++)
        {
            actor.ReplaceMoveInput(i % 2 == 0 ? -.8f : 0, i % 2 == 0 ? .6f : 0);
            _bootstrapper.Tick(delta);
            expected.Add((WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(new FrameIndex(_bootstrapper.Context.LastFrame))),
                actor.transform.Value.Position, actor.motion.State.Velocity));
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.True(_bootstrapper.TryRestoreSimulationFrame(coordinator, frame, out var result), result.ToString());
            Assert.Equal(initial.VelX, producer.ExtractFull(1).VelX);
            Assert.Equal(initial.VelZ, producer.ExtractFull(1).VelZ);
            for (var i = 0; i < expected.Count; i++)
            {
                actor.ReplaceMoveInput(i % 2 == 0 ? -.8f : 0, i % 2 == 0 ? .6f : 0);
                _bootstrapper.Tick(delta);
                var actual = producer.ExtractFull(1);
                Assert.Equal(expected[i].Position, actor.transform.Value.Position);
                Assert.Equal(expected[i].Velocity, actor.motion.State.Velocity);
                Assert.Equal(expected[i].Velocity.X, actual.VelX);
                Assert.Equal(expected[i].Velocity.Z, actual.VelZ);
                Assert.Equal(expected[i].World,
                    WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(new FrameIndex(_bootstrapper.Context.LastFrame))));
            }
        }
    }
}
