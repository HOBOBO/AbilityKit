using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Systems.Motion;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

/*
 * 诊断用例刻意隔离底层 Command 与字段 Provider，不把该最小装配当成正式 Battle 恢复。
 * 证明现有骨架能力为何不足以开放 Motion 重建；后续获准扩展时应同步替换这些边界断言。
 */
[Collection("Moba lifecycle preparation")]
public sealed class MobaMotionRebuildBoundaryTests
{
    [Fact]
    public void Audit_raw_commands_restore_motion_values_but_normal_init_discards_them_on_rebuilt_skeleton()
    {
        using var f = new Fixture();
        var savedVersion = f.Actors.GetEntityVersion(1);
        var savedVelocity = new Vec3(3.25f, 0, -2.5f);
        var state = f.Actor.motion.State;
        state.Velocity = savedVelocity;
        state.Time = 7.5f;
        f.Actor.motion.State = state;
        f.Actor.motion.Pipeline.Policy.SetSuppressedGroups(-771, 19);
        var snapshot = f.Coordinator.Capture(f.TargetFrame);

        f.DestroyAfterTarget();
        Assert.True(f.Coordinator.TryRestore(snapshot, out var result), result.ToString());
        var restored = f.RestoredActor();
        Assert.Equal(savedVersion, f.Actors.GetEntityVersion(1));
        Assert.Equal(savedVelocity, restored.motion.State.Velocity);
        Assert.Equal(7.5f, restored.motion.State.Time);
        Assert.Equal(savedVelocity, restored.motion.Output.NewVelocity);
        Assert.False(restored.motion.Initialized);
        Assert.Null(restored.motion.Pipeline);

        /*
         * 正常出生初始化不是恢复绑定入口：它会重新建立 MotionState 并清空 Output。
         * 用实际 System 复现覆盖，避免仅因旧 smoke 的字段往返成功就放开正式世界。
         */
        f.Init.Execute();
        Assert.True(restored.motion.Initialized);
        Assert.Equal(Vec3.Zero, restored.motion.State.Velocity);
        Assert.Equal(0f, restored.motion.State.Time);
        Assert.Equal(Vec3.Zero, restored.motion.Output.NewVelocity);
        Assert.False(restored.motion.Pipeline.Policy.TryGetSuppressedGroups(-771, out _));
        Assert.Empty(restored.motion.Pipeline.CaptureSourceOrder());
    }

    [Fact]
    public void Audit_policy_values_alone_do_not_restore_locomotion_ownership_after_raw_actor_rebuild()
    {
        using var f = new Fixture();
        using var locomotion = new MobaLocomotionRuntimeService();
        var version = f.Actors.GetEntityVersion(1);
        var originalPipeline = f.Actor.motion.Pipeline;
        var originalSource = locomotion.Create(f.Actor, version, 17.5f);
        originalSource.SetInput(.25f, -.75f);
        locomotion.RememberPipelineForRollback(f.Actor, version, originalPipeline);
        originalPipeline.Policy.SetSuppressedGroups(-772, 23);
        var dependencyPayload = f.DependenciesProvider.Export(f.TargetFrame);
        var snapshot = f.Coordinator.Capture(f.TargetFrame);

        f.DestroyAfterTarget();
        Assert.True(f.Coordinator.TryRestore(snapshot, out var result), result.ToString());
        var restored = f.RestoredActor();
        var failure = Assert.Throws<InvalidOperationException>(() =>
            f.DependenciesProvider.PrepareRestore(f.TargetFrame, dependencyPayload));
        Assert.Contains("layout or Solver binding changed", failure.Message);

        /*
         * 即使为了诊断先运行正常初始化并单独恢复 Policy，旧来源的所有者锚点仍不成立。
         * 此路径不属于正式恢复方案，不修改来源服务的同实例门禁。
         */
        f.Init.Execute();
        f.DependenciesProvider.Import(f.TargetFrame, dependencyPayload);
        Assert.True(restored.motion.Pipeline.Policy.TryGetSuppressedGroups(-772, out var groups));
        Assert.Equal(new[] { 23 }, groups);
        Assert.NotSame(originalPipeline, restored.motion.Pipeline);
        Assert.Empty(restored.motion.Pipeline.CaptureSourceOrder());
        Assert.False(locomotion.TryGetBinding(1, restored, version, restored.motion.Pipeline, out _));
        failure = Assert.Throws<InvalidOperationException>(() =>
            locomotion.ValidateSlotForRollback(1, restored, version, restored.motion.Pipeline));
        Assert.Contains("same surviving initialized actor Pipeline", failure.Message);
    }

