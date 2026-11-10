using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    public enum ProjectileHitPolicyKind
    {
        ExitOnHit = 0,
        Pierce = 1,
        /// <summary>Retarget after each hit until the configured maximum number of hit targets is reached.</summary>
        Bounce = 2,
    }

    public readonly struct ProjectileSpawnParams
    {
        public readonly int OwnerId;
        public readonly int TemplateId;
        public readonly int LauncherActorId;
        public readonly int RootActorId;
        public readonly int SpawnFrame;

        public readonly Vec3 Position;
        public readonly Vec3 Direction;
        public readonly float Speed;
        public readonly int TrackingTargetActorId;
        public readonly float TrackingArcHeight;
        public readonly float TrackingArcHeightPerUnit;
        public readonly float TrackingArcDurationSeconds;
        public readonly float BounceSearchRadius;

        public readonly int ReturnAfterFrames;
        public readonly float ReturnSpeed;
        public readonly float ReturnStopDistance;

        public readonly int LifetimeFrames;
        public readonly float MaxDistance;

        public readonly int CollisionLayerMask;
        public readonly ColliderId IgnoreCollider;
        public readonly Vec3 CollisionHalfExtents;

        public readonly IProjectileHitPolicy HitPolicy;
        public readonly int HitsRemaining;

        public readonly ProjectileHitPolicyKind HitPolicyKind;
        /// <summary>For Bounce, the maximum number of distinct target hits including the initial hit; for Pierce, maximum hits.</summary>
        public readonly int HitPolicyParam;

        public readonly int TickIntervalFrames;

        public readonly IProjectileHitFilter HitFilter;
        /// <summary>Optional stable key for resolving the filter after importing into another world.</summary>
        public readonly int HitFilterKey;
        public readonly int HitCooldownFrames;

        public readonly bool StartSuspended;
        public readonly int PatternSlotIndex;
        public readonly int PatternSlotCount;

        public ProjectileSpawnParams(
            int ownerId,
            int templateId,
            int launcherActorId,
            int rootActorId,
            int spawnFrame,
            in Vec3 position,
            in Vec3 direction,
            float speed,
            int returnAfterFrames,
            float returnSpeed,
            float returnStopDistance,
            int lifetimeFrames,
            float maxDistance,
            int collisionLayerMask,
            ColliderId ignoreCollider,
            IProjectileHitPolicy hitPolicy = null,
            int hitsRemaining = 1,
            ProjectileHitPolicyKind hitPolicyKind = ProjectileHitPolicyKind.ExitOnHit,
            int hitPolicyParam = 0,
            int tickIntervalFrames = 0,
            IProjectileHitFilter hitFilter = null,
            int hitCooldownFrames = 0,
            bool startSuspended = false,
            int patternSlotIndex = 0,
            int patternSlotCount = 1,
            int trackingTargetActorId = 0,
            Vec3 collisionHalfExtents = default,
            float trackingArcHeight = 0f,
            float trackingArcDurationSeconds = 0f,
            float bounceSearchRadius = 0f,
            float trackingArcHeightPerUnit = 0f,
            int hitFilterKey = 0)
        {
            OwnerId = ownerId;
            TemplateId = templateId;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            SpawnFrame = spawnFrame;
            Position = position;
            Direction = direction;
            Speed = speed;
            TrackingTargetActorId = trackingTargetActorId;
            TrackingArcHeight = MathUtil.Max(0f, trackingArcHeight);
            TrackingArcHeightPerUnit = MathUtil.Max(0f, trackingArcHeightPerUnit);
            TrackingArcDurationSeconds = MathUtil.Max(0f, trackingArcDurationSeconds);
            BounceSearchRadius = MathUtil.Max(0f, bounceSearchRadius);
            ReturnAfterFrames = returnAfterFrames;
            ReturnSpeed = returnSpeed;
            ReturnStopDistance = returnStopDistance;
            LifetimeFrames = lifetimeFrames;
            MaxDistance = maxDistance;
            CollisionLayerMask = collisionLayerMask;
            IgnoreCollider = ignoreCollider;
            CollisionHalfExtents = new Vec3(
                MathUtil.Max(0f, collisionHalfExtents.X),
                MathUtil.Max(0f, collisionHalfExtents.Y),
                MathUtil.Max(0f, collisionHalfExtents.Z));

            HitPolicy = hitPolicy;
            HitsRemaining = hitsRemaining;

            HitPolicyKind = hitPolicyKind;
            HitPolicyParam = hitPolicyParam;

            TickIntervalFrames = tickIntervalFrames;

            HitFilter = hitFilter;
            HitFilterKey = hitFilterKey;
            HitCooldownFrames = hitCooldownFrames;

            StartSuspended = startSuspended;
            PatternSlotIndex = patternSlotIndex < 0 ? 0 : patternSlotIndex;
            PatternSlotCount = patternSlotCount <= 0 ? 1 : patternSlotCount;
        }

        public ProjectileSpawnParams WithDirection(in Vec3 direction)
        {
            return new ProjectileSpawnParams(
                ownerId: OwnerId,
                templateId: TemplateId,
                launcherActorId: LauncherActorId,
                rootActorId: RootActorId,
                spawnFrame: SpawnFrame,
                position: Position,
                direction: direction,
                speed: Speed,
                returnAfterFrames: ReturnAfterFrames,
                returnSpeed: ReturnSpeed,
                returnStopDistance: ReturnStopDistance,
                lifetimeFrames: LifetimeFrames,
                maxDistance: MaxDistance,
                collisionLayerMask: CollisionLayerMask,
                ignoreCollider: IgnoreCollider,
                hitPolicy: HitPolicy,
                hitsRemaining: HitsRemaining,
                hitPolicyKind: HitPolicyKind,
                hitPolicyParam: HitPolicyParam,
                tickIntervalFrames: TickIntervalFrames,
                hitFilter: HitFilter,
                hitCooldownFrames: HitCooldownFrames,
                hitFilterKey: HitFilterKey,
                startSuspended: StartSuspended,
                patternSlotIndex: PatternSlotIndex,
                patternSlotCount: PatternSlotCount,
                trackingTargetActorId: TrackingTargetActorId,
                collisionHalfExtents: CollisionHalfExtents,
                trackingArcHeight: TrackingArcHeight,
                trackingArcDurationSeconds: TrackingArcDurationSeconds,
                bounceSearchRadius: BounceSearchRadius,
                trackingArcHeightPerUnit: TrackingArcHeightPerUnit);
        }

        public ProjectileSpawnParams WithTrackingArc(float arcHeight, float durationSeconds = 0f)
        {
            return new ProjectileSpawnParams(
                ownerId: OwnerId, templateId: TemplateId,
                launcherActorId: LauncherActorId, rootActorId: RootActorId,
                spawnFrame: SpawnFrame, position: Position, direction: Direction,
                speed: Speed, returnAfterFrames: ReturnAfterFrames,
                returnSpeed: ReturnSpeed, returnStopDistance: ReturnStopDistance,
                lifetimeFrames: LifetimeFrames, maxDistance: MaxDistance,
                collisionLayerMask: CollisionLayerMask, ignoreCollider: IgnoreCollider,
                hitPolicy: HitPolicy, hitsRemaining: HitsRemaining,
                hitPolicyKind: HitPolicyKind, hitPolicyParam: HitPolicyParam,
                tickIntervalFrames: TickIntervalFrames, hitFilter: HitFilter,
                hitCooldownFrames: HitCooldownFrames, hitFilterKey: HitFilterKey, startSuspended: StartSuspended,
                patternSlotIndex: PatternSlotIndex, patternSlotCount: PatternSlotCount,
                trackingTargetActorId: TrackingTargetActorId,
                collisionHalfExtents: CollisionHalfExtents,
                trackingArcHeight: arcHeight,
                trackingArcDurationSeconds: durationSeconds,
                trackingArcHeightPerUnit: TrackingArcHeightPerUnit,
                bounceSearchRadius: BounceSearchRadius);
        }

        /// <summary>Sets additional arc height per world unit of planar target distance. The value is sampled at each segment start.</summary>
        public ProjectileSpawnParams WithTrackingArcHeightPerUnit(float heightPerUnit)
        {
            return new ProjectileSpawnParams(
                ownerId: OwnerId, templateId: TemplateId,
                launcherActorId: LauncherActorId, rootActorId: RootActorId,
                spawnFrame: SpawnFrame, position: Position, direction: Direction,
                speed: Speed, returnAfterFrames: ReturnAfterFrames,
                returnSpeed: ReturnSpeed, returnStopDistance: ReturnStopDistance,
                lifetimeFrames: LifetimeFrames, maxDistance: MaxDistance,
                collisionLayerMask: CollisionLayerMask, ignoreCollider: IgnoreCollider,
                hitPolicy: HitPolicy, hitsRemaining: HitsRemaining,
                hitPolicyKind: HitPolicyKind, hitPolicyParam: HitPolicyParam,
                tickIntervalFrames: TickIntervalFrames, hitFilter: HitFilter,
                hitCooldownFrames: HitCooldownFrames, hitFilterKey: HitFilterKey, startSuspended: StartSuspended,
                patternSlotIndex: PatternSlotIndex, patternSlotCount: PatternSlotCount,
                trackingTargetActorId: TrackingTargetActorId,
                collisionHalfExtents: CollisionHalfExtents,
                trackingArcHeight: TrackingArcHeight,
                trackingArcDurationSeconds: TrackingArcDurationSeconds,
                bounceSearchRadius: BounceSearchRadius,
                trackingArcHeightPerUnit: heightPerUnit);
        }

        public ProjectileSpawnParams WithSpeed(float speed)
        {
            return new ProjectileSpawnParams(
                ownerId: OwnerId, templateId: TemplateId,
                launcherActorId: LauncherActorId, rootActorId: RootActorId,
                spawnFrame: SpawnFrame, position: Position, direction: Direction,
                speed: speed, returnAfterFrames: ReturnAfterFrames,
                returnSpeed: ReturnSpeed, returnStopDistance: ReturnStopDistance,
                lifetimeFrames: LifetimeFrames, maxDistance: MaxDistance,
                collisionLayerMask: CollisionLayerMask, ignoreCollider: IgnoreCollider,
                hitPolicy: HitPolicy, hitsRemaining: HitsRemaining,
                hitPolicyKind: HitPolicyKind, hitPolicyParam: HitPolicyParam,
                tickIntervalFrames: TickIntervalFrames, hitFilter: HitFilter,
                hitCooldownFrames: HitCooldownFrames, hitFilterKey: HitFilterKey, startSuspended: StartSuspended,
                patternSlotIndex: PatternSlotIndex, patternSlotCount: PatternSlotCount,
                trackingTargetActorId: TrackingTargetActorId,
                collisionHalfExtents: CollisionHalfExtents,
                trackingArcHeight: TrackingArcHeight,
                trackingArcDurationSeconds: TrackingArcDurationSeconds,
                bounceSearchRadius: BounceSearchRadius,
                trackingArcHeightPerUnit: TrackingArcHeightPerUnit);
        }

        public ProjectileSpawnParams WithLifetimeFrames(int lifetimeFrames)
        {
            return new ProjectileSpawnParams(
                ownerId: OwnerId, templateId: TemplateId,
                launcherActorId: LauncherActorId, rootActorId: RootActorId,
                spawnFrame: SpawnFrame, position: Position, direction: Direction,
                speed: Speed, returnAfterFrames: ReturnAfterFrames,
                returnSpeed: ReturnSpeed, returnStopDistance: ReturnStopDistance,
                lifetimeFrames: lifetimeFrames, maxDistance: MaxDistance,
                collisionLayerMask: CollisionLayerMask, ignoreCollider: IgnoreCollider,
                hitPolicy: HitPolicy, hitsRemaining: HitsRemaining,
                hitPolicyKind: HitPolicyKind, hitPolicyParam: HitPolicyParam,
                tickIntervalFrames: TickIntervalFrames, hitFilter: HitFilter,
                hitCooldownFrames: HitCooldownFrames, hitFilterKey: HitFilterKey, startSuspended: StartSuspended,
                patternSlotIndex: PatternSlotIndex, patternSlotCount: PatternSlotCount,
                trackingTargetActorId: TrackingTargetActorId,
                collisionHalfExtents: CollisionHalfExtents,
                trackingArcHeight: TrackingArcHeight,
                trackingArcHeightPerUnit: TrackingArcHeightPerUnit,
                trackingArcDurationSeconds: TrackingArcDurationSeconds,
                bounceSearchRadius: BounceSearchRadius);
        }

        public ProjectileSpawnParams WithHitPolicyParam(int hitPolicyParam)
        {
            return new ProjectileSpawnParams(
                ownerId: OwnerId, templateId: TemplateId,
                launcherActorId: LauncherActorId, rootActorId: RootActorId,
                spawnFrame: SpawnFrame, position: Position, direction: Direction,
                speed: Speed, returnAfterFrames: ReturnAfterFrames,
                returnSpeed: ReturnSpeed, returnStopDistance: ReturnStopDistance,
                lifetimeFrames: LifetimeFrames, maxDistance: MaxDistance,
                collisionLayerMask: CollisionLayerMask, ignoreCollider: IgnoreCollider,
                hitPolicy: null, hitsRemaining: HitsRemaining,
                hitPolicyKind: HitPolicyKind, hitPolicyParam: hitPolicyParam,
                tickIntervalFrames: TickIntervalFrames, hitFilter: HitFilter,
                hitCooldownFrames: HitCooldownFrames, hitFilterKey: HitFilterKey, startSuspended: StartSuspended,
                patternSlotIndex: PatternSlotIndex, patternSlotCount: PatternSlotCount,
                trackingTargetActorId: TrackingTargetActorId,
                collisionHalfExtents: CollisionHalfExtents,
                trackingArcHeight: TrackingArcHeight,
                trackingArcHeightPerUnit: TrackingArcHeightPerUnit,
                trackingArcDurationSeconds: TrackingArcDurationSeconds,
                bounceSearchRadius: BounceSearchRadius);
        }

        public ProjectileSpawnParams WithPosition(in Vec3 position)
        {
            return new ProjectileSpawnParams(
                ownerId: OwnerId,
                templateId: TemplateId,
                launcherActorId: LauncherActorId,
                rootActorId: RootActorId,
                spawnFrame: SpawnFrame,
                position: position,
                direction: Direction,
                speed: Speed,
                returnAfterFrames: ReturnAfterFrames,
                returnSpeed: ReturnSpeed,
                returnStopDistance: ReturnStopDistance,
                lifetimeFrames: LifetimeFrames,
                maxDistance: MaxDistance,
                collisionLayerMask: CollisionLayerMask,
                ignoreCollider: IgnoreCollider,
                hitPolicy: HitPolicy,
                hitsRemaining: HitsRemaining,
                hitPolicyKind: HitPolicyKind,
                hitPolicyParam: HitPolicyParam,
                tickIntervalFrames: TickIntervalFrames,
                hitFilter: HitFilter,
                hitCooldownFrames: HitCooldownFrames,
                hitFilterKey: HitFilterKey,
                startSuspended: StartSuspended,
                patternSlotIndex: PatternSlotIndex,
                patternSlotCount: PatternSlotCount,
                trackingTargetActorId: TrackingTargetActorId,
                collisionHalfExtents: CollisionHalfExtents,
                trackingArcHeight: TrackingArcHeight,
                trackingArcHeightPerUnit: TrackingArcHeightPerUnit,
                trackingArcDurationSeconds: TrackingArcDurationSeconds,
                bounceSearchRadius: BounceSearchRadius);
        }

        public ProjectileSpawnParams WithPatternSlot(int slotIndex, int slotCount)
        {
            return new ProjectileSpawnParams(
                ownerId: OwnerId,
                templateId: TemplateId,
                launcherActorId: LauncherActorId,
                rootActorId: RootActorId,
                spawnFrame: SpawnFrame,
                position: Position,
                direction: Direction,
                speed: Speed,
                returnAfterFrames: ReturnAfterFrames,
                returnSpeed: ReturnSpeed,
                returnStopDistance: ReturnStopDistance,
                lifetimeFrames: LifetimeFrames,
                maxDistance: MaxDistance,
                collisionLayerMask: CollisionLayerMask,
                ignoreCollider: IgnoreCollider,
                hitPolicy: HitPolicy,
                hitsRemaining: HitsRemaining,
                hitPolicyKind: HitPolicyKind,
                hitPolicyParam: HitPolicyParam,
                tickIntervalFrames: TickIntervalFrames,
                hitFilter: HitFilter,
                hitCooldownFrames: HitCooldownFrames,
                hitFilterKey: HitFilterKey,
                startSuspended: StartSuspended,
                patternSlotIndex: slotIndex,
                patternSlotCount: slotCount,
                trackingTargetActorId: TrackingTargetActorId,
                collisionHalfExtents: CollisionHalfExtents,
                trackingArcHeight: TrackingArcHeight,
                trackingArcHeightPerUnit: TrackingArcHeightPerUnit,
                trackingArcDurationSeconds: TrackingArcDurationSeconds,
                bounceSearchRadius: BounceSearchRadius);
        }

        public ProjectileSpawnParams WithSpawnFrame(int spawnFrame)
        {
            return new ProjectileSpawnParams(
                ownerId: OwnerId,
                templateId: TemplateId,
                launcherActorId: LauncherActorId,
                rootActorId: RootActorId,
                spawnFrame: spawnFrame,
                position: Position,
                direction: Direction,
                speed: Speed,
                returnAfterFrames: ReturnAfterFrames,
                returnSpeed: ReturnSpeed,
                returnStopDistance: ReturnStopDistance,
                lifetimeFrames: LifetimeFrames,
                maxDistance: MaxDistance,
                collisionLayerMask: CollisionLayerMask,
                ignoreCollider: IgnoreCollider,
                hitPolicy: HitPolicy,
                hitsRemaining: HitsRemaining,
                hitPolicyKind: HitPolicyKind,
                hitPolicyParam: HitPolicyParam,
                tickIntervalFrames: TickIntervalFrames,
                hitFilter: HitFilter,
                hitCooldownFrames: HitCooldownFrames,
                hitFilterKey: HitFilterKey,
                startSuspended: StartSuspended,
                patternSlotIndex: PatternSlotIndex,
                patternSlotCount: PatternSlotCount,
                trackingTargetActorId: TrackingTargetActorId,
                collisionHalfExtents: CollisionHalfExtents,
                trackingArcHeight: TrackingArcHeight,
                trackingArcHeightPerUnit: TrackingArcHeightPerUnit,
                trackingArcDurationSeconds: TrackingArcDurationSeconds,
                bounceSearchRadius: BounceSearchRadius);
        }
    }
}
