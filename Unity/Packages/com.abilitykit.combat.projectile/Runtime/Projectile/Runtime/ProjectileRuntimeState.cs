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
            bool isSuspended)
        {
            Id = id;
            Position = position;
            Direction = direction;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            PatternSlotIndex = patternSlotIndex;
            PatternSlotCount = patternSlotCount;
            IsSuspended = isSuspended;
        }

        public ProjectileId Id { get; }
        public Vec3 Position { get; }
        public Vec3 Direction { get; }
        public int LauncherActorId { get; }
        public int RootActorId { get; }
        public int PatternSlotIndex { get; }
        public int PatternSlotCount { get; }
        public bool IsSuspended { get; }
    }
}
