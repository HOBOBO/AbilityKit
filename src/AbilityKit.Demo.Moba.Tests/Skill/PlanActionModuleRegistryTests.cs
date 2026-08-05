using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class PlanActionModuleRegistryTests
{
    [Fact]
    public void CreateDefault_UsesCompleteGeneratedManifestInStableOrder()
    {
        AppContext.SetSwitch("AbilityKit.Moba.DisablePlanActionReflectionFallback", true);
        try
        {
            var registry = PlanActionModuleRegistry.CreateDefault();

            Assert.Equal(31, registry.Descriptors.Length);
            Assert.Equal(31, registry.Modules.Length);
            Assert.Equal(31, registry.Descriptors.Select(item => item.ActionName).Distinct(StringComparer.Ordinal).Count());

            for (var index = 1; index < registry.Descriptors.Length; index++)
            {
                var previous = registry.Descriptors[index - 1];
                var current = registry.Descriptors[index];
                Assert.True(
                    previous.Order < current.Order ||
                    previous.Order == current.Order &&
                    string.CompareOrdinal(previous.ModuleName, current.ModuleName) < 0,
                    $"Manifest order is unstable at index {index}: {previous.ModuleName}, {current.ModuleName}");
            }
        }
        finally
        {
            AppContext.SetSwitch("AbilityKit.Moba.DisablePlanActionReflectionFallback", false);
        }
    }
}
