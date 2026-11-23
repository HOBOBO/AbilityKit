using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Attributes.Core;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Services.Projection;
using AbilityKit.Demo.Moba.Services.StateImport;
using AbilityKit.Demo.Moba.Systems.Motion;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba lifecycle preparation")]
public sealed class MobaLocalDualModeContractTests
{
    private const ulong StateWorldId = 5;

    [Theory]
    [InlineData(.05f)]
    [InlineData(.033f)]
    public void Typed_settlements_and_projections_preserve_their_distinct_world_contracts(float deltaTime)
    {
        using var frameAuthority = new MovementWorld("frame-authority");
        using var stateAuthority = new MovementWorld("state-authority");
        using var frameReplica = new MovementWorld("frame-replica");
        using var stateReplica = new MovementWorld("state-replica", initialHp: 20);
        var scheduled = new LocalSettledFrames();
        var delivered = new LocalSettledFrames();
        var admission = new AuthoritativeSnapshotAdmission();
        admission.Reset(StateWorldId);
        var timeline = new MobaActorIntent[][]
        {
            [MobaActorIntent.MoveDirection(0, 1), MobaActorIntent.MoveDirection(1, 0)],
            [],
            [MobaActorIntent.MoveDirection(-.75f, .25f)],
            [],
            [MobaActorIntent.MoveDirection(0, 0)],
            [],
        };
        var settledWorld = new MovementCheckpoint[timeline.Length];
        var projections = new MobaActorStateImport[timeline.Length];
        var ready = new List<MobaActorIntent>();
        var replicaTick = 0;

        void CatchUpReplica()
        {
            while (delivered.TryRead(replicaTick + 1, ready))
            {
                frameReplica.Step(ready, deltaTime);
                Assert.Equal(settledWorld[replicaTick], frameReplica.Capture());
                replicaTick++;
            }
        }

        for (var tick = 1; tick <= timeline.Length; tick++)
        {
            var commands = timeline[tick - 1];
            scheduled.Receive(tick, commands);
            Assert.True(scheduled.TryRead(tick, ready));
            frameAuthority.Step(ready, deltaTime);
            stateAuthority.Step(commands, deltaTime);

            settledWorld[tick - 1] = frameAuthority.Capture();
            Assert.Equal(settledWorld[tick - 1], stateAuthority.Capture());
            projections[tick - 1] = stateAuthority.Project();

            if (tick != 2) delivered.Receive(tick, commands);
            CatchUpReplica();
            if (tick == 3)
            {
                Assert.Equal(1, replicaTick);
                delivered.Receive(2, timeline[1]);
                CatchUpReplica();
                Assert.Equal(3, replicaTick);
            }

            var sample = projections[tick - 1];
            var beforeImport = stateReplica.Capture();
            if (tick == 2) continue;
            if (tick == 3)
            {
                Assert.False(TryImport(stateReplica, admission, tick, [sample, sample]));
                Assert.Equal(1, admission.LastAcceptedFrame);
                Assert.Equal(beforeImport, stateReplica.Capture());
            }
            if (tick == 4)
            {
                Assert.False(TryImport(stateReplica, admission, 2, [projections[1]]));
                Assert.Equal(3, admission.LastAcceptedFrame);
                Assert.Equal(beforeImport, stateReplica.Capture());
            }
            if (tick == 5)
            {
                admission.RequireFullBaseline();
                Assert.Equal(AuthoritativeSnapshotAdmissionStatus.BaselineRequired,
                    admission.Check(StateWorldId, tick, isFullSnapshot: false).Status);
                Assert.False(admission.HasBaseline);
                Assert.Equal(4, admission.LastAcceptedFrame);
                Assert.Equal(beforeImport, stateReplica.Capture());
            }
            Assert.True(TryImport(stateReplica, admission, tick, [sample]));
            Assert.True(admission.HasBaseline);
            Assert.Equal(tick, admission.LastAcceptedFrame);
            var imported = stateReplica.Capture();
            Assert.Equal(settledWorld[tick - 1].Pose, imported.Pose);
            Assert.Equal(settledWorld[tick - 1].Hp, imported.Hp);
            Assert.Equal(imported.Pose.Position, imported.Motion.Position);
            Assert.Equal(0f, imported.ClockTime);
            Assert.Equal(0f, imported.Motion.Time);
            Assert.Equal(Vec3.Zero, imported.Motion.Velocity);
            Assert.Equal(Vec3.Zero, imported.Input);
        }

        Assert.Equal(timeline.Length, replicaTick);
        Assert.True(settledWorld[1].Pose.Position.X > settledWorld[0].Pose.Position.X);
        Assert.Equal(settledWorld[^2].Pose.Position, settledWorld[^1].Pose.Position);
        var anchor = settledWorld[1];
        frameReplica.Restore(anchor);
        frameReplica.Step([MobaActorIntent.MoveDirection(0, -1)], deltaTime);
        Assert.NotEqual(settledWorld[2].Pose.Position, frameReplica.Capture().Pose.Position);
        frameReplica.Restore(anchor);
        Assert.Equal(anchor, frameReplica.Capture());
        for (var tick = 3; tick <= replicaTick; tick++)
        {
            Assert.True(delivered.TryRead(tick, ready));
            frameReplica.Step(ready, deltaTime);
            Assert.Equal(settledWorld[tick - 1], frameReplica.Capture());
        }
    }

