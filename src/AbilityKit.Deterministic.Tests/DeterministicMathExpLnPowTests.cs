using System;
using Xunit;

namespace AbilityKit.Deterministic.Tests;

/// <summary>
/// Exp/Ln/Pow contracts: accuracy against System.Math within a few fixed-point ULP,
/// edge/domain behavior (saturation, fail-loud throws, integer-exponent fast path),
/// and golden raw bit locks that pin the integer algorithms across runtimes.
/// </summary>
public sealed class DeterministicMathExpLnPowTests
{
    private static Fixed64 F(double value) => Fixed64.FromDouble(value);

    private static double Rel(double expected, double actual)
    {
        return Math.Abs(expected - actual) / Math.Max(1e-300, Math.Abs(expected));
    }

    [Fact]
    public void Exp_MatchesSystemMathAcrossRange()
    {
        for (var x = -22.5; x <= 21.0; x += 0.37)
        {
            var expectedRaw = Math.Exp(x) * 4294967296.0;
            var actualRaw = (double)DeterministicMath.Exp(F(x)).RawValue;
            // A few ULP relative for large results, a few raw units absolute for
            // small ones; below one raw unit the result legitimately rounds to 0 or 1.
            var tolerance = Math.Max(8.0, Math.Abs(expectedRaw) * 5e-9);
            Assert.True(Math.Abs(expectedRaw - actualRaw) <= tolerance,
                $"Exp({x}): expected raw {expectedRaw}, got raw {actualRaw}");
        }
    }

    [Fact]
    public void Exp_SaturatesBelowFixedPointResolution()
    {
        Assert.Equal(Fixed64.Zero, DeterministicMath.Exp(F(-24.0)));
        // exp(-22.87) is the first input whose raw value rounds to 0; -22 stays >= 1.
        Assert.True(DeterministicMath.Exp(F(-22.0)).RawValue >= 1L);
    }

    [Fact]
    public void Exp_OverflowThrows()
    {
        Assert.Throws<OverflowException>(() => DeterministicMath.Exp(F(22.0)));
    }

    [Fact]
    public void Ln_MatchesSystemMathAcrossRange()
    {
        foreach (var x in new[] { 2.5e-10, 1e-5, 0.25, 0.5, 1.0, 1.5, 2.0, 3.7, 10.0, 133.7, 1e6, 2.1e9 })
        {
            // Compare against the log of the quantized input, not of x itself.
            var quantized = F(x).ToDouble();
            var expected = Math.Log(quantized);
            var actual = DeterministicMath.Ln(F(x)).ToDouble();
            Assert.True(Math.Abs(expected - actual) < 5e-8,
                $"Ln({x} -> {quantized}): expected {expected}, got {actual}");
        }
    }

