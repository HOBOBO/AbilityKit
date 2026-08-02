using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Navigation.Tests;

public sealed class NavigationGridTests
{
    [Fact]
    public void Grid_basic_properties_and_blocked()
    {
        // 3×3 网格，中心格 blocked
        var blocked = new[] { false, false, false, false, true, false, false, false, false };
        var grid = new NavigationGrid(Vec3.Zero, 1f, 3, 3, blocked);

        Assert.Equal(3, grid.Width);
        Assert.Equal(3, grid.Height);
        Assert.Equal(9, grid.CellCount);
        Assert.Equal(1f, grid.CellSize);

        Assert.True(grid.IsInBounds(0, 0));
        Assert.False(grid.IsInBounds(5, 0));

        Assert.False(grid.IsBlocked(0, 0));     // 角格可走
        Assert.True(grid.IsBlocked(1, 1));       // 中心格 blocked
        Assert.False(grid.IsBlocked(2, 2));      // 对角可走
    }

    [Fact]
    public void Index_maps_cells()
    {
        var grid = new NavigationGrid(Vec3.Zero, 2f, 2, 3, new bool[6]);
        Assert.Equal(0, grid.Index(0, 0));
        Assert.Equal(2, grid.Index(0, 1));
        Assert.Equal(1, grid.Index(1, 0));
        Assert.Equal(5, grid.Index(1, 2));
    }

    [Fact]
    public void WorldSize() => Assert.Equal(5f, new NavigationGrid(Vec3.Zero, 1f, 5, 1, new bool[5]).WorldSizeX);

    [Fact]
    public void Options_defaults()
    {
        var opts = new NavigationWorldOptions();
        Assert.Equal(0.5f, opts.CellSize);
        Assert.Equal(0.5f, opts.AgentRadius);
        Assert.True(opts.AllowDiagonal);
    }
}
