using AbilityKit.Analyzer.Configuration;
using Xunit;

namespace AbilityKit.Analyzer.Configuration.Tests;

public sealed class PackageConstraintsConfigTests
{
    [Fact]
    public void GetEffectiveConstraint_PrefersExactConstraintOverWildcard()
    {
        var exact = CreateConstraint("Exact.Namespace");
        var wildcard = CreateConstraint("Wildcard.Namespace");
        var config = new PackageConstraintsConfig
        {
            Constraints = new Dictionary<string, PackageConstraint>
            {
                ["AbilityKit.Demo.*"] = wildcard,
                ["AbilityKit.Demo.Runtime"] = exact
            }
        };

        Assert.Same(exact, config.GetEffectiveConstraint("AbilityKit.Demo.Runtime"));
    }

    [Fact]
    public void GetEffectiveConstraint_UsesMatchingWildcardConstraint()
    {
        var wildcard = CreateConstraint("Wildcard.Namespace");
        var config = new PackageConstraintsConfig
        {
            Constraints = new Dictionary<string, PackageConstraint>
            {
                ["AbilityKit.Demo.*"] = wildcard
            }
        };

        Assert.Same(wildcard, config.GetEffectiveConstraint("AbilityKit.Demo.Runtime"));
    }

    [Fact]
    public void GetEffectiveConstraint_DoesNotApplyDefaultsToUnlistedPackageWhenDisabled()
    {
        var config = new PackageConstraintsConfig
        {
            GlobalDefaults = new GlobalConstraintDefaults
            {
                Enabled = true,
                ApplyToUnlistedPackages = false,
                ForbiddenNamespaces = new List<string> { "UnityEngine" }
            }
        };

        Assert.Null(config.GetEffectiveConstraint("AbilityKit.Runtime"));
    }

    [Fact]
    public void GetEffectiveConstraint_ReturnsExplicitConstraintWhenGlobalDefaultsAreDisabled()
    {
        var explicitConstraint = CreateConstraint("UnityEngine");
        var config = new PackageConstraintsConfig
        {
            GlobalDefaults = new GlobalConstraintDefaults
            {
                Enabled = false,
                ApplyToUnlistedPackages = true
            },
            Constraints = new Dictionary<string, PackageConstraint>
            {
                ["AbilityKit.Runtime"] = explicitConstraint
            }
        };

        Assert.Same(explicitConstraint, config.GetEffectiveConstraint("AbilityKit.Runtime"));
    }

    [Fact]
    public void NullConfigurationFieldsAndEntriesDoNotThrow()
    {
        var config = new PackageConstraintsConfig
        {
            Constraints = null,
            GlobalDefaults = null
        };
        var constraint = new PackageConstraint
        {
            ForbiddenNamespaces = new List<string> { null, "UnityEngine" },
            ForbiddenAssemblies = new List<string> { null, "UnityEngine" }
        };

        Assert.Null(config.GetEffectiveConstraint("AbilityKit.Runtime"));
        Assert.True(constraint.IsNamespaceForbidden("UnityEngine.UI"));
        Assert.True(constraint.IsAssemblyForbidden("UnityEngine.CoreModule"));

        var configWithNullDefaultLists = new PackageConstraintsConfig
        {
            GlobalDefaults = new GlobalConstraintDefaults
            {
                Enabled = true,
                ApplyToUnlistedPackages = true,
                ForbiddenNamespaces = null,
                ForbiddenAssemblies = null
            }
        };
        var effectiveConstraint = configWithNullDefaultLists.GetEffectiveConstraint("AbilityKit.Runtime");

        Assert.NotNull(effectiveConstraint);
        Assert.Empty(effectiveConstraint.ForbiddenNamespaces);
        Assert.Empty(effectiveConstraint.ForbiddenAssemblies);
    }

    private static PackageConstraint CreateConstraint(string forbiddenNamespace)
    {
        return new PackageConstraint
        {
            ForbiddenNamespaces = new List<string> { forbiddenNamespace }
        };
    }
}
