using AbilityKit.Ability.World.DI;
using Xunit;

namespace AbilityKit.World.DI.Tests;

public sealed class WorldModulePlannerTests
{
    [Fact]
    public void Create_OrdersByDependenciesThenOrderThenSourceIndex()
    {
        IWorldModule[] modules =
        {
            new IndependentLateModule(),
            new DependentModule(),
            new RootModule(),
            new IndependentEarlyModule()
        };

        var plan = WorldModulePlanner.Create(modules, "TestWorld");

        Assert.Collection(
            plan.Entries,
            entry => Assert.IsType<IndependentEarlyModule>(entry.Module),
            entry => Assert.IsType<RootModule>(entry.Module),
            entry => Assert.IsType<DependentModule>(entry.Module),
            entry => Assert.IsType<IndependentLateModule>(entry.Module));
        Assert.Equal(2, plan.Entries[1].SourceIndex);
        Assert.Equal("dependent", plan.Entries[2].Id);
    }

    [Fact]
    public void Create_ResolvesAssignableDependency()
    {
        IWorldModule[] modules =
        {
            new InterfaceDependentModule(),
            new ContractModule()
        };

        var plan = WorldModulePlanner.Create(modules);

        Assert.IsType<ContractModule>(plan.Entries[0].Module);
        Assert.IsType<InterfaceDependentModule>(plan.Entries[1].Module);
    }

    [Fact]
    public void Create_RejectsDuplicateModuleType()
    {
        IWorldModule[] modules = { new RootModule(), new RootModule() };

        var exception = Assert.Throws<InvalidOperationException>(
            () => WorldModulePlanner.Create(modules, "TestWorld"));

        Assert.Contains("TestWorld duplicate world module type", exception.Message);
        Assert.Contains(typeof(RootModule).FullName!, exception.Message);
    }

    [Fact]
    public void Create_RejectsDuplicateModuleId()
    {
        IWorldModule[] modules =
        {
            new DuplicateIdModuleA(),
            new DuplicateIdModuleB()
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => WorldModulePlanner.Create(modules));

        Assert.Contains("duplicate world module id: duplicate", exception.Message);
    }

    [Fact]
    public void Create_RejectsAssignableConflict()
    {
        IWorldModule[] modules =
        {
            new ConflictingModule(),
            new ContractModule()
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => WorldModulePlanner.Create(modules));

        Assert.Contains("module conflict", exception.Message);
        Assert.Contains(typeof(IModuleContract).FullName!, exception.Message);
        Assert.Contains(typeof(ContractModule).FullName!, exception.Message);
    }

    [Fact]
    public void Create_RejectsMissingDependency()
    {
        IWorldModule[] modules = { new DependentModule() };

        var exception = Assert.Throws<InvalidOperationException>(
            () => WorldModulePlanner.Create(modules));

        Assert.Contains("module dependency missing", exception.Message);
        Assert.Contains(typeof(RootModule).FullName!, exception.Message);
    }

    [Fact]
    public void Create_ReportsConcreteCyclePath()
    {
        IWorldModule[] modules = { new CycleAModule(), new CycleBModule() };

        var exception = Assert.Throws<InvalidOperationException>(
            () => WorldModulePlanner.Create(modules));

        Assert.Contains("module dependency cycle detected", exception.Message);
        Assert.Contains(typeof(CycleAModule).FullName!, exception.Message);
        Assert.Contains(typeof(CycleBModule).FullName!, exception.Message);
        Assert.Contains(" -> ", exception.Message);
    }

    private interface IModuleContract
    {
    }

    private abstract class ModuleBase : IWorldModule, IWorldModuleInfo
    {
        protected ModuleBase(
            string? id = null,
            int order = 0,
            Type[]? dependsOn = null,
            Type[]? conflictsWith = null)
        {
            Id = id!;
            Order = order;
            DependsOn = dependsOn ?? Array.Empty<Type>();
            ConflictsWith = conflictsWith ?? Array.Empty<Type>();
        }

        public string Id { get; }
        public int Order { get; }
        public Type[] DependsOn { get; }
        public Type[] ConflictsWith { get; }

        public void Configure(WorldContainerBuilder builder)
        {
        }
    }

    private sealed class RootModule : ModuleBase
    {
        public RootModule() : base("root", order: 10)
        {
        }
    }

    private sealed class DependentModule : ModuleBase
    {
        public DependentModule()
            : base("dependent", order: -100, dependsOn: new[] { typeof(RootModule) })
        {
        }
    }

    private sealed class IndependentEarlyModule : ModuleBase
    {
        public IndependentEarlyModule() : base(order: -20)
        {
        }
    }

    private sealed class IndependentLateModule : ModuleBase
    {
        public IndependentLateModule() : base(order: 20)
        {
        }
    }

    private sealed class ContractModule : ModuleBase, IModuleContract
    {
    }

    private sealed class InterfaceDependentModule : ModuleBase
    {
        public InterfaceDependentModule()
            : base(dependsOn: new[] { typeof(IModuleContract) })
        {
        }
    }

    private sealed class DuplicateIdModuleA : ModuleBase
    {
        public DuplicateIdModuleA() : base("duplicate")
        {
        }
    }

    private sealed class DuplicateIdModuleB : ModuleBase
    {
        public DuplicateIdModuleB() : base("duplicate")
        {
        }
    }

    private sealed class ConflictingModule : ModuleBase
    {
        public ConflictingModule()
            : base(conflictsWith: new[] { typeof(IModuleContract) })
        {
        }
    }

    private sealed class CycleAModule : ModuleBase
    {
        public CycleAModule()
            : base(dependsOn: new[] { typeof(CycleBModule) })
        {
        }
    }

    private sealed class CycleBModule : ModuleBase
    {
        public CycleBModule()
            : base(dependsOn: new[] { typeof(CycleAModule) })
        {
        }
    }
}
