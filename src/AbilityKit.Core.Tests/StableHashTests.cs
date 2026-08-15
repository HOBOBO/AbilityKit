using AbilityKit.Core.Identifiers;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class StableHashTests
{
    [Theory]
    [InlineData("", -2128831035)]
    [InlineData("hello", 1335831723)]
    [InlineData("ability.cast", 1961249110)]
    [InlineData("能力", 1868853449)]
    [InlineData("emoji-😀", 802847573)]
    public void Utf16_v1_matches_published_vectors(string value, int expected)
    {
        Assert.Equal(expected, StableHashV1.Fnv1a32Utf16(value));
        Assert.Equal(expected & 0x7FFFFFFF, StableHashV1.Fnv1a32Utf16NonNegative(value));
    }

    [Theory]
    [InlineData("", -2128831035)]
    [InlineData("hello", 1335831723)]
    [InlineData("ability.cast", 1961249110)]
    [InlineData("能力", 932951673)]
    [InlineData("emoji-😀", 765500573)]
    public void Utf8_v1_matches_published_vectors(string value, int expected)
    {
        Assert.Equal(expected, StableHashV1.Fnv1a32Utf8(value));
    }

    [Fact]
    public void Utf8_v1_replaces_invalid_surrogates()
    {
        var invalid = new string((char)0xD800, 1);

        Assert.Equal(55024714, StableHashV1.Fnv1a32Utf8(invalid));
        Assert.Equal(797285387, StableHashV1.Fnv1a32Utf8("left-" + invalid + "-right"));
    }

    [Fact]
    public void Stable_hash_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => StableHashV1.Fnv1a32Utf16(null!));
        Assert.Throws<ArgumentNullException>(() => StableHashV1.Fnv1a32Utf8(null!));
    }

    [Fact]
    public void String_registry_preserves_legacy_utf16_ids()
    {
        var registry = new StableStringIdRegistry();

        Assert.Equal(1868853449, registry.GetOrRegister("能力"));
    }
}
