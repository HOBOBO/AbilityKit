using AbilityKit.Pipeline;
using Xunit;

namespace AbilityKit.Pipeline.Tests;

public sealed class AbilityPipelinePhaseIdTests
{
    [Fact]
    public void Constructor_sets_value()
    {
        var id = new AbilityPipelinePhaseId("combat_resolve");
        Assert.Equal("combat_resolve", id.Value);
    }

    [Fact]
    public void Default_is_null() => Assert.Null(default(AbilityPipelinePhaseId).Value);
}
