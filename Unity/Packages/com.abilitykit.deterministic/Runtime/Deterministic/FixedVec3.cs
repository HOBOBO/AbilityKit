using System;

namespace AbilityKit.Deterministic
{
    public readonly struct FixedVec3 : IEquatable<FixedVec3>
    {
        public static FixedVec3 Zero => new(Fixed64.Zero, Fixed64.Zero, Fixed64.Zero);

        public static FixedVec3 One => new(Fixed64.One, Fixed64.One, Fixed64.One);

        public FixedVec3(Fixed64 x, Fixed64 y, Fixed64 z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Fixed64 X { get; }

        public Fixed64 Y { get; }

        public Fixed64 Z { get; }

        public Fixed64 SqrMagnitude => Dot(this, this);

        public Fixed64 Magnitude => DeterministicMath.Sqrt(SqrMagnitude);

        public FixedVec3 Normalized
        {
            get
            {
                var magnitude = Magnitude;
                return magnitude.RawValue == 0 ? Zero : this / magnitude;
            }
        }

        public static Fixed64 Dot(FixedVec3 left, FixedVec3 right)
        {
            return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
        }

        public static FixedVec3 Cross(FixedVec3 left, FixedVec3 right)
        {
            return new(
                (left.Y * right.Z) - (left.Z * right.Y),
                (left.Z * right.X) - (left.X * right.Z),
                (left.X * right.Y) - (left.Y * right.X));
        }

        public static Fixed64 Distance(FixedVec3 left, FixedVec3 right)
        {
            return (left - right).Magnitude;
        }

        /// <summary>Unsigned angle between the vectors, in [0, π]; a zero vector throws.</summary>
        public static Fixed64 Angle(FixedVec3 from, FixedVec3 to)
        {
            if (from.SqrMagnitude.RawValue == 0 || to.SqrMagnitude.RawValue == 0)
            {
                throw new ArgumentException("Angle is undefined for a zero vector.");
            }

            var cosine = Dot(from, to) / (from.Magnitude * to.Magnitude);
            return DeterministicMath.Acos(DeterministicMath.Clamp(cosine, -Fixed64.One, Fixed64.One));
        }

        public static FixedVec3 Lerp(FixedVec3 from, FixedVec3 to, Fixed64 t)
        {
            return from + ((to - from) * t);
        }

        public static FixedVec3 LerpClamped(FixedVec3 from, FixedVec3 to, Fixed64 t)
        {
            return Lerp(from, to, DeterministicMath.Clamp(t, Fixed64.Zero, Fixed64.One));
        }

        public bool Equals(FixedVec3 other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object? obj) => obj is FixedVec3 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override string ToString() => $"({X}, {Y}, {Z})";

        public static FixedVec3 operator +(FixedVec3 left, FixedVec3 right)
        {
            return new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static FixedVec3 operator -(FixedVec3 left, FixedVec3 right)
        {
            return new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static FixedVec3 operator -(FixedVec3 value)
        {
            return new(-value.X, -value.Y, -value.Z);
        }

        public static FixedVec3 operator *(FixedVec3 vector, Fixed64 scalar)
        {
            return new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);
        }

        public static FixedVec3 operator *(Fixed64 scalar, FixedVec3 vector) => vector * scalar;

        public static FixedVec3 operator /(FixedVec3 vector, Fixed64 scalar)
        {
            return new(vector.X / scalar, vector.Y / scalar, vector.Z / scalar);
        }

        public static bool operator ==(FixedVec3 left, FixedVec3 right) => left.Equals(right);

        public static bool operator !=(FixedVec3 left, FixedVec3 right) => !left.Equals(right);
    }
}
