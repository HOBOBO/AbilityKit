using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.Projectile
{
    public readonly struct AreaSpawnParams
    {
        public readonly int OwnerId;
        public readonly Vec3 Center;
        public readonly float Radius;

        public readonly int LifetimeFrames;
        public readonly int CollisionLayerMask;

        // Stay events are emitted every N frames (0 disables Stay events).
        public readonly int StayIntervalFrames;

        public AreaSpawnParams(int ownerId, in Vec3 center, float radius, int lifetimeFrames, int collisionLayerMask, int stayIntervalFrames)
        {
            OwnerId = ownerId;
            Center = center;
            Radius = radius;
            LifetimeFrames = lifetimeFrames;
            CollisionLayerMask = collisionLayerMask;
            StayIntervalFrames = stayIntervalFrames;
        }

        public AreaSpawnParams WithCenter(in Vec3 center)
        {
            return new AreaSpawnParams(OwnerId, center, Radius, LifetimeFrames, CollisionLayerMask, StayIntervalFrames);
        }
    }
}
