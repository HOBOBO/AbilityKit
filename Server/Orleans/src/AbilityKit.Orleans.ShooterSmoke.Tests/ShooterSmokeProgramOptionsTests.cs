namespace AbilityKit.Orleans.ShooterSmoke.Tests;

using Xunit;

public sealed class ShooterSmokeProgramOptionsTests
{
    [Fact]
    public void Parse_DefaultsPayloadModeToTemplate()
    {
        var options = ShooterSmokeProgramOptions.Parse(Array.Empty<string>());

        Assert.Equal("template", options.StateSyncPayloadMode);
    }

    [Theory]
    [InlineData("packed", "packed")]
    [InlineData("pure-state", "pure-state")]
    [InlineData("purestate", "pure-state")]
    [InlineData("pure_state", "pure-state")]
    [InlineData("template", "template")]
    [InlineData("", "template")]
    public void Parse_NormalizesExplicitPayloadMode(string value, string expected)
    {
        var options = ShooterSmokeProgramOptions.Parse(
            ["--state-sync-payload-mode", value]);

        Assert.Equal(expected, options.StateSyncPayloadMode);
    }
}
