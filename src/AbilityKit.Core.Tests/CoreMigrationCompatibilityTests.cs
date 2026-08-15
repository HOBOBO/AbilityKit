#pragma warning disable CS0618 // Compatibility coverage for APIs retained until the next major version.
using System.Reflection;
using AbilityKit.Core.Debugging;
using AbilityKit.Core.Markers;
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
        typeof(MarkerBootstrapper<,>),
        typeof(KeyedMarkerBootstrapper<,,>),
        typeof(StaticMarkerBootstrapper<,,>),
    };

    private sealed class DisposableProbe : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
