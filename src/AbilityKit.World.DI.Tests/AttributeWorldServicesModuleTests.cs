using System.Reflection;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using Xunit;

namespace AbilityKit.World.DI.Tests;

public sealed class AttributeWorldServicesModuleTests
{
    [Fact]
    public void Configure_MultiContractScopedService_ResolvesOneInstancePerScope()
    {
        AttributeWorldServicesModule.ClearCache();
        var builder = new WorldContainerBuilder()
            .AddModule(new AttributeWorldServicesModule(
                WorldServiceProfile.Default,
                new[] { typeof(AttributeWorldServicesModuleTests).Assembly },
                new[] { typeof(AttributeWorldServicesModuleTests).Namespace! }));

        using var container = builder.Build();
        using var firstScope = container.CreateScope();
        using var secondScope = container.CreateScope();

        var primary = firstScope.Resolve<ITestPrimaryPort>();
        var secondary = firstScope.Resolve<ITestSecondaryPort>();
        var implementation = firstScope.Resolve<TestMultiContractService>();
        var fromSecondScope = secondScope.Resolve<ITestPrimaryPort>();

        Assert.Same(primary, secondary);
        Assert.Same(primary, implementation);
        Assert.NotSame(primary, fromSecondScope);
    }

    public interface ITestPrimaryPort
    {
    }

    public interface ITestSecondaryPort
    {
    }

    [WorldService(typeof(ITestPrimaryPort), WorldLifetime.Scoped)]
    [WorldService(typeof(ITestSecondaryPort), WorldLifetime.Scoped)]
    [WorldService(typeof(TestMultiContractService), WorldLifetime.Scoped)]
    public sealed class TestMultiContractService : ITestPrimaryPort, ITestSecondaryPort
    {
    }
}
