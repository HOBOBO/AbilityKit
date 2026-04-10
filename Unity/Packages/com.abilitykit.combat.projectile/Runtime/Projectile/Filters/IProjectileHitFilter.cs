using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.Projectile
{
    public interface IProjectileHitFilter
    {
        bool ShouldHit(int ownerId, ColliderId collider, int frame);
    }
}
