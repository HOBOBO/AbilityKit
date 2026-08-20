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
    public void Stable_string_ids_return_non_null_output_when_id_is_unknown()
    {
        var registry = new StableStringIdRegistry();

        Assert.False(registry.TryGetName(123, out var name));
        Assert.Equal(string.Empty, name);
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
    public void Flat_settings_accept_null_dictionary_as_an_empty_compatibility_input()
    {
        var settings = new FlatJsonSettings(null);

        Assert.Empty(settings.Values);
    }

    [Fact]
    public void Flat_settings_return_non_null_string_output_when_conversion_fails()
    {
        var settings = new FlatJsonSettings(new Dictionary<string, object>
        {
            ["null-text"] = new NullStringValue(),
        });

        Assert.False(settings.TryGetString("missing", out var missing));
        Assert.Equal(string.Empty, missing);
        Assert.False(settings.TryGetString("null-text", out var nullText));
        Assert.Equal(string.Empty, nullText);
    }

    [Fact]
    public void Deprecated_configuration_models_have_safe_empty_defaults()
    {
        var module = new ModuleInstallerConfig();
        var modules = new ModuleInstallerConfigSet();

        Assert.Equal(string.Empty, module.ModuleKey);
        Assert.Equal(string.Empty, module.InstallerType);
        Assert.Equal(string.Empty, module.InstallerMethod);
        Assert.Empty(modules.Modules);
        Assert.Null(modules.FindModule("missing"));
    }

    [Fact]
    public void Layered_settings_use_null_change_key_for_wholesale_changes()
    {
        var settings = new LayeredJsonSettingsStore();
        string? changedKey = "unchanged";
        settings.OnChanged += key => changedKey = key;

        settings.ReplaceBase(null);

        Assert.Null(changedKey);
        Assert.False(settings.TryGetRaw("missing", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Persistent_config_loader_accepts_missing_inputs_without_null_forgiveness()
    {
        Assert.Null(PersistentJsonConfigLoader.TryLoad<object>(string.Empty, null));
        Assert.NotNull(PersistentJsonConfigLoader.LoadOrDefault<EmptyConfig>(string.Empty, null));
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
        Assert.False(registry.TryGet("missing", out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void Marker_system_registration_and_scanning_are_repeatable_and_snapshot_isolated()
    {
        MarkerSystem.Reset();
        try
        {
            var registry = new MarkerRegistry<TestMarkerAttribute>();
            var filterCalls = 0;
            Func<System.Reflection.Assembly, bool> filter = _ =>
            {
                filterCalls++;
                return true;
            };
            MarkerSystem.Register<TestMarkerAttribute, MarkerRegistry<TestMarkerAttribute>>(registry, filter);
            MarkerSystem.Register<TestMarkerAttribute, MarkerRegistry<TestMarkerAttribute>>(registry, filter);
            var snapshot = MarkerSystem.GetRegistrations();

            Assert.Equal(1, MarkerSystem.RegistrationCount);
            MarkerSystem.ScanAll(new[] { typeof(FoundationUtilityTests).Assembly });
            MarkerSystem.ScanAll(new[] { typeof(FoundationUtilityTests).Assembly });

            Assert.True(MarkerSystem.IsInitialized);
            Assert.Single(registry.Types);
            Assert.Single(snapshot);
            Assert.Equal(2, filterCalls);

            TestMarkerAttribute.ThrowOnScan = true;
            Assert.ThrowsAny<Exception>(
                () => MarkerSystem.ScanAll(new[] { typeof(FoundationUtilityTests).Assembly }));
            Assert.False(MarkerSystem.IsInitialized);
            TestMarkerAttribute.ThrowOnScan = false;

            MarkerSystem.Register<TestMarkerAttribute, MarkerRegistry<TestMarkerAttribute>>(
                new MarkerRegistry<TestMarkerAttribute>());

            Assert.False(MarkerSystem.IsInitialized);
            Assert.Single(snapshot);
            Assert.Equal(2, MarkerSystem.RegistrationCount);
        }
        finally
        {
            TestMarkerAttribute.ThrowOnScan = false;
            MarkerSystem.Reset();
        }
    }

    private sealed class TestMarkerAttribute : MarkerAttribute
    {
        public static bool ThrowOnScan { get; set; }

        public override void OnScanned(Type implType, IMarkerRegistry registry)
        {
            if (ThrowOnScan) throw new InvalidOperationException("scan failed");
            registry.Register(implType);
        }
    }

    [TestMarker]
    private sealed class MarkerImplementation
    {
    }

    private sealed class EmptyConfig
    {
    }

    private sealed class NullStringValue
    {
        public override string? ToString() => null;
    }
}
#pragma warning restore CS0618
