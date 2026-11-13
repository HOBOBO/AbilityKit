using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Projectile.Tests;

public sealed class AreaBoundarySweepTests
{
    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, 1)]
    [InlineData(true, -1)]
    [InlineData(true, 1)]
    public void Complete_crossing_emits_direction_and_replays_from_saved_previous_pose(bool grid, int direction)
    {
        ICollisionWorld collision = grid ? new GridCollisionWorld() : new NaiveCollisionWorld();
        var world = new AreaWorld(collision);
        var collider = Add(collision, new Vec3(-3 * direction, 0, 0));
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(1, center, 4, 10, -1, 0,
            OverlapShapeKind.Rectangle, Vec3.Forward, width: 1, length: 4,
            anchorAtOrigin: false, rootContextId: 100, groupKey: 7,
            claimTargetsWithinGroup: true, boundarySweep: true);
        var area = world.Spawn(parameters, 0, new());
        Assert.Empty(Tick(world, 0));
        var before = world.ExportRollback(0);
        Move(collision, collider, new Vec3(3 * direction, 0, 0));
        var crossing = Assert.Single(Tick(world, 1));
        Assert.Equal(direction, crossing.CrossingDirection);
        Assert.True(world.TryAcceptEnter(area, 99));
        var after = world.ExportRollback(1);

        world.ImportRollback(before);
        crossing = Assert.Single(Tick(world, 1));
        Assert.Equal(direction, crossing.CrossingDirection);
        Assert.True(world.TryAcceptEnter(area, 99));
        Assert.Equal(after, world.ExportRollback(1));
        world.ImportRollback(after);
        Move(collision, collider, new Vec3(-3 * direction, 0, 0));
        Assert.Equal(-direction, Assert.Single(Tick(world, 2)).CrossingDirection);
        Assert.False(world.TryAcceptEnter(area, 99));
    }

    [Fact]
    public void Delayed_boundary_does_not_replay_unarmed_motion_and_overlap_is_not_duplicated()
    {
        var collision = new NaiveCollisionWorld();
        var collider = Add(collision, new Vec3(-3, 0, 0));
        var world = new AreaWorld(collision);
        world.Spawn(new AreaSpawnParams(1, Vec3.Zero, 4, 10, -1, 0,
            OverlapShapeKind.Rectangle, Vec3.Forward, width: 1, length: 4,
            anchorAtOrigin: false, armDelayFrames: 2, boundarySweep: true), 0, new());
        Assert.Empty(Tick(world, 0));
        Move(collision, collider, new Vec3(3, 0, 0));
        Assert.Empty(Tick(world, 1));
        Move(collision, collider, new Vec3(-3, 0, 0));
        Assert.Empty(Tick(world, 2));
        Move(collision, collider, Vec3.Zero);
        Assert.Equal(0, Assert.Single(Tick(world, 3)).CrossingDirection);
        Move(collision, collider, new Vec3(3, 0, 0));
        Assert.Empty(Tick(world, 4));
        Move(collision, collider, new Vec3(-3, 0, 0));
        Assert.Equal(-1, Assert.Single(Tick(world, 5)).CrossingDirection);
    }

    [Fact]
    public void End_cap_miss_removed_collider_and_disabled_sweep_do_not_emit_crossings()
    {
        var collision = new GridCollisionWorld();
        var miss = Add(collision, new Vec3(-3, 0, 4));
        var removed = Add(collision, new Vec3(-3, 0, 0));
        var world = new AreaWorld(collision);
        world.Spawn(new AreaSpawnParams(1, Vec3.Zero, 4, 4, -1, 0,
            OverlapShapeKind.Rectangle, Vec3.Forward, width: 1, length: 4,
            anchorAtOrigin: false, boundarySweep: true), 0, new());
        Assert.Empty(Tick(world, 0));
        Move(collision, miss, new Vec3(3, 0, 4));
        collision.Remove(removed);
        Assert.Empty(Tick(world, 1));
        var fresh = Add(collision, new Vec3(3, 0, 0));
        Assert.Empty(Tick(world, 2));
        Assert.Empty(Tick(world, 3));
        Move(collision, fresh, new Vec3(-3, 0, 0));
        Assert.Empty(Tick(world, 4)); // expired before query
        Assert.Equal(0, world.ActiveCount);

        world.Spawn(new AreaSpawnParams(1, Vec3.Zero, 4, 10, -1, 0,
            OverlapShapeKind.Rectangle, Vec3.Forward, width: 1, length: 4,
            anchorAtOrigin: false), 5, new());
        Assert.Empty(Tick(world, 5));
        Move(collision, fresh, new Vec3(3, 0, 0));
        Assert.Empty(Tick(world, 6));
    }

    [Fact]
    public void Multiple_edges_and_colliders_share_one_target_claim()
    {
        var collision = new GridCollisionWorld();
        var first = Add(collision, new Vec3(-3, 0, 0));
        var second = Add(collision, new Vec3(-3, 0, 0.2f));
        var world = new AreaWorld(collision);
        var p = new AreaSpawnParams(1, Vec3.Zero, 4, 10, -1, 0,
            OverlapShapeKind.Rectangle, Vec3.Forward, width: 1, length: 4,
            anchorAtOrigin: false, rootContextId: 555, groupKey: 8,
            claimTargetsWithinGroup: true, boundarySweep: true);
        world.Spawn(p, 0, new());
        world.Spawn(p, 0, new());
        Tick(world, 0);
        Move(collision, first, new Vec3(3, 0, 0));
        Move(collision, second, new Vec3(3, 0, 0.2f));
        var events = Tick(world, 1);
        Assert.Equal(4, events.Count);
        Assert.Equal(1, events.Count(e => world.TryAcceptEnter(e.Area, 77)));
    }

    private static ColliderId Add(ICollisionWorld world, Vec3 position) => world.Add(
        new Transform3(position, Quat.Identity, Vec3.One), ColliderShape.CreateSphere(Vec3.Zero, 0.25f), 0);
    private static void Move(ICollisionWorld world, ColliderId id, Vec3 position) =>
        Assert.True(world.UpdateTransform(id, new Transform3(position, Quat.Identity, Vec3.One)));
    private static List<AreaEnterEvent> Tick(AreaWorld world, int frame)
    {
        var events = new List<AreaEnterEvent>();
        world.Tick(frame, events, new(), new(), new());
        return events;
    }
}
