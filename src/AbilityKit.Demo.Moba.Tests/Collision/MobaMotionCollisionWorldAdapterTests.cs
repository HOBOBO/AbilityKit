using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services.Motion;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Collision;

public sealed class MobaMotionCollisionWorldAdapterTests
{
    [Fact]
    public void Sweep_stops_before_a_thin_world_wall_at_high_speed()
    {
        var world = new NaiveCollisionWorld();
        var wall = AddBox(
            world,
            new Vec3(5f, 0f, 0f),
            new Vec3(0.1f, 2f, 2f),
            MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var start = Vec3.Zero;
        var delta = new Vec3(20f, 0f, 0f);

        var collided = adapter.Sweep(
            moverId: 0,
            in start,
            in delta,
            radius: 0.5f,
            obstacleMask: MobaCollisionLayers.WorldMask,
            ignoreMask: 0,
            out var hit,
            out var appliedDelta);

        Assert.True(collided);
        Assert.Equal(wall.Value, hit.TargetId);
        Assert.InRange(appliedDelta.X, 4.39f, 4.41f);
        Assert.Equal(0f, appliedDelta.Y);
        Assert.Equal(0f, appliedDelta.Z);
        Assert.InRange(hit.Time01, 0.219f, 0.221f);
    }

    [Fact]
    public void Sweep_only_blocks_layers_in_the_obstacle_mask()
    {
        var world = new NaiveCollisionWorld();
        AddBox(
            world,
            new Vec3(3f, 0f, 0f),
            new Vec3(0.1f, 2f, 2f),
            MobaCollisionLayers.UnitId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);
        var start = Vec3.Zero;
        var delta = new Vec3(10f, 0f, 0f);

        var collided = adapter.Sweep(
            moverId: 0,
            in start,
            in delta,
            radius: 0.5f,
            obstacleMask: MobaCollisionLayers.WorldMask,
            ignoreMask: 0,
            out var hit,
            out var appliedDelta);

        Assert.False(collided);
        Assert.False(hit.Hit);
        Assert.Equal(delta, appliedDelta);
    }

    [Fact]
    public void Oriented_box_sweep_respects_ignored_colliders()
    {
        var world = new NaiveCollisionWorld();
        var ignored = AddBox(
            world,
            new Vec3(3f, 0f, 0f),
            new Vec3(0.1f, 2f, 2f),
            MobaCollisionLayers.WorldId);
        var expected = AddBox(
            world,
            new Vec3(6f, 0f, 0f),
            new Vec3(0.1f, 2f, 2f),
            MobaCollisionLayers.WorldId);
        var box = new OrientedBoxSweep(
            Vec3.Zero,
            new Vec3(1f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            new Vec3(0f, 0f, 1f),
            new Vec3(0.5f, 0.5f, 0.5f));
        var direction = new Vec3(1f, 0f, 0f);
        var filter = new LayerFilter(
            MobaCollisionLayers.WorldMask,
            new[] { ignored.Value });

        var collided = world.SweepOrientedBox(
            in box,
            in direction,
            maxDistance: 10f,
            in filter,
            out var hit);

        Assert.True(collided);
        Assert.Equal(expected, hit.Collider);
        Assert.InRange(hit.Distance, 5.39f, 5.41f);
    }

    [Fact]
    public void Sweep_blocks_rotated_obb_at_rotated_surface()
    {
        // 旋转 OBB：经适配器端到端扫掠，移动体(球)应在旋转后的面处停下，而非外接 AABB 处。
        var world = new NaiveCollisionWorld();
        var center = new Vec3(5f, 0f, 0f);
        var halfExtents = new Vec3(1.5f, 1f, 3f);
        const float yaw = 45f;
        var rot = Quat.FromAxisAngle(Vec3.Up, yaw * ((float)System.Math.PI / 180f));
        var transform = new Transform3(center, rot, Vec3.One);
        var shape = ColliderShape.CreateObb(Vec3.Zero, Quat.Identity, halfExtents);
        world.Add(in transform, in shape, MobaCollisionLayers.WorldId);
        var adapter = new MobaMotionCollisionWorldAdapter(world, null);

        // 沿 OBB（旋转后）+X 面法线逼近：起点在面外 10、半径 0.5。
        var refObb = new Obb(center, rot, halfExtents);
        refObb.GetAxes(out var right, out _, out _);
        var start = center + right * (halfExtents.X + 10f);
        var delta = -right * 20f;

        var collided = adapter.Sweep(0, in start, in delta, 0.5f, MobaCollisionLayers.WorldMask, 0, out var hit, out var appliedDelta);

        Assert.True(collided);
        // 球心停在“面 + 半径”处：applied 距离 = 10 - 0.5 = 9.5（旋转生效，非外接 AABB）。
        Assert.Equal(9.5f, appliedDelta.Magnitude, 2);
    }

    private static ColliderId AddBox(
        ICollisionWorld world,
        in Vec3 center,
        in Vec3 halfExtents,
        int layerId)
    {
        var transform = new Transform3(center, Quat.Identity, Vec3.One);
        var shape = ColliderShape.CreateAabb(-halfExtents, halfExtents);
        return world.Add(in transform, in shape, layerId);
    }
}
