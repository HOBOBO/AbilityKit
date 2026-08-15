using System;
using Xunit;

namespace AbilityKit.Deterministic.Tests;

public sealed class DeterministicMathTests
{
        [Fact]
        public void Sqrt_IsExactForPerfectSquares()
        {
            Assert.Equal(Fixed64.Zero, DeterministicMath.Sqrt(Fixed64.Zero));
            Assert.Equal(Fixed64.One, DeterministicMath.Sqrt(Fixed64.One));
            Assert.Equal(Fixed64.FromInt64(2), DeterministicMath.Sqrt(Fixed64.FromInt64(4)));
            Assert.Equal(Fixed64.FromInt64(3), DeterministicMath.Sqrt(Fixed64.FromInt64(9)));
            Assert.Equal(Fixed64.FromInt64(1000), DeterministicMath.Sqrt(Fixed64.FromInt64(1000000)));
        }

        [Fact]
        public void Sqrt_MatchesSystemMathWithinTolerance()
        {
            var values = new[] { 0.5, 1.5, 2.0, 3.0, 10.0, 133.7, 1000.0, 1048576.0 };
            foreach (var value in values)
            {
                var expected = Math.Sqrt(value);
                var actual = DeterministicMath.Sqrt(Fixed64.FromDouble(value)).ToDouble();
                Assert.True(Math.Abs(expected - actual) < 1e-8, $"Sqrt({value}): expected {expected}, got {actual}");
            }
        }

