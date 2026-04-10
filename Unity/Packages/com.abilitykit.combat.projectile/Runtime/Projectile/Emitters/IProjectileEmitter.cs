using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.Projectile
{
    public interface IProjectileEmitter
    {
        ProjectileId Emit(in ProjectileSpawnParams spawn);
    }
}
