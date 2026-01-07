using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public interface IProjectileHitFilter
    {
        bool ShouldHit(int ownerId, ColliderId collider, int frame);
    }
}
