using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Collision;

/// <summary>
/// 三条碰撞收尾修复的验证：
/// G1/G2 — OBB 障碍的 SweepOrientedBox（ToBoxLocalBounds 加 OBB case + NaiveCollisionWorld.ToWorldShape 加 OBB case）。
/// G3   — GridBroadphase 多 cell 对象移动/移除后不残留 ID。
/// </summary>
public sealed class CollisionCorrectnessFixTests
{
    private const int WorldLayer = 2;
    private const int WorldMask = 1 << WorldLayer;

    [Fact]
    public void SweepOrientedBox_hits_axis_aligned_obb_wall_on_both_backends()
    {
        AddObbScene(out var naive, out var grid, out var wall, Quat.Identity);

        var box = new OrientedBoxSweep(
            new Vec3(0f, 0f, 0f),
            new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), new Vec3(0f, 0f, 1f),
            new Vec3(0.5f, 0.5f, 0.5f));
        var dir = new Vec3(1f, 0f, 0f);
        var filter = new LayerFilter(WorldMask);

        var naiveSweep = (IOrientedBoxSweepCollisionWorld)naive;
        var gridSweep = (IOrientedBoxSweepCollisionWorld)grid;

        Assert.True(naiveSweep.SweepOrientedBox(in box, in dir, 10f, in filter, out var nHit));
        Assert.True(gridSweep.SweepOrientedBox(in box, in dir, 10f, in filter, out var gHit));

        Assert.Equal(wall, nHit.Collider);
        Assert.Equal(wall, gHit.Collider);
        Assert.InRange(gHit.Distance, 3.99f, 4.01f);
        Assert.Equal(nHit.Distance, gHit.Distance, 2);
        Assert.Equal(-1f, gHit.Normal.X, 1);
    }

    [Fact]
    public void SweepOrientedBox_hits_rotated_obb_wall_equivalently()
    {
        // 45° Y 旋转的 OBB 立方：两端共享 OrientedBoxSweepQueries，结果应一致且命中合理。
        var rot = Quat.FromAxisAngle(Vec3.Up, 0.785398f); // ~45°
        AddObbScene(out var naive, out var grid, out var wall, rot);

        var box = new OrientedBoxSweep(
            new Vec3(0f, 0f, 0f),
            new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), new Vec3(0f, 0f, 1f),
            new Vec3(0.5f, 0.5f, 0.5f));
        var dir = new Vec3(1f, 0f, 0f);
        var filter = new LayerFilter(WorldMask);

        Assert.True(((IOrientedBoxSweepCollisionWorld)naive).SweepOrientedBox(in box, in dir, 10f, in filter, out var nHit));
        Assert.True(((IOrientedBoxSweepCollisionWorld)grid).SweepOrientedBox(in box, in dir, 10f, in filter, out var gHit));

        Assert.Equal(wall, nHit.Collider);
        Assert.Equal(nHit.Collider, gHit.Collider);
        Assert.Equal(nHit.Distance, gHit.Distance, 3);
        Assert.True(gHit.Distance > 0f && gHit.Distance < 10f);
        Assert.True(gHit.Normal.X < 0f, "命中法向应指回扫掠起点（-X 分量）。");
    }

    [Fact]
    public void GridBroadphase_leaves_no_stale_ids_after_move_and_remove()
    {
        var bp = new GridBroadphase(cellSize: 2f, poolSize: 16);
        var big = new Aabb(new Vec3(0f, 0f, 0f), new Vec3(10f, 2f, 2f));  // 跨多个 X cell
        var moved = new Aabb(new Vec3(20f, 0f, 0f), new Vec3(30f, 2f, 2f));
        var scratch = new int[16];

        bp.Update(1, in big);
        Assert.Contains(1, QueryAll(bp, new Aabb(new Vec3(0f, 0f, 0f), new Vec3(10f, 2f, 2f)), scratch));

        bp.Update(1, in moved);
        Assert.Empty(QueryAll(bp, new Aabb(new Vec3(0f, 0f, 0f), new Vec3(10f, 2f, 2f)), scratch));   // 旧区域无残留
        Assert.Contains(1, QueryAll(bp, new Aabb(new Vec3(20f, 0f, 0f), new Vec3(30f, 2f, 2f)), scratch));

        bp.Remove(1);
        Assert.Empty(QueryAll(bp, new Aabb(new Vec3(20f, 0f, 0f), new Vec3(30f, 2f, 2f)), scratch));  // 移除后新区域也无残留
    }

    private static void AddObbScene(out ICollisionWorld naive, out ICollisionWorld grid, out ColliderId wall, Quat rotation)
    {
        naive = new NaiveCollisionWorld();
        grid = new GridCollisionWorld(cellSize: 2f, initialCapacity: 16);

        wall = AddObb(naive, new Vec3(5f, 0f, 0f), rotation, new Vec3(0.5f, 2f, 2f));
        AddObb(grid, new Vec3(5f, 0f, 0f), rotation, new Vec3(0.5f, 2f, 2f));
    }

    private static ColliderId AddObb(ICollisionWorld world, in Vec3 center, in Quat rotation, in Vec3 halfExtents)
    {
        var transform = new Transform3(center, rotation, Vec3.One);
        var shape = ColliderShape.CreateObb(Vec3.Zero, Quat.Identity, halfExtents);
        return world.Add(in transform, in shape, WorldLayer);
    }

    private static System.Collections.Generic.List<int> QueryAll(GridBroadphase bp, in Aabb aabb, int[] scratch)
    {
        var result = new System.Collections.Generic.List<int>();
        var count = bp.Query(in aabb, scratch, scratch.Length);
        for (var i = 0; i < count; i++) result.Add(scratch[i]);
        return result;
    }
}
