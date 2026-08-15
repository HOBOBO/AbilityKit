using AbilityKit.Demo.Moba.View.Settings;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Settings.Tests;

public sealed class MobaViewSettingsTests
{
    [Fact]
    public void Layered_settings_use_override_persistent_base_precedence()
    {
        var settings = new LayeredJsonSettingsStore();
        settings.ReplaceBase(new FlatJsonSettings(new Dictionary<string, object>
        {
            ["quality"] = "base",
            ["enabled"] = false,
        }));
        settings.ReplacePersistent(new FlatJsonSettings(new Dictionary<string, object>
        {
            ["quality"] = "persistent",
        }));
        settings.SetOverride("quality", "override");

        Assert.True(settings.TryGetString("quality", out var quality));
        Assert.Equal("override", quality);
        Assert.True(settings.TryGetBool("enabled", out var enabled));
        Assert.False(enabled);

        Assert.True(settings.ClearOverride("quality"));
        Assert.True(settings.TryGetString("quality", out quality));
        Assert.Equal("persistent", quality);
    }

    [Fact]
    public void Json_settings_files_round_trip_through_supplied_codecs()
    {
        var path = Path.Combine(Path.GetTempPath(), $"abilitykit-settings-{Guid.NewGuid():N}.json");
        try
        {
            Assert.True(JsonSettingsFiles.TrySaveOverrides(
                path,
                new Dictionary<string, object> { ["count"] = 3 },
                values => values["count"].ToString()!));

            var settings = JsonSettingsFiles.LoadFlatOrEmpty(
                path,
                text => new Dictionary<string, object> { ["count"] = int.Parse(text) });

            Assert.True(settings.TryGetInt("count", out var count));
            Assert.Equal(3, count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
