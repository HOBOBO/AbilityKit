#pragma warning disable CS0618 // Compatibility coverage for deprecated Core configuration APIs.
using AbilityKit.Core.Configuration;
using AbilityKit.Core.Identifiers;
using AbilityKit.Core.Markers;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class FoundationUtilityTests
{
    [Fact]
    public void Stable_string_ids_are_repeatable_and_reversible()
    {
        var first = new StableStringIdRegistry();
        var second = new StableStringIdRegistry();

        var firstId = first.GetOrRegister("ability.cast");
        var secondId = second.GetOrRegister("ability.cast");

        Assert.Equal(firstId, secondId);
        Assert.True(first.TryGetName(firstId, out var name));
        Assert.Equal("ability.cast", name);
        Assert.True(first.TryGetId(name, out var roundTrip));
        Assert.Equal(firstId, roundTrip);
    }

    [Fact]
    public void Flat_settings_convert_supported_scalar_representations()
    {
        var settings = new FlatJsonSettings(new Dictionary<string, object>
        {
            ["enabled"] = "true",
            ["count"] = 4L,
            ["rate"] = 1.5d,
        });

        Assert.True(settings.TryGetBool("enabled", out var enabled) && enabled);
        Assert.True(settings.TryGetInt("count", out var count));
        Assert.Equal(4, count);
        Assert.True(settings.TryGetFloat("rate", out var rate));
        Assert.Equal(1.5f, rate);
    }

    [Fact]
    public void Keyed_marker_registry_replaces_key_without_duplicating_type_catalog()
    {
        var registry = new KeyedMarkerRegistry<string, TestMarkerAttribute>();

        registry.Register("test", typeof(MarkerImplementation));
        registry.Register("test", typeof(MarkerImplementation));

        Assert.Equal(1, registry.Count);
        Assert.Single(registry.Types);
        Assert.Equal(typeof(MarkerImplementation), registry.Get("test"));
    }

    private sealed class TestMarkerAttribute : MarkerAttribute
    {
    }

    private sealed class MarkerImplementation
    {
    }
}
#pragma warning restore CS0618