    private static bool TryImport(MovementWorld target, AuthoritativeSnapshotAdmission admission, int tick,
        MobaActorStateImport[] actors)
    {
        if (!admission.Check(StateWorldId, tick, isFullSnapshot: true).Accepted) return false;
        var result = target.Importer.ImportScopedFullSnapshot(actors, tick, [MovementWorld.ActorId]);
        if (result.Failed != 0) return false;
        return admission.Admit(StateWorldId, tick, isFullSnapshot: true).Accepted;
    }

    private sealed class LocalSettledFrames
    {
        private readonly record struct OrderedIntent(int Order, MobaActorIntent Input);
        private readonly HashSet<int> _settled = new();
        private readonly FrameCommandBuffer<int, OrderedIntent> _commands = new(
            commandComparer: Comparer<OrderedIntent>.Create((left, right) => left.Order.CompareTo(right.Order)));
        private readonly List<OrderedIntent> _ordered = new();

        internal void Receive(int tick, IReadOnlyList<MobaActorIntent> inputs)
        {
            Assert.True(_settled.Add(tick));
            for (var index = inputs.Count - 1; index >= 0; index--)
                _commands.SetCommand(tick, index, new OrderedIntent(index, inputs[index]));
        }

        internal bool TryRead(int tick, List<MobaActorIntent> target)
        {
            target.Clear();
            if (!_settled.Contains(tick)) return false;
            _commands.CopyFrameCommands(tick, _ordered);
            foreach (var command in _ordered) target.Add(command.Input);
            return true;
        }
    }

    private readonly record struct MovementCheckpoint(Transform3 Pose, MotionState Motion, MotionOutput Output,
        Vec3 Input, Vec3 SourceInput, float ClockTime, float DeltaTime, float Hp);

    private sealed class MovementWorld : IDisposable
    {
        internal const int ActorId = 1;
        private readonly EntitasWorld _world;
        private readonly ActorEntity _actor;
        private readonly WorldClock _clock;
        private readonly MotionPipeline _pipeline = new();
        private readonly MobaActorProjectionProducer _projection;
        internal MobaLogicWorldStateImporter Importer { get; }

