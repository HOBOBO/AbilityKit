using System;
using System.Globalization;

namespace AbilityKit.Deterministic
{

/// <summary>
/// Signed Q32.32 fixed-point value for deterministic simulation code.
/// </summary>
public readonly struct Fixed64 : IEquatable<Fixed64>, IComparable<Fixed64>, IComparable
{
    private const int FractionalBitsValue = 32;
    private const long OneRawValue = 1L << FractionalBitsValue;

    private const decimal OneDecimal = 4294967296m;

    public static int FractionalBits => FractionalBitsValue;

    public static long OneRaw => OneRawValue;

    public static Fixed64 Zero => new(0);

    public static Fixed64 One => new(OneRawValue);

    public static Fixed64 Half => new(OneRawValue >> 1);

    public static Fixed64 MinValue => new(long.MinValue);

    public static Fixed64 MaxValue => new(long.MaxValue);

    private Fixed64(long rawValue)
    {
        RawValue = rawValue;
    }

    public long RawValue { get; }

    public static Fixed64 FromRaw(long rawValue) => new(rawValue);

    public static Fixed64 FromInt32(int value) => new(checked((long)value * OneRaw));

    public static Fixed64 FromInt64(long value) => new(checked(value * OneRaw));

    public static Fixed64 FromRatio(long numerator, long denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException();
        }

        return new(RatioRaw(numerator, denominator));
    }

    public static Fixed64 FromDecimal(decimal value) => new(checked((long)(value * OneDecimal)));

    public static Fixed64 FromDouble(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Floating-point boundary value must be finite.");
        }

        return new(checked((long)(value * OneRaw)));
    }

    public static Fixed64 FromSingle(float value) => FromDouble(value);

    public long ToInt64() => RawValue / OneRaw;

    public int ToInt32() => checked((int)ToInt64());

    public decimal ToDecimal() => RawValue / OneDecimal;

    public double ToDouble() => RawValue / (double)OneRaw;

    public float ToSingle() => (float)ToDouble();

    public int CompareTo(Fixed64 other) => RawValue.CompareTo(other.RawValue);

    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is Fixed64 other)
        {
            return CompareTo(other);
        }

        throw new ArgumentException($"Object must be of type {nameof(Fixed64)}.", nameof(obj));
    }

    public bool Equals(Fixed64 other) => RawValue == other.RawValue;

    public override bool Equals(object? obj) => obj is Fixed64 other && Equals(other);

    public override int GetHashCode() => RawValue.GetHashCode();

    public override string ToString() => ToDecimal().ToString(CultureInfo.InvariantCulture);

    public static Fixed64 operator +(Fixed64 left, Fixed64 right) => new(checked(left.RawValue + right.RawValue));

    public static Fixed64 operator -(Fixed64 left, Fixed64 right) => new(checked(left.RawValue - right.RawValue));

    public static Fixed64 operator -(Fixed64 value) => new(checked(-value.RawValue));

    public static Fixed64 operator *(Fixed64 left, Fixed64 right)
    {
        return new(MultiplyRaw(left.RawValue, right.RawValue));
    }

    public static Fixed64 operator /(Fixed64 left, Fixed64 right)
    {
        if (right.RawValue == 0)
        {
            throw new DivideByZeroException();
        }

        return new(DivideRaw(left.RawValue, right.RawValue));
    }

    public static bool operator ==(Fixed64 left, Fixed64 right) => left.RawValue == right.RawValue;

    public static bool operator !=(Fixed64 left, Fixed64 right) => left.RawValue != right.RawValue;

    public static bool operator <(Fixed64 left, Fixed64 right) => left.RawValue < right.RawValue;

    public static bool operator <=(Fixed64 left, Fixed64 right) => left.RawValue <= right.RawValue;

    public static bool operator >(Fixed64 left, Fixed64 right) => left.RawValue > right.RawValue;

    public static bool operator >=(Fixed64 left, Fixed64 right) => left.RawValue >= right.RawValue;

    public static implicit operator Fixed64(int value) => FromInt32(value);

    public static explicit operator int(Fixed64 value) => value.ToInt32();

    public static explicit operator long(Fixed64 value) => value.ToInt64();

    public static explicit operator decimal(Fixed64 value) => value.ToDecimal();

    public static explicit operator double(Fixed64 value) => value.ToDouble();

    public static explicit operator float(Fixed64 value) => value.ToSingle();

    private static long MultiplyRaw(long leftRaw, long rightRaw)
    {
#if NET7_0_OR_GREATER
        return checked((long)(((Int128)leftRaw * rightRaw) >> FractionalBits));
#else
        return checked((long)(((decimal)leftRaw * rightRaw) / OneDecimal));
#endif
    }

    private static long DivideRaw(long leftRaw, long rightRaw)
    {
#if NET7_0_OR_GREATER
        return checked((long)(((Int128)leftRaw << FractionalBits) / rightRaw));
#else
        return checked((long)(((decimal)leftRaw * OneDecimal) / rightRaw));
#endif
    }

    private static long RatioRaw(long numerator, long denominator)
    {
#if NET7_0_OR_GREATER
        return checked((long)(((Int128)numerator << FractionalBits) / denominator));
#else
        return checked((long)(((decimal)numerator * OneDecimal) / denominator));
#endif
    }
    }
}