    [Fact]
    public void Audit_valid_destruction_command_does_not_grant_formal_motion_actor_roster_projection()
    {
        using var f = new Fixture();
        var version = f.Actors.GetEntityVersion(1);
        var commandPayload = f.Commands.StateProvider.Export(f.TargetFrame);
        f.DestroyAfterTarget();
        var commandCount = f.Commands.Log.Count;
        var currentCommands = f.Commands.StateProvider.Export(f.CurrentFrame);

        f.Commands.StateProvider.ValidateImport(f.TargetFrame, commandPayload);
        var failure = Assert.Throws<InvalidOperationException>(() =>
            f.Commands.ValidateSupportedActorRosterProjection(f.TargetFrame, commandPayload,
                new[] { 1 }, new Dictionary<int, int> { [1] = version }, new HashSet<int>(), new HashSet<int>()));
        Assert.Contains("not a supported flying projectile or ProjectileLauncher", failure.Message);
        Assert.Equal(commandCount, f.Commands.Log.Count);
        Assert.Equal(currentCommands, f.Commands.StateProvider.Export(f.CurrentFrame));
        Assert.False(f.Actors.Contains(1));
        Assert.False(f.Entities.TryGetActorEntity(1, out _));
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Contexts Contexts = new();
        internal readonly MobaActorRegistry Actors = new();
        internal readonly MobaEntityManager Entities = new(null);
        internal readonly MobaMotionRuntimeDependencyService Dependencies = new();
        internal readonly FrameTime Time = new();
        internal readonly FrameIndex TargetFrame = new(12);
        internal readonly MobaActorRollbackCommandRuntime Commands;
        internal readonly MobaMotionRuntimeDependencyRollbackProvider DependenciesProvider;
        internal readonly MobaMotionInitSystem Init;
        internal readonly RollbackCoordinator Coordinator;
        internal readonly ActorEntity Actor;
        internal FrameIndex CurrentFrame => Time.Frame;

        internal Fixture()
        {
            Time.Reset(TargetFrame, .2f, 1f / 60f);
            var resolver = new Resolver();
            resolver.Add(Actors);
            resolver.Add(Dependencies);
            Dependencies.OnInit(resolver);
            Init = new MobaMotionInitSystem(Contexts, resolver);
            Init.Initialize();
            var ids = new ActorIdAllocator();
            Commands = Entities.GetOrCreateActorRollbackCommands(Contexts.actor, Actors, Time);
            var transform = new Transform3(new Vec3(2, 0, 3), Quat.Identity, Vec3.One);
            var info = new MobaEntityInfo(ids.Next(), MobaEntityKind.Hero, in transform,
                (Team)1, EntityMainType.Unit, UnitSubType.Hero, new PlayerId("motion-rebuild-audit"));
            var spec = new MobaActorBuildSpec(in info, MobaActorBuildSourceKind.PlayerLoadout, 1001, 0);
            Actor = ActorSpawnPipeline.BuildActorAndRegister(Contexts.actor, Actors, Entities, in spec).Entity;
            Init.Execute();
            DependenciesProvider = new MobaMotionRuntimeDependencyRollbackProvider(Actors, Dependencies);

            /*
             * 复用已有低层 Actor smoke 的最小装配，特意不注册完整生命周期及 10047。
             * 它只能证明 Command 建骨架和字段恢复；正式世界必须保留完整预检。
             */
            var registry = new RollbackRegistry();
            registry.Register(new MobaEntitasEntityRollbackProvider(Contexts.actor, ids, Actors, Entities,
                allowMissingActorsRestoredByCommands: true));
            registry.Register(Commands.StateProvider);
            registry.Register(new MobaActorMotionRollbackProvider(Actors));
            registry.Register(new MobaEntitasComponentRollbackProvider(Contexts.actor,
                allowMissingActorsRestoredByCommands: true));
            Coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        }

        internal void DestroyAfterTarget()
        {
            Time.StepTo(new FrameIndex(TargetFrame.Value + 1), 1f / 60f);
            Assert.True(new MobaActorSpawnRegistrar(Actors, Entities).Unregister(1, out var removed, publishDespawn: false));
            removed.Destroy();
            Assert.False(Actors.Contains(1));
        }

        internal ActorEntity RestoredActor()
        {
            Assert.True(Actors.TryGet(1, out var actor));
            Assert.True(Entities.TryGetActorEntity(1, out var indexed));
            Assert.Same(actor, indexed);
            return actor;
        }

        public void Dispose()
        {
            Entities.Dispose();
            Contexts.actor.DestroyAllEntities();
            Actors.Dispose();
            Dependencies.Dispose();
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
}

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Fact]
    public void Motion_rebuild_audit_missing_world_owner_is_rejected_before_commands_or_fields_are_imported()
    {
        var registry = MobaRollbackRegistryBuilder.Create(_world);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var snapshot = coordinator.Capture(_frame);
        var actors = Get<MobaActorRegistry>();
        var entities = Get<MobaEntityManager>();
        Assert.True(entities.TryGetActorRollbackCommands(out var commands));
        _bootstrapper.Tick(1f / 30f);
        Assert.True(actors.TryGet(1, out var actor));
        Get<MobaBrainService>().DeactivateBrain(actor);
        Assert.True(new MobaActorSpawnRegistrar(actors, entities).Unregister(1, out var removed, publishDespawn: false));
        removed.Destroy();

        /*
         * 使用真实世界的完整快照，独立核对每层阻断；没有放宽或替换正式 Provider。
         * 原有效销毁日志仍存在，失败来自未支持的聚合恢复，不是缺少逆命令。
         */
        var currentFrame = new FrameIndex(_bootstrapper.Context.LastFrame);
        var beforeCommands = commands.StateProvider.Export(currentFrame);
        var commandCount = commands.Log.Count;
        var context = new RollbackSnapshotPreflightContext(snapshot.Frame, snapshot.Entries,
            registry.Providers.ToDictionary(p => p.Key));
        Assert.True(registry.TryGet(MobaSkillLifecycleRollbackProvider.DefaultKey, out var lifecycle));
        var lifecycleFailure = Assert.Throws<InvalidOperationException>(() =>
            ((IRollbackSnapshotPreflightProvider)lifecycle).ValidateSnapshot(context));
        Assert.Contains("missing target actor is not a supported flying projectile or ProjectileLauncher", lifecycleFailure.Message);
        Assert.True(registry.TryGet(MobaMotionRuntimeDependencyRollbackProvider.DefaultKey, out var dependencies));
        var dependencyFailure = Assert.Throws<InvalidOperationException>(() =>
            ((IRollbackSnapshotPreflightProvider)dependencies).ValidateSnapshot(context));
        Assert.Contains("surviving same-generation Motion owner", dependencyFailure.Message);
        Assert.True(actors.TryGet(2, out var second));
        var secondPipeline = second.motion.Pipeline;
        var secondState = second.motion.State;

        Assert.False(coordinator.TryRestore(snapshot, out var result));
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(commandCount, commands.Log.Count);
        Assert.Equal(beforeCommands, commands.StateProvider.Export(currentFrame));
        Assert.False(actors.Contains(1));
        Assert.False(entities.TryGetActorEntity(1, out _));
        Assert.Same(secondPipeline, second.motion.Pipeline);
        Assert.Equal(secondState, second.motion.State);
        Assert.Equal(currentFrame.Value, _bootstrapper.Context.LastFrame);
    }
}
