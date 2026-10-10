using System;

namespace AbilityKit.Deterministic
{
    /// <summary>
    /// Deterministic math over <see cref="Fixed64"/>. All algorithms are implemented with
    /// integer-only operations (add/sub/shift/compare plus <see cref="Fixed64"/> arithmetic),
    /// so results are bit-identical across .NET, Mono, and IL2CPP.
    /// Trigonometry uses CORDIC; square root uses a digit-by-digit restoring integer method;
    /// exp/ln/pow use Cody-Waite ln(2) reduction with Taylor/atanh series in raw integer arithmetic.
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

        // ln(2) as Q32.32 rounded to nearest, plus its Cody-Waite split: the hi part has
        // the low 12 bits cleared so k * ln2Hi stays exact in 64-bit arithmetic for the
        // argument reduction used by Exp/Ln.
        private const long Ln2RawValue = 2977044472L;
        private const long Ln2HiRawValue = 2977042432L;
        private const long Ln2LoRawValue = 2040L;

        // exp saturates to zero below this input: exp(x) * 2^32 < 0.5 first happens at
        // x < -33*ln(2), so -34*ln(2) guards the boundary (the rounded shift would
        // produce 0 anyway inside that window).
        private const long ExpSaturationRawValue = -34L * Ln2RawValue;

        private const int ExpTaylorTerms = 12;
        private const int LnAtanhTerms = 10;

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

        /// <summary>
        /// Natural exponential e^x. Argument reduction by ln(2) with a Cody-Waite split,
        /// then a Taylor series; every operation is 64-bit integer arithmetic with
        /// explicit rounding, so the result is bit-identical across runtimes.
        /// Inputs below -23*ln(2) saturate to zero; overflowing inputs throw.
        /// </summary>
        public static Fixed64 Exp(Fixed64 value)
        {
            var raw = value.RawValue;
            if (raw < ExpSaturationRawValue)
            {
                return Fixed64.Zero;
            }

            // k = floor((x + ln2/2) / ln2) = round-half-up of x/ln2. C# integer
            // division truncates toward zero, so negatives need an explicit floor fix.
            var kScaled = raw + (Ln2RawValue >> 1);
            var kLong = kScaled / Ln2RawValue;
            if (kScaled % Ln2RawValue != 0 && kScaled < 0)
            {
                kLong--;
            }

            var k = (int)kLong;
            if (k >= 62)
            {
                throw new OverflowException("Exp overflows the fixed-point range.");
            }

            // r = x - k*ln2, computed as x - k*ln2Hi - k*ln2Lo so k*ln2Hi is exact.
            var r = raw - checked(k * Ln2HiRawValue) - checked(k * Ln2LoRawValue);

            // Taylor series sum(r^n / n!) for |r| <= ln(2)/2; the 12th-order remainder
            // is below 2^-50 of the result.
            var term = r;
            var sum = Fixed64.OneRaw + r;
            for (var n = 2; n <= ExpTaylorTerms; n++)
            {
                term = DivRoundSmall(MulRoundSmall(term, r), n);
                sum += term;
            }

            if (k >= 0)
            {
                // Shifts are never checked in C#, so scale by multiplication to keep
                // the overflow fail-loud.
                return Fixed64.FromRaw(checked(sum * (1L << k)));
            }

            return Fixed64.FromRaw(RightShiftRound(sum, -k));
        }

