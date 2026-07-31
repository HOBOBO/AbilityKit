using System.Collections.Generic;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Collision;

/// <summary>
/// GridCollisionWorld 与 NaiveCollisionWorld 的等价性验证：同一组 AABB/Sphere/OBB 障碍下，
/// Raycast/OverlapSphere/SweepOrientedBox/ShouldIgnore 结果一致。
/// 注意：ToBoxLocalBounds（共享窄相）仅处理 Sphere/Capsule/Aabb，不处理 OBB 形状——
/// 故用 AABB 障碍做等价比对（OBB sweep 是 naive/grid 共有的既有缺陷，与本次 grid 接线无关）。
/// </summary>
public sealed class GridCollisionWorldTests
{
    private const int WorldLayer = 2;
    private const int WorldMask = 1 << WorldLayer;

    [Fact]
    public void Raycast_is_equivalent_to_naive()
    {
        Build(out var naive, out var grid, out var wall);

        var ray = new Ray3(new Vec3(0f, 0f, 0f), new Vec3(1f, 0f, 0f));
        Assert.True(naive.Raycast(ray, 10f, new LayerFilter(WorldMask), out var nHit));
        Assert.True(grid.Raycast(ray, 10f, new LayerFilter(WorldMask), out var gHit));

        Assert.Equal(nHit.Collider, gHit.Collider);
        Assert.Equal(wall, gHit.Collider);
        Assert.InRange(gHit.Distance, 4.49f, 4.51f);
        Assert.Equal(-1f, gHit.Normal.X, 1);
    }

    [Fact]
    public void OverlapSphere_is_equivalent_to_naive()
    {
        Build(out var naive, out var grid, out _);

        var sphere = new Sphere(new Vec3(5f, 0f, 0f), 0.5f);
        var nResults = new List<ColliderId>();
        var gResults = new List<ColliderId>();
        var nCount = naive.OverlapSphere(in sphere, new LayerFilter(WorldMask), nResults);
        var gCount = grid.OverlapSphere(in sphere, new LayerFilter(WorldMask), gResults);

        Assert.Equal(nCount, gCount);
        Assert.Equal(nResults, gResults);
        Assert.True(gCount >= 1);
    }

