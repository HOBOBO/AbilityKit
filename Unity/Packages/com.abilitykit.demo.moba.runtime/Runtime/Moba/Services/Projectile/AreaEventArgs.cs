using AbilityKit.Core.Math;
using AbilityKit.Core.Common.Projectile;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    public sealed class AreaEventArgs
    {
        public string EventId;
        public int AreaId;
        public int OwnerActorId;
        public int TargetActorId;
        public int Frame;

        public Vec3 Center;
        public float Radius;
        public ColliderId Collider;

        public int CollisionLayerMask;
        public int MaxTargets;

        public object Raw;
    }
}
