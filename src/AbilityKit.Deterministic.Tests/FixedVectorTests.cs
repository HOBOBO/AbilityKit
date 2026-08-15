using System;
using Xunit;

namespace AbilityKit.Deterministic.Tests;

public sealed class FixedVectorTests
{
    [Fact]
    public void Vec2_DotAndSqrMagnitude_UseFixedArithmetic()
    {
        var vector = new FixedVec2(Fixed64.FromInt64(3), Fixed64.FromInt64(4));

        Assert.Equal(Fixed64.FromInt64(25), vector.SqrMagnitude);
        Assert.Equal(Fixed64.FromInt64(25), FixedVec2.Dot(vector, vector));
    }

    [Fact]
    public void Vec3_Operators_ApplyPerComponent()
    {
        var left = new FixedVec3(Fixed64.One, Fixed64.FromInt64(2), Fixed64.FromInt64(3));
        var right = new FixedVec3(Fixed64.FromInt64(4), Fixed64.FromInt64(5), Fixed64.FromInt64(6));

        Assert.Equal(new FixedVec3(Fixed64.FromInt64(5), Fixed64.FromInt64(7), Fixed64.FromInt64(9)), left + right);
        Assert.Equal(new FixedVec3(Fixed64.FromInt64(2), Fixed64.FromInt64(4), Fixed64.FromInt64(6)), left * Fixed64.FromInt64(2));
        Assert.Equal(Fixed64.FromInt64(32), FixedVec3.Dot(left, right));
    }

    [Fact]
    public void Magnitude_IsExactForPerfectSquares()
    {
        var vec2 = new FixedVec2(Fixed64.FromInt64(3), Fixed64.FromInt64(4));
        var vec3 = new FixedVec3(Fixed64.FromInt64(2), Fixed64.FromInt64(3), Fixed64.FromInt64(6));

        Assert.Equal(Fixed64.FromInt64(5), vec2.Magnitude);
        Assert.Equal(Fixed64.FromInt64(7), vec3.Magnitude);
    }

    [Fact]
    public void Normalized_ProducesUnitVector_And_ZeroVectorStaysZero()
    {
        var normalized = new FixedVec2(Fixed64.FromInt64(3), Fixed64.FromInt64(4)).Normalized;

        Assert.True(Math.Abs(normalized.X.ToDouble() - 0.6) < 1e-8);
        Assert.True(Math.Abs(normalized.Y.ToDouble() - 0.8) < 1e-8);
        Assert.True(Math.Abs(normalized.Magnitude.ToDouble() - 1.0) < 1e-8);

        Assert.Equal(FixedVec3.Zero, FixedVec3.Zero.Normalized);
    }

    [Fact]
    public void Distance_MatchesCartesianDistance()
    {
        var a = new FixedVec2(Fixed64.One, Fixed64.One);
        var b = new FixedVec2(Fixed64.FromInt64(4), Fixed64.FromInt64(5));

        Assert.Equal(Fixed64.FromInt64(5), FixedVec2.Distance(a, b));
        Assert.Equal(Fixed64.Zero, FixedVec3.Distance(FixedVec3.One, FixedVec3.One));
    }

    [Fact]
    public void Cross_FollowsRightHandRule()
    {
        var unitX = new FixedVec3(Fixed64.One, Fixed64.Zero, Fixed64.Zero);
        var unitY = new FixedVec3(Fixed64.Zero, Fixed64.One, Fixed64.Zero);
        var unitZ = new FixedVec3(Fixed64.Zero, Fixed64.Zero, Fixed64.One);

        Assert.Equal(unitZ, FixedVec3.Cross(unitX, unitY));
        Assert.Equal(-unitZ, FixedVec3.Cross(unitY, unitX));
        Assert.Equal(Fixed64.One, FixedVec2.Cross(new FixedVec2(Fixed64.One, Fixed64.Zero), new FixedVec2(Fixed64.Zero, Fixed64.One)));
        Assert.Equal(Fixed64.FromInt64(-1), FixedVec2.Cross(new FixedVec2(Fixed64.Zero, Fixed64.One), new FixedVec2(Fixed64.One, Fixed64.Zero)));
    }

    [Fact]
    public void Angle_ReturnsQuarterTurnForPerpendicularVectors()
    {
        var unitX = new FixedVec3(Fixed64.One, Fixed64.Zero, Fixed64.Zero);
        var unitY = new FixedVec3(Fixed64.Zero, Fixed64.One, Fixed64.Zero);

        Assert.True(Math.Abs(FixedVec3.Angle(unitX, unitY).ToDouble() - Math.PI / 2.0) < 1e-6);
        Assert.Equal(Fixed64.Zero, FixedVec3.Angle(unitX, unitX));

        var vec2X = new FixedVec2(Fixed64.One, Fixed64.Zero);
        var vec2Y = new FixedVec2(Fixed64.Zero, Fixed64.One);
        Assert.True(Math.Abs(FixedVec2.Angle(vec2X, vec2Y).ToDouble() - Math.PI / 2.0) < 1e-6);
        Assert.True(Math.Abs(FixedVec2.Angle(vec2X, -vec2Y).ToDouble() + Math.PI / 2.0) < 1e-6);
    }

    [Fact]
    public void Angle_ZeroVector_Throws()
    {
        Assert.Throws<ArgumentException>(() => FixedVec3.Angle(FixedVec3.Zero, FixedVec3.One));
    }

    [Fact]
    public void Lerp_InterpolatesPerComponent()
    {
        var from = new FixedVec3(Fixed64.Zero, Fixed64.Zero, Fixed64.Zero);
        var to = new FixedVec3(Fixed64.FromInt64(10), Fixed64.FromInt64(20), Fixed64.FromInt64(30));
        var half = Fixed64.FromRatio(1, 2);

        Assert.Equal(new FixedVec3(Fixed64.FromInt64(5), Fixed64.FromInt64(10), Fixed64.FromInt64(15)), FixedVec3.Lerp(from, to, half));

        var clamped = FixedVec3.LerpClamped(from, to, Fixed64.FromInt64(2));
        Assert.Equal(to, clamped);
    }
}
