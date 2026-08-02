using AbilityKit.Ability.Triggering;
using Xunit;

namespace AbilityKit.Ability.Tests;

public sealed class TriggerEventTests
{
    [Fact]
    public void Constructor_sets_id() => Assert.Equal("test", new TriggerEvent("test").Id);

    [Fact]
    public void Constructor_null_id_throws() => Assert.Throws<ArgumentNullException>(() => new TriggerEvent(null!));
}
