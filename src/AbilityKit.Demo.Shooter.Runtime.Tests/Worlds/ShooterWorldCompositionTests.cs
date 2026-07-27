using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Diagnostics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Shooter.Runtime;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Runtime;

public sealed class ShooterWorldCompositionTests
{
    [Fact]
    public void Constructor_AppliesSharedModulePlanAndPublishesCompositionReport()
    {
        var worldId = new WorldId("shooter-composition-plan");
        var options = new WorldCreateOptions(worldId, "shooter-composition-test");
        options.Modules.Add(new DependentModule());
        options.Modules.Add(new RootModule());

        using var world = new ShooterLogicWorld(options);

        Assert.IsType<DependentMarker>(world.Services.Resolve<IMarker>());
        Assert.Same(world, world.Services.Resolve<IWorld>());
        Assert.Equal(worldId, world.Services.Resolve<WorldId>());
        Assert.Equal("shooter-composition-test", world.Services.Resolve<string>());

        Assert.True(WorldDebugRegistry.TryGet(worldId.Value, out var report));
        Assert.Collection(
            report.Modules,
            entry => Assert.Equal(typeof(RootModule).FullName, entry.Type),
            entry => Assert.Equal(typeof(DependentModule).FullName, entry.Type));
        Assert.Contains(
            report.ServiceRegistrations,
            entry => entry.ServiceType == typeof(IMarker).FullName &&
                     entry.SourceModuleType == typeof(DependentModule).FullName &&
                     entry.Outcome == WorldServiceRegistrationOutcome.Replaced.ToString());
        Assert.Contains(
            report.ServiceRegistrations,
            entry => entry.ServiceType == typeof(IWorld).FullName &&
                     entry.Ownership == WorldServiceOwnership.External.ToString());

        WorldDebugRegistry.Clear(worldId.Value);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencyBeforeConfiguringModules()
    {
        ConfiguredModuleCount = 0;
        var options = new WorldCreateOptions(
            new WorldId("shooter-composition-missing"),
            "shooter-composition-test");
        options.Modules.Add(new DependentModule());

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ShooterLogicWorld(options));

        Assert.Contains("module dependency missing", exception.Message);
        Assert.Equal(0, ConfiguredModuleCount);
    }

    [Fact]
    public void Constructor_RejectsDuplicateModuleTypeBeforeConfiguringModules()
    {
        ConfiguredModuleCount = 0;
        var options = new WorldCreateOptions(
            new WorldId("shooter-composition-duplicate"),
            "shooter-composition-test");
        options.Modules.Add(new RootModule());
        options.Modules.Add(new RootModule());

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ShooterLogicWorld(options));

        Assert.Contains("duplicate world module type", exception.Message);
        Assert.Equal(0, ConfiguredModuleCount);
    }

    private static int ConfiguredModuleCount { get; set; }

    private interface IMarker
    {
    }

    private sealed class RootMarker : IMarker
    {
    }

    private sealed class DependentMarker : IMarker
    {
    }

    private sealed class RootModule : IWorldModule, IWorldModuleInfo
    {
        public string Id => "test.root";
        public int Order => 100;
        public Type[] DependsOn => Array.Empty<Type>();
        public Type[] ConflictsWith => Array.Empty<Type>();

        public void Configure(WorldContainerBuilder builder)
        {
            ConfiguredModuleCount++;
            builder.Register<IMarker>(
                WorldLifetime.Singleton,
                _ => new RootMarker());
        }
    }

    private sealed class DependentModule : IWorldModule, IWorldModuleInfo
    {
        public string Id => "test.dependent";
        public int Order => -100;
        public Type[] DependsOn => new[] { typeof(RootModule) };
        public Type[] ConflictsWith => Array.Empty<Type>();

        public void Configure(WorldContainerBuilder builder)
        {
            ConfiguredModuleCount++;
            builder.Register<IMarker>(
                WorldLifetime.Singleton,
                _ => new DependentMarker());
        }
    }
}
