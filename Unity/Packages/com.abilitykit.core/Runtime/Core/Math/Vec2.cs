using System;

namespace AbilityKit.Core.Math
{
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);
        public static Vec2 One => new Vec2(1f, 1f);

        public float SqrMagnitude => X * X + Y * Y;
        public float Magnitude => (float)System.Math.Sqrt(SqrMagnitude);

        public Vec2 Normalized
        {
            get
            {
                var mag = Magnitude;
                return mag > 0f ? this / mag : Zero;
            }
        }

        public static float Dot(in Vec2 a, in Vec2 b) => a.X * b.X + a.Y * b.Y;

        public static Vec2 Min(in Vec2 a, in Vec2 b) => new Vec2(System.MathF.Min(a.X, b.X), System.MathF.Min(a.Y, b.Y));
        public static Vec2 Max(in Vec2 a, in Vec2 b) => new Vec2(System.MathF.Max(a.X, b.X), System.MathF.Max(a.Y, b.Y));

        public static Vec2 operator +(in Vec2 a, in Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(in Vec2 a, in Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(in Vec2 v) => new Vec2(-v.X, -v.Y);
        public static Vec2 operator *(in Vec2 v, float s) => new Vec2(v.X * s, v.Y * s);
        public static Vec2 operator *(float s, in Vec2 v) => new Vec2(v.X * s, v.Y * s);
        public static Vec2 operator /(in Vec2 v, float s) => new Vec2(v.X / s, v.Y / s);

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";
    }
}
