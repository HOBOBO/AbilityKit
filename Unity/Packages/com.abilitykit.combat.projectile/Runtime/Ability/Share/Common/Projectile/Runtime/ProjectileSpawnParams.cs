using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public enum ProjectileHitPolicyKind
    {
        ExitOnHit = 0,
        Pierce = 1,
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

        public readonly int ReturnAfterFrames;
        public readonly float ReturnSpeed;
        public readonly float ReturnStopDistance;

        public readonly int LifetimeFrames;
        public readonly float MaxDistance;

        public readonly int CollisionLayerMask;
        public readonly ColliderId IgnoreCollider;

        public readonly IProjectileHitPolicy HitPolicy;
        public readonly int HitsRemaining;

        public readonly ProjectileHitPolicyKind HitPolicyKind;
        public readonly int HitPolicyParam;

        public readonly int TickIntervalFrames;

        public readonly IProjectileHitFilter HitFilter;
        public readonly int HitCooldownFrames;

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
            int hitCooldownFrames = 0)
        {
            OwnerId = ownerId;
            TemplateId = templateId;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            SpawnFrame = spawnFrame;
            Position = position;
            Direction = direction;
            Speed = speed;
            ReturnAfterFrames = returnAfterFrames;
            ReturnSpeed = returnSpeed;
            ReturnStopDistance = returnStopDistance;
            LifetimeFrames = lifetimeFrames;
            MaxDistance = maxDistance;
            CollisionLayerMask = collisionLayerMask;
            IgnoreCollider = ignoreCollider;

            HitPolicy = hitPolicy;
            HitsRemaining = hitsRemaining;

            HitPolicyKind = hitPolicyKind;
            HitPolicyParam = hitPolicyParam;

            TickIntervalFrames = tickIntervalFrames;

            HitFilter = hitFilter;
            HitCooldownFrames = hitCooldownFrames;
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
                hitCooldownFrames: HitCooldownFrames);
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
                hitCooldownFrames: HitCooldownFrames);
        }
    }
}
