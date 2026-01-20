using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public readonly struct ProjectileSpawnEvent
    {
        public readonly ProjectileId Projectile;
        public readonly int OwnerId;
        public readonly int TemplateId;
        public readonly int LauncherActorId;
        public readonly int RootActorId;
        public readonly int Frame;
        public readonly Vec3 Position;
        public readonly Vec3 Direction;

        public ProjectileSpawnEvent(ProjectileId projectile, int ownerId, int templateId, int launcherActorId, int rootActorId, int frame, in Vec3 position, in Vec3 direction)
        {
            Projectile = projectile;
            OwnerId = ownerId;
            TemplateId = templateId;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            Frame = frame;
            Position = position;
            Direction = direction;
        }
    }

    public readonly struct ProjectileTickEvent
    {
        public readonly ProjectileId Projectile;
        public readonly int OwnerId;
        public readonly int TemplateId;
        public readonly int LauncherActorId;
        public readonly int RootActorId;
        public readonly int Frame;
        public readonly Vec3 Position;

        public ProjectileTickEvent(ProjectileId projectile, int ownerId, int templateId, int launcherActorId, int rootActorId, int frame, in Vec3 position)
        {
            Projectile = projectile;
            OwnerId = ownerId;
            TemplateId = templateId;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            Frame = frame;
            Position = position;
        }
    }

    public readonly struct ProjectileHitEvent
    {
        public readonly ProjectileId Projectile;
        public readonly int OwnerId;
        public readonly int TemplateId;
        public readonly int LauncherActorId;
        public readonly int RootActorId;
        public readonly ColliderId HitCollider;
        public readonly float Distance;
        public readonly Vec3 Point;
        public readonly Vec3 Normal;
        public readonly int Frame;

        public ProjectileHitEvent(ProjectileId projectile, int ownerId, int templateId, int launcherActorId, int rootActorId, ColliderId hitCollider, float distance, in Vec3 point, in Vec3 normal, int frame)
        {
            Projectile = projectile;
            OwnerId = ownerId;
            TemplateId = templateId;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            HitCollider = hitCollider;
            Distance = distance;
            Point = point;
            Normal = normal;
            Frame = frame;
        }
    }

    public readonly struct ProjectileExitEvent
    {
        public readonly ProjectileId Projectile;
        public readonly int OwnerId;
        public readonly int TemplateId;
        public readonly int LauncherActorId;
        public readonly int RootActorId;
        public readonly ProjectileExitReason Reason;
        public readonly int Frame;
        public readonly Vec3 Position;

        public ProjectileExitEvent(ProjectileId projectile, int ownerId, int templateId, int launcherActorId, int rootActorId, ProjectileExitReason reason, int frame, in Vec3 position)
        {
            Projectile = projectile;
            OwnerId = ownerId;
            TemplateId = templateId;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            Reason = reason;
            Frame = frame;
            Position = position;
        }
    }
}
