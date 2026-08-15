using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.MotionSystem.Constraints
{
    public enum MotionEndOverlapPolicy
    {
        Reject = 0,
        ClampToLastValid = 1,
        ProjectToNearestFree = 2,
        AllowInside = 3,
        /// <summary>沿位移方向（to→from）把终点投影到障碍物边界外。用于穿墙位移落地墙内时。</summary>
        ProjectAlongDirection = 4,
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

        /// <summary>撞击后是否沿墙切向滑动（而非单次钳制丢弃切向分量）。默认 false 以保持既有行为。</summary>
        public readonly bool SlideAlongWalls;

        /// <summary>滑动迭代上限（每次迭代消去一个被阻挡的法向分量）。</summary>
        public readonly int MaxSlideIterations;

        /// <summary>
        /// 墙滑切向速度恢复率。0 保留投影后的切向速度，1 将未消耗的水平位移长度恢复到墙切线方向。
        /// </summary>
        public readonly float WallSlideSpeedRecovery;

        public MotionCollisionConstraints(
            bool enable,
            bool allowPassThrough,
            MotionEndOverlapPolicy endOverlapPolicy,
            float radius,
            float skin,
            int obstacleMask,
            int ignoreMask,
            bool slideAlongWalls = false,
            int maxSlideIterations = 2,
            float wallSlideSpeedRecovery = 0f)
        {
            Enable = enable;
            AllowPassThrough = allowPassThrough;
            EndOverlapPolicy = endOverlapPolicy;
            Radius = radius;
            Skin = skin;
            ObstacleMask = obstacleMask;
            IgnoreMask = ignoreMask;
            SlideAlongWalls = slideAlongWalls;
            MaxSlideIterations = maxSlideIterations < 1 ? 1 : maxSlideIterations;
            WallSlideSpeedRecovery = wallSlideSpeedRecovery < 0f
                ? 0f
                : wallSlideSpeedRecovery > 1f
                    ? 1f
                    : wallSlideSpeedRecovery;
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
            var len = DeterministicMathBridge.Magnitude(in desiredDelta);
            if (len <= 1e-6f) return Vec3.Zero;
            var s = maxDistance / len;
            return new Vec3(desiredDelta.X * s, desiredDelta.Y * s, desiredDelta.Z * s);
        }
    }
}
