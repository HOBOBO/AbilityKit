using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    public enum ProjectileCollisionResponse
    {
        Ignore = 0,
        Hit = 1,
        Block = 2,
    }

    public interface IProjectileHitFilter
    {
        bool ShouldHit(int ownerId, ColliderId collider, int frame);
    }

    /// <summary>
    /// Resolves a hit filter referenced by a projectile rollback snapshot.
    /// Filter implementations are intentionally not serialized with the combat
    /// snapshot because they may hold world services or other runtime references.
    /// </summary>
    public interface IProjectileHitFilterResolver
    {
        IProjectileHitFilter Resolve(int key);
    }

    public interface IProjectileCollisionResponseResolver
    {
        ProjectileCollisionResponse ResolveCollision(int ownerId, ColliderId collider, int frame);
    }
}
