using AbilityKit.Combat.Projectile;
using Xunit;

namespace AbilityKit.Combat.Projectile.Tests;

public sealed class ProjectileIdTests
{
    [Fact]
    public void Constructor_sets_value()
    {
        var id = new ProjectileId(42);
        Assert.Equal(42, id.Value);
    }

    [Fact]
    public void Default_is_zero() => Assert.Equal(0, default(ProjectileId).Value);
}
