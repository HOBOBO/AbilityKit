using AbilityKit.Demo.Moba.Session;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Session;

public sealed class MobaSessionDefaultsTests
{
    [Fact]
    public void ResolveGameplayId_uses_configured_value_and_default_fallback()
    {
        var defaults = new MobaSessionDefaults { GameplayId = 2 };

        Assert.Equal(2, defaults.ResolveGameplayId(0));
        Assert.Equal(3, defaults.ResolveGameplayId(3));
    }
}
