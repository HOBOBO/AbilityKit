#pragma warning disable CS0618
using System.Globalization;
using AbilityKit.Core.Configuration;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class LayeredJsonSettingsStoreTests
{
    [Fact]
    public void RemovingHigherLayersRevealsTheNextExistingValue()
    {
        var settings = new LayeredJsonSettingsStore();
        settings.ReplaceBase(new FlatJsonSettings(new Dictionary<string, object> { ["value"] = 1 }));
        settings.ReplacePersistent(new FlatJsonSettings(new Dictionary<string, object> { ["value"] = 2 }));
        settings.SetOverride("value", 3);
        Assert.True(settings.TryGetInt("value", out var overridden));
        Assert.Equal(3, overridden);
        Assert.True(settings.ClearOverride("value"));
        Assert.True(settings.TryGetInt("value", out var persistent));
        Assert.Equal(2, persistent);
        settings.ReplacePersistent(null);
        Assert.True(settings.TryGetInt("value", out var baseline));
        Assert.Equal(1, baseline);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HigherLayerNullOrUnsupportedValueShadowsLowerValues(bool useOverride, bool useNull)
    {
        var settings = new LayeredJsonSettingsStore();
        settings.ReplaceBase(new FlatJsonSettings(new Dictionary<string, object> { ["value"] = 1 }));
        object shadow = useNull ? null! : new NullTextValue();
        if (useOverride)
        {
            settings.ReplacePersistent(new FlatJsonSettings(new Dictionary<string, object> { ["value"] = 2 }));
            settings.SetOverride("value", shadow);
        }
        else
        {
            settings.ReplacePersistent(new FlatJsonSettings(new Dictionary<string, object> { ["value"] = shadow }));
        }

        Assert.True(settings.TryGetRaw("value", out var raw));
        Assert.Same(shadow, raw);
        Assert.False(settings.TryGetBool("value", out var boolean));
        Assert.False(boolean);
        Assert.False(settings.TryGetInt("value", out var integer));
        Assert.Equal(0, integer);
        Assert.False(settings.TryGetFloat("value", out var floating));
        Assert.Equal(0f, floating);
        Assert.False(settings.TryGetString("value", out var text));
        Assert.Equal(string.Empty, text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("missing")]
    public void MissingOrEmptyKeysReturnFailureAndDefaultOutputs(string? key)
    {
        var settings = new LayeredJsonSettingsStore();
        Assert.False(settings.TryGetRaw(key!, out var raw));
        Assert.Null(raw);
        Assert.False(settings.TryGetBool(key!, out var boolean));
        Assert.False(boolean);
        Assert.False(settings.TryGetInt(key!, out var integer));
        Assert.Equal(0, integer);
        Assert.False(settings.TryGetFloat(key!, out var floating));
        Assert.Equal(0f, floating);
        Assert.False(settings.TryGetString(key!, out var text));
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void ScalarConversionsPreserveThresholdsRangesAndTruncation()
    {
        var settings = new LayeredJsonSettingsStore();
        settings.SetOverride("small-float", 1e-7f);
        settings.SetOverride("small-double", 1e-10d);
        settings.SetOverride("integer-overflow", (long)int.MaxValue + 1);
        settings.SetOverride("fraction", -3.75d);
        settings.SetOverride("boolean", true);
        settings.SetOverride("empty-text", string.Empty);
        Assert.True(settings.TryGetBool("small-float", out var smallFloat));
        Assert.False(smallFloat);
        Assert.True(settings.TryGetBool("small-double", out var smallDouble));
        Assert.True(smallDouble);
        Assert.False(settings.TryGetInt("integer-overflow", out var overflow));
        Assert.Equal(0, overflow);
        Assert.True(settings.TryGetInt("fraction", out var truncated));
        Assert.Equal(-3, truncated);
        Assert.True(settings.TryGetInt("boolean", out var booleanInteger));
        Assert.Equal(1, booleanInteger);
        Assert.True(settings.TryGetFloat("fraction", out var fraction));
        Assert.Equal(-3.75f, fraction);
        Assert.False(settings.TryGetFloat("boolean", out var booleanFloat));
        Assert.Equal(0f, booleanFloat);
        Assert.True(settings.TryGetString("empty-text", out var emptyText));
        Assert.Equal(string.Empty, emptyText);
    }

    [Fact]
    public void ParsingAndFormattingRetainTheCurrentCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var settings = new LayeredJsonSettingsStore();
            settings.SetOverride("text", "1,5");
            settings.SetOverride("number", 1.5d);
            Assert.True(settings.TryGetFloat("text", out var value));
            Assert.Equal(1.5f, value);
            Assert.True(settings.TryGetString("number", out var text));
            Assert.Equal("1,5", text);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void EffectiveViewIsAnIndependentShallowSnapshot()
    {
        var sharedValue = new object();
        var settings = new LayeredJsonSettingsStore();
        settings.ReplaceBase(new FlatJsonSettings(new Dictionary<string, object> { ["shared"] = sharedValue, ["value"] = 1 }));
        settings.SetOverride("value", 2);
        var snapshot = settings.BuildEffectiveView();
        Assert.Same(sharedValue, snapshot["shared"]);
        settings.SetOverride("value", 3);
        snapshot["shared"] = new object();
        Assert.Equal(2, snapshot["value"]);
        Assert.True(settings.TryGetInt("value", out var current));
        Assert.Equal(3, current);
        Assert.True(settings.TryGetRaw("shared", out var original));
        Assert.Same(sharedValue, original);
    }

    [Fact]
    public void ChangeNotificationsPreserveSingleKeyAndWholeLayerSemantics()
    {
        var settings = new LayeredJsonSettingsStore();
        var keys = new List<string?>();
        settings.OnChanged += keys.Add;
        settings.ReplaceBase(null);
        settings.ReplacePersistent(null);
        settings.ClearOverrides();
        settings.SetOverride("value", 1);
        settings.SetOverride("value", 1);
        Assert.False(settings.ClearOverride("missing"));
        Assert.True(settings.ClearOverride("value"));
        Assert.False(settings.ClearOverride("value"));
        settings.SetOverride("value", 2);
        settings.ClearOverrides();
        settings.ClearOverrides();
        settings.SetOverride(string.Empty, 1);
        settings.TryGetInt("missing", out _);
        Assert.Equal(new string?[] { null, null, "value", "value", "value", "value", null }, keys);
    }

    [Fact]
    public void ChangeNotificationFailureDoesNotRollBackCommittedValue()
    {
        var settings = new LayeredJsonSettingsStore();
        var failure = new InvalidOperationException("notification failed");
        settings.OnChanged += _ => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => settings.SetOverride("value", 7)));
        Assert.True(settings.TryGetInt("value", out var value));
        Assert.Equal(7, value);
    }

    [Fact]
    public void ScalarReadsDoNotAllocateMergedDictionaries()
    {
        var settings = new LayeredJsonSettingsStore();
        settings.SetOverride("value", 1);
        settings.SetOverride("text", "ready");
        for (var index = 0; index < 64; index++)
        {
            settings.TryGetBool("value", out _);
            settings.TryGetInt("value", out _);
            settings.TryGetFloat("value", out _);
            settings.TryGetString("text", out _);
        }

        var valid = true;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++)
        {
            valid &= settings.TryGetBool("value", out var boolean) && boolean;
            valid &= settings.TryGetInt("value", out var integer) && integer == 1;
            valid &= settings.TryGetFloat("value", out var floating) && floating == 1f;
            valid &= settings.TryGetString("text", out var text) && text == "ready";
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.True(valid);
        Assert.Equal(0L, allocated);
    }

    private sealed class NullTextValue
    {
        public override string? ToString() => null;
    }
}
#pragma warning restore CS0618