    [Fact]
    public void SweepOrientedBox_is_equivalent_to_naive()
    {
        Build(out var naive, out var grid, out var wall);

        var box = new OrientedBoxSweep(
            new Vec3(0f, 0f, 0f),
            new Vec3(1f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            new Vec3(0f, 0f, 1f),
            new Vec3(0.5f, 0.5f, 0.5f));
        var dir = new Vec3(1f, 0f, 0f);
        var filter = new LayerFilter(WorldMask);

        Assert.True(((IOrientedBoxSweepCollisionWorld)naive).SweepOrientedBox(in box, in dir, 10f, in filter, out var nHit));
        Assert.True(((IOrientedBoxSweepCollisionWorld)grid).SweepOrientedBox(in box, in dir, 10f, in filter, out var gHit));

        Assert.Equal(nHit.Collider, gHit.Collider);
        Assert.Equal(wall, gHit.Collider);
        Assert.InRange(gHit.Distance, 3.99f, 4.01f);
        Assert.Equal(-1f, gHit.Normal.X, 1);
    }

    [Fact]
    public void ShouldIgnore_excludes_collider_on_both_backends()
    {
        Build(out var naive, out var grid, out var wall);

        var ray = new Ray3(new Vec3(0f, 0f, 0f), new Vec3(1f, 0f, 0f));
        var filter = new LayerFilter(WorldMask, new[] { wall.Value });

        Assert.False(naive.Raycast(ray, 10f, in filter, out _));
        Assert.False(grid.Raycast(ray, 10f, in filter, out _));
    }

    [Fact]
    public void OverlapSphere_finds_rotated_obb_obstacle_on_grid()
    {
        // 旋转 OBB 障碍：修复前 Grid 的 ToWorldAabb 把 OBB 退化成原点零尺寸 AABB，
        // broadphase 查不到 → 对所有查询“隐形”。验证修复后能被 OverlapSphere 检测到。
        var grid = new GridCollisionWorld(cellSize: 2f, initialCapacity: 16);
        AddObb(grid, new Vec3(5f, 0f, 5f), yawDegrees: 45f, new Vec3(1f, 2f, 0.5f));

        var results = new List<ColliderId>();
        var count = grid.OverlapSphere(new Sphere(new Vec3(5f, 0f, 5f), 0.5f), new LayerFilter(WorldMask), results);

        Assert.True(count >= 1);
    }

    [Fact]
    public void Rotated_obb_sweep_is_equivalent_between_grid_and_naive()
    {
        // 同一旋转 OBB 障碍下 Grid 与 Naive 的扫掠应一致：修复前 Grid 查不到 OBB（false）而
        // Naive 能查到（true），二者不一致即暴露 broadphase 缺陷。
        var naive = new NaiveCollisionWorld();
        var grid = new GridCollisionWorld(cellSize: 2f, initialCapacity: 16);
        AddObb(naive, new Vec3(5f, 0f, 5f), 45f, new Vec3(1f, 2f, 0.5f));
        AddObb(grid, new Vec3(5f, 0f, 5f), 45f, new Vec3(1f, 2f, 0.5f));

        var box = new OrientedBoxSweep(
            new Vec3(1f, 0f, 5f),
            new Vec3(1f, 0f, 0f),
            new Vec3(0f, 1f, 0f),
            new Vec3(0f, 0f, 1f),
            new Vec3(0.5f, 0.5f, 0.5f));
        var dir = new Vec3(1f, 0f, 0f);
        var filter = new LayerFilter(WorldMask);

        var nOk = ((IOrientedBoxSweepCollisionWorld)naive).SweepOrientedBox(in box, in dir, 10f, in filter, out var nHit);
        var gOk = ((IOrientedBoxSweepCollisionWorld)grid).SweepOrientedBox(in box, in dir, 10f, in filter, out var gHit);

        Assert.Equal(nOk, gOk);
        Assert.True(gOk);
        Assert.Equal(nHit.Distance, gHit.Distance, 3);
    }

    [Fact]
    public void Sphere_sweep_blocks_rotated_obb_along_its_face_normal()
    {
        // 旋转 OBB：沿其（旋转后的）+X 面法线逼近，球心应在“面 + 半径”处精确停下。
        // 这是“旋转生效”的直接证据——若把 OBB 当外接 AABB 处理，会得到不同的错误距离。
        var grid = new GridCollisionWorld(cellSize: 2f, initialCapacity: 16);
        var center = new Vec3(5f, 0f, 5f);
        var halfExtents = new Vec3(1.5f, 1f, 3f);
        const float yaw = 45f;
        AddObb(grid, center, yaw, halfExtents);

        // 用与碰撞世界相同的旋转构造参考 OBB，取其 +X 面法线(Right)。
        var rot = Quat.FromAxisAngle(Vec3.Up, yaw * ((float)System.Math.PI / 180f));
        var refObb = new Obb(center, rot, halfExtents);
        refObb.GetAxes(out var right, out _, out _);

        const float radius = 0.5f;
        const float standOff = 10f;
        var start = center + right * (halfExtents.X + standOff);   // +X 面外 standOff 处
        var dir = -right;                                           // 沿面法线逼近
        var filter = new LayerFilter(WorldMask);

        var ok = ((ISphereSweepCollisionWorld)grid).SweepSphere(start, dir, standOff * 2f, radius, filter, out var hit);

        Assert.True(ok);
        // 球心停在“面 + 半径”处 → 命中距离 = standOff - radius（精确，旋转生效）。
        Assert.Equal(standOff - radius, hit.Distance, 2);
    }

    [Fact]
    public void Sphere_sweep_rotated_obb_is_equivalent_between_grid_and_naive()
    {
        var naive = new NaiveCollisionWorld();
        var grid = new GridCollisionWorld(cellSize: 2f, initialCapacity: 16);
        AddObb(naive, new Vec3(5f, 0f, 5f), 45f, new Vec3(1.5f, 1f, 3f));
        AddObb(grid, new Vec3(5f, 0f, 5f), 45f, new Vec3(1.5f, 1f, 3f));

        var dir = new Vec3(1f, 0f, 0f);
        var filter = new LayerFilter(WorldMask);
        var start = new Vec3(-5f, 0f, 5f);

        var nOk = ((ISphereSweepCollisionWorld)naive).SweepSphere(start, dir, 30f, 0.5f, filter, out var nHit);
        var gOk = ((ISphereSweepCollisionWorld)grid).SweepSphere(start, dir, 30f, 0.5f, filter, out var gHit);

        Assert.Equal(nOk, gOk);
        Assert.True(gOk);
        Assert.Equal(nHit.Distance, gHit.Distance, 3);
    }

    private static void Build(out ICollisionWorld naive, out ICollisionWorld grid, out ColliderId wall)
    {
        naive = new NaiveCollisionWorld();
        grid = new GridCollisionWorld(cellSize: 2f, initialCapacity: 16);

        // 同序添加，保证两端 ColliderId 一致。
        wall = AddBox(naive, new Vec3(5f, 0f, 0f), new Vec3(0.5f, 2f, 2f));
        AddBox(grid, new Vec3(5f, 0f, 0f), new Vec3(0.5f, 2f, 2f));

        AddSphere(naive, new Vec3(-5f, 0f, 0f), 1f);
        AddSphere(grid, new Vec3(-5f, 0f, 0f), 1f);
    }

    private static ColliderId AddBox(ICollisionWorld world, in Vec3 center, in Vec3 halfExtents)
    {
        var transform = new Transform3(center, Quat.Identity, Vec3.One);
        var shape = ColliderShape.CreateAabb(-halfExtents, halfExtents);
        return world.Add(in transform, in shape, WorldLayer);
    }

    private static ColliderId AddSphere(ICollisionWorld world, in Vec3 center, float radius)
    {
        var transform = new Transform3(center, Quat.Identity, Vec3.One);
        var shape = ColliderShape.CreateSphere(Vec3.Zero, radius);
        return world.Add(in transform, in shape, WorldLayer);
    }

    private static ColliderId AddObb(ICollisionWorld world, in Vec3 center, float yawDegrees, in Vec3 halfExtents)
    {
        var rotation = Quat.FromAxisAngle(Vec3.Up, yawDegrees * ((float)System.Math.PI / 180f));
        var transform = new Transform3(center, rotation, Vec3.One);
        var shape = ColliderShape.CreateObb(Vec3.Zero, Quat.Identity, halfExtents);
        return world.Add(in transform, in shape, WorldLayer);
    }
}
