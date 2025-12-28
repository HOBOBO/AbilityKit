using System;

namespace AbilityKit.Ability.Share.Math
{
    public readonly struct Transform3 : IEquatable<Transform3>
    {
        public readonly Vec3 Position;
        public readonly Quat Rotation;
        public readonly Vec3 Scale;

        public Transform3(in Vec3 position, in Quat rotation, in Vec3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public static Transform3 Identity => new Transform3(Vec3.Zero, Quat.Identity, Vec3.One);

        public Vec3 TransformPoint(in Vec3 localPoint)
        {
            var scaled = new Vec3(localPoint.X * Scale.X, localPoint.Y * Scale.Y, localPoint.Z * Scale.Z);
            var rotated = Rotation.Rotate(scaled);
            return rotated + Position;
        }

        public Vec3 InverseTransformPoint(in Vec3 worldPoint)
        {
            var p = worldPoint - Position;
            var invRot = Rotation.Inverse;
            var unrotated = invRot.Rotate(p);

            var ix = MathUtil.Abs(Scale.X) > MathUtil.Epsilon ? 1f / Scale.X : 0f;
            var iy = MathUtil.Abs(Scale.Y) > MathUtil.Epsilon ? 1f / Scale.Y : 0f;
            var iz = MathUtil.Abs(Scale.Z) > MathUtil.Epsilon ? 1f / Scale.Z : 0f;

            return new Vec3(unrotated.X * ix, unrotated.Y * iy, unrotated.Z * iz);
        }

        public Vec3 TransformDirection(in Vec3 localDir)
        {
            // Direction ignores translation; scale applies.
            var scaled = new Vec3(localDir.X * Scale.X, localDir.Y * Scale.Y, localDir.Z * Scale.Z);
            return Rotation.Rotate(scaled);
        }

        public Vec3 InverseTransformDirection(in Vec3 worldDir)
        {
            var invRot = Rotation.Inverse;
            var unrotated = invRot.Rotate(worldDir);

            var ix = MathUtil.Abs(Scale.X) > MathUtil.Epsilon ? 1f / Scale.X : 0f;
            var iy = MathUtil.Abs(Scale.Y) > MathUtil.Epsilon ? 1f / Scale.Y : 0f;
            var iz = MathUtil.Abs(Scale.Z) > MathUtil.Epsilon ? 1f / Scale.Z : 0f;

            return new Vec3(unrotated.X * ix, unrotated.Y * iy, unrotated.Z * iz);
        }

        public Transform3 Inverse
        {
            get
            {
                var invRot = Rotation.Inverse;

                var ix = MathUtil.Abs(Scale.X) > MathUtil.Epsilon ? 1f / Scale.X : 0f;
                var iy = MathUtil.Abs(Scale.Y) > MathUtil.Epsilon ? 1f / Scale.Y : 0f;
                var iz = MathUtil.Abs(Scale.Z) > MathUtil.Epsilon ? 1f / Scale.Z : 0f;

                var invScale = new Vec3(ix, iy, iz);

                // Inverse translation: -(R^-1 * (P)) / S
                var p = invRot.Rotate(-Position);
                p = new Vec3(p.X * ix, p.Y * iy, p.Z * iz);

                return new Transform3(p, invRot, invScale);
            }
        }

        public bool Equals(Transform3 other) => Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Scale.Equals(other.Scale);
        public override bool Equals(object obj) => obj is Transform3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Position, Rotation, Scale);
        public override string ToString() => $"Pos={Position}, Rot={Rotation}, Scale={Scale}";
    }
}
