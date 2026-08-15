using System;

namespace AbilityKit.Deterministic
{
    /// <summary>
    /// Deterministic math over <see cref="Fixed64"/>. All algorithms are implemented with
    /// integer-only operations (add/sub/shift/compare plus <see cref="Fixed64"/> arithmetic),
    /// so results are bit-identical across .NET, Mono, and IL2CPP.
    /// Trigonometry uses CORDIC; square root uses a digit-by-digit restoring integer method.
    /// </summary>
    public static class DeterministicMath
    {
        // Q32.32 raw values of the mathematical constants, rounded to nearest.
        private const long PiRawValue = 13493037705L;
        private const long TwoPiRawValue = 26986075409L;
        private const long HalfPiRawValue = 6746518852L;
        private const long ERawValue = 11674931555L;

        // CORDIC gain compensation: prod(i >= 0, 1 / sqrt(1 + 2^-2i)) as Q32.32.
        private const long CordicGainRawValue = 2608131496L;

        // atan(2^-i) as Q32.32, i = 0..31, rounded to nearest.
        private static readonly long[] AtanPowTable =
        {
            3373259426L, 1991351318L, 1052175346L, 534100635L,
            268086748L, 134174063L, 67103403L, 33553749L,
            16777131L, 8388597L, 4194303L, 2097152L,
            1048576L, 524288L, 262144L, 131072L,
            65536L, 32768L, 16384L, 8192L,
            4096L, 2048L, 1024L, 512L,
            256L, 128L, 64L, 32L,
            16L, 8L, 4L, 2L,
        };

        public static Fixed64 Pi => Fixed64.FromRaw(PiRawValue);

        public static Fixed64 TwoPi => Fixed64.FromRaw(TwoPiRawValue);

        public static Fixed64 HalfPi => Fixed64.FromRaw(HalfPiRawValue);

        public static Fixed64 E => Fixed64.FromRaw(ERawValue);

        public static Fixed64 Abs(Fixed64 value)
        {
            if (value.RawValue == long.MinValue)
            {
                throw new OverflowException();
            }

            return value.RawValue < 0 ? Fixed64.FromRaw(-value.RawValue) : value;
        }

        public static Fixed64 Min(Fixed64 left, Fixed64 right) => left <= right ? left : right;

        public static Fixed64 Max(Fixed64 left, Fixed64 right) => left >= right ? left : right;

