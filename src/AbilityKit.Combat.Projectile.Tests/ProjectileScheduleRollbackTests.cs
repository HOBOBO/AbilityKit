using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using Xunit;
using MemoryPack;

namespace AbilityKit.Combat.Projectile.Tests;

public sealed class ProjectileScheduleRollbackTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void Pending_emission_cursor_replays_twice_without_historical_spawns(int checkpointFrame)
    {
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        var id = service.ScheduleEmit(new FanPattern(3, 25), Spawn(), ProjectileScheduleParams.Repeat(1, 2, 3));
        for (var f = 1; f <= checkpointFrame; f++) Tick(service, f);
        var checkpoint = service.ExportRollback(new(checkpointFrame));
        var expected = new List<byte[]>();
        var expectedSpawns = new List<int>();
        for (var f = checkpointFrame + 1; f <= 7; f++)
        {
            expectedSpawns.Add(Tick(service, f));
            expected.Add(service.ExportRollback(new(f)));
        }
        for (var replay = 0; replay < 2; replay++)
        {
            service.ImportRollback(new(checkpointFrame), checkpoint);
            Assert.Equal(checkpointFrame < 5, service.HasSchedule(id));
            Assert.Equal(0, service.PendingNotificationCount);
            Assert.Equal(checkpoint, service.ExportRollback(new(checkpointFrame)));
            for (var f = checkpointFrame + 1; f <= 7; f++)
            {
                Assert.Equal(expectedSpawns[f - checkpointFrame - 1], Tick(service, f));
                Assert.Equal(expected[f - checkpointFrame - 1], service.ExportRollback(new(f)));
            }
        }
    }

    [Fact]
    public void Restore_removes_future_tasks_restores_allocator_and_preserves_cancellation()
    {
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        var retained = service.ScheduleEmit(new SingleShotPattern(), Spawn(), ProjectileScheduleParams.Infinite(2, 0));
        var cancelled = service.ScheduleEmit(new BurstPattern(2), Spawn(), ProjectileScheduleParams.Infinite(2, 1));
        Assert.True(service.CancelSchedule(cancelled));
        var checkpoint = service.ExportRollback(new(1));
        var future = service.ScheduleEmit(new SingleShotPattern(), Spawn(), ProjectileScheduleParams.Once(2));
        Assert.True(service.CancelSchedule(retained));
        service.ImportRollback(new(1), checkpoint);
        Assert.True(service.HasSchedule(retained));
        Assert.False(service.HasSchedule(cancelled));
        Assert.False(service.HasSchedule(future));
        Assert.Equal(future, service.ScheduleEmit(new SingleShotPattern(), Spawn(), ProjectileScheduleParams.Once(2)));
        Assert.Equal(2, Tick(service, 2));
        Assert.Equal(1, Tick(service, 3));
        Assert.True(service.CancelSchedule(retained));
        Assert.Equal(0, Tick(service, 4));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Frozen_patterns_preserve_exact_spawn_values_and_replay_after_completion(int kind)
    {
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        IProjectileSpawnPattern pattern = kind switch {
            1 => new SingleShotPattern(), 2 => new BurstPattern(4),
            3 => new FanPattern(4, -35), _ => new ScatterPattern(4, 35, -25)
        };
        var spawn = new ProjectileSpawnParams(1, 2, 3, 4, 7, new Vec3(.1234567f, 3, 6),
            new Vec3(2, .5f, 3), 3.14159f, 17, 6.14159f, .01234567f, 30, 100, 0,
            ColliderId.Invalid, hitPolicyKind: ProjectileHitPolicyKind.Pierce, hitPolicyParam: 4,
            hitsRemaining: 5, tickIntervalFrames: 3, startSuspended: true, patternSlotIndex: 2,
            patternSlotCount: 4, trackingArcHeight: .456789f, trackingArcHeightPerUnit: .1234567f,
            trackingArcDurationSeconds: .3333333f, bounceSearchRadius: 20, collisionHalfExtents: new(.1f, .2f, .3f),
            hitCooldownFrames: 9, maxDistanceResponse: ProjectileMaxDistanceResponse.SuspendAtDestination);
        service.ScheduleEmit(pattern, spawn, ProjectileScheduleParams.Once(2));
        var checkpoint = service.ExportRollback(new(1));
        var count = Tick(service, 2);
        Assert.Equal(kind == 1 ? 1 : 4, count);
        var expected = service.ExportRollback(new(2));
        service.ImportRollback(new(1), checkpoint);
        Assert.Equal(checkpoint, service.ExportRollback(new(1)));
        Assert.Equal(count, Tick(service, 2));
        Assert.Equal(expected, service.ExportRollback(new(2)));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("allocator")]
    [InlineData("duplicate")]
    [InlineData("roster")]
    [InlineData("interval")]
    [InlineData("pattern-version")]
    [InlineData("provider-kind")]
    [InlineData("missing-resolver")]
    [InlineData("filter")]
    [InlineData("nonfinite")]
    public void Invalid_future_work_rejects_before_importing_any_state(string corruption)
    {
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        service.ScheduleEmit(new SingleShotPattern(), Spawn(), ProjectileScheduleParams.Infinite(2, 1));
        var frame = new FrameIndex(1);
        var p = MemoryPackSerializer.Deserialize<ProjectileServiceRollbackPayload>(service.ExportRollback(frame));
        var s = p.Schedules[0];
        switch (corruption)
        {
            case "version": p.Version = 1; break;
            case "allocator": p.NextScheduleId = s.Id; break;
            case "duplicate": p.Schedules = new[] { s, s }; break;
            case "roster": p.Schedules = null!; break;
            case "interval": s.IntervalFrames = -1; break;
            case "pattern-version": s.Pattern = new(s.Pattern.Kind, BitConverter.GetBytes(3)); break;
            case "provider-kind": s.IsProvider = true; break;
            case "missing-resolver": s.Pattern = new(1024, new byte[] { 1 }); break;
            case "filter": s.FilterIndex = 1; break;
            case "nonfinite": s.BaseSpawn.Speed = float.NaN; break;
        }
        if (p.Schedules?.Length == 1) p.Schedules[0] = s;
        var broken = MemoryPackSerializer.Serialize(p);
        var probe = new ImportCounter();
        var registry = new RollbackRegistry();
        registry.Register(probe);
        registry.Register(new ProjectileRollbackProvider(service));
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var invalid = new WorldRollbackSnapshot(WorldRollbackSnapshotCodec.CurrentVersion, frame,
            new[] { new WorldRollbackSnapshotEntry(probe.Key, Array.Empty<byte>()),
                new WorldRollbackSnapshotEntry(ProjectileRollbackProvider.DefaultKey, broken) });
        var before = service.ExportRollback(frame);
        Assert.False(coordinator.TryRestore(invalid, out _));
        Assert.Equal(0, probe.Imports);
        Assert.Equal(before, service.ExportRollback(frame));
        Assert.ThrowsAny<Exception>(() => service.ImportRollback(frame, broken));
        Assert.Equal(before, service.ExportRollback(frame));
    }

    [Fact]
    public void Unsupported_custom_pattern_runs_without_rollback_and_capture_explicitly_rejects_it()
    {
        using var collisions = new CollisionService();
        using var service = new ProjectileService(collisions);
        var id = service.ScheduleEmit(new CustomPattern(), Spawn(), ProjectileScheduleParams.Infinite(2, 1));
        Assert.Throws<InvalidOperationException>(() => service.ExportRollback(new(1)));
        Assert.Equal(1, Tick(service, 2));
        Assert.True(service.CancelSchedule(id));
        Assert.Equal(0, Tick(service, 3));
    }

    private sealed class CustomPattern : IProjectileSpawnPattern
    {
        public void Build(in ProjectileSpawnParams baseSpawn, List<ProjectileSpawnParams> results) => results.Add(baseSpawn);
    }

    private sealed class ImportCounter : IRollbackStateProvider
    {
        public int Key => 1;
        public int Imports;
        public byte[] Export(FrameIndex frame) => Array.Empty<byte>();
        public void Import(FrameIndex frame, byte[] payload) => Imports++;
    }

    internal static ProjectileSpawnParams Spawn() => new(1, 1, 2, 1, 0, Vec3.Zero, Vec3.Forward,
        1, 0, 0, 0, 30, 30, 0, ColliderId.Invalid);

    internal static int Tick(ProjectileService service, int frame)
    {
        service.Tick(frame, .02f);
        var spawns = new List<ProjectileSpawnEvent>();
        service.DrainSpawnEvents(spawns);
        service.DrainTickEvents(new());
        service.DrainExitEvents(new());
        service.DrainHitEvents(new());
        service.DrainArrivalEvents(new());
        return spawns.Count;
    }
}
