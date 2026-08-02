using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Modifiers.Tests;

public sealed class ModifierKeyTests
{
    [Fact]
    public void Create_packs_bytes_into_uint() => Assert.NotEqual(0u, ModifierKey.Create(1, 2, 3).Packed);

    [Fact]
    public void None_is_default_zero() => Assert.Equal(0u, ModifierKey.None.Packed);

    [Fact]
    public void FromPacked_roundtrips()
    {
        var k = ModifierKey.Create(5, 10, 15);
        Assert.Equal(k.Packed, ModifierKey.FromPacked(k.Packed).Packed);
    }
}
