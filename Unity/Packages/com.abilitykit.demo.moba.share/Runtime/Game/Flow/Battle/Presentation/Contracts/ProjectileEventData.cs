namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>Platform-neutral presentation contract for a projectile lifecycle event.</summary>
    public readonly struct ProjectileEventData
    {
        public ProjectilePresentationEventKind Kind { get; }
        public int ProjectileActorId { get; }
        public int OwnerActorId { get; }
        public int TemplateId { get; }
        public int LauncherActorId { get; }
        public int RootActorId { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public int HitCollider { get; }
        public int ExitReason { get; }
        public int ProjectileId { get; }
        public float ForwardX { get; }
        public float ForwardY { get; }
        public float ForwardZ { get; }

        public ProjectileEventData(
            ProjectilePresentationEventKind kind,
            int projectileActorId,
            int ownerActorId,
            int templateId,
            int launcherActorId,
            int rootActorId,
            float x,
            float y,
            float z,
            int hitCollider,
            int exitReason,
            int projectileId,
            float forwardX,
            float forwardY,
            float forwardZ)
        {
            Kind = kind;
            ProjectileActorId = projectileActorId;
            OwnerActorId = ownerActorId;
            TemplateId = templateId;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            X = x;
            Y = y;
            Z = z;
            HitCollider = hitCollider;
            ExitReason = exitReason;
            ProjectileId = projectileId;
            ForwardX = forwardX;
            ForwardY = forwardY;
            ForwardZ = forwardZ;
        }
    }

    public enum ProjectilePresentationEventKind
    {
        Unknown = 0,
        Spawn = 1,
        Hit = 2,
        Exit = 3,
    }
}
