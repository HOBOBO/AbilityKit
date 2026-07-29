using AbilityKit.Core.Pooling;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    internal sealed class Projectile : IPoolable
    {
        internal const int HitColliderBufferCapacity = 9;

        public readonly ColliderId[] HitCollidersThisTick = new ColliderId[HitColliderBufferCapacity];

        public ProjectileId Id;
        public int OwnerId;

        public int TotalHitCount;

        public int TemplateId;
        public int LauncherActorId;
        public int RootActorId;

        public int SpawnFrame;

        public Vec3 Position;
        public Vec3 Direction;
        public float Speed;
        public int TrackingTargetActorId;

        public int ReturnAfterFrames;
        public float ReturnSpeed;
        public float ReturnStopDistance;
        public bool IsReturning;

        public int LifetimeFramesLeft;
        public float DistanceLeft;

        public int CollisionLayerMask;
        public ColliderId IgnoreCollider;
        public Vec3 CollisionHalfExtents;

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
            Position = Vec3.Zero;
            Direction = Vec3.Zero;
            Speed = 0f;
            TrackingTargetActorId = 0;
            ReturnAfterFrames = 0;
            ReturnSpeed = 0f;
            ReturnStopDistance = 0f;
            IsReturning = false;
            LifetimeFramesLeft = 0;
            DistanceLeft = 0f;
            CollisionLayerMask = 0;
            IgnoreCollider = default;
            CollisionHalfExtents = Vec3.Zero;
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
