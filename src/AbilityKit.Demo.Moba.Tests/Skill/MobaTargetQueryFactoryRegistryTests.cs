using AbilityKit.Demo.Moba.Services.Search;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaTargetQueryFactoryRegistryTests
{
    [Fact]
    public void BuildDefaultRegistry_UsesCompleteGeneratedManifest()
    {
        AppContext.SetSwitch("AbilityKit.Moba.DisableTargetQueryFactoryReflectionFallback", true);
        try
        {
            var registry = MobaTargetQueryFactoryRegistry.BuildDefaultRegistry();

            Assert.Equal(8, registry.SourceCount);
            Assert.Equal(10, registry.FilterCount);
            Assert.Equal(4, registry.OrderCount);
            Assert.Equal(2, registry.SelectCount);
        }
        finally
        {
            AppContext.SetSwitch("AbilityKit.Moba.DisableTargetQueryFactoryReflectionFallback", false);
        }
    }
}
