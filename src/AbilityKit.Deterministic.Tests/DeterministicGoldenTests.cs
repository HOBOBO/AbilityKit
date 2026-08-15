using Xunit;

namespace AbilityKit.Deterministic.Tests;

/// <summary>
/// Bit-exact golden values for the integer-only algorithms. These lock the raw outputs so any
/// change that shifts a single bit of CORDIC / sqrt / hashing shows up in review and CI.
/// Cross-platform bit-identity (Mono / IL2CPP / .NET) is guaranteed by construction: every
/// operation involved is plain 64-bit integer arithmetic, identical on every runtime.
/// </summary>
public sealed class DeterministicGoldenTests
{
    [Theory]
    [InlineData(0.0, 1L)]
    [InlineData(0.5, 2059117009L)]
    [InlineData(1.0, 3614090358L)]
    [InlineData(1.5, 4284208344L)]
    [InlineData(3.14159, 11401L)]
    [InlineData(-0.75, -2927616182L)]
    [InlineData(-2.5, -2570418285L)]
    public void Sin_GoldenRawValue(double angle, long expectedRaw)
    {
        Assert.Equal(expectedRaw, DeterministicMath.Sin(Fixed64.FromDouble(angle)).RawValue);
    }

    [Theory]
    [InlineData(0.0, 4294967303L)]
    [InlineData(0.5, 3769188408L)]
    [InlineData(1.0, 2320580737L)]
    [InlineData(1.5, 303813972L)]
    [InlineData(3.14159, -4294967297L)]
    [InlineData(-0.75, 3142579757L)]
    [InlineData(-2.5, -3440885629L)]
    public void Cos_GoldenRawValue(double angle, long expectedRaw)
    {
        Assert.Equal(expectedRaw, DeterministicMath.Cos(Fixed64.FromDouble(angle)).RawValue);
    }

    [Theory]
    [InlineData(0.5, 2346351324L)]
    [InlineData(1.0, 6689015230L)]
    [InlineData(-0.75, -4001176335L)]
    public void Tan_GoldenRawValue(double angle, long expectedRaw)
    {
        Assert.Equal(expectedRaw, DeterministicMath.Tan(Fixed64.FromDouble(angle)).RawValue);
    }

    [Theory]
    [InlineData(0.5, 3037000500L)]
    [InlineData(2.0, 6074001000L)]
    [InlineData(9.0, 12884901888L)]
    [InlineData(123.456, 47721706134L)]
    [InlineData(1000000.0, 4294967296000L)]
    public void Sqrt_GoldenRawValue(double value, long expectedRaw)
    {
        Assert.Equal(expectedRaw, DeterministicMath.Sqrt(Fixed64.FromDouble(value)).RawValue);
    }

    [Theory]
    [InlineData(-0.99, -6138611439L)]
    [InlineData(-0.5, -2248839617L)]
    [InlineData(0.0, 0L)]
    [InlineData(0.3, 1308644985L)]
    [InlineData(0.75, 3642398893L)]
    [InlineData(1.0, 6746518852L)]
    public void Asin_GoldenRawValue(double value, long expectedRaw)
    {
        Assert.Equal(expectedRaw, DeterministicMath.Asin(Fixed64.FromDouble(value)).RawValue);
    }

    [Theory]
    [InlineData(-0.99, 12885130291L)]
    [InlineData(-0.5, 8995358469L)]
    [InlineData(0.0, 6746518852L)]
    [InlineData(0.3, 5437873867L)]
    [InlineData(0.75, 3104119959L)]
    [InlineData(1.0, 0L)]
    public void Acos_GoldenRawValue(double value, long expectedRaw)
    {
        Assert.Equal(expectedRaw, DeterministicMath.Acos(Fixed64.FromDouble(value)).RawValue);
    }

    [Fact]
    public void Atan2_And_Rounding_GoldenRawValues()
    {
        Assert.Equal(1991351315L, DeterministicMath.Atan2(Fixed64.One, Fixed64.FromInt64(2)).RawValue);
        Assert.Equal(-11501686390L, DeterministicMath.Atan2(Fixed64.FromInt64(-1), Fixed64.FromInt64(-2)).RawValue);

        var minusOneAndHalf = Fixed64.FromRatio(-3, 2);
        Assert.Equal(-8589934592L, DeterministicMath.Floor(minusOneAndHalf).RawValue);
        Assert.Equal(-4294967296L, DeterministicMath.Ceiling(minusOneAndHalf).RawValue);
        Assert.Equal(-4294967296L, DeterministicMath.Round(minusOneAndHalf).RawValue);
    }

    [Fact]
    public void Constants_GoldenRawValues()
    {
        Assert.Equal(13493037705L, DeterministicMath.Pi.RawValue);
        Assert.Equal(6746518852L, DeterministicMath.HalfPi.RawValue);
        Assert.Equal(11674931555L, DeterministicMath.E.RawValue);
    }

    [Fact]
    public void Hash_And_Geometry_GoldenRawValues()
    {
        Assert.Equal(634246865027890484L, DeterministicHash.Hash(Fixed64.One));
        Assert.Equal(
            -6950241901664833471L,
            DeterministicHash.Hash(new FixedVec3(Fixed64.One, Fixed64.FromInt64(-2), Fixed64.FromRatio(1, 2))));

        var diagonal = new FixedVec3(Fixed64.FromInt64(2), Fixed64.FromInt64(3), Fixed64.FromInt64(6));
        Assert.Equal(30064771072L, diagonal.Magnitude.RawValue);

        var normalized = new FixedVec2(Fixed64.FromInt64(3), Fixed64.FromInt64(4)).Normalized;
        Assert.Equal(2576980377L, normalized.X.RawValue);

        var rightAngle = FixedVec2.Angle(
            new FixedVec2(Fixed64.One, Fixed64.Zero),
            new FixedVec2(Fixed64.Zero, Fixed64.One));
        Assert.Equal(6746518852L, rightAngle.RawValue);
    }
}
