using MemoryPack;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    /// <summary>
    /// 保存发射请求的全部纯值，按原始浮点位保留冻结参数；运行时策略与过滤器另行重绑。
    /// </summary>
    [MemoryPackable]
    internal partial struct ProjectileScheduledSpawnSnapshot
    {
        public int OwnerId;
        public int TemplateId;
        public int LauncherActorId;
        public int RootActorId;
        public int SpawnFrame;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float DirectionX;
        public float DirectionY;
        public float DirectionZ;
        public float Speed;
        public int TrackingTargetActorId;
        public float TrackingArcHeight;
        public float TrackingArcHeightPerUnit;
        public float TrackingArcDurationSeconds;
        public float BounceSearchRadius;
        public int ReturnAfterFrames;
        public float ReturnSpeed;
        public float ReturnStopDistance;
        public int LifetimeFrames;
        public float MaxDistance;
        public ProjectileMaxDistanceResponse MaxDistanceResponse;
        public int CollisionLayerMask;
        public int IgnoreCollider;
        public float CollisionHalfExtentsX;
        public float CollisionHalfExtentsY;
        public float CollisionHalfExtentsZ;
        public int HitsRemaining;
        public ProjectileHitPolicyKind HitPolicyKind;
        public int HitPolicyParam;
        public int TickIntervalFrames;
        public int HitCooldownFrames;
        public ProjectileHitIdentityPolicy HitIdentityPolicy;
        public bool StartSuspended;
        public int PatternSlotIndex;
        public int PatternSlotCount;

        internal ProjectileScheduledSpawnSnapshot(in ProjectileSpawnParams p)
        {
            OwnerId = p.OwnerId;
            TemplateId = p.TemplateId;
            LauncherActorId = p.LauncherActorId;
            RootActorId = p.RootActorId;
            SpawnFrame = p.SpawnFrame;
            PositionX = p.Position.X;
            PositionY = p.Position.Y;
            PositionZ = p.Position.Z;
            DirectionX = p.Direction.X;
            DirectionY = p.Direction.Y;
            DirectionZ = p.Direction.Z;
            Speed = p.Speed;
            TrackingTargetActorId = p.TrackingTargetActorId;
            TrackingArcHeight = p.TrackingArcHeight;
            TrackingArcHeightPerUnit = p.TrackingArcHeightPerUnit;
            TrackingArcDurationSeconds = p.TrackingArcDurationSeconds;
            BounceSearchRadius = p.BounceSearchRadius;
            ReturnAfterFrames = p.ReturnAfterFrames;
            ReturnSpeed = p.ReturnSpeed;
            ReturnStopDistance = p.ReturnStopDistance;
            LifetimeFrames = p.LifetimeFrames;
            MaxDistance = p.MaxDistance;
            MaxDistanceResponse = p.MaxDistanceResponse;
            CollisionLayerMask = p.CollisionLayerMask;
            IgnoreCollider = p.IgnoreCollider.Value;
            CollisionHalfExtentsX = p.CollisionHalfExtents.X;
            CollisionHalfExtentsY = p.CollisionHalfExtents.Y;
            CollisionHalfExtentsZ = p.CollisionHalfExtents.Z;
            HitsRemaining = p.HitsRemaining;
            HitPolicyKind = p.HitPolicyKind;
            HitPolicyParam = p.HitPolicyParam;
            TickIntervalFrames = p.TickIntervalFrames;
            HitCooldownFrames = p.HitCooldownFrames;
            HitIdentityPolicy = p.HitIdentityPolicy;
            StartSuspended = p.StartSuspended;
            PatternSlotIndex = p.PatternSlotIndex;
            PatternSlotCount = p.PatternSlotCount;
        }

        internal ProjectileSpawnParams Restore(IProjectileHitFilter filter, int filterKey)
        {
            return new ProjectileSpawnParams(in this, filter, filterKey);
        }

        internal void Validate()
        {
            if (float.IsNaN(PositionX) || float.IsInfinity(PositionX) ||
                float.IsNaN(PositionY) || float.IsInfinity(PositionY) ||
                float.IsNaN(PositionZ) || float.IsInfinity(PositionZ) ||
                float.IsNaN(DirectionX) || float.IsInfinity(DirectionX) ||
                float.IsNaN(DirectionY) || float.IsInfinity(DirectionY) ||
                float.IsNaN(DirectionZ) || float.IsInfinity(DirectionZ) ||
                float.IsNaN(Speed) || float.IsInfinity(Speed) ||
                float.IsNaN(TrackingArcHeight) || float.IsInfinity(TrackingArcHeight) ||
                float.IsNaN(TrackingArcHeightPerUnit) || float.IsInfinity(TrackingArcHeightPerUnit) ||
                float.IsNaN(TrackingArcDurationSeconds) || float.IsInfinity(TrackingArcDurationSeconds) ||
                float.IsNaN(BounceSearchRadius) || float.IsInfinity(BounceSearchRadius) ||
                float.IsNaN(ReturnSpeed) || float.IsInfinity(ReturnSpeed) ||
                float.IsNaN(ReturnStopDistance) || float.IsInfinity(ReturnStopDistance) ||
                float.IsNaN(MaxDistance) || float.IsInfinity(MaxDistance) ||
                float.IsNaN(CollisionHalfExtentsX) || float.IsInfinity(CollisionHalfExtentsX) ||
                float.IsNaN(CollisionHalfExtentsY) || float.IsInfinity(CollisionHalfExtentsY) ||
                float.IsNaN(CollisionHalfExtentsZ) || float.IsInfinity(CollisionHalfExtentsZ))
                throw new System.InvalidOperationException("Non-finite schedule spawn parameters.");
        }
    }
}
