using Xunit;

namespace AbilityKit.Deterministic.Tests;

public sealed class DeterministicRandomTests
{
    [Fact]
    public void SameSeed_ProducesSameUInt64Sequence()
    {
        var first = new DeterministicRandom(42);
        var second = new DeterministicRandom(42);

        Assert.Equal(first.NextUInt64(), second.NextUInt64());
        Assert.Equal(first.NextUInt64(), second.NextUInt64());
        Assert.Equal(first.NextUInt64(), second.NextUInt64());
        Assert.Equal(3UL, first.Sequence);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentUInt64Sequence()
    {
        var first = new DeterministicRandom(42);
        var second = new DeterministicRandom(43);

        Assert.NotEqual(first.NextUInt64(), second.NextUInt64());
    }

    [Fact]
    public void NextFixed01_ReturnsValueWithinUnitRange()
    {
        var random = new DeterministicRandom(42);

        for (var i = 0; i < 64; i++)
        {
            var value = random.NextFixed01();
            Assert.True(value >= Fixed64.Zero);
            Assert.True(value < Fixed64.One);
        }
    }

    [Fact]
    public void NextInt32_ReturnsValueWithinRange()
    {
        var random = new DeterministicRandom(42);

        for (var i = 0; i < 64; i++)
        {
            var value = random.NextInt32(-5, 7);
            Assert.InRange(value, -5, 6);
        }
    }
}
