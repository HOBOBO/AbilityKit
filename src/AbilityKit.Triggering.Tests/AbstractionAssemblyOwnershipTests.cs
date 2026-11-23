using AbilityKit.Triggering.Runtime.Abstractions;
using AbilityKit.Triggering.Runtime.Context;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class AbstractionAssemblyOwnershipTests
{
    [Fact]
    public void Abstraction_types_have_one_owner_and_resolve_from_the_legacy_assembly_name()
    {
        var abstractionAssembly = typeof(ITriggerConfig).Assembly;
        var runtimeAssembly = typeof(ActionContext).Assembly;
        var abstractionNamespace = typeof(ITriggerConfig).Namespace;
        var abstractionTypes = abstractionAssembly.GetExportedTypes()
            .Where(type => type.Namespace == abstractionNamespace)
            .OrderBy(type => type.FullName)
            .ToArray();

        Assert.Equal("AbilityKit.Triggering.Abstractions", abstractionAssembly.GetName().Name);
        Assert.NotEmpty(abstractionTypes);
        Assert.DoesNotContain(runtimeAssembly.GetTypes(), type => type.Namespace == abstractionNamespace);
        Assert.Equal(abstractionTypes, runtimeAssembly.GetForwardedTypes().OrderBy(type => type.FullName));
        Assert.All(abstractionTypes, type => Assert.Same(type, Type.GetType($"{type.FullName}, {runtimeAssembly.FullName}")));
    }
}
