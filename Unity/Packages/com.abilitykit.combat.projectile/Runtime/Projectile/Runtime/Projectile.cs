using AbilityKit.Core.Pooling;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Combat.Projectile
{
    internal sealed class Projectile : IPoolable
    {
        internal const int HitColliderBufferCapacity = 9;

        public readonly int[] HitColliderIdsThisTick = new int[HitColliderBufferCapacity];

        public ProjectileId Id;
        public int OwnerId;

        public int TotalHitCount;

        public int TemplateId;
        public int LauncherActorId;
        public int RootActorId;

        public int SpawnFrame;

        // 运动学状态全部定点化：逐帧积分路径跨平台位一致。
        public FixedVec3 Position;
        public FixedVec3 Direction;
        public Fixed64 Speed;
        public int TrackingTargetActorId;

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
        public int HitCooldownFrames;
        public ColliderId LastHitCollider;
        public int LastHitAllowedFrame;

        public bool IsSuspended;
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
            Position = FixedVec3.Zero;
            Direction = FixedVec3.Zero;
            Speed = Fixed64.Zero;
            TrackingTargetActorId = 0;
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
            HitCooldownFrames = 0;
            LastHitCollider = default;
            LastHitAllowedFrame = 0;
            IsSuspended = false;
            PatternSlotIndex = 0;
            PatternSlotCount = 0;
        }

        void IPoolable.OnPoolDestroy()
        {
            ((IPoolable)this).OnPoolRelease();
        }
    }
}
