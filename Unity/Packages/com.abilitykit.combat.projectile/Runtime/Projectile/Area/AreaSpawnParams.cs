using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    public readonly struct AreaSpawnParams
    {
        public readonly int OwnerId;
        public readonly Vec3 Center;
        public readonly float Radius;

        public readonly int LifetimeFrames;
        public readonly int CollisionLayerMask;

        // 每 N 帧发送一次 Stay 事件（0 表示禁用 Stay 事件）。
        public readonly int StayIntervalFrames;
        public readonly OverlapShapeKind ShapeKind;
        public readonly Vec3 Forward;
        public readonly float HalfAngleDeg;
        public readonly float Width;
        public readonly float Length;
        public readonly bool AnchorAtOrigin;
        public readonly float RadiusGrowthPerFrame;
        public readonly float InitialRadius;

        public AreaSpawnParams(
            int ownerId,
            in Vec3 center,
            float radius,
            int lifetimeFrames,
            int collisionLayerMask,
            int stayIntervalFrames,
            OverlapShapeKind shapeKind = OverlapShapeKind.Circle,
            Vec3 forward = default,
            float halfAngleDeg = 0f,
            float width = 0f,
            float length = 0f,
            bool anchorAtOrigin = true,
            float radiusGrowthPerFrame = 0f,
            float initialRadius = 0f)
        {
            OwnerId = ownerId;
            Center = center;
            Radius = radius;
            LifetimeFrames = lifetimeFrames;
            CollisionLayerMask = collisionLayerMask;
            StayIntervalFrames = stayIntervalFrames;
            ShapeKind = shapeKind;
            Forward = forward;
            HalfAngleDeg = halfAngleDeg;
            Width = width;
            Length = length;
            AnchorAtOrigin = anchorAtOrigin;
            RadiusGrowthPerFrame = radiusGrowthPerFrame;
            InitialRadius = initialRadius;
        }

        public AreaSpawnParams WithCenter(in Vec3 center)
        {
            return new AreaSpawnParams(OwnerId, center, Radius, LifetimeFrames, CollisionLayerMask, StayIntervalFrames,
                ShapeKind, Forward, HalfAngleDeg, Width, Length, AnchorAtOrigin, RadiusGrowthPerFrame, InitialRadius);
        }
    }
}