        public static Fixed64 Clamp(Fixed64 value, Fixed64 min, Fixed64 max)
        {
            if (min > max)
            {
                throw new ArgumentException("Minimum must be less than or equal to maximum.", nameof(min));
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static Fixed64 Lerp(Fixed64 from, Fixed64 to, Fixed64 t) => from + ((to - from) * t);

        public static Fixed64 LerpClamped(Fixed64 from, Fixed64 to, Fixed64 t)
        {
            return Lerp(from, to, Clamp(t, Fixed64.Zero, Fixed64.One));
        }

        /// <summary>
        /// Largest integer less than or equal to the value (arithmetic shift on the raw value;
        /// exact for any input, no rounding error).
        /// </summary>
        public static Fixed64 Floor(Fixed64 value)
        {
            return Fixed64.FromRaw(value.RawValue >> Fixed64.FractionalBits << Fixed64.FractionalBits);
        }

        /// <summary>
        /// Smallest integer greater than or equal to the value. Exact for any input.
        /// </summary>
        public static Fixed64 Ceiling(Fixed64 value)
        {
            var floorRaw = value.RawValue >> Fixed64.FractionalBits << Fixed64.FractionalBits;
            var ceilingRaw = floorRaw == value.RawValue ? floorRaw : checked(floorRaw + Fixed64.OneRaw);
            return Fixed64.FromRaw(ceilingRaw);
        }

        /// <summary>
        /// Rounds to the nearest integer; halves round up (toward positive infinity).
        /// </summary>
        public static Fixed64 Round(Fixed64 value)
        {
            return Floor(value + Fixed64.Half);
        }

        /// <summary>
        /// Square root, rounded to nearest representable value. Negative input throws
        /// (there is no NaN in fixed point).
        /// </summary>
        public static Fixed64 Sqrt(Fixed64 value)
        {
            if (value.RawValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Square root of a negative fixed-point value is undefined.");
            }

            if (value.RawValue == 0)
            {
                return Fixed64.Zero;
            }

            // Target: round(sqrt(vRaw * 2^32)) — the raw value of sqrt(v). Computed with the
            // digit-by-digit restoring integer square root over the 96-bit operand vRaw << 32,
            // using only 64-bit integer arithmetic.
            var root = SqrtScaled32((ulong)value.RawValue, out var remainder);

            // Invariant of the restoring sqrt: N = root^2 + remainder. Round to nearest by
            // comparing the distances to root and root + 1.
            if ((2UL * root + 1UL - remainder) < remainder)
            {
                root += 1UL;
            }

            return Fixed64.FromRaw(checked((long)root));
        }

        /// <summary>
        /// Sine of an angle in radians. Valid for any input magnitude (argument reduction
        /// into [-π/2, π/2] before CORDIC rotation).
        /// </summary>
        public static Fixed64 Sin(Fixed64 angle)
        {
            ReduceForSinCos(angle.RawValue, out var reducedRaw, out var sinSign, out _);
            CordicRotate(reducedRaw, out var sinRaw, out _);
            return Fixed64.FromRaw(sinSign * sinRaw);
        }

        /// <summary>Cosine of an angle in radians. Valid for any input magnitude.</summary>
        public static Fixed64 Cos(Fixed64 angle)
        {
            ReduceForSinCos(angle.RawValue, out var reducedRaw, out _, out var cosSign);
            CordicRotate(reducedRaw, out _, out var cosRaw);
            return Fixed64.FromRaw(cosSign * cosRaw);
        }

        /// <summary>
        /// Tangent of an angle in radians. Throws when the cosine is exactly zero;
        /// near π/2 the result saturates at the fixed-point range instead of diverging to infinity.
        /// </summary>
        public static Fixed64 Tan(Fixed64 angle)
        {
            ReduceForSinCos(angle.RawValue, out var reducedRaw, out var sinSign, out var cosSign);
            CordicRotate(reducedRaw, out var sinRaw, out var cosRaw);
            return Fixed64.FromRaw(sinSign * sinRaw) / Fixed64.FromRaw(cosSign * cosRaw);
        }

        /// <summary>Arcsine in radians; input outside [-1, 1] throws.</summary>
        public static Fixed64 Asin(Fixed64 value)
        {
            if (value < -Fixed64.One || value > Fixed64.One)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Asin input must be within [-1, 1].");
            }

            return Atan2(value, Sqrt(Fixed64.One - (value * value)));
        }

        /// <summary>Arccosine in radians; input outside [-1, 1] throws.</summary>
        public static Fixed64 Acos(Fixed64 value)
        {
            return HalfPi - Asin(value);
        }

        /// <summary>Arctangent of a ratio, equivalent to Atan2(value, 1).</summary>
        public static Fixed64 Atan(Fixed64 value)
        {
            return Atan2(value, Fixed64.One);
        }

        /// <summary>
        /// Angle of the vector (y, x) in radians, result in (-π, π]. Follows the IEEE
        /// atan2 conventions for zero inputs: atan2(±0, -x) = π, atan2(±y, 0) = ±π/2.
        /// </summary>
        public static Fixed64 Atan2(Fixed64 y, Fixed64 x)
        {
            if (y.RawValue == 0)
            {
                return x.RawValue >= 0 ? Fixed64.Zero : Pi;
            }

            if (x.RawValue == 0)
            {
                return y.RawValue > 0 ? HalfPi : -HalfPi;
            }

            long result;
            if (x.RawValue > 0)
            {
                if (y.RawValue > 0)
                {
                    result = CordicVectoring(y.RawValue, x.RawValue);
                }
                else
                {
                    result = -CordicVectoring(-y.RawValue, x.RawValue);
                }
            }
            else
            {
                if (y.RawValue > 0)
                {
                    result = PiRawValue - CordicVectoring(y.RawValue, -x.RawValue);
                }
                else
                {
                    result = CordicVectoring(-y.RawValue, -x.RawValue) - PiRawValue;
                }
            }

            return Fixed64.FromRaw(result);
        }

        private static void ReduceForSinCos(long raw, out long reducedRaw, out long sinSign, out long cosSign)
        {
            var wrapped = raw % TwoPiRawValue;
            if (wrapped < -PiRawValue)
            {
                wrapped += TwoPiRawValue;
            }
            else if (wrapped >= PiRawValue)
            {
                wrapped -= TwoPiRawValue;
            }

            sinSign = 1L;
            cosSign = 1L;
            if (wrapped > HalfPiRawValue)
            {
                // (π/2, π): sin(w) = sin(π - w), cos(w) = -cos(π - w)
                reducedRaw = PiRawValue - wrapped;
                cosSign = -1L;
            }
            else if (wrapped < -HalfPiRawValue)
            {
                // (-π, -π/2): sin(w) = -sin(π + w), cos(w) = -cos(π + w)
                reducedRaw = PiRawValue + wrapped;
                sinSign = -1L;
                cosSign = -1L;
            }
            else
            {
                reducedRaw = wrapped;
            }
        }

        private static void CordicRotate(long angleRaw, out long sinRaw, out long cosRaw)
        {
            // Rotation mode with pre-applied gain compensation; input is within [-π/2, π/2],
            // which the atan table sum (~1.7433 rad) covers.
            var x = CordicGainRawValue;
            var y = 0L;
            var z = angleRaw;
            var table = AtanPowTable;
            for (var i = 0; i < table.Length; i++)
            {
                var xStep = y >> i;
                var yStep = x >> i;
                if (z >= 0)
                {
                    x -= xStep;
                    y += yStep;
                    z -= table[i];
                }
                else
                {
                    x += xStep;
                    y -= yStep;
                    z += table[i];
                }
            }

            sinRaw = y;
            cosRaw = x;
        }

        private static long CordicVectoring(long yRaw, long xRaw)
        {
            // Vectoring mode; caller guarantees x > 0, y > 0 so the result is in (0, π/2).
            var x = xRaw;
            var y = yRaw;

            // Leave headroom for the CORDIC gain (~1.647x magnitude growth).
            while (x >= (1L << 58) || y >= (1L << 58))
            {
                x >>= 1;
                y >>= 1;
            }

            var angle = 0L;
            var table = AtanPowTable;
            for (var i = 0; i < table.Length; i++)
            {
                var xStep = y >> i;
                var yStep = x >> i;
                if (y >= 0)
                {
                    x += xStep;
                    y -= yStep;
                    angle += table[i];
                }
                else
                {
                    x -= xStep;
                    y += yStep;
                    angle -= table[i];
                }
            }

            return angle;
        }

        /// <summary>
        /// Digit-by-digit restoring integer square root over the 96-bit operand
        /// value &lt;&lt; 32. Returns floor(sqrt(value * 2^32)) and leaves the remainder
        /// (N - root^2) in <paramref name="remainder"/>; both fit in 64 bits because
        /// root &lt; 2^48.
        /// </summary>
        private static ulong SqrtScaled32(ulong value, out ulong remainder)
        {
            ulong root = 0UL;
            ulong rem = 0UL;

            for (var shift = 62; shift >= 0; shift -= 2)
            {
                SqrtStep(ref rem, ref root, (value >> shift) & 3UL);
            }

            for (var i = 0; i < 16; i++)
            {
                SqrtStep(ref rem, ref root, 0UL);
            }

            remainder = rem;
            return root;
        }

        private static void SqrtStep(ref ulong rem, ref ulong root, ulong pair)
        {
            rem = (rem << 2) | pair;
            root <<= 1;
            var trial = (root << 1) + 1UL;
            if (rem >= trial)
            {
                rem -= trial;
                root += 1UL;
            }
        }
    }
}