    [Fact]
    public void Ln_NonPositiveInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMath.Ln(Fixed64.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMath.Ln(F(-1.0)));
    }

    [Fact]
    public void Ln_OfE_IsOne()
    {
        Assert.Equal(Fixed64.One, DeterministicMath.Ln(DeterministicMath.E));
    }

    [Fact]
    public void Pow_IntegerExponent_UsesExactFastPath()
    {
        Assert.Equal(F(1024.0), DeterministicMath.Pow(F(2.0), F(10.0)));
        Assert.Equal(F(0.5), DeterministicMath.Pow(F(2.0), F(-1.0)));
        Assert.Equal(F(-8.0), DeterministicMath.Pow(F(-2.0), F(3.0)));
        Assert.Equal(F(16.0), DeterministicMath.Pow(F(-2.0), F(4.0)));
        // x^1 and x^2 stay bit-identical to direct arithmetic.
        var x = F(3.75);
        Assert.Equal(x, DeterministicMath.Pow(x, Fixed64.One));
        Assert.Equal(x * x, DeterministicMath.Pow(x, F(2.0)));
        Assert.Equal(Fixed64.One, DeterministicMath.Pow(F(7.3), Fixed64.Zero));
    }

    [Fact]
    public void Pow_ZeroBase_Contracts()
    {
        Assert.Equal(Fixed64.Zero, DeterministicMath.Pow(Fixed64.Zero, F(3.0)));
        Assert.Equal(Fixed64.One, DeterministicMath.Pow(Fixed64.Zero, Fixed64.Zero));
        Assert.Throws<DivideByZeroException>(() => DeterministicMath.Pow(Fixed64.Zero, F(-2.0)));
    }

    [Fact]
    public void Pow_NegativeBaseWithFractionalExponent_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMath.Pow(F(-2.0), F(0.5)));
    }

    [Fact]
    public void Pow_IntegerExponentOutsideRange_Throws()
    {
        Assert.Throws<OverflowException>(() => DeterministicMath.Pow(F(2.0), F(63.0)));
        Assert.Throws<OverflowException>(() => DeterministicMath.Pow(F(2.0), F(-63.0)));
    }

    [Fact]
    public void Pow_FractionalExponent_MatchesSystemMath()
    {
        foreach (var (baseValue, exponent) in new[]
        {
            (2.0, 0.5), (10.0, 1.5), (4.0, 0.25), (0.5, 0.5), (0.9, 100.0), (1.5, 7.5),
            (2.0, 0.3333333333), (12345.678, 0.1),
        })
        {
            // Compare in raw units against the quantized inputs: relative tolerances
            // explode for small results where one raw ULP is already a large relative step.
            var quantizedBase = F(baseValue).ToDouble();
            var quantizedExponent = F(exponent).ToDouble();
            var expectedRaw = Math.Pow(quantizedBase, quantizedExponent) * 4294967296.0;
            var actualRaw = (double)DeterministicMath.Pow(F(baseValue), F(exponent)).RawValue;
            var tolerance = Math.Max(8.0, Math.Abs(expectedRaw) * 5e-9);
            Assert.True(Math.Abs(expectedRaw - actualRaw) <= tolerance,
                $"Pow({baseValue}, {exponent}): expected raw {expectedRaw}, got raw {actualRaw}");
        }
    }

    [Fact]
    public void GoldenRawBits_HoldAcrossRuntimes()
    {
        var cases = GoldenRawCases();
        Assert.Equal(cases.Length, GoldenRawExpected.Length);
        var mismatches = new System.Text.StringBuilder();
        for (var i = 0; i < cases.Length; i++)
        {
            var actual = cases[i].Op switch
            {
                GoldenOp.Exp => DeterministicMath.Exp(Fixed64.FromRaw(cases[i].RawA)).RawValue,
                GoldenOp.Ln => DeterministicMath.Ln(Fixed64.FromRaw(cases[i].RawA)).RawValue,
                _ => DeterministicMath.Pow(Fixed64.FromRaw(cases[i].RawA), Fixed64.FromRaw(cases[i].RawB)).RawValue,
            };
            if (actual != GoldenRawExpected[i])
                mismatches.AppendLine($"golden[{i}] ({cases[i].Op} raw {cases[i].RawA}/{cases[i].RawB}): expected {GoldenRawExpected[i]}, got {actual}");
        }

        Assert.True(mismatches.Length == 0, "exp/ln/pow golden raw drift: " + mismatches);
    }

    private enum GoldenOp
    {
        Exp,
        Ln,
        Pow,
    }

    private readonly struct GoldenCase
    {
        public GoldenCase(GoldenOp op, long rawA, long rawB = 0)
        {
            Op = op;
            RawA = rawA;
            RawB = rawB;
        }

        public GoldenOp Op { get; }
        public long RawA { get; }
        public long RawB { get; }
    }

    private static GoldenCase[] GoldenRawCases()
    {
        return new[]
        {
            new GoldenCase(GoldenOp.Exp, Fixed64.Zero.RawValue),                       // exp(0) = 1
            new GoldenCase(GoldenOp.Exp, Fixed64.One.RawValue),                        // exp(1)
            new GoldenCase(GoldenOp.Exp, -Fixed64.One.RawValue),                       // exp(-1)
            new GoldenCase(GoldenOp.Exp, Fixed64.FromDouble(-2.5).RawValue),
            new GoldenCase(GoldenOp.Exp, Fixed64.FromDouble(21.4).RawValue),
            new GoldenCase(GoldenOp.Exp, Fixed64.FromDouble(-23.0).RawValue),
            new GoldenCase(GoldenOp.Ln, Fixed64.One.RawValue),                         // ln(1) = 0
            new GoldenCase(GoldenOp.Ln, Fixed64.FromInt64(2).RawValue),
            new GoldenCase(GoldenOp.Ln, Fixed64.FromDouble(0.5).RawValue),
            new GoldenCase(GoldenOp.Ln, Fixed64.FromInt64(10).RawValue),
            new GoldenCase(GoldenOp.Ln, 1L),                                           // ln(2^-32)
            new GoldenCase(GoldenOp.Pow, Fixed64.FromInt64(2).RawValue, Fixed64.FromInt64(10).RawValue),
            new GoldenCase(GoldenOp.Pow, Fixed64.FromInt64(2).RawValue, -Fixed64.One.RawValue),
            new GoldenCase(GoldenOp.Pow, Fixed64.FromDouble(0.5).RawValue, Fixed64.FromInt64(3).RawValue),
            new GoldenCase(GoldenOp.Pow, Fixed64.FromInt64(2).RawValue, Fixed64.FromDouble(0.5).RawValue),
            new GoldenCase(GoldenOp.Pow, Fixed64.FromInt64(10).RawValue, Fixed64.FromDouble(1.5).RawValue),
        };
    }

    // Locked from the .NET implementation (2026-10-08); Unity/Mono/IL2CPP must match
    // these raw values bit-for-bit. Rebake both sides together when the algorithm changes.
    private static readonly long[] GoldenRawExpected =
    {
        4294967296L,        // Exp(0) = 1
        11674931552L,       // Exp(1) ~ e (3 raw off the E constant, series rounding)
        1580030169L,        // Exp(-1)
        352552385L,         // Exp(-2.5)
        8450098539970691072L, // Exp(21.4), near the top of the representable range
        0L,                 // Exp(-23), saturates below fixed-point resolution
        0L,                 // Ln(1) = 0
        2977044472L,        // Ln(2) = the ln(2) raw constant exactly
        -2977044472L,       // Ln(0.5)
        9889527670L,        // Ln(10)
        -95265423104L,      // Ln(2^-32) = -32*ln(2)
        4398046511104L,     // Pow(2, 10) = 1024 exactly
        2147483648L,        // Pow(2, -1) = 0.5 exactly
        536870912L,         // Pow(0.5, 3) = 0.125 exactly
        6074001000L,        // Pow(2, 0.5) ~ sqrt(2)
        135818791264L,      // Pow(10, 1.5)
    };
}
