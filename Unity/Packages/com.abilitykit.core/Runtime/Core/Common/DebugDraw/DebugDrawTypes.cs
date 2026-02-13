using System;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.DebugDraw
{
    public readonly struct DebugDrawMask : IEquatable<DebugDrawMask>
    {
        public readonly int Value;

        public DebugDrawMask(int value)
        {
            Value = value;
        }

        public bool Equals(DebugDrawMask other) => Value == other.Value;
        public override bool Equals(object obj) => obj is DebugDrawMask other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static DebugDrawMask None => new DebugDrawMask(0);
        public static DebugDrawMask All => new DebugDrawMask(~0);
    }

    public readonly struct DebugDrawColor
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;
        public readonly byte A;

        public DebugDrawColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static DebugDrawColor Green => new DebugDrawColor(0, 255, 0, 255);
        public static DebugDrawColor Red => new DebugDrawColor(255, 0, 0, 255);
        public static DebugDrawColor Yellow => new DebugDrawColor(255, 255, 0, 255);
        public static DebugDrawColor Cyan => new DebugDrawColor(0, 255, 255, 255);
        public static DebugDrawColor White => new DebugDrawColor(255, 255, 255, 255);
    }

    public readonly struct DebugDrawStyle
    {
        public readonly DebugDrawColor Color;

        public DebugDrawStyle(in DebugDrawColor color)
        {
            Color = color;
        }

        public static DebugDrawStyle Default => new DebugDrawStyle(DebugDrawColor.Green);
    }

    public readonly struct DebugDrawContext
    {
        public readonly DebugDrawMask EnabledMask;

        public DebugDrawContext(DebugDrawMask enabledMask)
        {
            EnabledMask = enabledMask;
        }
    }

    public interface IDebugDraw
    {
        void DrawWireSphere(in Vec3 center, float radius, in DebugDrawStyle style);
        void DrawWireCapsule(in Vec3 a, in Vec3 b, float radius, in DebugDrawStyle style);
        void DrawWireAabb(in Vec3 center, in Vec3 size, in DebugDrawStyle style);
        void DrawLine(in Vec3 a, in Vec3 b, in DebugDrawStyle style);
    }

    public interface IDebugDrawContributor
    {
        DebugDrawMask Mask { get; }
        void Draw(in DebugDrawContext ctx, IDebugDraw draw);
    }
}
