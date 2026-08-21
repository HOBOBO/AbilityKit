using AbilityKit.HFSM;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class HfsmBindingCatalogTests
{
    [Fact]
    public void ScansMetadataWithoutCreatingImplementationInstances()
    {
        DescribedState.Created = 0;
        var catalog = new HfsmBindingCatalog();

        var count = catalog.ScanAssembly(typeof(HfsmBindingCatalogTests).Assembly);

        Assert.True(count >= 1);
        Assert.True(catalog.TryGetDescriptor(HfsmBindingKind.State, "tests.described", out var descriptor));
        Assert.Equal("Described State", descriptor.DisplayName);
        Assert.Equal(typeof(DescribedState), descriptor.ImplementationType);
        Assert.Equal(0, DescribedState.Created);
    }

    [Fact]
    public void RejectsDuplicateKindAndKeyButAllowsSameKeyForAnotherKind()
    {
        var catalog = new HfsmBindingCatalog();
        catalog.Register(new HfsmBindingDescriptor(HfsmBindingKind.State, "shared", "State"));
        catalog.Register(new HfsmBindingDescriptor(HfsmBindingKind.Condition, "shared", "Condition"));

        Assert.Throws<InvalidOperationException>(() =>
            catalog.Register(new HfsmBindingDescriptor(HfsmBindingKind.State, "shared", "Duplicate")));
    }

    [Fact]
    public void AssemblyScanRecordsDuplicateMetadataInsteadOfAbortingTheCatalog()
    {
        var catalog = new HfsmBindingCatalog();

        catalog.ScanAssembly(typeof(HfsmBindingCatalogTests).Assembly);

        Assert.Contains(catalog.Issues, issue => issue.Code == "HFSMBIND001");
        Assert.True(catalog.Contains(HfsmBindingKind.State, "tests.described"));
    }

    [Fact]
    public void AssemblyScanRecordsInvalidStableKeysInsteadOfAbortingTheCatalog()
    {
        var catalog = new HfsmBindingCatalog();

        catalog.ScanAssembly(typeof(HfsmBindingCatalogTests).Assembly);

        Assert.Contains(catalog.Issues, issue => issue.Code == "HFSMBIND004");
    }

    [HfsmBinding(HfsmBindingKind.State, "tests.described", "Described State", "Tests")]
    [HfsmBinding(HfsmBindingKind.State, "tests.described", "Duplicate Described State", "Tests")]
    private sealed class DescribedState : HfsmStateBase<TestOwner>
    {
        public static int Created;

        public DescribedState()
        {
            Created++;
        }
    }

    [HfsmBinding(HfsmBindingKind.Action, "", "Invalid Action")]
    private sealed class InvalidKeyBinding
    {
    }
}
