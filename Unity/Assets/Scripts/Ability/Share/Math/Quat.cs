using System;

namespace AbilityKit.Ability.Share.Math
{
    public readonly struct Quat : IEquatable<Quat>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float W;

        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static Quat Identity => new Quat(0f, 0f, 0f, 1f);

        public static Quat FromAxisAngle(in Vec3 axis, float angleRad)
        {
            var ax = axis.Normalized;
            var half = angleRad * 0.5f;
            var s = System.MathF.Sin(half);
            var c = System.MathF.Cos(half);
            return new Quat(ax.X * s, ax.Y * s, ax.Z * s, c);
        }

        public Quat Normalized
        {
            get
            {
                var lenSq = X * X + Y * Y + Z * Z + W * W;
                if (lenSq <= MathUtil.Epsilon) return Identity;
                var inv = 1f / MathUtil.Sqrt(lenSq);
                return new Quat(X * inv, Y * inv, Z * inv, W * inv);
            }
        }

        public Quat Conjugate => new Quat(-X, -Y, -Z, W);

        public Quat Inverse
        {
            get
            {
                // For unit quaternions, inverse == conjugate.
                var lenSq = X * X + Y * Y + Z * Z + W * W;
                if (lenSq <= MathUtil.Epsilon) return Identity;
                var inv = 1f / lenSq;
                return new Quat(-X * inv, -Y * inv, -Z * inv, W * inv);
            }
        }

        public static Quat operator *(in Quat a, in Quat b)
        {
            return new Quat(
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
                a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
        }

        public Vec3 Rotate(in Vec3 v)
        {
            // q * (v,0) * q^-1
            var qv = new Quat(v.X, v.Y, v.Z, 0f);
            var r = this * qv * Inverse;
            return new Vec3(r.X, r.Y, r.Z);
        }

        public System.Numerics.Quaternion ToNumerics() => new System.Numerics.Quaternion(X, Y, Z, W);
        public static Quat FromNumerics(in System.Numerics.Quaternion q) => new Quat(q.X, q.Y, q.Z, q.W);

        public bool Equals(Quat other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
        public override bool Equals(object obj) => obj is Quat other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        public override string ToString() => $"({X}, {Y}, {Z}, {W})";
    }
}
