using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public interface IProjectileEmitter
    {
        ProjectileId Emit(in ProjectileSpawnParams spawn);
    }
}
