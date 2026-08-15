using System;

namespace AbilityKit.Deterministic
{
    public readonly struct FixedVec2 : IEquatable<FixedVec2>
    {
        public static FixedVec2 Zero => new(Fixed64.Zero, Fixed64.Zero);

        public static FixedVec2 One => new(Fixed64.One, Fixed64.One);

        public FixedVec2(Fixed64 x, Fixed64 y)
        {
            X = x;
            Y = y;
        }

        public Fixed64 X { get; }

        public Fixed64 Y { get; }

        public Fixed64 SqrMagnitude => Dot(this, this);

        public Fixed64 Magnitude => DeterministicMath.Sqrt(SqrMagnitude);

        public FixedVec2 Normalized
        {
            get
            {
                var magnitude = Magnitude;
                return magnitude.RawValue == 0 ? Zero : this / magnitude;
            }
        }

        public static Fixed64 Dot(FixedVec2 left, FixedVec2 right)
        {
            return (left.X * right.X) + (left.Y * right.Y);
        }

        /// <summary>2D cross product (z of the 3D cross); sign indicates turn direction.</summary>
        public static Fixed64 Cross(FixedVec2 left, FixedVec2 right)
        {
            return (left.X * right.Y) - (left.Y * right.X);
        }

        public static Fixed64 Distance(FixedVec2 left, FixedVec2 right)
        {
            return (left - right).Magnitude;
        }

        /// <summary>Signed angle from <paramref name="from"/> to <paramref name="to"/>, in (-π, π].</summary>
        public static Fixed64 Angle(FixedVec2 from, FixedVec2 to)
        {
            return DeterministicMath.Atan2(Cross(from, to), Dot(from, to));
        }

        public static FixedVec2 Lerp(FixedVec2 from, FixedVec2 to, Fixed64 t)
        {
            return from + ((to - from) * t);
        }

        public static FixedVec2 LerpClamped(FixedVec2 from, FixedVec2 to, Fixed64 t)
        {
            return Lerp(from, to, DeterministicMath.Clamp(t, Fixed64.Zero, Fixed64.One));
        }

        public bool Equals(FixedVec2 other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is FixedVec2 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X}, {Y})";

        public static FixedVec2 operator +(FixedVec2 left, FixedVec2 right)
        {
            return new(left.X + right.X, left.Y + right.Y);
        }

        public static FixedVec2 operator -(FixedVec2 left, FixedVec2 right)
        {
            return new(left.X - right.X, left.Y - right.Y);
        }

        public static FixedVec2 operator -(FixedVec2 value)
        {
            return new(-value.X, -value.Y);
        }

        public static FixedVec2 operator *(FixedVec2 vector, Fixed64 scalar)
        {
            return new(vector.X * scalar, vector.Y * scalar);
        }

        public static FixedVec2 operator *(Fixed64 scalar, FixedVec2 vector) => vector * scalar;

        public static FixedVec2 operator /(FixedVec2 vector, Fixed64 scalar)
        {
            return new(vector.X / scalar, vector.Y / scalar);
        }

        public static bool operator ==(FixedVec2 left, FixedVec2 right) => left.Equals(right);

        public static bool operator !=(FixedVec2 left, FixedVec2 right) => !left.Equals(right);
    }
}
