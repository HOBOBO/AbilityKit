using System;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    public readonly struct AreaSpawnParams
    {
        public readonly int OwnerId;
        public readonly Vec3 Center;
        public readonly float Radius;
        public readonly float InnerRadius;

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
        // Area collision becomes active after this many frames. Zero arms on spawn.
        public readonly int ArmDelayFrames;
        // When enabled, the first accepted Enter event consumes the Area's Enter gate.
        public readonly bool ConsumeOnFirstEnter;
        public readonly bool FollowOwner;
        public readonly int FollowActorId;
        public readonly long RootContextId;
        public readonly int GroupKey;
        public readonly bool ClaimTargetsWithinGroup;
        public readonly bool BoundarySweep;

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
            float initialRadius = 0f,
            int armDelayFrames = 0,
            bool consumeOnFirstEnter = false,
            bool followOwner = false,
            int followActorId = 0,
            long rootContextId = 0,
            int groupKey = 0,
            bool claimTargetsWithinGroup = false,
            float innerRadius = 0f,
            bool boundarySweep = false)
        {
            OwnerId = ownerId;
            Center = center;
            Radius = radius;
            InnerRadius = Math.Max(0f, Math.Min(innerRadius, radius));
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
            ArmDelayFrames = armDelayFrames > 0 ? armDelayFrames : 0;
            ConsumeOnFirstEnter = consumeOnFirstEnter;
            FollowOwner = followOwner;
            FollowActorId = followActorId > 0 ? followActorId : ownerId;
            RootContextId = rootContextId;
            GroupKey = groupKey;
            ClaimTargetsWithinGroup = claimTargetsWithinGroup;
            BoundarySweep = boundarySweep;
        }

        public AreaSpawnParams WithCenter(in Vec3 center)
        {
            return new AreaSpawnParams(OwnerId, center, Radius, LifetimeFrames, CollisionLayerMask, StayIntervalFrames,
                ShapeKind, Forward, HalfAngleDeg, Width, Length, AnchorAtOrigin, RadiusGrowthPerFrame, InitialRadius, ArmDelayFrames, ConsumeOnFirstEnter, FollowOwner, FollowActorId,
                RootContextId, GroupKey, ClaimTargetsWithinGroup, InnerRadius, BoundarySweep);
        }
    }
}
