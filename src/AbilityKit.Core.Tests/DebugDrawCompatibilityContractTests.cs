/*
 * 覆盖下一主版本前保留的旧绘制值契约，不为弃用 API 增加生产使用方。
 * 编辑器事件、Handles 状态和绘制几何需另在真实 Unity 环境验收。
 */
#pragma warning disable CS0618
using System.Globalization;
using System.Reflection;
using AbilityKit.Core.Debugging;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class DebugDrawCompatibilityContractTests
{
    [Fact]
    public void Mask_preserves_arbitrary_bits_and_works_as_a_value_key()
    {
        foreach (var bits in new[] { 0, 1, -1, int.MinValue, int.MaxValue, 0x55555555 })
        {
            var mask = new DebugDrawMask(bits);
            var same = new DebugDrawMask(bits);
            var different = new DebugDrawMask(bits ^ 1);
            var map = new Dictionary<DebugDrawMask, string> { [mask] = "登记值" };

            Assert.Equal(bits, mask.Value);
            Assert.True(mask.Equals(same));
            Assert.True(mask.Equals((object)same));
            Assert.False(mask.Equals(different));
            Assert.False(mask.Equals(null));
            Assert.False(mask.Equals((object)bits));
            Assert.Equal(bits, mask.GetHashCode());
            Assert.Equal("登记值", map[same]);
        }
    }

    [Fact]
    public void All_includes_unnamed_and_sign_bits_while_default_mask_is_none()
    {
        Assert.Equal(0, default(DebugDrawMask).Value);
        Assert.Equal(default, DebugDrawMask.None);
        Assert.Equal(-1, DebugDrawMask.All.Value);
        Assert.Equal(int.MinValue, DebugDrawMask.All.Value & int.MinValue);
        Assert.Equal(1 << 24, DebugDrawMask.All.Value & (1 << 24));
        Assert.Equal(0, DebugDrawMask.None.Value & DebugDrawMask.All.Value);
    }

    [Fact]
    public void Mask_text_uses_current_culture_and_is_not_a_portable_bit_format()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NegativeSign = "负";
            CultureInfo.CurrentCulture = culture;
            Assert.Equal("负1", DebugDrawMask.All.ToString());
            Assert.Equal("0", DebugDrawMask.None.ToString());
            Assert.Equal("32", new DebugDrawMask(1 << 5).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Named_palette_is_opaque_and_explicit_alpha_is_preserved()
    {
        Assert.Equal((0, 255, 0, 255), Channels(DebugDrawColor.Green));
        Assert.Equal((255, 0, 0, 255), Channels(DebugDrawColor.Red));
        Assert.Equal((255, 255, 0, 255), Channels(DebugDrawColor.Yellow));
        Assert.Equal((0, 255, 255, 255), Channels(DebugDrawColor.Cyan));
        Assert.Equal((255, 255, 255, 255), Channels(DebugDrawColor.White));
        Assert.Equal((1, 2, 3, 255), Channels(new DebugDrawColor(1, 2, 3)));
        Assert.Equal((1, 2, 3, 0), Channels(new DebugDrawColor(1, 2, 3, 0)));
        Assert.Equal((1, 2, 3, 127), Channels(new DebugDrawColor(1, 2, 3, 127)));
    }

    [Fact]
    public void Zero_initialized_color_and_style_differ_from_named_default_style()
    {
        Assert.Equal((0, 0, 0, 0), Channels(default));
        Assert.Equal((0, 0, 0, 0), Channels(new DebugDrawColor()));
        Assert.Equal((0, 0, 0, 0), Channels(default(DebugDrawStyle).Color));
        Assert.Equal((0, 255, 0, 255), Channels(DebugDrawStyle.Default.Color));
        Assert.Equal(0, default(DebugDrawContext).EnabledMask.Value);
    }

    [Fact]
    public void Style_and_context_copy_values_without_retaining_source_variables()
    {
        var color = new DebugDrawColor(1, 2, 3, 4);
        var style = new DebugDrawStyle(in color);
        var mask = new DebugDrawMask(int.MinValue | 0x20);
        var context = new DebugDrawContext(mask);
        color = DebugDrawColor.White;
        mask = DebugDrawMask.None;

        Assert.Equal((1, 2, 3, 4), Channels(style.Color));
        Assert.Equal(int.MinValue | 0x20, context.EnabledMask.Value);
        Assert.Equal((255, 255, 255, 255), Channels(color));
        Assert.Equal(0, mask.Value);
    }

    [Theory]
    [InlineData(typeof(DebugDrawMask))]
    [InlineData(typeof(DebugDrawColor))]
    [InlineData(typeof(DebugDrawStyle))]
    [InlineData(typeof(DebugDrawContext))]
    [InlineData(typeof(IDebugDraw))]
    [InlineData(typeof(IDebugDrawContributor))]
    public void Every_legacy_runtime_type_keeps_its_migration_message(Type type)
    {
        var attribute = type.GetCustomAttribute<ObsoleteAttribute>();
        Assert.NotNull(attribute);
        Assert.False(attribute.IsError);
        Assert.Equal("Debug draw belongs to AbilityKit.Diagnostics; migrate before the next major version.", attribute.Message);
    }

    private static (int R, int G, int B, int A) Channels(DebugDrawColor color) =>
        (color.R, color.G, color.B, color.A);
}