        [Fact]
        public void Sqrt_NegativeInput_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMath.Sqrt(Fixed64.FromInt64(-1)));
        }

        [Fact]
        public void Floor_Ceiling_Round_HandleNegativeFractions()
        {
            var minusOneAndHalf = Fixed64.FromRatio(-3, 2);
            var oneAndHalf = Fixed64.FromRatio(3, 2);

            Assert.Equal(Fixed64.FromInt64(-2), DeterministicMath.Floor(minusOneAndHalf));
            Assert.Equal(Fixed64.FromInt64(-1), DeterministicMath.Ceiling(minusOneAndHalf));
            Assert.Equal(Fixed64.FromInt64(-1), DeterministicMath.Round(minusOneAndHalf));

            Assert.Equal(Fixed64.FromInt64(1), DeterministicMath.Floor(oneAndHalf));
            Assert.Equal(Fixed64.FromInt64(2), DeterministicMath.Ceiling(oneAndHalf));
            Assert.Equal(Fixed64.FromInt64(2), DeterministicMath.Round(oneAndHalf));

            // Integral values stay put.
            Assert.Equal(Fixed64.FromInt64(-7), DeterministicMath.Floor(Fixed64.FromInt64(-7)));
            Assert.Equal(Fixed64.FromInt64(-7), DeterministicMath.Ceiling(Fixed64.FromInt64(-7)));
            Assert.Equal(Fixed64.FromInt64(-7), DeterministicMath.Round(Fixed64.FromInt64(-7)));
        }

        [Fact]
        public void SinCos_MatchSystemMathAcrossPeriods()
        {
            var step = Math.PI / 17.0;
            for (var angle = -6.5 * Math.PI; angle <= 6.5 * Math.PI; angle += step)
            {
                var fixedAngle = Fixed64.FromDouble(angle);
                var sinError = Math.Abs(DeterministicMath.Sin(fixedAngle).ToDouble() - Math.Sin(angle));
                var cosError = Math.Abs(DeterministicMath.Cos(fixedAngle).ToDouble() - Math.Cos(angle));
                Assert.True(sinError < 1e-8, $"Sin({angle}): error {sinError}");
                Assert.True(cosError < 1e-8, $"Cos({angle}): error {cosError}");
            }
        }

        [Fact]
        public void SinCos_HaveAccurateSpecialValues()
        {
            Assert.True(Math.Abs(DeterministicMath.Sin(Fixed64.Zero).ToDouble()) < 1e-8);
            Assert.True(Math.Abs(DeterministicMath.Cos(Fixed64.Zero).ToDouble() - 1.0) < 1e-8);
            Assert.True(Math.Abs(DeterministicMath.Sin(DeterministicMath.HalfPi).ToDouble() - 1.0) < 1e-8);
            Assert.True(Math.Abs(DeterministicMath.Cos(DeterministicMath.HalfPi).ToDouble()) < 1e-8);
            Assert.True(Math.Abs(DeterministicMath.Sin(-DeterministicMath.HalfPi).ToDouble() + 1.0) < 1e-8);
            Assert.True(Math.Abs(DeterministicMath.Sin(DeterministicMath.Pi).ToDouble()) < 1e-8);
        }

        [Fact]
        public void SinCos_SatisfyPythagoreanIdentity()
        {
            var step = Math.PI / 23.0;
            for (var angle = -Math.PI; angle <= Math.PI; angle += step)
            {
                var sin = DeterministicMath.Sin(Fixed64.FromDouble(angle));
                var cos = DeterministicMath.Cos(Fixed64.FromDouble(angle));
                var sum = (sin * sin) + (cos * cos);
                Assert.True(Math.Abs(sum.ToDouble() - 1.0) < 1e-7, $"sin^2+cos^2 at {angle}: {sum}");
            }
        }

        [Fact]
        public void Tan_MatchesSystemMath()
        {
            foreach (var angle in new[] { 0.25, -0.5, 1.0, -1.25, 2.0 })
            {
                var expected = Math.Tan(angle);
                var actual = DeterministicMath.Tan(Fixed64.FromDouble(angle)).ToDouble();
                Assert.True(Math.Abs(expected - actual) < 1e-7, $"Tan({angle}): expected {expected}, got {actual}");
            }
        }

        [Fact]
        public void Atan2_CoversAllQuadrantsAndAxes()
        {
            Assert.True(Math.Abs(DeterministicMath.Atan2(Fixed64.FromRatio(1, 2), Fixed64.One).ToDouble() - Math.Atan2(0.5, 1.0)) < 1e-7);
            Assert.True(Math.Abs(DeterministicMath.Atan2(Fixed64.One, Fixed64.FromInt64(-2)).ToDouble() - Math.Atan2(1.0, -2.0)) < 1e-7);
            Assert.True(Math.Abs(DeterministicMath.Atan2(Fixed64.FromInt64(-1), Fixed64.FromInt64(-2)).ToDouble() - Math.Atan2(-1.0, -2.0)) < 1e-7);
            Assert.True(Math.Abs(DeterministicMath.Atan2(Fixed64.FromInt64(-1), Fixed64.FromInt64(2)).ToDouble() - Math.Atan2(-1.0, 2.0)) < 1e-7);

            // Axis conventions.
            Assert.Equal(Fixed64.Zero, DeterministicMath.Atan2(Fixed64.Zero, Fixed64.One));
            Assert.Equal(DeterministicMath.Pi, DeterministicMath.Atan2(Fixed64.Zero, Fixed64.FromInt64(-1)));
            Assert.Equal(DeterministicMath.HalfPi, DeterministicMath.Atan2(Fixed64.One, Fixed64.Zero));
            Assert.Equal(-DeterministicMath.HalfPi, DeterministicMath.Atan2(Fixed64.FromInt64(-1), Fixed64.Zero));
        }

        [Fact]
        public void Atan2_MatchesSystemMathOnGrid()
        {
            var coords = new[] { -3.0, -1.0, -0.25, 0.5, 2.0, 7.5 };
            foreach (var y in coords)
            {
                foreach (var x in coords)
                {
                    var expected = Math.Atan2(y, x);
                    var actual = DeterministicMath.Atan2(Fixed64.FromDouble(y), Fixed64.FromDouble(x)).ToDouble();
                    Assert.True(Math.Abs(expected - actual) < 1e-7, $"Atan2({y}, {x}): expected {expected}, got {actual}");
                }
            }
        }

        [Fact]
        public void Atan2_RoundTripsWithSinCos()
        {
            var step = Math.PI / 13.0;
            for (var angle = -Math.PI + 0.01; angle <= Math.PI - 0.01; angle += step)
            {
                var fixedAngle = Fixed64.FromDouble(angle);
                var reconstructed = DeterministicMath.Atan2(DeterministicMath.Sin(fixedAngle), DeterministicMath.Cos(fixedAngle));
                Assert.True(Math.Abs(reconstructed.ToDouble() - angle) < 1e-6, $"Round-trip at {angle}: {reconstructed}");
            }
        }

        [Fact]
        public void Asin_Acos_MatchSystemMathAndGuardDomain()
        {
            foreach (var value in new[] { -0.99, -0.5, -0.1, 0.0, 0.3, 0.75, 1.0 })
            {
                var asinError = Math.Abs(DeterministicMath.Asin(Fixed64.FromDouble(value)).ToDouble() - Math.Asin(value));
                var acosError = Math.Abs(DeterministicMath.Acos(Fixed64.FromDouble(value)).ToDouble() - Math.Acos(value));
                Assert.True(asinError < 1e-6, $"Asin({value}): error {asinError}");
                Assert.True(acosError < 1e-6, $"Acos({value}): error {acosError}");
            }

            Assert.Equal(DeterministicMath.HalfPi, DeterministicMath.Asin(Fixed64.One));
            Assert.Equal(-DeterministicMath.HalfPi, DeterministicMath.Asin(Fixed64.FromInt64(-1)));
            Assert.Equal(Fixed64.Zero, DeterministicMath.Acos(Fixed64.One));

            Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMath.Asin(Fixed64.FromDouble(1.01)));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicMath.Acos(Fixed64.FromDouble(-1.01)));
        }

        [Fact]
        public void Constants_MatchSystemMath()
        {
            Assert.True(Math.Abs(DeterministicMath.Pi.ToDouble() - Math.PI) < 1e-9);
            Assert.True(Math.Abs(DeterministicMath.TwoPi.ToDouble() - (2.0 * Math.PI)) < 1e-9);
            Assert.True(Math.Abs(DeterministicMath.HalfPi.ToDouble() - (Math.PI / 2.0)) < 1e-9);
            Assert.True(Math.Abs(DeterministicMath.E.ToDouble() - Math.E) < 1e-9);
        }
}
