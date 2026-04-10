using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Constraints
{
    public enum MotionEndOverlapPolicy
    {
        Reject = 0,
        ClampToLastValid = 1,
        ProjectToNearestFree = 2,
        AllowInside = 3,
    }

    public readonly struct MotionCollisionConstraints
    {
        public readonly bool Enable;
        public readonly bool AllowPassThrough;
        public readonly MotionEndOverlapPolicy EndOverlapPolicy;

        public readonly float Radius;
        public readonly float Skin;

        public readonly int ObstacleMask;
        public readonly int IgnoreMask;

        public MotionCollisionConstraints(
            bool enable,
            bool allowPassThrough,
            MotionEndOverlapPolicy endOverlapPolicy,
            float radius,
            float skin,
            int obstacleMask,
            int ignoreMask)
        {
            Enable = enable;
            AllowPassThrough = allowPassThrough;
            EndOverlapPolicy = endOverlapPolicy;
            Radius = radius;
            Skin = skin;
            ObstacleMask = obstacleMask;
            IgnoreMask = ignoreMask;
        }

        public static MotionCollisionConstraints Disabled => new MotionCollisionConstraints(
            enable: false,
            allowPassThrough: true,
            endOverlapPolicy: MotionEndOverlapPolicy.AllowInside,
            radius: 0f,
            skin: 0f,
            obstacleMask: 0,
            ignoreMask: 0);
    }

    public enum MotionLeashPolicy
    {
        Reject = 0,
        ClampToRadius = 1,
    }

    public readonly struct MotionLeashConstraints
    {
        public readonly bool Enable;
        public readonly Vec3 Center;
        public readonly float Radius;
        public readonly MotionLeashPolicy Policy;

        public MotionLeashConstraints(bool enable, in Vec3 center, float radius, MotionLeashPolicy policy)
        {
            Enable = enable;
            Center = center;
            Radius = radius;
            Policy = policy;
        }

        public static MotionLeashConstraints Disabled => new MotionLeashConstraints(
            enable: false,
            center: Vec3.Zero,
            radius: 0f,
            policy: MotionLeashPolicy.ClampToRadius);
    }

    public readonly struct MotionConstraints
    {
        public readonly MotionCollisionConstraints Collision;
        public readonly MotionLeashConstraints Leash;

        public MotionConstraints(in MotionCollisionConstraints collision, in MotionLeashConstraints leash)
        {
            Collision = collision;
            Leash = leash;
        }

        public static MotionConstraints Disabled => new MotionConstraints(MotionCollisionConstraints.Disabled, MotionLeashConstraints.Disabled);

        public MotionConstraints WithCollision(in MotionCollisionConstraints collision)
        {
            return new MotionConstraints(in collision, in Leash);
        }

        public MotionConstraints WithLeash(in MotionLeashConstraints leash)
        {
            return new MotionConstraints(in Collision, in leash);
        }

        public Vec3 ClampDelta(in Vec3 desiredDelta, float maxDistance)
        {
            if (maxDistance <= 0f) return Vec3.Zero;
            var d2 = desiredDelta.SqrMagnitude;
            if (d2 <= 0f) return Vec3.Zero;
            if (d2 <= maxDistance * maxDistance) return desiredDelta;
            var len = desiredDelta.Magnitude;
            if (len <= 1e-6f) return Vec3.Zero;
            var s = maxDistance / len;
            return new Vec3(desiredDelta.X * s, desiredDelta.Y * s, desiredDelta.Z * s);
        }
    }
}
