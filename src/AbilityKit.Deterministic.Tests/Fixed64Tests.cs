using Xunit;

namespace AbilityKit.Deterministic.Tests;

public sealed class Fixed64Tests
{
    [Fact]
    public void FromRaw_RoundTripsRawValue()
    {
        var value = Fixed64.FromRaw(123456789);

        Assert.Equal(123456789, value.RawValue);
    }

    [Fact]
    public void FromInt64_ShiftsIntoQ32_32RawValue()
    {
        var value = Fixed64.FromInt64(3);

        Assert.Equal(3L * Fixed64.OneRaw, value.RawValue);
        Assert.Equal(3, value.ToInt64());
    }

    [Fact]
    public void FromRatio_UsesTruncationTowardZero()
    {
        var positive = Fixed64.FromRatio(1, 3);
        var negative = Fixed64.FromRatio(-1, 3);

        Assert.Equal(1431655765, positive.RawValue);
        Assert.Equal(-1431655765, negative.RawValue);
    }

    [Fact]
    public void Arithmetic_UsesFixedPointScale()
    {
        var oneAndHalf = Fixed64.FromRatio(3, 2);
        var two = Fixed64.FromInt64(2);

        Assert.Equal(Fixed64.FromRatio(7, 2), oneAndHalf + two);
        Assert.Equal(Fixed64.FromRatio(1, 2), two - oneAndHalf);
        Assert.Equal(Fixed64.FromInt64(3), oneAndHalf * two);
        Assert.Equal(Fixed64.FromRatio(3, 4), oneAndHalf / two);
    }

    [Fact]
    public void DecimalBoundaryConversion_RoundTripsExpectedValue()
    {
        var value = Fixed64.FromDecimal(12.25m);

        Assert.Equal(12.25m, value.ToDecimal());
        Assert.Equal("12.25", value.ToString());
    }

    [Fact]
    public void DivideByZero_Throws()
    {
        Assert.Throws<DivideByZeroException>(() => Fixed64.One / Fixed64.Zero);
        Assert.Throws<DivideByZeroException>(() => Fixed64.FromRatio(1, 0));
    }
}
