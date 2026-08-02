using AbilityKit.Combat;
using Xunit;

namespace AbilityKit.Combat.Damage.Tests;

public sealed class DamageRequestTests
{
    [Fact]
    public void Struct_default_is_all_null()
    {
        var d = default(DamageRequest);
        Assert.Null(d.Source);
        Assert.Null(d.Attacker);
    }
}