        /// <summary>
        /// Natural logarithm for positive inputs; non-positive input throws (fixed point
        /// has no NaN). ln(x) = e*ln2 + 2*atanh((f-1)/(f+1)) after normalizing x = f*2^e
        /// with f in [1, 2), evaluated with a truncated atanh series.
        /// </summary>
        public static Fixed64 Ln(Fixed64 value)
        {
            var raw = value.RawValue;
            if (raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Natural log requires a positive fixed-point value.");
            }

            var e = 0;
            while (raw >= (Fixed64.OneRaw << 1))
            {
                raw >>= 1;
                e++;
            }

            while (raw < Fixed64.OneRaw)
            {
                raw <<= 1;
                e--;
            }

            // f in [1, 2): u = (f-1)/(f+1) in [0, 1/3), and ln(f) = 2*atanh(u).
            var u = DivPow2Round(raw - Fixed64.OneRaw, raw + Fixed64.OneRaw);
            var uSquared = MulRoundSmall(u, u);
            var term = u;
            var sum = u;
            for (var k = 1; k <= LnAtanhTerms; k++)
            {
                term = MulRoundSmall(term, uSquared);
                sum += DivRoundSmall(term, 2 * k + 1);
            }

            var twoAtanh = checked(sum + sum);
            return Fixed64.FromRaw(checked(twoAtanh + e * Ln2HiRawValue + e * Ln2LoRawValue));
        }

