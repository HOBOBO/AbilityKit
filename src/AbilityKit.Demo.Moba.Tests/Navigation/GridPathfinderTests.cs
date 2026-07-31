using System.Collections.Generic;
using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Navigation;

public sealed class GridPathfinderTests
{
    private const float Cell = 1f;

    private static NavigationWorld MakeWorld(int width, int height, bool simplify, params (int cx, int cz)[] blocked)
    {
        var blockedArr = new bool[width * height];
        foreach (var (cx, cz) in blocked)
        {
            if ((uint)cx < (uint)width && (uint)cz < (uint)height)
            {
                blockedArr[cz * width + cx] = true;
            }
        }

        var grid = new NavigationGrid(Vec3.Zero, Cell, width, height, blockedArr);
        return new NavigationWorld(grid, new NavigationWorldOptions
        {
            CellSize = Cell,
            AgentRadius = Cell * 0.4f,
            AllowDiagonal = true,
            SimplifyPath = simplify,
        });
    }

    private static Vec3 Center(int cx, int cz) => new Vec3((cx + 0.5f) * Cell, 0f, (cz + 0.5f) * Cell);

    [Fact]
    public void FindPath_routes_around_a_wall()
    {
        // 7x7，cx=3 的 cz 0..4 全堵，仅 cz 5/6 留缺口。
        var blocked = new (int, int)[]
        {
            (3, 0), (3, 1), (3, 2), (3, 3), (3, 4),
        };
        var world = MakeWorld(7, 7, simplify: true, blocked);

        var status = world.FindPath(Center(0, 0), Center(6, 0), 0.5f, out var path);

        Assert.Equal(PathStatus.Found, status);
        Assert.True(path.Length > 2, "绕障路径不应被拉直成直线。");

        // 末点替换为精确目标。
        Assert.Equal(Center(6, 0), path.Waypoints[path.Length - 1]);

        // 路径点（除起止）不得落在 blocked cell。
        var grid = world.Grid;
        for (int i = 0; i < path.Length; i++)
        {
            grid.WorldToCellClamped(path.Waypoints[i], out var cx, out var cz);
            Assert.False(grid.IsBlocked(cx, cz), $"路径点 {i} 落在 blocked cell ({cx},{cz})。");
        }
    }

    [Fact]
    public void FindPath_returns_failed_when_target_unreachable()
    {
        // cx=1 整列堵死，左列与右侧完全断开。
        var blocked = new (int, int)[]
        {
            (1, 0), (1, 1), (1, 2), (1, 3), (1, 4), (1, 5), (1, 6),
        };
        var world = MakeWorld(7, 7, simplify: true, blocked);

        var status = world.FindPath(Center(0, 0), Center(6, 0), 0.5f, out var path);

        Assert.Equal(PathStatus.Failed, status);
        Assert.False(path.HasPath);
    }

    [Fact]
    public void FindPath_is_deterministic()
    {
        var blocked = new (int, int)[]
        {
            (3, 0), (3, 1), (3, 2), (3, 3), (3, 4),
        };
        var world = MakeWorld(7, 7, simplify: true, blocked);

        world.FindPath(Center(0, 0), Center(6, 0), 0.5f, out var first);
        world.FindPath(Center(0, 0), Center(6, 0), 0.5f, out var second);

        Assert.Equal(first.Length, second.Length);
        for (int i = 0; i < first.Length; i++)
        {
            Assert.Equal(first.Waypoints[i], second.Waypoints[i]);
        }
    }

    [Fact]
    public void FindPath_simplifies_a_clear_straight_line()
    {
        var world = MakeWorld(7, 7, simplify: true);

        var status = world.FindPath(Center(0, 0), Center(6, 0), 0.5f, out var path);

        Assert.Equal(PathStatus.Found, status);
        Assert.Equal(2, path.Length);
        Assert.Equal(Center(0, 0), path.Waypoints[0]);
        Assert.Equal(Center(6, 0), path.Waypoints[1]);
    }

    [Fact]
    public void IsWalkable_and_project_reflect_blocked_cells()
    {
        var blocked = new (int, int)[] { (0, 0) };
        var world = MakeWorld(3, 3, simplify: false, blocked);

        Assert.False(world.IsWalkable(Center(0, 0), 0f));
        Assert.True(world.IsWalkable(Center(1, 1), 0f));

        Assert.True(world.TryProjectToWalkable(Center(0, 0), 0f, out var projected));
        Assert.True(world.IsWalkable(projected, 0f));
    }
}
