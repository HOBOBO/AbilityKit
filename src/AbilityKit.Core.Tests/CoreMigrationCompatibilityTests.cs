#pragma warning disable CS0618 // Compatibility coverage for APIs retained until the next major version.
using System.Reflection;
using AbilityKit.Core.Debugging;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Markers;
using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Pooling;
using AbilityKit.Core.Reflection;
using AbilityKit.Core.Utilities;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class CoreMigrationCompatibilityTests
{
    [Fact]
    public void Legacy_debug_draw_value_contract_remains_available()
    {
        var context = new DebugDrawContext(new DebugDrawMask(3));

        Assert.Equal(3, context.EnabledMask.Value);
        Assert.Equal(0, DebugDrawMask.None.Value);
        Assert.Equal(0, DebugDrawStyle.Default.Color.R);
        Assert.Equal(255, DebugDrawStyle.Default.Color.G);
    }

    [Fact]
    public void Legacy_dispose_helper_still_clears_the_owned_reference()
    {
        DisposableProbe? probe = new DisposableProbe();

        DisposeUtils.TryDispose(ref probe);

        Assert.Null(probe);
    }

    [Fact]
    public void Legacy_dispose_helper_clears_nullable_interface_references()
    {
        IDisposable? disposable = new DisposableProbe();

        DisposeUtils.TryDispose(ref disposable);

        Assert.Null(disposable);
    }

    [Fact]
    public void Legacy_reflection_type_lookup_reports_missing_types_without_null_forgiveness()
    {
        var testType = typeof(CoreMigrationCompatibilityTests);

        Assert.Same(testType, ReflectionInvokeUtils.FindType(testType.AssemblyQualifiedName!));
        Assert.Same(testType, ReflectionInvokeUtils.FindType(testType.FullName!));
        Assert.Null(ReflectionInvokeUtils.FindType(string.Empty));
        Assert.Null(ReflectionInvokeUtils.FindType("AbilityKit.Tests.TypeThatDoesNotExist"));
    }

    [Fact]
    public void Legacy_reflection_invocation_reports_no_exception_when_method_is_missing()
    {
        var invoked = ReflectionInvokeUtils.TryInvokeStaticMethod(
            typeof(CoreMigrationCompatibilityTests).FullName!,
            "MissingMethod",
            out var exception);

        Assert.False(invoked);
        Assert.Null(exception);
    }

    [Theory]
    [MemberData(nameof(CoreValueObjects))]
    public void Core_value_objects_compare_unequal_to_null(object value)
    {
        Assert.False(value.Equals(null));
    }

    [Theory]
    [MemberData(nameof(DeprecatedGlobalTypes))]
    public void Migrating_global_entry_points_are_explicitly_deprecated(Type type)
    {
        var obsolete = type.GetCustomAttribute<ObsoleteAttribute>();

        Assert.NotNull(obsolete);
        Assert.Contains("before the next major version", obsolete!.Message);
    }

    public static TheoryData<Type> DeprecatedGlobalTypes => new()
    {
        typeof(DisposeUtils),
        typeof(DebugDrawMask),
        typeof(MarkerSystem),
        typeof(ReflectionInvokeUtils),
        typeof(MarkerBootstrapper<,>),
        typeof(KeyedMarkerBootstrapper<,,>),
        typeof(StaticMarkerBootstrapper<,,>),
    };

    public static TheoryData<object> CoreValueObjects => new()
    {
        new DebugDrawMask(1),
        new EventKey("test", typeof(int)),
        Quat.Identity,
        Transform3.Identity,
        Vec2.Zero,
        Vec3.Zero,
        new PoolConfigRequest(PoolRegistry.GlobalScopeName, typeof(object)),
    };

    private sealed class DisposableProbe : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
