using AbilityKit.Core.Pooling;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using System.Collections.Generic;

namespace AbilityKit.Combat.Projectile
{
    internal sealed class Projectile : IPoolable
    {
        internal const int HitColliderBufferCapacity = 9;

        public readonly int[] HitColliderIdsThisTick = new int[HitColliderBufferCapacity];
        public readonly List<int> HitActorIds = new List<int>(8);

        public ProjectileId Id;
        public int OwnerId;

        public int TotalHitCount;

        public int TemplateId;
        public int LauncherActorId;
        public int RootActorId;

        public int SpawnFrame;

        // 运动学状态全部定点化：逐帧积分路径跨平台位一致。
        public FixedVec3 SpawnPosition;
        public FixedVec3 Position;
        public FixedVec3 Direction;
        public Fixed64 Speed;
        public Fixed64 DistanceTravelled;
        public Fixed64 ElapsedSeconds;
        public int TrackingTargetActorId;
        public Fixed64 TrackingArcHeight;
        public Fixed64 TrackingArcBaseHeight;
        public Fixed64 TrackingArcHeightPerUnit;
        public Fixed64 TrackingArcDuration;
        public bool TrackingArcDurationConfigured;
        public Fixed64 TrackingArcElapsed;
        public Fixed64 TrackingArcStartY;
        public Fixed64 TrackingArcEndY;
        public Fixed64 BounceSearchRadius;

        public int ReturnAfterFrames;
        public Fixed64 ReturnSpeed;
        public Fixed64 ReturnStopDistance;
        public bool IsReturning;

        public int LifetimeFramesLeft;
        public Fixed64 DistanceLeft;

        public int CollisionLayerMask;
        public ColliderId IgnoreCollider;
        public FixedVec3 CollisionHalfExtents;

        public IProjectileHitPolicy HitPolicy;
        public int HitsRemaining;

        public ProjectileHitPolicyKind HitPolicyKind;
        public int HitPolicyParam;

        public int TickIntervalFrames;
        public int NextTickFrame;

        public IProjectileHitFilter HitFilter;
        public int HitFilterKey;
        public int HitCooldownFrames;
        public ProjectileHitIdentityPolicy HitIdentityPolicy;
        public ColliderId LastHitCollider;
        public int LastHitAllowedFrame;

        public bool IsSuspended;
        public ProjectileMaxDistanceResponse MaxDistanceResponse;
        public int ArrivedFrame = -1;
        public int PatternSlotIndex;
        public int PatternSlotCount;

        void IPoolable.OnPoolGet()
        {
        }

        void IPoolable.OnPoolRelease()
        {
            Id = default;
            OwnerId = 0;
            TotalHitCount = 0;
            TemplateId = 0;
            LauncherActorId = 0;
            RootActorId = 0;
            SpawnFrame = 0;
            SpawnPosition = FixedVec3.Zero;
            Position = FixedVec3.Zero;
            Direction = FixedVec3.Zero;
            Speed = Fixed64.Zero;
            DistanceTravelled = Fixed64.Zero;
            ElapsedSeconds = Fixed64.Zero;
            TrackingTargetActorId = 0;
            TrackingArcHeight = Fixed64.Zero;
            TrackingArcBaseHeight = Fixed64.Zero;
            TrackingArcHeightPerUnit = Fixed64.Zero;
            TrackingArcDuration = Fixed64.Zero;
            TrackingArcDurationConfigured = false;
            TrackingArcElapsed = Fixed64.Zero;
            TrackingArcStartY = Fixed64.Zero;
            TrackingArcEndY = Fixed64.Zero;
            BounceSearchRadius = Fixed64.Zero;
            ReturnAfterFrames = 0;
            ReturnSpeed = Fixed64.Zero;
            ReturnStopDistance = Fixed64.Zero;
            IsReturning = false;
            LifetimeFramesLeft = 0;
            DistanceLeft = Fixed64.Zero;
            CollisionLayerMask = 0;
            IgnoreCollider = default;
            CollisionHalfExtents = FixedVec3.Zero;
            HitPolicy = null;
            HitsRemaining = 0;
            HitPolicyKind = default;
            HitPolicyParam = 0;
            TickIntervalFrames = 0;
            NextTickFrame = 0;
            HitFilter = null;
            HitFilterKey = 0;
            HitCooldownFrames = 0;
            HitIdentityPolicy = default;
            LastHitCollider = default;
            LastHitAllowedFrame = 0;
            IsSuspended = false;
            MaxDistanceResponse = ProjectileMaxDistanceResponse.Exit;
            ArrivedFrame = -1;
            PatternSlotIndex = 0;
            PatternSlotCount = 0;
            HitActorIds.Clear();
        }

        void IPoolable.OnPoolDestroy()
        {
            ((IPoolable)this).OnPoolRelease();
        }
    }
}