        internal MovementWorld(string id, int initialHp = 87)
        {
            var options = new WorldCreateOptions(new WorldId(id), "local-movement-contract")
                .SetEntitasContextsFactory(new MobaEntitasContextsFactory());
            options.Modules.Add(new MovementModule());
            _world = new EntitasWorld(options);
            _world.Initialize();
            _clock = Assert.IsType<WorldClock>(_world.Services.Resolve<IWorldClock>());
            var registry = _world.Services.Resolve<MobaActorRegistry>();
            _actor = ((Contexts)_world.Contexts).actor.CreateEntity();
            _actor.AddActorId(ActorId);
            registry.Register(ActorId, _actor);
            var start = new Vec3(3, 0, 8);
            _actor.AddTransform(new Transform3(start, Quat.Identity, Vec3.One));
            _actor.AddMoveInput(0, 0);
            _actor.AddMotion(_pipeline, new MotionState(start), default, _pipeline.Solver, null, null, true, default);
            var attributes = new AttributeContext();
            attributes.SetBase(MobaAttributeIds.MOVE_SPEED, 7.5f);
            attributes.SetBase(MobaAttributeIds.MAX_HP, 100f);
            _actor.AddAttributeGroup(attributes.GetGroupFor(MobaAttributeIds.MAX_HP), attributes);
            var hp = new ResourceState { Current = initialHp };
            _actor.AddResourceContainer(new ResourceContainer { Map = new() { [ResourceType.Hp] = hp } }, true);
            _projection = new MobaActorProjectionProducer(registry);
            Importer = new MobaLogicWorldStateImporter(registry, null!, null!);
        }

        internal void Step(IReadOnlyList<MobaActorIntent> inputs, float deltaTime)
        {
            foreach (var input in inputs) _actor.ReplaceMoveInput(input.MoveX, input.MoveZ);
            _world.Tick(deltaTime);
        }

        internal MovementCheckpoint Capture() => new(_actor.transform.Value, _actor.motion.State, _actor.motion.Output,
            new Vec3(_actor.moveInput.Dx, 0, _actor.moveInput.Dz), Source()?.Input ?? Vec3.Zero,
            _clock.Time, _clock.DeltaTime, new MobaAttrs(_actor).Hp);

        internal void Restore(MovementCheckpoint checkpoint)
        {
            _actor.ReplaceMoveInput(checkpoint.Input.X, checkpoint.Input.Z);
            _actor.ReplaceTransform(checkpoint.Pose);
            _actor.ReplaceMotion(_pipeline, checkpoint.Motion, checkpoint.Output, _pipeline.Solver, null, null, true, default);
            Assert.IsType<LocomotionMotionSource>(Source()).SetInput(checkpoint.SourceInput.X, checkpoint.SourceInput.Z);
            _clock.RestoreState(checkpoint.ClockTime, checkpoint.DeltaTime);
            var attributes = new MobaAttrs(_actor);
            attributes.Hp = checkpoint.Hp;
        }

        internal MobaActorStateImport Project()
        {
            var value = _projection.ExtractFull(ActorId);
            var yaw = MathF.Atan2(2f * (value.RotW * value.RotY + value.RotX * value.RotZ),
                1f - 2f * (value.RotY * value.RotY + value.RotZ * value.RotZ));
            return new MobaActorStateImport(value.ActorId, value.PosX, value.PosY, value.PosZ, yaw,
                value.Hp, value.HpMax, value.TeamId, value.Kind, value.Code, value.OwnerNetId);
        }

        private LocomotionMotionSource? Source() => _pipeline.CaptureSourceOrder().OfType<LocomotionMotionSource>().SingleOrDefault();

        public void Dispose()
        {
            _projection.Dispose();
            _world.Dispose();
            _pipeline.Dispose();
        }
    }

    private sealed class MovementModule : IWorldModule, IEntitasSystemsInstaller
    {
        public void Configure(WorldContainerBuilder builder)
        {
            builder.Register(WorldLifetime.Scoped, _ => new MobaActorRegistry());
            builder.Register(WorldLifetime.Scoped, _ => new MobaEntityManager(null!));
            builder.Register(WorldLifetime.Scoped, resolver => new ActorIdIndex(resolver.Resolve<Entitas.IContexts>()));
            builder.Register(WorldLifetime.Scoped, resolver => new MobaCombatRulesService(new MobaActorLookupService(
                resolver.Resolve<ActorIdIndex>(), resolver.Resolve<MobaActorRegistry>(),
                resolver.Resolve<MobaEntityManager>(), resolver.Resolve<Entitas.IContexts>())));
            builder.Register(WorldLifetime.Scoped, _ => new MobaLocomotionRuntimeService());
        }

        public void Install(Entitas.IContexts contexts, Entitas.Systems systems, IWorldResolver services)
        {
            systems.Add(new MobaMotionLocomotionInputSystem(contexts, services));
            systems.Add(new MobaMotionTickSystem(contexts, services));
        }
    }
}
