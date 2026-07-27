using AbilityKit.Ability.World.DI;
using Xunit;

namespace AbilityKit.World.DI.Tests;

public sealed class WorldServiceRegistrationTests
{
    [Fact]
    public void Register_ReplacesExistingRegistrationAndRecordsLegacyPolicy()
    {
        var builder = new WorldContainerBuilder();
        builder.Register<ITestService>(WorldLifetime.Singleton, _ => new TestServiceA());
        builder.Register<ITestService>(WorldLifetime.Singleton, _ => new TestServiceB());

        using var container = builder.Build();

        Assert.IsType<TestServiceB>(container.Resolve<ITestService>());
        Assert.Equal(WorldServiceRegistrationOutcome.Added, builder.Registrations[0].Outcome);
        Assert.Equal(WorldServiceRegistrationOutcome.Replaced, builder.Registrations[1].Outcome);
        Assert.Equal(WorldServiceRegistrationPolicy.Replace, builder.Registrations[1].Policy);
    }

    [Fact]
    public void TryRegister_KeepsExistingRegistrationAndRecordsLegacyPolicy()
    {
        var builder = new WorldContainerBuilder();
        builder.Register<ITestService>(WorldLifetime.Singleton, _ => new TestServiceA());
        builder.TryRegister<ITestService>(WorldLifetime.Singleton, _ => new TestServiceB());

        using var container = builder.Build();

        Assert.IsType<TestServiceA>(container.Resolve<ITestService>());
        Assert.Equal(WorldServiceRegistrationOutcome.KeptExisting, builder.Registrations[1].Outcome);
        Assert.Equal(WorldServiceRegistrationPolicy.KeepExisting, builder.Registrations[1].Policy);
        Assert.Equal(typeof(ITestService), builder.Registrations[1].ImplementationType);
    }

    [Fact]
    public void Register_RejectPolicyReportsBothRegistrations()
    {
        var builder = new WorldContainerBuilder();
        builder.Register(
            typeof(ITestService),
            typeof(TestServiceA),
            WorldLifetime.Singleton,
            _ => new TestServiceA(),
            WorldServiceRegistrationPolicy.Reject);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Register(
            typeof(ITestService),
            typeof(TestServiceB),
            WorldLifetime.Singleton,
            _ => new TestServiceB(),
            WorldServiceRegistrationPolicy.Reject));

        Assert.Contains("registration rejected", exception.Message);
        Assert.Contains(typeof(TestServiceA).FullName!, exception.Message);
        Assert.Contains(typeof(TestServiceB).FullName!, exception.Message);
        Assert.Equal(WorldServiceRegistrationOutcome.Rejected, builder.Registrations[1].Outcome);
        Assert.Equal(typeof(TestServiceA), builder.Registrations[1].PreviousImplementationType);
    }

    [Fact]
    public void AddModule_RecordsModuleSourceAndRestoresParentForNestedModules()
    {
        var builder = new WorldContainerBuilder();

        builder.AddModule(new ParentModule());

        Assert.Collection(
            builder.Registrations,
            registration => Assert.Equal(typeof(ParentModule), registration.SourceModuleType),
            registration => Assert.Equal(typeof(ChildModule), registration.SourceModuleType),
            registration => Assert.Equal(typeof(ParentModule), registration.SourceModuleType));
    }

    [Fact]
    public void CompositionReport_ContainsModulePlanAndRegistrationProvenance()
    {
        IWorldModule[] modules = { new ParentModule() };
        var plan = WorldModulePlanner.Create(modules);
        var builder = new WorldContainerBuilder();
        builder.AddModule(plan.Entries[0].Module);

        var report = WorldCompositionReportBuilder.Create(
            "world-1",
            "test",
            plan,
            builder);

        var module = Assert.Single(report.Modules);
        Assert.Equal(typeof(ParentModule).FullName, module.Type);
        Assert.Equal(0, module.SourceIndex);
        Assert.Collection(
            report.ServiceRegistrations,
            registration => Assert.Equal(typeof(ParentModule).FullName, registration.SourceModuleType),
            registration => Assert.Equal(typeof(ChildModule).FullName, registration.SourceModuleType),
            registration => Assert.Equal(typeof(ParentModule).FullName, registration.SourceModuleType));
    }

    private interface ITestService
    {
    }

    private sealed class TestServiceA : ITestService
    {
    }

    private sealed class TestServiceB : ITestService
    {
    }

    private sealed class ParentModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            builder.RegisterInstance("parent-before");
            builder.AddModule(new ChildModule());
            builder.RegisterInstance(42);
        }
    }

    private sealed class ChildModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            builder.RegisterInstance(1.5d);
        }
    }
}
