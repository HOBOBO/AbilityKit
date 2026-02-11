using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Share.Common.Projectile;

namespace AbilityKit.Ability.Share.Impl.Moba.Services.Projectile
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
