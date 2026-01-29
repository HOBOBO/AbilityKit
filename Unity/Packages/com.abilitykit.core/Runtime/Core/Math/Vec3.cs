using System;

namespace AbilityKit.Ability.Share.Math
{
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 Zero => new Vec3(0f, 0f, 0f);
        public static Vec3 One => new Vec3(1f, 1f, 1f);
        public static Vec3 Up => new Vec3(0f, 1f, 0f);
        public static Vec3 Down => new Vec3(0f, -1f, 0f);
        public static Vec3 Right => new Vec3(1f, 0f, 0f);
        public static Vec3 Left => new Vec3(-1f, 0f, 0f);
        public static Vec3 Forward => new Vec3(0f, 0f, 1f);
        public static Vec3 Back => new Vec3(0f, 0f, -1f);

        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        public float Magnitude => (float)System.Math.Sqrt(SqrMagnitude);

        public Vec3 Normalized
        {
            get
            {
                var mag = Magnitude;
                return mag > 0f ? this / mag : Zero;
            }
        }

        public static float Dot(in Vec3 a, in Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(in Vec3 a, in Vec3 b)
        {
            return new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }

        public static Vec3 Min(in Vec3 a, in Vec3 b)
        {
            return new Vec3(System.MathF.Min(a.X, b.X), System.MathF.Min(a.Y, b.Y), System.MathF.Min(a.Z, b.Z));
        }

        public static Vec3 Max(in Vec3 a, in Vec3 b)
        {
            return new Vec3(System.MathF.Max(a.X, b.X), System.MathF.Max(a.Y, b.Y), System.MathF.Max(a.Z, b.Z));
        }

        public static float Distance(in Vec3 a, in Vec3 b) => (a - b).Magnitude;

        public static Vec3 Lerp(in Vec3 a, in Vec3 b, float t)
        {
            t = MathUtil.Clamp01(t);
            return a + (b - a) * t;
        }

        public static Vec3 operator +(in Vec3 a, in Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(in Vec3 a, in Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(in Vec3 v) => new Vec3(-v.X, -v.Y, -v.Z);
        public static Vec3 operator *(in Vec3 v, float s) => new Vec3(v.X * s, v.Y * s, v.Z * s);
        public static Vec3 operator *(float s, in Vec3 v) => new Vec3(v.X * s, v.Y * s, v.Z * s);
        public static Vec3 operator /(in Vec3 v, float s) => new Vec3(v.X / s, v.Y / s, v.Z / s);

        public System.Numerics.Vector3 ToNumerics() => new System.Numerics.Vector3(X, Y, Z);
        public static Vec3 FromNumerics(in System.Numerics.Vector3 v) => new Vec3(v.X, v.Y, v.Z);

        public bool Equals(Vec3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is Vec3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
