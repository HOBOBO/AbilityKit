using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Events;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Systems.Motion;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaMotionRuntimeDependencyTests
{
    private static readonly FrameIndex Frame = new(12);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Init_keeps_standard_solver_policy_and_collision_behavior_without_rollback(bool collision)
    {
        using var f = new Fixture(collision);
        var actor = f.Add(1);
        f.Init.Execute();
        var pipeline = actor.motion.Pipeline;
        var policy = pipeline.Policy;
        var solver = actor.motion.Solver;
        Assert.True(actor.motion.Initialized);
        Assert.NotNull(policy);
        Assert.Null(actor.motion.Policy);
        if (collision)
        {
            Assert.Same(f.Dependencies.ResolveDefaultSolver(), solver);
            Assert.True(f.Dependencies.OwnsStandardSolver(solver));
        }
        else
        {
            Assert.Null(solver);
            Assert.Same(NoMotionSolver.Instance, pipeline.Solver);
        }
        var second = f.Add(2);
        f.Init.Execute();
        Assert.Same(pipeline, actor.motion.Pipeline);
        Assert.Same(policy, pipeline.Policy);
        Assert.NotSame(policy, second.motion.Pipeline.Policy);
        Assert.Same(solver, second.motion.Solver);
        var transform = new Transform3(new Vec3(5, 0, 0), Quat.Identity, Vec3.One);
        var shape = ColliderShape.CreateAabb(new Vec3(-.1f, -2, -2), new Vec3(.1f, 2, 2));
        f.Collision?.World.Add(transform, shape, MobaCollisionLayers.WorldId);
        pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(20, 0, 0), 2, 1,
            MotionGroups.Locomotion, MotionStacking.Additive));
        var state = new MotionState(Vec3.Zero);
        var output = new MotionOutput();
        pipeline.Tick(1, ref state, .5f, ref output);
        Assert.InRange(state.Position.X, collision ? 4.39f : 9.99f, collision ? 4.41f : 10.01f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Policy_restores_complete_values_without_reinitializing_sources_or_solver(bool collision)
    {
        using var f = new Fixture(collision);
        var actor = f.Add(1);
        f.Init.Execute();
        var pipeline = actor.motion.Pipeline;
        var solver = actor.motion.Solver;
        var policy = pipeline.Policy;
        policy.SetSuppressedGroups(-7, int.MaxValue, 0, -1, -1);
        var source = new FixedDeltaMotionSource(new Vec3(1, 0, 0), 3, 1, MotionGroups.Ability, MotionStacking.Additive);
        pipeline.AddSource(source);
        var beforeSource = source.CaptureExactState();
        var target = f.Provider.Export(Frame);
        policy.SetSuppressedGroups(-7);
        policy.SetSuppressedGroups(9123, 4);
        f.Provider.PrepareRestore(Frame, target);
        Assert.True(policy.TryGetSuppressedGroups(9123, out _));
        f.Provider.Import(Frame, target);
        Assert.Same(policy, pipeline.Policy);
        Assert.Same(solver, actor.motion.Solver);
        Assert.Equal(new[] { int.MaxValue, 0, -1, -1 }, Read(policy, -7));
        Assert.False(policy.TryGetSuppressedGroups(9123, out _));
        Assert.Equal(beforeSource, source.CaptureExactState());
        Assert.Equal(new[] { source }, pipeline.CaptureSourceOrder());
        Assert.Equal(target, f.Provider.Export(Frame));
        f.Provider.Import(Frame, target);
        Assert.Equal(target, f.Provider.Export(Frame));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Policy_presence_and_empty_policy_are_distinct_and_restore_bidirectionally(bool saved, bool live)
    {
        using var f = new Fixture(false);
        var actor = f.Add(1);
        f.Init.Execute();
        actor.motion.Pipeline.Policy = saved ? new MotionPipelinePolicy() : null;
        var target = f.Provider.Export(Frame);
        var current = actor.motion.Pipeline.Policy = live ? MotionPipelinePolicy.CreateDefault() : null;
        f.Provider.Import(Frame, target);
        Assert.Equal(saved, actor.motion.Pipeline.Policy != null);
        if (saved && live) Assert.Same(current, actor.motion.Pipeline.Policy);
        Assert.Equal(target, f.Provider.Export(Frame));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Mid_commit_failure_restores_every_policy_value_reference_and_binding(int failAt)
    {
        using var f = new Fixture(true);
        var actors = new[] { f.Add(1), f.Add(2) };
        f.Init.Execute();
        var target = f.Provider.Export(Frame);
        actors[0].motion.Pipeline.Policy = null;
        actors[1].motion.Pipeline.Policy.SetSuppressedGroups(445, 999);
        var refs = actors.Select(a => a.motion.Pipeline.Policy).ToArray();
        var solvers = actors.Select(a => a.motion.Solver).ToArray();
        var live = f.Provider.Export(Frame);
        var reached = false;
        f.Provider.BindingRestoredForTesting = id =>
        {
            if (id != failAt) return;
            reached = true;
            throw new InvalidOperationException("注入运行依赖恢复故障");
        };
        Assert.Throws<InvalidOperationException>(() => f.Provider.Import(Frame, target));
        Assert.True(reached);
        Assert.Equal(live, f.Provider.Export(Frame));
        for (var i = 0; i < actors.Length; i++)
        {
            Assert.Same(refs[i], actors[i].motion.Pipeline.Policy);
            Assert.Same(solvers[i], actors[i].motion.Solver);
        }
    }

    [Theory]
    [InlineData("shared-policy")]
    [InlineData("shared-pipeline")]
    [InlineData("component-policy")]
    [InlineData("component-events")]
    [InlineData("pipeline-events")]
    [InlineData("custom-solver")]
    [InlineData("diagnostics")]
    [InlineData("replacement-collision")]
    [InlineData("missing-actor")]
    [InlineData("different-generation")]
    public void Unsupported_layout_is_rejected_without_any_policy_import(string defect)
    {
        using var f = new Fixture(true);
        var one = f.Add(1);
        var two = f.Add(2);
        f.Init.Execute();
        var target = f.Provider.Export(Frame);
        var policy = one.motion.Pipeline.Policy;
        policy.SetSuppressedGroups(100, 200);
        switch (defect)
        {
            case "shared-policy": two.motion.Pipeline.Policy = policy; break;
            case "shared-pipeline": two.motion.Pipeline = one.motion.Pipeline; break;
            case "component-policy": two.motion.Policy = new MotionPipelinePolicy(); break;
            case "component-events": two.motion.Events = new Sink(); break;
            case "pipeline-events": two.motion.Pipeline.Events = new Sink(); break;
            case "custom-solver": two.motion.Pipeline.Solver = new Solver(); break;
            case "diagnostics": ((ConfigurableMotionSolver)two.motion.Solver).Diagnostics = new Diagnostics(); break;
            case "replacement-collision": f.Resolver.Add<ICollisionService>(new CollisionService()); break;
            case "missing-actor": f.Actors.Unregister(2); break;
            case "different-generation":
                f.Actors.Register(2, f.Contexts.actor.CreateEntity());
                f.Actors.Register(2, two);
                break;
        }
        Assert.ThrowsAny<Exception>(() => f.Provider.PrepareRestore(Frame, target));
        Assert.ThrowsAny<Exception>(() => f.Provider.Import(Frame, target));
        Assert.Equal(new[] { 200 }, Read(policy, 100));
    }

    [Theory]
    [InlineData("trailing")]
    [InlineData("version")]
    [InlineData("count")]
    [InlineData("bool")]
    [InlineData("truncated")]
    public void Malformed_payload_is_rejected_before_import(string defect)
    {
        using var f = new Fixture(false);
        var actor = f.Add(1);
        f.Init.Execute();
        var target = f.Provider.Export(Frame);
        var malformed = target.ToArray();
        if (defect == "trailing") malformed = target.Append((byte)0).ToArray();
        if (defect == "version") malformed[0] = 99;
        if (defect == "count") BitConverter.GetBytes(int.MaxValue).CopyTo(malformed, 4);
        if (defect == "bool") malformed[16] = 2;
        if (defect == "truncated") malformed = target[..^1];
        Assert.ThrowsAny<Exception>(() => f.Provider.Import(Frame, malformed));
        Assert.Equal(target, f.Provider.Export(Frame));
    }

    [Fact]
    public void Uninitialized_standard_skeleton_can_be_captured_without_initialization_or_service_creation()
    {
        using var f = new Fixture(true);
        var actor = f.Add(1);
        var target = f.Provider.Export(Frame);
        f.Provider.Import(Frame, target);
        Assert.False(actor.motion.Initialized);
        Assert.Null(actor.motion.Pipeline);
        Assert.Null(actor.motion.Solver);
    }

    [Fact]
    public void NoMotion_pipeline_override_does_not_misclassify_component_default_solver()
    {
        using var f = new Fixture(true);
        var actor = f.Add(1);
        f.Init.Execute();
        var solver = actor.motion.Solver;
        actor.motion.Pipeline.Solver = NoMotionSolver.Instance;
        var target = f.Provider.Export(Frame);
        f.Provider.Import(Frame, target);
        Assert.Same(solver, actor.motion.Solver);
        Assert.Same(NoMotionSolver.Instance, actor.motion.Pipeline.Solver);
    }

    [Fact]
    public void Ordinary_custom_dependencies_still_initialize_and_execute_without_rollback_registration()
    {
        using var f = new Fixture(true);
        var actor = f.Add(1);
        var solver = new Solver();
        var events = new Sink();
        var policy = new MotionPipelinePolicy();
        actor.motion.Solver = solver;
        actor.motion.Events = events;
        actor.motion.Policy = policy;
        f.Init.Execute();
        Assert.Same(solver, actor.motion.Solver);
        Assert.Same(solver, actor.motion.Pipeline.Solver);
        Assert.Same(events, actor.motion.Pipeline.Events);
        Assert.Same(policy, actor.motion.Pipeline.Policy);
        var source = new FixedDeltaMotionSource(new Vec3(3, 0, 0), 2, 1, MotionGroups.Ability, MotionStacking.Additive);
        actor.motion.Pipeline.AddSource(source);
        var state = actor.motion.State;
        var output = new MotionOutput();
        actor.motion.Pipeline.Tick(1, ref state, .1f, ref output);
        Assert.Equal(1, solver.Calls);
        Assert.InRange(state.Position.X, .299f, .301f);
        Assert.Throws<InvalidOperationException>(() => f.Provider.Export(Frame));
        Assert.Same(events, actor.motion.Pipeline.Events);
        Assert.Same(solver, actor.motion.Pipeline.Solver);
    }

    private static int[] Read(MotionPipelinePolicy policy, int key)
    {
        Assert.True(policy.TryGetSuppressedGroups(key, out var result));
        return result;
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Contexts Contexts = new();
        internal readonly MobaActorRegistry Actors = new();
        internal readonly Resolver Resolver = new();
        internal readonly MobaMotionRuntimeDependencyService Dependencies = new();
        internal readonly CollisionService? Collision;
        internal readonly MobaMotionRuntimeDependencyRollbackProvider Provider;
        internal readonly MobaMotionInitSystem Init;

        internal Fixture(bool collision)
        {
            Resolver.Add(Actors);
            Resolver.Add(Dependencies);
            if (collision) Resolver.Add<ICollisionService>(Collision = new CollisionService());
            Dependencies.OnInit(Resolver);
            Provider = new MobaMotionRuntimeDependencyRollbackProvider(Actors, Dependencies);
            Init = new MobaMotionInitSystem(Contexts, Resolver);
            Init.Initialize();
        }

        internal ActorEntity Add(int id)
        {
            var actor = ActorEntityFactory.Create(Contexts.actor).WithActorId(id)
                .WithTransform(new Transform3(Vec3.Zero, Quat.Identity, Vec3.One)).WithMotion().Build();
            Actors.Register(id, actor);
            return actor;
        }

        public void Dispose()
        {
            Contexts.actor.DestroyAllEntities();
            Dependencies.Dispose();
            Collision?.Dispose();
            Actors.Dispose();
        }
    }

    private sealed class Resolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _services = new();
        internal void Add<T>(T value) where T : class => _services[typeof(T)] = value;
        public object Resolve(Type type) => _services[type];
        public T Resolve<T>() => (T)Resolve(typeof(T));
        public bool TryResolve(Type type, out object instance) => _services.TryGetValue(type, out instance!);
        public bool TryResolve<T>(out T instance)
        {
            var found = TryResolve(typeof(T), out var value);
            instance = found ? (T)value : default!;
            return found;
        }
    }

    private sealed class Solver : IMotionSolver
    {
        internal int Calls;
        public MotionSolveResult Solve(int id, in MotionState state, in MotionOutput input, float dt)
        {
            Calls++;
            return MotionSolveResult.NoHit(input.DesiredDelta);
        }
    }

    private sealed class Sink : IMotionEventSink
    {
        public void OnHit(int id, in MotionState state, in MotionHit hit) => throw new Exception("不应补发碰撞");
        public void OnArrive(int id, in MotionState state) => throw new Exception("不应补发到达");
        public void OnExpired(int id, in MotionState state) => throw new Exception("不应补发结束");
    }

    private sealed class Diagnostics : IMotionSolverDiagnostics
    {
        public void OnConstraintsProviderException(int id, in MotionState state, in MotionOutput input, float dt, Exception ex) { }
        public void OnEndOverlapResolved(int id, in MotionState state, in AbilityKit.Combat.MotionSystem.Constraints.MotionCollisionConstraints constraints,
            AbilityKit.Combat.MotionSystem.Constraints.MotionEndOverlapPolicy policy, bool resolved) { }
    }
}
