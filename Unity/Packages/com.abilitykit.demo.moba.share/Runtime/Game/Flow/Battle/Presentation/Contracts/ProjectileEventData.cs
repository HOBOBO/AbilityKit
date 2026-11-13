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
        public int ActorVersion { get; }
        public string ProfileId { get; }
        public string StatePath { get; }
        public int PhaseInstance { get; }
        public int EnteredFrame { get; }
        public int SnapshotFrame { get; }
        public float LocalSeconds { get; }

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
            float forwardZ,
            int actorVersion = 0, string profileId = null, string statePath = null,
            int phaseInstance = 0, int enteredFrame = 0, int snapshotFrame = 0, float localSeconds = 0)
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
            ActorVersion = actorVersion;
            ProfileId = profileId;
            StatePath = statePath;
            PhaseInstance = phaseInstance;
            EnteredFrame = enteredFrame;
            SnapshotFrame = snapshotFrame;
            LocalSeconds = localSeconds;
        }
    }

    public enum ProjectilePresentationEventKind
    {
        Unknown = 0,
        Spawn = 1,
        Hit = 2,
        Exit = 3,
        Phase = 4,
    }
}
