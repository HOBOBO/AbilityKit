using AbilityKit.Core.Common.Projectile;
using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Services.Projectile
{
    public sealed class ProjectileHitArgs
    {
        public int CasterActorId;
        public int TargetActorId;
        public int Frame;

        public int ProjectileTemplateId;
        public ProjectileId ProjectileId;

        public Vec3 Point;
        public Vec3 Normal;
        public ColliderId HitCollider;

        public object Raw;
    }
}
