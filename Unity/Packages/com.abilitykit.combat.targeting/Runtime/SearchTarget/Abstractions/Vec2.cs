using System;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 与具体数学库无关的二维坐标值。
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public float X { get; }
        public float Y { get; }

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float SqrMagnitude => X * X + Y * Y;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public Vec2 Add(Vec2 other) => this + other;
        public Vec2 Subtract(Vec2 other) => this - other;
        public Vec2 Multiply(float scalar) => this * scalar;
        public float Dot(Vec2 other) => X * other.X + Y * other.Y;

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(Vec2 left, Vec2 right) => left.Equals(right);
        public static bool operator !=(Vec2 left, Vec2 right) => !left.Equals(right);
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);

        public static readonly Vec2 Zero = new Vec2(0, 0);
        public static readonly Vec2 Up = new Vec2(0, 1);
    }
}
