using AbilityKit.Diagnostics.DebugDraw;
using Xunit;

namespace AbilityKit.Diagnostics.Tests;

public sealed class DebugDrawTypesTests
{
    [Fact]
    public void Masks_have_stable_none_all_and_value_semantics()
    {
        Assert.Equal(0, DebugDrawMask.None.Value);
        Assert.Equal(~0, DebugDrawMask.All.Value);
        Assert.Equal(new DebugDrawMask(7), new DebugDrawMask(7));
        Assert.NotEqual(new DebugDrawMask(7), new DebugDrawMask(8));
    }

    [Fact]
    public void Named_colors_and_default_style_are_stable()
    {
        AssertColor(DebugDrawColor.Green, 0, 255, 0, 255);
        AssertColor(DebugDrawColor.Red, 255, 0, 0, 255);
        AssertColor(DebugDrawColor.Yellow, 255, 255, 0, 255);
        AssertColor(DebugDrawColor.Cyan, 0, 255, 255, 255);
        AssertColor(DebugDrawColor.White, 255, 255, 255, 255);
        AssertColor(DebugDrawStyle.Default.Color, 0, 255, 0, 255);
    }

    [Fact]
    public void Context_preserves_the_enabled_mask()
    {
        var context = new DebugDrawContext(new DebugDrawMask(0b1010));

        Assert.Equal(0b1010, context.EnabledMask.Value);
    }

    private static void AssertColor(DebugDrawColor color, byte red, byte green, byte blue, byte alpha)
    {
        Assert.Equal(red, color.R);
        Assert.Equal(green, color.G);
        Assert.Equal(blue, color.B);
        Assert.Equal(alpha, color.A);
    }
}
