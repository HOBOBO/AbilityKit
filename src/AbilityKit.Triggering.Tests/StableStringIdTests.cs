using AbilityKit.Triggering.Eventing;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class StableStringIdTests
{
    [Theory]
    [InlineData("ability.cast", 1961249110)]
    [InlineData("能力", 1868853449)]
    [InlineData("emoji-😀", 802847573)]
    public void Existing_trigger_ids_preserve_utf16_non_negative_contract(string value, int expected)
    {
        Assert.Equal(expected & 0x7FFFFFFF, StableStringId.Get(value));
    }
}
