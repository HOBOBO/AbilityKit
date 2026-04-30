using System;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 2D 向量抽象接口
    /// </summary>
    public interface IVec2
    {
        float X { get; }
        float Y { get; }
        float SqrMagnitude { get; }
        float Magnitude { get; }

        IVec2 Add(IVec2 other);
        IVec2 Subtract(IVec2 other);
        IVec2 Multiply(float scalar);
        float Dot(IVec2 other);
    }

    /// <summary>
    /// 默认 Vec2 实现
    /// </summary>
    public readonly struct Vec2 : IVec2
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

        public IVec2 Add(IVec2 other) => new Vec2(X + other.X, Y + other.Y);
        public IVec2 Subtract(IVec2 other) => new Vec2(X - other.X, Y - other.Y);
        public IVec2 Multiply(float scalar) => new Vec2(X * scalar, Y * scalar);
        public float Dot(IVec2 other) => X * other.X + Y * other.Y;

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);

        public static readonly Vec2 Zero = new Vec2(0, 0);
        public static readonly Vec2 Up = new Vec2(0, 1);
    }
}
