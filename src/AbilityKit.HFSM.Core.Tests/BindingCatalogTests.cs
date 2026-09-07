using AbilityKit.HFSM;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Definition;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class BindingCatalogTests
{
    [Fact]
    public void ScansMetadataWithoutCreatingImplementationInstances()
    {
        DescribedState.Created = 0;
        var catalog = new BindingCatalog();

        var count = catalog.ScanAssembly(typeof(BindingCatalogTests).Assembly);

        Assert.True(count >= 1);
        Assert.True(catalog.TryGetDescriptor(BindingKind.State, "tests.described", out var descriptor));
        Assert.Equal("Described State", descriptor.DisplayName);
        Assert.Equal(typeof(DescribedState), descriptor.ImplementationType);
        Assert.Equal(0, DescribedState.Created);
    }

    [Fact]
    public void RejectsDuplicateKindAndKeyButAllowsSameKeyForAnotherKind()
    {
        var catalog = new BindingCatalog();
        catalog.Register(new BindingDescriptor(BindingKind.State, "shared", "State"));
        catalog.Register(new BindingDescriptor(BindingKind.Condition, "shared", "Condition"));

        Assert.Throws<InvalidOperationException>(() =>
            catalog.Register(new BindingDescriptor(BindingKind.State, "shared", "Duplicate")));
    }

    [Fact]
    public void AssemblyScanRecordsDuplicateMetadataInsteadOfAbortingTheCatalog()
    {
        var catalog = new BindingCatalog();

        catalog.ScanAssembly(typeof(BindingCatalogTests).Assembly);

        Assert.Contains(catalog.Issues, issue => issue.Code == "HFSMBIND001");
        Assert.True(catalog.Contains(BindingKind.State, "tests.described"));
    }

    [Fact]
    public void AssemblyScanRecordsInvalidStableKeysInsteadOfAbortingTheCatalog()
    {
        var catalog = new BindingCatalog();

        catalog.ScanAssembly(typeof(BindingCatalogTests).Assembly);

        Assert.Contains(catalog.Issues, issue => issue.Code == "HFSMBIND004");
    }

    [Binding(BindingKind.State, "tests.described", "Described State", "Tests")]
    [Binding(BindingKind.State, "tests.described", "Duplicate Described State", "Tests")]
    private sealed class DescribedState : RuntimeStateBase<TestOwner>
    {
        public static int Created;

        public DescribedState()
        {
            Created++;
        }
    }

    [Binding(BindingKind.Action, "", "Invalid Action")]
    private sealed class InvalidKeyBinding
    {
    }
}
