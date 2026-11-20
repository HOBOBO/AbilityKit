using System.Reflection;
using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Navigation.Tests;

public sealed class GridPathfinderWorkspaceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Search_generation_rollover_preserves_the_exact_path(bool diagonal, bool simplify)
    {
        var grid = new NavigationGrid(Vec3.Zero, 1f, 6, 6, new bool[36]);
        var options = new NavigationWorldOptions { AllowDiagonal = diagonal, SimplifyPath = simplify };
        var start = new Vec3(.8f, 0f, .2f);
        var target = grid.CellCenter(5, 5);
        var pathfinder = new GridPathfinder();
        var expected = pathfinder.FindPath(grid, in start, in target, options);
        Assert.Equal(PathStatus.Found, expected.Status);

        SetLastSearchGeneration(pathfinder);

        AssertSamePath(expected, pathfinder.FindPath(grid, in start, in target, options));
        AssertSamePath(expected, pathfinder.FindPath(grid, in start, in target, options));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Rollover_does_not_reuse_history_from_a_different_grid_with_the_same_cell_count(bool diagonal, bool simplify)
    {
        var options = new NavigationWorldOptions { AllowDiagonal = diagonal, SimplifyPath = simplify };
        var pathfinder = new GridPathfinder();
        var oldGrid = new NavigationGrid(Vec3.Zero, 1f, 6, 6, new bool[36]);
        var start = oldGrid.CellCenter(0, 0);
        var oldTarget = oldGrid.CellCenter(5, 5);
        Assert.Equal(PathStatus.Found, pathfinder.FindPath(oldGrid, in start, in oldTarget, options).Status);

        var blocked = new bool[36];
        for (var z = 0; z < 8; z++) blocked[z * 4 + 2] = true;
        var grid = new NavigationGrid(Vec3.Zero, 1f, 4, 9, blocked);
        var target = grid.CellCenter(3, 0);
        var expected = new GridPathfinder().FindPath(grid, in start, in target, options);
        Assert.Equal(PathStatus.Found, expected.Status);

        SetLastSearchGeneration(pathfinder);

        AssertSamePath(expected, pathfinder.FindPath(grid, in start, in target, options));
    }

    [Fact]
    public void Rollover_after_an_iteration_limited_failure_matches_a_fresh_query()
    {
        var grid = new NavigationGrid(Vec3.Zero, 1f, 6, 6, new bool[36]);
        var options = new NavigationWorldOptions { MaxIterations = 1 };
        var start = grid.CellCenter(0, 0);
        var target = grid.CellCenter(5, 5);
        var pathfinder = new GridPathfinder();
        Assert.Equal(PathStatus.Failed, pathfinder.FindPath(grid, in start, in target, options).Status);
        options.MaxIterations = 0;
        var expected = new GridPathfinder().FindPath(grid, in start, in target, options);
        Assert.Equal(PathStatus.Found, expected.Status);

        SetLastSearchGeneration(pathfinder);

        AssertSamePath(expected, pathfinder.FindPath(grid, in start, in target, options));
    }

    private static void SetLastSearchGeneration(GridPathfinder pathfinder)
    {
        /*
         * 直接抵达序号边界，避免执行二十多亿次查询；只改变序号，保留真实查询留下的工作区。
         * 生产代码不增加测试入口，断言始终比较寻路行为，而非内部数组如何清理。
         */
        var field = typeof(GridPathfinder).GetField("_search", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field.SetValue(pathfinder, int.MaxValue);
    }

    private static void AssertSamePath(NavigationPath expected, NavigationPath actual)
    {
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected.Waypoints[i], actual.Waypoints[i]);
    }
}
