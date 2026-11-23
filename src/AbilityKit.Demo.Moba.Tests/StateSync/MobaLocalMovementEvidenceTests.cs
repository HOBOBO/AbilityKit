using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Attributes.Core;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Systems.Motion;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba lifecycle preparation")]
public sealed class MobaLocalMovementEvidenceTests
{
    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.033f)]
    public void Independent_ordinary_movement_matches_authority_at_each_tick(float deltaTime)
    {
        const int actorId = 1;
        var contexts = new Contexts();
        using var index = new ActorIdIndex(contexts);
        using var actors = new MobaActorRegistry();
        using var entities = new MobaEntityManager(null!);
        using var authorityPipeline = new MotionPipeline();
        var clock = new WorldClock();
        var lookup = new MobaActorLookupService(index, actors, entities, contexts);
        var rules = new MobaCombatRulesService(lookup);
        using var services = new WorldContainerBuilder()
            .RegisterExternalInstance(typeof(MobaActorRegistry), actors)
            .RegisterInstance<IWorldClock>(clock)
            .RegisterInstance(rules)
            .RegisterInstance(new MobaLocomotionRuntimeService())
            .Build();

        var actor = contexts.actor.CreateEntity();
        actor.AddActorId(actorId);
        actors.Register(actorId, actor);
        Assert.True(actors.GetEntityVersion(actorId) > 0);
        var initialPose = new Transform3(new Vec3(3, 0, 8), Quat.LookRotation(new Vec3(1, 0, 1), Vec3.Up), Vec3.One);
        var initialMotion = new MotionState(initialPose.Position) { Forward = initialPose.Forward };
        actor.AddTransform(initialPose);
        actor.AddMoveInput(.35f, -.6f);
        actor.AddMotion(authorityPipeline, initialMotion, default, authorityPipeline.Solver, null, null, true, default);
        var attributes = new AttributeContext();
        attributes.SetBase(MobaAttributeIds.MOVE_SPEED, 7.5f);
        actor.AddAttributeGroup(attributes.GetGroupFor(MobaAttributeIds.MOVE_SPEED), attributes);
        var hp = new ResourceState { Current = 100 };
        actor.AddResourceContainer(new ResourceContainer { Map = new() { [ResourceType.Hp] = hp } }, true);

        var sampleInput = new MobaMotionLocomotionInputSystem(contexts, services);
        var tickMotion = new MobaMotionTickSystem(contexts, services);
        sampleInput.Initialize();
        tickMotion.Initialize();
        clock.Tick(deltaTime);
        sampleInput.Execute();
        tickMotion.Execute();
        Assert.NotEqual(initialPose.Position, actor.transform.Value.Position);
        var authoritySource = Assert.IsType<LocomotionMotionSource>(Assert.Single(authorityPipeline.CaptureSourceOrder()));

        using var isolatedPipeline = new MotionPipeline();
        var isolatedSource = new LocomotionMotionSource(7.5f, MotionInputSpace.World);
        isolatedPipeline.AddSource(isolatedSource);
        Assert.NotSame(isolatedSource, authoritySource);
        var isolatedPose = actor.transform.Value;
        var motion = actor.motion.State;
        var isolatedInput = new Vec3(actor.moveInput.Dx, 0f, actor.moveInput.Dz);
        var output = new MotionOutput();
        var schedule = new (Vec3? Input, float Speed, bool Alive, bool Moves)[]
        {
            (null, 7.5f, true, true),
            (new Vec3(-.75f, 0f, .25f), 11.25f, true, true),
            (null, 11.25f, false, false),
            (null, 11.25f, true, true),
            (Vec3.Zero, 11.25f, true, false),
            (null, 11.25f, true, false),
        };

        foreach (var (input, speed, alive, moves) in schedule)
        {
            var previousPosition = isolatedPose.Position;
            var authorityBefore = actor.transform.Value;
            var motionBefore = actor.motion.State;
            var sampledInputBefore = authoritySource.Input;
            if (input is { } currentInput) isolatedInput = currentInput;
            var effectiveInput = alive ? isolatedInput : Vec3.Zero;
            isolatedSource.Speed = speed;
            isolatedSource.SetInput(effectiveInput.X, effectiveInput.Z);
            motion.Position = isolatedPose.Position;
            motion.Forward = isolatedPose.Forward;
            var solve = isolatedPipeline.Tick(actorId, ref motion, deltaTime, ref output);
            Assert.False(solve.Hit.Hit);
            isolatedPose = new Transform3(motion.Position,
                alive ? Quat.LookRotation(output.NewForward, Vec3.Up) : isolatedPose.Rotation, isolatedPose.Scale);

            Assert.Equal(authorityBefore, actor.transform.Value);
            Assert.Equal(motionBefore, actor.motion.State);
            Assert.Equal(sampledInputBefore, authoritySource.Input);

            if (input is { } appliedInput) actor.ReplaceMoveInput(appliedInput.X, appliedInput.Z);
            attributes.SetBase(MobaAttributeIds.MOVE_SPEED, speed);
            hp.Current = alive ? 100 : 0;
            clock.Tick(deltaTime);
            sampleInput.Execute();
            tickMotion.Execute();

            Assert.Equal(isolatedPose.Position, actor.transform.Value.Position);
            Assert.Equal(isolatedPose.Rotation, actor.transform.Value.Rotation);
            Assert.Equal(isolatedPose.Scale, actor.transform.Value.Scale);
            Assert.Equal(motion.Position, actor.motion.State.Position);
            Assert.Equal(motion.Forward, actor.motion.State.Forward);
            Assert.Equal(motion.Velocity, actor.motion.State.Velocity);
            Assert.Equal(motion.Time, actor.motion.State.Time);
            Assert.Equal(output.AppliedDelta, actor.motion.Output.AppliedDelta);
            Assert.Same(authoritySource, Assert.Single(authorityPipeline.CaptureSourceOrder()));
            Assert.Equal(effectiveInput, authoritySource.Input);
            Assert.Equal(moves, !previousPosition.Equals(isolatedPose.Position));
        }
        contexts.actor.DestroyAllEntities();
    }
}