        /// <summary>
        /// Power function. Integer exponents use square-and-multiply on the raw value
        /// (sign is preserved; representable results are accurate to a few raw ULP, and
        /// out-of-range results throw from the checked products). Fractional exponents go
        /// through Exp(exponent * Ln(base)) and therefore require a positive base. A
        /// negative base with a fractional exponent throws, matching the fail-loud
        /// convention instead of returning NaN.
        /// </summary>
        public static Fixed64 Pow(Fixed64 baseValue, Fixed64 exponent)
        {
            var baseRaw = baseValue.RawValue;
            var exponentRaw = exponent.RawValue;
            if (baseRaw == 0)
            {
                if (exponentRaw > 0)
                {
                    return Fixed64.Zero;
                }

                if (exponentRaw == 0)
                {
                    return Fixed64.One;
                }

                throw new DivideByZeroException("Zero base with a negative exponent has no fixed-point value.");
            }

            var hasIntegerExponent = exponentRaw % Fixed64.OneRaw == 0;
            if (baseRaw < 0 && !hasIntegerExponent)
            {
                throw new ArgumentOutOfRangeException(nameof(baseValue), "A negative base requires an integer exponent.");
            }

            if (hasIntegerExponent)
            {
                var integerExponent = exponent.ToInt64();
                if (integerExponent < -int.MaxValue || integerExponent > int.MaxValue)
                {
                    throw new OverflowException("Integer exponent is outside the supported int32 range.");
                }

                var magnitudeRaw = IntegerPowRaw(baseRaw < 0 ? checked(-baseRaw) : baseRaw, (int)Math.Abs(integerExponent));
                var negativeResult = baseRaw < 0 && (Math.Abs(integerExponent) & 1L) == 1L;
                if (integerExponent >= 0)
                {
                    return Fixed64.FromRaw(negativeResult ? checked(-magnitudeRaw) : magnitudeRaw);
                }

                // Negative integer exponent: 1 / |base|^n as round(2^64 / m) computed
                // through 2^63 remainders so every step fits in a ulong.
                if (magnitudeRaw <= 1)
                {
                    // 1 / 2^-32 = 2^32 exceeds the positive Q32.32 range.
                    throw new OverflowException("Pow with a negative exponent overflows the fixed-point range.");
                }

                var denominator = (ulong)magnitudeRaw;
                var quotient63 = 0x8000000000000000UL / denominator;
                var remainder63 = 0x8000000000000000UL % denominator;
                var doubled = remainder63 * 2UL;
                var resultRaw = quotient63 * 2UL + doubled / denominator;
                if (doubled % denominator * 2UL >= denominator)
                {
                    resultRaw += 1UL;
                }

                return Fixed64.FromRaw(checked((long)resultRaw) * (negativeResult ? -1L : 1L));
            }

            var scaledRaw = MulRaw128Round(exponentRaw, Ln(Fixed64.FromRaw(baseRaw)).RawValue);
            return Exp(Fixed64.FromRaw(scaledRaw));
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

        // ==== exp/ln/pow raw helpers ====
        // These helpers use 64-bit integer operations only with explicit round-half
        // reductions. They deliberately avoid the Fixed64 multiply/divide operators so
        // their rounding never depends on the NET7_0_OR_GREATER compile path.

        /// <summary>
        /// Q32.32 product of two small raws (|operands| &lt; 2^31, so the 64-bit product
        /// is exact), rounded half up at the shift.
        /// </summary>
        private static long MulRoundSmall(long leftRaw, long rightRaw)
        {
            return checked(leftRaw * rightRaw + (1L << (Fixed64.FractionalBits - 1))) >> Fixed64.FractionalBits;
        }

        /// <summary>Divides a raw by a small positive index, rounding half away from zero.</summary>
        private static long DivRoundSmall(long numeratorRaw, int divisor)
        {
            return numeratorRaw >= 0
                ? (numeratorRaw + (divisor >> 1)) / divisor
                : (numeratorRaw - (divisor >> 1)) / divisor;
        }

        /// <summary>
        /// u = (f-1)/(f+1) during Ln normalization: numerator &lt; 2^32 and denominator in
        /// (2^33, 3*2^32], so the shifted numerator fits a ulong and the rounded quotient
        /// stays below 2^31.
        /// </summary>
        private static long DivPow2Round(long numeratorRaw, long denominatorRaw)
        {
            return (long)((((ulong)numeratorRaw << Fixed64.FractionalBits) + ((ulong)denominatorRaw >> 1))
                / (ulong)denominatorRaw);
        }

        /// <summary>Arithmetic shift right with round half up; shift must be in [1, 62].</summary>
        private static long RightShiftRound(long valueRaw, int shift)
        {
            return (valueRaw + (1L << (shift - 1))) >> shift;
        }

        /// <summary>
        /// Raw power by square-and-multiply using full-range rounded multiplies; checked
        /// products fail loud when |base|^n leaves the representable range.
        /// </summary>
        private static long IntegerPowRaw(long baseRaw, int exponent)
        {
            var result = Fixed64.OneRaw;
            var factor = baseRaw;
            while (exponent > 0)
            {
                if ((exponent & 1) == 1)
                {
                    result = MulRaw128Round(result, factor);
                }

                factor = exponent > 1 ? MulRaw128Round(factor, factor) : factor;
                exponent >>= 1;
            }

            return result;
        }

        /// <summary>
        /// Full-range Q32.32 product (round half away from zero) via an explicit
        /// 64x64 -> 128 unsigned multiply; throws when the scaled result leaves the
        /// signed 64-bit range. Same algorithm on every runtime.
        /// </summary>
        private static long MulRaw128Round(long leftRaw, long rightRaw)
        {
            var negative = leftRaw < 0 ^ rightRaw < 0;
            var left = (ulong)(leftRaw < 0 ? checked(-leftRaw) : leftRaw);
            var right = (ulong)(rightRaw < 0 ? checked(-rightRaw) : rightRaw);

            var leftLow = left & 0xFFFFFFFFUL;
            var leftHigh = left >> 32;
            var rightLow = right & 0xFFFFFFFFUL;
            var rightHigh = right >> 32;

            var lowLow = leftLow * rightLow;
            var lowHigh = leftLow * rightHigh;
            var highLow = leftHigh * rightLow;
            var highHigh = leftHigh * rightHigh;

            var carry = ((lowHigh & 0xFFFFFFFFUL) + (highLow & 0xFFFFFFFFUL) + (lowLow >> 32)) >> 32;
            var high = highHigh + (lowHigh >> 32) + (highLow >> 32) + carry;
            var low = (lowHigh << 32) + (highLow << 32) + lowLow;

            var resultHigh = high >> 32;
            if (resultHigh != 0UL)
            {
                throw new OverflowException("Fixed-point product overflows the 64-bit raw range.");
            }

            var magnitude = (high << 32) | (low >> 32);
            if ((low & 0x80000000UL) != 0UL)
            {
                magnitude += 1UL;
            }

            return negative ? checked(-(long)magnitude) : checked((long)magnitude);
        }
    }
}
