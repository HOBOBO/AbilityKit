using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    public readonly struct ProjectileRuntimeState
    {
        public ProjectileRuntimeState(
            ProjectileId id,
            in Vec3 position,
            in Vec3 direction,
            int launcherActorId,
            int rootActorId,
            int patternSlotIndex,
            int patternSlotCount,
            bool isSuspended,
            in Vec3 spawnPosition = default,
            float distanceTravelled = 0f,
            int spawnFrame = 0,
            float elapsedSeconds = 0f, int arrivedFrame = -1)
        {
            ArrivedFrame = arrivedFrame;
            Id = id;
            Position = position;
            Direction = direction;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            PatternSlotIndex = patternSlotIndex;
            PatternSlotCount = patternSlotCount;
            IsSuspended = isSuspended;
            SpawnPosition = spawnPosition;
            DistanceTravelled = distanceTravelled;
            SpawnFrame = spawnFrame;
            ElapsedSeconds = elapsedSeconds;
        }

        public int ArrivedFrame { get; }
        public bool HasArrived => ArrivedFrame >= 0;
        public ProjectileId Id { get; }
        public Vec3 Position { get; }
        public Vec3 Direction { get; }
        public int LauncherActorId { get; }
        public int RootActorId { get; }
        public int PatternSlotIndex { get; }
        public int PatternSlotCount { get; }
        public bool IsSuspended { get; }
        public Vec3 SpawnPosition { get; }
        public float DistanceTravelled { get; }
        public int SpawnFrame { get; }
        public float ElapsedSeconds { get; }
    }
}
