using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using MemoryPack;
using Xunit;

namespace AbilityKit.Combat.Projectile.Tests;

public sealed class ProjectileRollbackPreflightTests
{
    [Fact]
    public void Missing_policy_rejects_direct_import_before_clearing_current_projectiles()
    {
        const int kind = 93571;
        var world = new ProjectileWorld(new NaiveCollisionWorld());
        byte[] checkpoint;
        try
        {
            ProjectileHitPolicyFactory.RegisterFactory(kind, _ => new TestPolicy());
            world.Spawn(Spawn((ProjectileHitPolicyKind)kind));
            checkpoint = world.ExportRollback(new FrameIndex(3));
        }
        finally
        {
            ProjectileHitPolicyFactory.UnregisterFactory(kind);
        }
        world.Clear();
        var current = world.Spawn(Spawn());
        var before = world.ExportRollback(new FrameIndex(8));

        Assert.Throws<InvalidOperationException>(() => world.ImportRollback(new FrameIndex(3), checkpoint));

        Assert.Equal(before, world.ExportRollback(new FrameIndex(8)));
        Assert.True(world.TryGetRuntimeState(current, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failing_filter_resolution_does_not_replace_the_live_world(bool installResolver)
    {
        var collision = new NaiveCollisionWorld();
        var source = new ProjectileWorld(collision);
        var filtered = Spawn(filter: new TestFilter());
        source.Spawn(filtered);
        var checkpoint = source.ExportRollback(new FrameIndex(3));
        var target = new ProjectileWorld(collision);
        if (installResolver) target.SetHitFilterResolver(new MissingFilterResolver());
        target.Spawn(Spawn());
        var before = target.ExportRollback(new FrameIndex(8));

        Assert.Throws<InvalidOperationException>(() => target.ImportRollback(new FrameIndex(3), checkpoint));

        Assert.Equal(before, target.ExportRollback(new FrameIndex(8)));
    }

    [Fact]
    public void Preflight_does_not_invoke_custom_factories()
    {
        const int kind = 93572;
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        var calls = 0;
        try
        {
            ProjectileHitPolicyFactory.RegisterFactory(kind, _ => { calls++; return new TestPolicy(); });
            service.Spawn(Spawn((ProjectileHitPolicyKind)kind));
            var frame = new FrameIndex(3);
            var payload = service.ExportRollback(frame);
            calls = 0;

            new ProjectileRollbackProvider(service).ValidateImport(frame, payload);

            Assert.Equal(0, calls);
            Assert.Equal(payload, service.ExportRollback(frame));
        }
        finally
        {
            ProjectileHitPolicyFactory.UnregisterFactory(kind);
        }
    }

    [Fact]
    public void Registered_factory_failure_leaves_current_objects_and_allocator_unchanged()
    {
        const int kind = 93573;
        var fail = false;
        try
        {
            ProjectileHitPolicyFactory.RegisterFactory(kind, _ => fail ? null! : new TestPolicy());
            var source = new ProjectileWorld(new NaiveCollisionWorld());
            source.Spawn(Spawn());
            source.Spawn(Spawn((ProjectileHitPolicyKind)kind));
            var frame = new FrameIndex(3);
            var checkpoint = source.ExportRollback(frame);
            var target = new ProjectileWorld(new NaiveCollisionWorld());
            target.Spawn(Spawn());
            var before = target.ExportRollback(new FrameIndex(8));
            fail = true;

            Assert.Throws<InvalidOperationException>(() => target.ImportRollback(frame, checkpoint));

            Assert.Equal(before, target.ExportRollback(new FrameIndex(8)));
            Assert.Equal(2, target.Spawn(Spawn()).Value);
        }
        finally
        {
            ProjectileHitPolicyFactory.UnregisterFactory(kind);
        }
    }

    [Fact]
    public void Dependency_preflight_does_not_invoke_filter_resolvers()
    {
        var source = new ProjectileWorld(new NaiveCollisionWorld());
        source.Spawn(Spawn(filter: new TestFilter()));
        var snapshot = ProjectileWorld.ReadRollbackSnapshot(source.ExportRollback(default));
        var target = new ProjectileWorld(new NaiveCollisionWorld());
        var resolver = new CountingFilterResolver();
        target.SetHitFilterResolver(resolver);

        target.ValidateRollbackDependencies(snapshot);

        Assert.Equal(0, resolver.Calls);
        Assert.Equal(0, target.ActiveCount);
    }

    [Fact]
    public void Restored_projectiles_share_one_resolved_instance_for_the_same_filter_key()
    {
        var collision = new NaiveCollisionWorld();
        var source = new ProjectileWorld(collision);
        var filter = new TestFilter();
        source.Spawn(Spawn(filter: filter));
        source.Spawn(Spawn(filter: filter));
        var frame = new FrameIndex(3);
        var checkpoint = source.ExportRollback(frame);
        var target = new ProjectileWorld(collision);
        var resolver = new CountingFilterResolver();
        target.SetHitFilterResolver(resolver);

        target.ImportRollback(frame, checkpoint);

        Assert.Equal(1, resolver.Calls);
        Assert.Equal(checkpoint, target.ExportRollback(frame));
    }

    [Fact]
    public void Valid_coordinator_restore_replays_physics_identically_without_historical_notifications()
    {
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        service.Spawn(Spawn());
        service.SpawnArea(new AreaSpawnParams(7, Vec3.Zero, 2, 4, -1, 0), 0);
        service.Tick(1, .25f);
        DrainNotifications(service);
        var registry = new RollbackRegistry();
        registry.Register(new ProjectileRollbackProvider(service));
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var checkpoint = coordinator.Capture(new FrameIndex(1));
        var expected = new List<byte[]>();
        for (var frame = 2; frame <= 5; frame++)
        {
            service.Tick(frame, .25f);
            DrainNotifications(service);
            expected.Add(service.ExportRollback(new FrameIndex(frame)));
        }

        for (var replay = 0; replay < 2; replay++)
        {
            coordinator.Restore(checkpoint);
            Assert.Equal(0, service.PendingNotificationCount);
            for (var frame = 2; frame <= 5; frame++)
            {
                service.Tick(frame, .25f);
                DrainNotifications(service);
                Assert.Equal(expected[frame - 2], service.ExportRollback(new FrameIndex(frame)));
            }
        }
    }

    [Fact]
    public void Schedules_run_and_cancel_without_installing_rollback_providers()
    {
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        var id = service.ScheduleEmit(new SingleShotPattern(), Spawn(), ProjectileScheduleParams.Repeat(1, 2, 3));
        var spawns = new List<ProjectileSpawnEvent>();
        service.Tick(0, 0);
        service.DrainSpawnEvents(spawns);
        Assert.Empty(spawns);
        service.Tick(1, 0);
        service.DrainSpawnEvents(spawns);
        service.Tick(2, 0);
        service.DrainSpawnEvents(spawns);
        Assert.Single(spawns);
        service.Tick(3, 0);
        service.DrainSpawnEvents(spawns);
        Assert.Equal(2, spawns.Count);
        Assert.True(service.CancelSchedule(id));
        service.Tick(5, 0);
        service.DrainSpawnEvents(spawns);
        Assert.Equal(2, spawns.Count);
        Assert.False(service.HasSchedule(id));
    }

    private static void DrainNotifications(ProjectileService service)
    {
        service.DrainSpawnEvents(new List<ProjectileSpawnEvent>());
        service.DrainHitEvents(new List<ProjectileHitEvent>());
        service.DrainArrivalEvents(new List<ProjectileArrivalEvent>());
        service.DrainExitEvents(new List<ProjectileExitEvent>());
        service.DrainTickEvents(new List<ProjectileTickEvent>());
        service.DrainAreaSpawnEvents(new List<AreaSpawnEvent>());
        service.DrainAreaEnterEvents(new List<AreaEnterEvent>());
        service.DrainAreaStayEvents(new List<AreaStayEvent>());
        service.DrainAreaExitEvents(new List<AreaExitEvent>());
        service.DrainAreaExpireEvents(new List<AreaExpireEvent>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_area_payload_does_not_clear_areas_or_partially_restore_projectiles(bool throughService)
    {
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        var frame = new FrameIndex(3);
        service.Spawn(Spawn());
        var areaParams = new AreaSpawnParams(7, Vec3.Zero, 2, 30, -1, 0);
        service.SpawnArea(areaParams, frame.Value);
        var checkpoint = MemoryPackSerializer.Deserialize<ProjectileServiceRollbackPayload>(service.ExportRollback(frame));
        var areas = MemoryPackSerializer.Deserialize<AreaWorldSnapshot>(checkpoint.Areas);
        var invalid = MemoryPackSerializer.Serialize(new AreaWorldSnapshot(99, frame.Value,
            areas.NextId, areas.Items, areas.NextGroupId, areas.Groups));

        if (throughService)
        {
            service.Spawn(Spawn());
            var before = service.ExportRollback(new FrameIndex(8));
            var notifications = service.PendingNotificationCount;
            var broken = MemoryPackSerializer.Serialize(new ProjectileServiceRollbackPayload(2, checkpoint.Projectiles, invalid));
            Assert.Throws<InvalidOperationException>(() => service.ImportRollback(frame, broken));
            Assert.Equal(before, service.ExportRollback(new FrameIndex(8)));
            Assert.Equal(notifications, service.PendingNotificationCount);
        }
        else
        {
            var world = new AreaWorld(new NaiveCollisionWorld());
            world.ImportRollback(checkpoint.Areas);
            var before = world.ExportRollback(8);
            Assert.Throws<InvalidOperationException>(() => world.ImportRollback(invalid));
            Assert.Equal(before, world.ExportRollback(8));
        }
    }

    [Theory]
    [InlineData("projectile-allocator")]
    [InlineData("area-allocator")]
    [InlineData("area-group")]
    [InlineData("frame")]
    [InlineData("projectile-items")]
    [InlineData("area-items")]
    public void Coordinator_rejects_invalid_physical_payload_before_any_provider_import(string corruption)
    {
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        service.Spawn(Spawn());
        var frame = new FrameIndex(3);
        var payload = MemoryPackSerializer.Deserialize<ProjectileServiceRollbackPayload>(service.ExportRollback(frame));
        var projectiles = MemoryPackSerializer.Deserialize<ProjectileWorldSnapshotPayload>(payload.Projectiles);
        var areas = MemoryPackSerializer.Deserialize<AreaWorldSnapshot>(payload.Areas);
        var badProjectiles = MemoryPackSerializer.Serialize(new ProjectileWorldSnapshotPayload(18,
            corruption == "frame" ? new FrameIndex(2) : frame,
            corruption == "projectile-allocator" ? 1 : projectiles.NextId,
            corruption == "projectile-items" ? null! : projectiles.Items, projectiles.NextHitFilterKey));
        var groups = corruption == "area-group"
            ? new[] { new AreaGroupSnapshotItem(1, 100, 7, new[] { 42 }), new AreaGroupSnapshotItem(1, 100, 7, new[] { 43 }) }
            : areas.Groups;
        var badAreas = MemoryPackSerializer.Serialize(new AreaWorldSnapshot(areas.Version, frame.Value,
            corruption == "area-allocator" ? 0 : areas.NextId,
            corruption == "area-items" ? null! : areas.Items,
            corruption == "area-group" ? 2 : areas.NextGroupId, groups));
        var broken = MemoryPackSerializer.Serialize(new ProjectileServiceRollbackPayload(2, badProjectiles, badAreas));
        var sentinel = new ImportCounter();
        var registry = new RollbackRegistry();
        registry.Register(sentinel);
        registry.Register(new ProjectileRollbackProvider(service));
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var snapshot = new WorldRollbackSnapshot(WorldRollbackSnapshotCodec.CurrentVersion, frame,
            new[] { new WorldRollbackSnapshotEntry(sentinel.Key, Array.Empty<byte>()),
                new WorldRollbackSnapshotEntry(ProjectileRollbackProvider.DefaultKey, broken) });
        var before = service.ExportRollback(new FrameIndex(8));

        Assert.False(coordinator.TryRestore(snapshot, out _));

        Assert.Equal(0, sentinel.Imports);
        Assert.Equal(before, service.ExportRollback(new FrameIndex(8)));
    }

    private static ProjectileSpawnParams Spawn(ProjectileHitPolicyKind kind = ProjectileHitPolicyKind.ExitOnHit,
        IProjectileHitFilter? filter = null) =>
        new(1, 1, 1, 1, 0, Vec3.Zero, Vec3.Right, 1f, 0, 0f, 0f, 30, 30f, 1,
            ColliderId.Invalid, hitPolicyKind: kind, hitFilter: filter!);

    private sealed class TestPolicy : IProjectileHitPolicy
    {
        public bool ShouldExitOnHit(in ProjectileHitEvent hit, ref int hitsRemaining) => false;
    }

    private sealed class TestFilter : IProjectileHitFilter
    {
        public bool ShouldHit(int ownerId, ColliderId collider, int frame) => true;
    }

    private sealed class MissingFilterResolver : IProjectileHitFilterResolver
    {
        public IProjectileHitFilter Resolve(int key) => null!;
    }

    private sealed class CountingFilterResolver : IProjectileHitFilterResolver
    {
        public int Calls { get; private set; }
        public IProjectileHitFilter Resolve(int key) { Calls++; return new TestFilter(); }
    }

    private sealed class ImportCounter : IRollbackStateProvider
    {
        public int Key => 1;
        public int Imports { get; private set; }
        public byte[] Export(FrameIndex frame) => Array.Empty<byte>();
        public void Import(FrameIndex frame, byte[] payload) => Imports++;
    }
}
