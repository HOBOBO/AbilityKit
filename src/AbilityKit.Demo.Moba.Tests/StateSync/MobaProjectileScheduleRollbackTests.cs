using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Projectile.Launch;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaProjectileScheduleRollbackTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Dynamic_emits_restore_order_sampling_filters_and_shared_random_without_history(int checkpointFrame)
    {
        var configs = Configs();
        var random = new RollbackWorldRandom();
        random.SetSeed(291);
        using var modifiers = new MobaSkillParamModifierService();
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        using var actors = new MobaActorRegistry();
        var resolver = new MobaProjectileSchedulePatternResolver(configs, random, modifiers);
        service.OnInit(new Resolver(resolver, new MobaProjectileCollisionFilterResolver(actors)));
        Assert.True(configs.TryGetProjectile(101, out var config));
        var sharedFilter = new MobaProjectileCollisionFilter(actors);
        ProjectileScheduleId Schedule(int launcher, int root, int count) => service.ScheduleEmit(
            new MobaModifierProjectileSpawnPatternProvider(modifiers, config, 201, random,
                count, 20, 2, 2002, 20, 2001), Spawn(launcher, root, sharedFilter),
            ProjectileScheduleParams.Repeat(1, 2, 3));
        var removed = Schedule(11, 10, 1);
        var second = Schedule(12, 10, 1);
        var third = Schedule(13, 20, 2);
        Assert.True(service.CancelSchedule(removed));
        for (var f = 1; f <= checkpointFrame; f++) Tick(service, f);
        var registry = new RollbackRegistry();
        registry.Register(random);
        registry.Register(new MobaSkillParamModifierRollbackProvider(modifiers, restoreProjection: true));
        registry.Register(new ProjectileRollbackProvider(service));
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var checkpoint = coordinator.Capture(new(checkpointFrame));
        var draws = random.DrawCount;
        var expected = new List<byte[]>();
        var expectedEvents = new List<(int, int, int, Vec3, Vec3)[]>();
        for (var f = checkpointFrame + 1; f <= 7; f++)
        {
            Inputs(f, modifiers);
            var events = Tick(service, f);
            expectedEvents.Add(events);
            if (f == 3)
            {
                Assert.Equal(new[] { 13, 13, 12, 12, 12 }, events.Select(e => e.Item2));
                Assert.Equal(5, events.Length);
            }
            var snapshot = coordinator.Capture(new(f));
            expected.Add(WorldRollbackSnapshotCodec.Serialize(in snapshot));
        }
        for (var replay = 0; replay < 2; replay++)
        {
            coordinator.Restore(checkpoint);
            Assert.Equal(draws, random.DrawCount);
            Assert.Equal(0, service.PendingNotificationCount);
            Assert.True(service.HasSchedule(second));
            Assert.True(service.HasSchedule(third));
            var restored = coordinator.Capture(new(checkpointFrame));
            Assert.Equal(WorldRollbackSnapshotCodec.Serialize(in checkpoint), WorldRollbackSnapshotCodec.Serialize(in restored));
            Assert.Equal(draws, random.DrawCount);
            for (var f = checkpointFrame + 1; f <= 7; f++)
            {
                Inputs(f, modifiers);
                Assert.Equal(expectedEvents[f - checkpointFrame - 1], Tick(service, f));
                var snapshot = coordinator.Capture(new(f));
                Assert.Equal(expected[f - checkpointFrame - 1], WorldRollbackSnapshotCodec.Serialize(in snapshot));
            }
        }
    }

    [Theory]
    [InlineData("random")]
    [InlineData("modifiers")]
    [InlineData("config")]
    [InlineData("changed-config")]
    public void Missing_dynamic_dependencies_reject_before_any_import(string missing)
    {
        var configs = Configs();
        var random = new RollbackWorldRandom();
        using var modifiers = new MobaSkillParamModifierService();
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        service.OnInit(new Resolver(new MobaProjectileSchedulePatternResolver(configs, random, modifiers)));
        Assert.True(configs.TryGetProjectile(101, out var config));
        service.ScheduleEmit(new MobaModifierProjectileSpawnPatternProvider(modifiers, config, 201, random, 1, 0),
            Spawn(12, 10), ProjectileScheduleParams.Once(2));
        var frame = new FrameIndex(1);
        var registry = new RollbackRegistry();
        var counter = new ImportCounter();
        registry.Register(counter);
        registry.Register(new ProjectileRollbackProvider(service));
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var checkpoint = coordinator.Capture(frame);
        service.OnInit(new Resolver(new MobaProjectileSchedulePatternResolver(
            missing == "config" ? new MobaConfigDatabase() : missing == "changed-config" ? Configs(20) : configs,
            missing == "random" ? null! : random, missing == "modifiers" ? null! : modifiers)));
        Assert.False(coordinator.TryRestore(checkpoint, out _));
        Assert.Equal(0, counter.Imports);
        Assert.Equal(0UL, random.DrawCount);
        Assert.Single(Tick(service, 2));
    }

    private static MobaConfigDatabase Configs(float speed = 10)
    {
        var configs = new MobaConfigDatabase();
        var loaded = configs.ReloadFromDtoArrays(new Dictionary<Type, Array> {
            [typeof(ProjectileDTO)] = new[] { new ProjectileDTO {
                Id = 101, Speed = speed, LifetimeMs = 600, SpawnRandomOffsetX = 1,
                SpawnRandomOffsetY = 2, SpawnRandomOffsetZ = 3
            } }
        }, strict: false);
        Assert.True(loaded.Succeeded, loaded.Error);
        return configs;
    }

    private static ProjectileSpawnParams Spawn(int launcher, int root, IProjectileHitFilter? filter = null) =>
        new(1, 101, launcher, root, 0, Vec3.Zero, Vec3.Forward, 10, 0, 0, 0, 30, 100, 0,
            ColliderId.Invalid, hitPolicyKind: ProjectileHitPolicyKind.Pierce, hitPolicyParam: 2, hitFilter: filter!);

    private static void Inputs(int frame, MobaSkillParamModifierService modifiers)
    {
        if (frame == 5) modifiers.ClearActor(10);
        if (frame != 3) return;
        void Add(ModifierKey key, float value, int source)
        {
            var modifier = new ModifierData { Key = key, Op = ModifierOp.Add,
                Magnitude = MagnitudeSource.Fixed(value), SourceId = source };
            modifiers.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier,
                new MobaParameterSelector(slot: 2, baseSkillId: 2001, effectiveSkillId: 2002,
                    launcherTemplateId: 201, projectileTemplateId: 101));
        }
        Add(MobaSkillParamModifierKeys.Emitter.CountPerShot, 2, 71);
        Add(MobaSkillParamModifierKeys.Emitter.FanAngleDeg, 10, 72);
        Add(MobaSkillParamModifierKeys.Projectile.Speed, 5, 73);
        Add(MobaSkillParamModifierKeys.Projectile.LifetimeMs, 400, 74);
        Add(MobaSkillParamModifierKeys.Projectile.PierceHits, 2, 75);
    }

    private static (int, int, int, Vec3, Vec3)[] Tick(ProjectileService service, int frame)
    {
        service.Tick(frame, .02f);
        var events = new List<ProjectileSpawnEvent>();
        service.DrainSpawnEvents(events);
        service.DrainTickEvents(new()); service.DrainExitEvents(new());
        service.DrainHitEvents(new()); service.DrainArrivalEvents(new());
        return events.Select(e => (e.Projectile.Value, e.LauncherActorId, e.Frame, e.Position, e.Direction)).ToArray();
    }

    private sealed class Resolver(params object[] values) : IWorldResolver
    {
        public object Resolve(Type type) => values.First(v => type.IsInstanceOfType(v));
        public T Resolve<T>() => (T)Resolve(typeof(T));
        public bool TryResolve(Type type, out object instance)
        { instance = values.FirstOrDefault(type.IsInstanceOfType)!; return instance != null; }
        public bool TryResolve<T>(out T instance)
        { var found = TryResolve(typeof(T), out var value); instance = found ? (T)value : default!; return found; }
    }

    private sealed class ImportCounter : IRollbackStateProvider
    {
        public int Key => 1;
        public int Imports;
        public byte[] Export(FrameIndex frame) => Array.Empty<byte>();
        public void Import(FrameIndex frame, byte[] payload) => Imports++;
    }
}
