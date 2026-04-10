using System;

namespace AbilityKit.Core.Math
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

        public static Quat LookRotation(in Vec3 forward, in Vec3 up)
        {
            var f = forward.Normalized;
            if (f.SqrMagnitude <= MathUtil.Epsilon) return Identity;

            var r = Vec3.Cross(up, f).Normalized;
            if (r.SqrMagnitude <= MathUtil.Epsilon)
            {
                r = Vec3.Cross(Vec3.Up, f).Normalized;
                if (r.SqrMagnitude <= MathUtil.Epsilon) return Identity;
            }

            var u = Vec3.Cross(f, r);

            // Rotation matrix columns: r,u,f
            var m00 = r.X; var m01 = u.X; var m02 = f.X;
            var m10 = r.Y; var m11 = u.Y; var m12 = f.Y;
            var m20 = r.Z; var m21 = u.Z; var m22 = f.Z;

            var trace = m00 + m11 + m22;
            if (trace > 0f)
            {
                var s = System.MathF.Sqrt(trace + 1f) * 2f;
                var inv = 1f / s;
                return new Quat(
                    (m21 - m12) * inv,
                    (m02 - m20) * inv,
                    (m10 - m01) * inv,
                    0.25f * s).Normalized;
            }

            if (m00 > m11 && m00 > m22)
            {
                var s = System.MathF.Sqrt(1f + m00 - m11 - m22) * 2f;
                var inv = 1f / s;
                return new Quat(
                    0.25f * s,
                    (m01 + m10) * inv,
                    (m02 + m20) * inv,
                    (m21 - m12) * inv).Normalized;
            }

            if (m11 > m22)
            {
                var s = System.MathF.Sqrt(1f + m11 - m00 - m22) * 2f;
                var inv = 1f / s;
                return new Quat(
                    (m01 + m10) * inv,
                    0.25f * s,
                    (m12 + m21) * inv,
                    (m02 - m20) * inv).Normalized;
            }

            {
                var s = System.MathF.Sqrt(1f + m22 - m00 - m11) * 2f;
                var inv = 1f / s;
                return new Quat(
                    (m02 + m20) * inv,
                    (m12 + m21) * inv,
                    0.25f * s,
                    (m10 - m01) * inv).Normalized;
            }
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
